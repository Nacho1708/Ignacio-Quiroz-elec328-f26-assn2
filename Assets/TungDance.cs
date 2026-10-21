using System.Collections.Generic;
using Unity.Collections;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering;

// Drag this onto Tung Tung Tung Sahur and press Play: it dances to its own "tung tung tung sahur" beat.
// Switch to the 67 (hands swapping up and down) in the Inspector, or press M (or A on the right controller).
// The model has no skeleton, so this finds the arms and legs by where they are in the mesh
// and moves them like a puppet's. Works best on models built from separate pieces, like the Sahur.
[DisallowMultipleComponent]
public class TungDance : MonoBehaviour
{
    public enum Move { Dance, SixSeven }

    [Tooltip("What it's doing. In Play mode, press M (or A on the right controller) to switch.")]
    public Move move = Move.Dance;
    [Tooltip("Dance tempo, in beats per minute.")]
    [Range(60f, 200f)] public float bpm = 120f;
    [Tooltip("How fast the hands swap during the 67, in swaps per second.")]
    [Range(1f, 6f)] public float swapsPerSecond = 3f;
    [Tooltip("How big the moves are.")]
    [Range(0.2f, 2f)] public float energy = 1f;
    [Tooltip("A 'tung tung tung sahur' drum beat while dancing, and a 'six, seven' boop during the 67.")]
    public bool playSounds = true;

    [Header("Body parts (tuned for Tung Tung Tung Sahur)")]
    [Tooltip("Top of the legs, as a fraction of its height.")]
    [Range(0.1f, 0.6f)] public float legTop = 0.36f;
    [Tooltip("How far from the middle the arms are, as a fraction of its half-width.")]
    [Range(0.2f, 0.9f)] public float armsOutside = 0.55f;
    [Tooltip("Top of the arms (shoulders), as a fraction of its height.")]
    [Range(0.3f, 0.95f)] public float armTop = 0.7f;
    [Tooltip("Tick this if it faces the wrong way while dancing (arms swinging out behind it).")]
    public bool flipFront;

    const int Body = 0, RightArm = 1, LeftArm = 2, RightLeg = 3, LeftLeg = 4;
    const float FadeSeconds = 0.4f;

    // Everything below is measured in the "character frame": x to its right, y up from its feet, z to its front.
    MeshFilter meshFilter;
    Mesh original;
    Mesh working;
    Matrix4x4 toMesh;            // character frame -> the mesh's own space
    Vector3[] restPositions;
    Vector3[] restNormals;
    int[] part;                  // which body part each vertex belongs to
    float[] bendWeight;          // how much each body vertex follows the upper-body bend (0 at the hips, 1 at the top)
    readonly Vector3[] pivots = new Vector3[5]; // shoulders and hips
    float height;
    float hipHeight;
    Vector3[] vertices;
    Vector3[] normals;
    readonly Matrix4x4[] limbMatrices = new Matrix4x4[5];
    readonly Matrix4x4[] limbNormalMatrices = new Matrix4x4[5];

    float clock;                 // seconds of dancing (pauses with the game)
    Move shownMove;
    Move fadingFrom;
    float fade = 1f;             // 0 -> 1 while crossfading between moves
    float fadeIn;                // 0 -> 1 when it starts, from standing still
    int lastEighth = -1;
    int lastSwap = -1;
    AudioSource speaker;
    InputAction switchAction;
    bool ready;

    void Start()
    {
        meshFilter = GetComponent<MeshFilter>();
        if (meshFilter == null || meshFilter.sharedMesh == null)
        {
            Debug.LogWarning("TungDance needs a MeshFilter with a mesh on the same object.", this);
            enabled = false;
            return;
        }

        original = meshFilter.sharedMesh;
        working = ReadableCopy(original);
        if (working == null)
        {
            enabled = false;
            return;
        }
        working.MarkDynamic();

        FindCharacterFrame();
        FindBodyParts();
        vertices = new Vector3[restPositions.Length];
        normals = new Vector3[restPositions.Length];

        speaker = gameObject.AddComponent<AudioSource>();
        speaker.playOnAwake = false;
        speaker.spatialBlend = 1f; // the sound comes from the character
        speaker.dopplerLevel = 0f;
        speaker.maxDistance = 30f;

        shownMove = fadingFrom = move;
        ready = true;
        OnEnable();
    }

    void OnEnable()
    {
        if (!ready) return;
        meshFilter.sharedMesh = working;
        switchAction = new InputAction("Switch move", InputActionType.Button);
        switchAction.AddBinding("<Keyboard>/m");
        switchAction.AddBinding("<XRController>{RightHand}/{PrimaryButton}");
        switchAction.Enable();
        fadeIn = 0f;
    }

    void OnDisable()
    {
        if (!ready) return;
        meshFilter.sharedMesh = original; // stand still in its normal pose
        switchAction.Dispose();
    }

    void OnDestroy()
    {
        if (working != null) Destroy(working);
        if (speaker != null) Destroy(speaker);
    }

    void Update()
    {
        if (switchAction.WasPressedThisFrame())
            move = move == Move.Dance ? Move.SixSeven : Move.Dance;
        if (move != shownMove)
        {
            fadingFrom = shownMove;
            shownMove = move;
            fade = 0f;
        }

        clock += Time.deltaTime;
        fade = Mathf.Min(1f, fade + Time.deltaTime / FadeSeconds);
        fadeIn = Mathf.Min(1f, fadeIn + Time.deltaTime / FadeSeconds);

        Pose pose = PoseFor(shownMove);
        if (fade < 1f) pose = Pose.Lerp(PoseFor(fadingFrom), pose, Smooth(fade));
        pose = Pose.Lerp(default, pose, Smooth(fadeIn));
        Apply(pose);

        if (playSounds) PlaySounds();
    }

    // ---------- The moves ----------

    // A body pose. Angles are in degrees; distances are fractions of the character's height.
    struct Pose
    {
        public float hop;      // lift off the floor
        public float squash;   // + = shorter and wider, - = taller and thinner
        public float twist;    // turn to its right
        public float lean;     // lean to its right
        public float tilt;     // lean forward
        public float shift;    // slide to its right
        public Vector2 bend;   // upper body bends: x = to its right, y = forward
        public Vector2 rightArm, leftArm, rightLeg, leftLeg; // x = swing forward, y = out to the side

        public static Pose Lerp(Pose a, Pose b, float t)
        {
            return new Pose
            {
                hop = Mathf.LerpUnclamped(a.hop, b.hop, t),
                squash = Mathf.LerpUnclamped(a.squash, b.squash, t),
                twist = Mathf.LerpUnclamped(a.twist, b.twist, t),
                lean = Mathf.LerpUnclamped(a.lean, b.lean, t),
                tilt = Mathf.LerpUnclamped(a.tilt, b.tilt, t),
                shift = Mathf.LerpUnclamped(a.shift, b.shift, t),
                bend = Vector2.LerpUnclamped(a.bend, b.bend, t),
                rightArm = Vector2.LerpUnclamped(a.rightArm, b.rightArm, t),
                leftArm = Vector2.LerpUnclamped(a.leftArm, b.leftArm, t),
                rightLeg = Vector2.LerpUnclamped(a.rightLeg, b.rightLeg, t),
                leftLeg = Vector2.LerpUnclamped(a.leftLeg, b.leftLeg, t),
            };
        }

        public Pose Scaled(float amount)
        {
            return Lerp(default, this, amount); // unclamped, so energy can go above 1
        }
    }

    Pose PoseFor(Move which)
    {
        return (which == Move.Dance ? DancePose(clock * bpm / 60f) : SixSevenPose(clock * swapsPerSecond)).Scaled(energy);
    }

    // Four routines, two bars (8 beats) each, blending into the next one on the last half beat.
    static Pose DancePose(float beats)
    {
        float routineStart = Mathf.Floor(beats / 8f) * 8f;
        int routine = Mathf.FloorToInt(routineStart / 8f) % 4;
        float t = beats - routineStart;
        Pose pose = Routine(routine, t);
        float blend = Mathf.InverseLerp(7.5f, 8f, t);
        return blend > 0f ? Pose.Lerp(pose, Routine((routine + 1) % 4, t - 8f), Smooth(blend)) : pose;
    }

    static Pose Routine(int routine, float t)
    {
        float swing = Mathf.Sin(Mathf.PI * t);           // +1, -1, +1... on alternate beats
        float onBeat = Mathf.Exp(-8f * Frac(t));         // a quick kick right on each beat
        float bounce = Mathf.Abs(Mathf.Sin(Mathf.PI * t)); // up between beats, down on them
        var pose = new Pose();
        switch (routine)
        {
            case 0: // Tung tung tung: drum with one arm per beat, then "SAHUR!" with both arms up.
            {
                float strike = Sharp(Mathf.Cos(Mathf.PI * t));
                pose.rightArm = new Vector2(60f - 45f * strike, 10f);
                pose.leftArm = new Vector2(60f + 45f * strike, 10f);
                pose.squash = 0.05f * onBeat;
                pose.lean = 5f * Mathf.Cos(Mathf.PI * t);
                float sahur = Bump(Wrap(t, 4f), 3f, 0.7f);
                pose.rightArm = Vector2.Lerp(pose.rightArm, new Vector2(155f, 25f), sahur);
                pose.leftArm = Vector2.Lerp(pose.leftArm, new Vector2(155f, 25f), sahur);
                pose.hop = 0.06f * sahur;
                pose.squash = Mathf.Lerp(pose.squash, -0.06f, sahur);
                break;
            }
            case 1: // Side step: sway side to side, lifting the other leg and swinging the arms.
                pose.lean = 8f * swing;
                pose.shift = 0.03f * swing;
                pose.bend = new Vector2(-0.03f * swing, 0f);
                pose.rightLeg = new Vector2(22f * Mathf.Max(0f, -swing), 8f * Mathf.Max(0f, -swing));
                pose.leftLeg = new Vector2(22f * Mathf.Max(0f, swing), 8f * Mathf.Max(0f, swing));
                pose.rightArm = new Vector2(35f * swing, 12f);
                pose.leftArm = new Vector2(-35f * swing, 12f);
                pose.hop = 0.015f * bounce;
                break;
            case 2: // Wiggle: twist like jelly with the arms out, flapping.
                pose.twist = 25f * swing;
                pose.bend = new Vector2(-0.07f * swing, 0f);
                float flap = 20f * Mathf.Sin(2f * Mathf.PI * t);
                pose.rightArm = new Vector2(10f, 70f + flap);
                pose.leftArm = new Vector2(10f, 70f + flap);
                pose.squash = 0.04f * onBeat;
                pose.hop = 0.01f * bounce;
                break;
            default: // Raise the roof: both arms up, pumping on the beat, head bobbing.
                float pump = 25f * onBeat;
                pose.rightArm = new Vector2(150f + pump, 15f);
                pose.leftArm = new Vector2(150f + pump, 15f);
                pose.bend = new Vector2(0f, 0.04f * Mathf.Cos(2f * Mathf.PI * t));
                pose.lean = 4f * swing;
                pose.hop = 0.03f * bounce;
                pose.squash = 0.04f * onBeat;
                break;
        }
        return pose;
    }

    // The 67: hands out in front, swapping up and down, with a little bob and a smug lean back.
    static Pose SixSevenPose(float swaps)
    {
        float seesaw = Sharp(Mathf.Cos(Mathf.PI * swaps)); // +1: right hand up, -1: left hand up
        return new Pose
        {
            rightArm = new Vector2(70f + 20f * seesaw, -8f),
            leftArm = new Vector2(70f - 20f * seesaw, -8f),
            squash = 0.025f * Mathf.Exp(-6f * Frac(swaps)),
            lean = 3f * seesaw,
            twist = 4f * seesaw,
            bend = new Vector2(0f, -0.02f),
        };
    }

    static float Frac(float x) => x - Mathf.Floor(x);
    static float Wrap(float x, float length) => x - Mathf.Floor(x / length) * length;
    static float Smooth(float t) => t * t * (3f - 2f * t);
    static float Sharp(float x) => Mathf.Sign(x) * Mathf.Sqrt(Mathf.Abs(x)); // snappier than a plain sine
    static float Bump(float x, float center, float width) => Mathf.Clamp01(1f - Mathf.Abs(x - center) / width);

    // ---------- Moving the mesh ----------

    void Apply(Pose pose)
    {
        // The whole body: squash from the feet, lean and turn, then hop and slide.
        Vector3 squash = new Vector3(1f + 0.5f * pose.squash, 1f - pose.squash, 1f + 0.5f * pose.squash);
        Quaternion turn = Quaternion.AngleAxis(pose.twist, Vector3.up) *
                          Quaternion.AngleAxis(-pose.lean, Vector3.forward) *
                          Quaternion.AngleAxis(pose.tilt, Vector3.right);
        Vector3 move = new Vector3(pose.shift, pose.hop, 0f) * height;
        Matrix4x4 body = toMesh * Matrix4x4.TRS(move, turn, squash);
        Matrix4x4 bodyNormals = body.inverse.transpose;
        Vector3 bend = new Vector3(pose.bend.x, 0f, pose.bend.y) * height;

        // Each limb swings around its shoulder or hip, then follows the body.
        SetLimb(RightArm, pose.rightArm, 1f, body, bodyNormals, bend);
        SetLimb(LeftArm, pose.leftArm, -1f, body, bodyNormals, bend);
        SetLimb(RightLeg, pose.rightLeg, 1f, body, bodyNormals, Vector3.zero);
        SetLimb(LeftLeg, pose.leftLeg, -1f, body, bodyNormals, Vector3.zero);

        for (int i = 0; i < vertices.Length; i++)
        {
            int p = part[i];
            if (p == Body)
            {
                vertices[i] = body.MultiplyPoint3x4(restPositions[i] + bend * bendWeight[i]);
                normals[i] = bodyNormals.MultiplyVector(restNormals[i]).normalized;
            }
            else
            {
                vertices[i] = limbMatrices[p].MultiplyPoint3x4(restPositions[i]);
                normals[i] = limbNormalMatrices[p].MultiplyVector(restNormals[i]).normalized;
            }
        }
        working.SetVertices(vertices);
        working.SetNormals(normals);
        working.RecalculateBounds();
    }

    // side = 1 for the right limb, -1 for the left one, so "out to the side" mirrors.
    void SetLimb(int limb, Vector2 angles, float side, Matrix4x4 body, Matrix4x4 bodyNormals, Vector3 bend)
    {
        // Out to the side first, then swing forward, so a raised arm still points outward.
        Quaternion swing = Quaternion.AngleAxis(-angles.x, Vector3.right) *
                           Quaternion.AngleAxis(side * angles.y, Vector3.forward);
        Vector3 pivot = pivots[limb];
        Vector3 follow = bend * BendWeight(pivot.y);
        limbMatrices[limb] = body * Matrix4x4.Translate(pivot + follow) * Matrix4x4.Rotate(swing) * Matrix4x4.Translate(-pivot);
        limbNormalMatrices[limb] = bodyNormals * Matrix4x4.Rotate(swing);
    }

    float BendWeight(float y)
    {
        float t = Mathf.Clamp01((y - hipHeight) / Mathf.Max(1e-6f, height - hipHeight));
        return t * t;
    }

    // ---------- Finding the body parts ----------

    // Works out which way is up and which way it faces, and sets up the character frame.
    void FindCharacterFrame()
    {
        Vector3[] local = working.vertices;

        // Up: whichever of the model's own axes points most upward in the scene.
        Vector3[] axes = { Vector3.right, Vector3.up, Vector3.forward };
        int upAxis = 0;
        for (int a = 1; a < 3; a++)
            if (Mathf.Abs(Vector3.Dot(transform.TransformDirection(axes[a]), Vector3.up)) >
                Mathf.Abs(Vector3.Dot(transform.TransformDirection(axes[upAxis]), Vector3.up)))
                upAxis = a;
        Vector3 up = axes[upAxis] * Mathf.Sign(Vector3.Dot(transform.TransformDirection(axes[upAxis]), Vector3.up));

        // Front: noses and toes stick out forward, so see which way the head and feet poke out
        // further from the middle of the body. The other horizontal axis is left/right.
        float low = float.MaxValue, high = float.MinValue;
        foreach (Vector3 v in local)
        {
            low = Mathf.Min(low, Vector3.Dot(v, up));
            high = Mathf.Max(high, Vector3.Dot(v, up));
        }
        float span = high - low;
        Vector3 middle = Vector3.zero;
        int count = 0;
        foreach (Vector3 v in local)
        {
            float h = (Vector3.Dot(v, up) - low) / span;
            if (h > 0.4f && h < 0.6f) { middle += v; count++; }
        }
        middle = count > 0 ? middle / count : Vector3.zero;

        Vector3 front = Vector3.zero;
        float strongest = -1f;
        for (int a = 0; a < 3; a++)
        {
            if (a == upAxis) continue;
            float sticksOut = 0f;
            foreach (bool feetRegion in new[] { true, false })
            {
                float most = float.MinValue, least = float.MaxValue;
                foreach (Vector3 v in local)
                {
                    float h = (Vector3.Dot(v, up) - low) / span;
                    if (feetRegion ? h > 0.12f : h < 0.75f) continue;
                    float along = Vector3.Dot(v - middle, axes[a]);
                    most = Mathf.Max(most, along);
                    least = Mathf.Min(least, along);
                }
                if (most >= least) sticksOut += most + least; // > 0: pokes out more toward +axis
            }
            if (Mathf.Abs(sticksOut) > strongest)
            {
                strongest = Mathf.Abs(sticksOut);
                front = axes[a] * (sticksOut >= 0f ? 1f : -1f);
            }
        }
        if (flipFront) front = -front;
        Vector3 right = Vector3.Cross(up, front);

        // Character frame: rotate so x = right, y = up, z = front, with the origin between its feet.
        Matrix4x4 rotate = Matrix4x4.identity;
        rotate.SetRow(0, right);
        rotate.SetRow(1, up);
        rotate.SetRow(2, front);
        restPositions = new Vector3[local.Length];
        Vector3 min = Vector3.positiveInfinity, max = Vector3.negativeInfinity;
        for (int i = 0; i < local.Length; i++)
        {
            restPositions[i] = rotate.MultiplyVector(local[i]);
            min = Vector3.Min(min, restPositions[i]);
            max = Vector3.Max(max, restPositions[i]);
        }
        Vector3 feet = new Vector3((min.x + max.x) * 0.5f, min.y, (min.z + max.z) * 0.5f);
        for (int i = 0; i < restPositions.Length; i++)
            restPositions[i] -= feet;
        height = max.y - min.y;

        Vector3[] localNormals = working.normals;
        restNormals = new Vector3[local.Length];
        for (int i = 0; i < local.Length; i++)
            restNormals[i] = i < localNormals.Length ? rotate.MultiplyVector(localNormals[i]) : Vector3.up;

        toMesh = (Matrix4x4.Translate(-feet) * rotate).inverse;
    }

    // Splits the mesh into its separate pieces and sorts each piece into body, arms or legs by where it is.
    void FindBodyParts()
    {
        int n = restPositions.Length;
        int[] parent = new int[n];
        for (int i = 0; i < n; i++) parent[i] = i;
        int Find(int a)
        {
            while (parent[a] != a) a = parent[a] = parent[parent[a]];
            return a;
        }
        for (int s = 0; s < working.subMeshCount; s++)
        {
            int[] triangles = working.GetTriangles(s);
            for (int t = 0; t + 2 < triangles.Length; t += 3)
            {
                int a = Find(triangles[t]);
                parent[a] = Find(triangles[t + 1]);
                a = Find(triangles[t + 1]);
                parent[a] = Find(triangles[t + 2]);
            }
        }

        var pieces = new Dictionary<int, List<int>>();
        float halfWidth = 0f;
        for (int i = 0; i < n; i++)
        {
            int root = Find(i);
            if (!pieces.TryGetValue(root, out List<int> list)) pieces[root] = list = new List<int>();
            list.Add(i);
            halfWidth = Mathf.Max(halfWidth, Mathf.Abs(restPositions[i].x));
        }

        hipHeight = legTop * height;
        part = new int[n];
        var armCandidates = new List<(List<int> piece, Vector3 center)>();
        float[] armReach = { float.MinValue, float.MinValue }; // how high each arm's outermost pieces go (right, left)
        foreach (List<int> piece in pieces.Values)
        {
            Vector3 center = Vector3.zero;
            Vector3 min = Vector3.positiveInfinity, max = Vector3.negativeInfinity;
            foreach (int i in piece)
            {
                center += restPositions[i];
                min = Vector3.Min(min, restPositions[i]);
                max = Vector3.Max(max, restPositions[i]);
            }
            center /= piece.Count;
            bool small = (max - min).magnitude < 0.4f * height; // limbs are made of small pieces

            int which = Body;
            if (small && center.y < hipHeight && Mathf.Abs(center.x) < armsOutside * halfWidth)
            {
                which = center.x > 0f ? RightLeg : LeftLeg;
            }
            else if (small && Mathf.Abs(center.x) >= armsOutside * halfWidth && center.y > 0.2f * height && center.y < armTop * height)
            {
                armCandidates.Add((piece, center));
                int side = center.x > 0f ? 0 : 1;
                if (Mathf.Abs(center.x) >= 0.75f * halfWidth) armReach[side] = Mathf.Max(armReach[side], max.y);
                continue;
            }
            foreach (int i in piece) part[i] = which;
        }

        // An arm can't go much higher than its outermost pieces do. This keeps bits of the body's
        // side near the shoulders (which also stick out a little) from swinging with the arms.
        foreach ((List<int> piece, Vector3 center) in armCandidates)
        {
            int side = center.x > 0f ? 0 : 1;
            int which = center.y <= armReach[side] + 0.02f * height ? (side == 0 ? RightArm : LeftArm) : Body;
            foreach (int i in piece) part[i] = which;
        }

        // Pivots: shoulders at the top of each arm, hips at the top of each leg.
        for (int limb = RightArm; limb <= LeftLeg; limb++)
        {
            float top = float.MinValue, bottom = float.MaxValue;
            for (int i = 0; i < n; i++)
                if (part[i] == limb) { top = Mathf.Max(top, restPositions[i].y); bottom = Mathf.Min(bottom, restPositions[i].y); }
            if (top < bottom) continue; // no such limb on this model

            Vector3 sum = Vector3.zero;
            int count = 0;
            float band = top - 0.15f * (top - bottom);
            for (int i = 0; i < n; i++)
                if (part[i] == limb && restPositions[i].y >= band) { sum += restPositions[i]; count++; }
            pivots[limb] = sum / count;
            if (limb >= RightLeg) pivots[limb].y = top; // legs swing from their very top
        }

        bendWeight = new float[n];
        for (int i = 0; i < n; i++)
            bendWeight[i] = part[i] == Body ? BendWeight(restPositions[i].y) : 0f;
    }

    // A copy of the mesh this script can change every frame.
    static Mesh ReadableCopy(Mesh source)
    {
        if (source.isReadable)
        {
            Mesh copy = Instantiate(source);
            copy.name = source.name + " (dancing)";
            return copy;
        }
#if UNITY_EDITOR
        // Read/Write is off for this model, which blocks scripts in Play mode. The Editor can still read it.
        using (Mesh.MeshDataArray dataArray = UnityEditor.MeshUtility.AcquireReadOnlyMeshData(source))
        {
            Mesh.MeshData data = dataArray[0];
            int count = data.vertexCount;
            var copy = new Mesh { name = source.name + " (dancing)", indexFormat = data.indexFormat };
            using (var positions = new NativeArray<Vector3>(count, Allocator.Temp))
            {
                data.GetVertices(positions);
                copy.SetVertices(positions);
            }
            if (data.HasVertexAttribute(VertexAttribute.Normal))
                using (var sourceNormals = new NativeArray<Vector3>(count, Allocator.Temp))
                {
                    data.GetNormals(sourceNormals);
                    copy.SetNormals(sourceNormals);
                }
            if (data.HasVertexAttribute(VertexAttribute.Tangent))
                using (var tangents = new NativeArray<Vector4>(count, Allocator.Temp))
                {
                    data.GetTangents(tangents);
                    copy.SetTangents(tangents);
                }
            if (data.HasVertexAttribute(VertexAttribute.Color))
                using (var colors = new NativeArray<Color>(count, Allocator.Temp))
                {
                    data.GetColors(colors);
                    copy.SetColors(colors);
                }
            for (int channel = 0; channel < 8; channel++)
            {
                if (!data.HasVertexAttribute(VertexAttribute.TexCoord0 + channel)) continue;
                using (var uvs = new NativeArray<Vector4>(count, Allocator.Temp))
                {
                    data.GetUVs(channel, uvs);
                    copy.SetUVs(channel, uvs);
                }
            }
            copy.subMeshCount = data.subMeshCount;
            for (int s = 0; s < data.subMeshCount; s++)
            {
                SubMeshDescriptor sub = data.GetSubMesh(s);
                using (var indices = new NativeArray<int>(sub.indexCount, Allocator.Temp))
                {
                    data.GetIndices(indices, s);
                    copy.SetIndices(indices, sub.topology, s);
                }
            }
            copy.RecalculateBounds();
            return copy;
        }
#else
        Debug.LogError($"TungDance can't read the mesh '{source.name}'. Select the model file and tick Read/Write in its import settings.");
        return null;
#endif
    }

#if UNITY_EDITOR
    // Runs when you add this component. Built games can only reshape a model with Read/Write on, so turn it on.
    void Reset()
    {
        MeshFilter filter = GetComponent<MeshFilter>();
        Mesh mesh = filter != null ? filter.sharedMesh : null;
        if (mesh == null || mesh.isReadable) return;
        string path = UnityEditor.AssetDatabase.GetAssetPath(mesh);
        if (UnityEditor.AssetImporter.GetAtPath(path) is UnityEditor.ModelImporter importer && !importer.isReadable)
        {
            importer.isReadable = true;
            importer.SaveAndReimport();
            Debug.Log($"TungDance turned on Read/Write for {path} so the dance also works in builds.", this);
        }
    }
#endif

    // ---------- Sounds, made from scratch so there are no audio files to import ----------

    static AudioClip tung, sa, hur, six, seven;

    void PlaySounds()
    {
        if (tung == null)
        {
            tung = Knock(180f);
            sa = Knock(300f);
            hur = Knock(225f);
            six = Knock(330f);
            seven = Knock(250f);
        }

        if (shownMove == Move.Dance)
        {
            // Every bar: tung, tung, tung, sa-hur!
            int eighth = Mathf.FloorToInt(clock * bpm / 30f);
            if (eighth != lastEighth)
            {
                lastEighth = eighth;
                int step = eighth % 8;
                AudioClip clip = step == 0 || step == 2 || step == 4 ? tung : step == 6 ? sa : step == 7 ? hur : null;
                if (clip != null) speaker.PlayOneShot(clip, 0.7f);
            }
        }
        else
        {
            int swap = Mathf.FloorToInt(clock * swapsPerSecond);
            if (swap != lastSwap)
            {
                lastSwap = swap;
                speaker.PlayOneShot(swap % 2 == 0 ? six : seven, 0.6f);
            }
        }
    }

    // A wooden "tung": a struck-log tone that drops in pitch, with a knock and a click.
    static AudioClip Knock(float pitch)
    {
        const int rate = 22050;
        float[] samples = new float[(int)(0.3f * rate)];
        float phase = 0f, overtone = 0f;
        for (int i = 0; i < samples.Length; i++)
        {
            float t = i / (float)rate;
            float frequency = pitch * (1f + 0.35f * Mathf.Exp(-t / 0.02f));
            phase += 2f * Mathf.PI * frequency / rate;
            overtone += 2f * Mathf.PI * frequency * 2.76f / rate;
            float click = t < 0.004f ? Random.Range(-1f, 1f) * (1f - t / 0.004f) * 0.6f : 0f;
            samples[i] = 0.7f * (Mathf.Sin(phase) * Mathf.Exp(-t / 0.09f) + 0.5f * Mathf.Sin(overtone) * Mathf.Exp(-t / 0.025f) + click);
        }
        AudioClip clip = AudioClip.Create("Tung", samples.Length, 1, rate, false);
        clip.SetData(samples, 0);
        return clip;
    }
}
