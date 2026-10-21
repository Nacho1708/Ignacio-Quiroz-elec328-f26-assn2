using System.Collections.Generic;
using Unity.Collections;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering;

// Drag this onto Tung Tung Tung Sahur and press Play: it does a backflip every few seconds,
// and whenever you press B (on the keyboard, or B on the right controller).
// Crouch, explosive jump, a tucked flip with a slow-motion hang upside down at the top,
// swoosh trails and afterimages, then it sticks the landing with a shockwave and a victory pose.
// Like TungDance, it finds the arms and legs by where they are in the mesh (the model has no skeleton).
[DisallowMultipleComponent]
public class TungBackflip : MonoBehaviour
{
    [Tooltip("Seconds between automatic backflips. 0 = only when you press B.")]
    [Range(0f, 30f)] public float autoFlipEvery = 5f;
    [Tooltip("How high it jumps, as a fraction of its height.")]
    [Range(0.2f, 1.5f)] public float jumpHeight = 0.6f;
    [Tooltip("How much it slows down while it hangs upside down at the top. 0 = no slow motion.")]
    [Range(0f, 0.9f)] public float slowMotion = 0.6f;
    [Tooltip("Swoosh trails and afterimages while it flips.")]
    public bool trails = true;
    public Color trailColor = new Color(0.2f, 0.9f, 1f);
    public Color afterimageColor = new Color(1f, 0.35f, 0.9f);
    [Tooltip("Dust when it jumps and a shockwave when it lands.")]
    public bool dust = true;
    public bool playSounds = true;

    [Header("Body parts (tuned for Tung Tung Tung Sahur)")]
    [Tooltip("Top of the legs, as a fraction of its height.")]
    [Range(0.1f, 0.6f)] public float legTop = 0.36f;
    [Tooltip("How far from the middle the arms are, as a fraction of its half-width.")]
    [Range(0.2f, 0.9f)] public float armsOutside = 0.55f;
    [Tooltip("Top of the arms (shoulders), as a fraction of its height.")]
    [Range(0.3f, 0.95f)] public float armTop = 0.7f;
    [Tooltip("Tick this if it flips the wrong way (forwards).")]
    public bool flipFront;

    // The flip's timeline, in seconds of "flip time", which runs slower while it hangs at the top.
    const float CrouchEnd = 0.32f, Takeoff = 0.42f, Land = 1.42f, End = 2.3f;
    const float BlendIn = 0.15f;       // seconds to blend from whatever pose it was in (e.g. TungDance)
    const float GhostEvery = 0.05f;    // seconds between afterimages

    const int Body = 0, RightArm = 1, LeftArm = 2, RightLeg = 3, LeftLeg = 4;

    // Measured in the "character frame": x to its right, y up from its feet, z to its front.
    MeshFilter meshFilter;
    Mesh original;
    Mesh working;
    Matrix4x4 toMesh;
    Vector3[] restPositions;
    Vector3[] restNormals;
    int[] part;
    float[] curlWeight;              // how much each body vertex follows the curl (0 at the hips, 1 at the top)
    readonly Vector3[] pivots = new Vector3[5];
    float height;
    float hipHeight;
    float toeZ, heelZ;               // front and back edges of its feet
    Vector3 headTop;                 // rest positions of the points the swoosh trails follow
    Vector3 rightFoot, leftFoot;
    Vector3[] vertices;
    Vector3[] normals;
    Vector3[] startVertices;         // the shape it was in when the flip started
    Vector3[] startNormals;
    readonly Matrix4x4[] limbMatrices = new Matrix4x4[5];
    readonly Matrix4x4[] limbNormalMatrices = new Matrix4x4[5];
    Matrix4x4 bodyMatrix;            // this frame's whole-body move (character frame -> mesh)
    bool swooshesPlaced;

    bool ready;
    bool flipping;
    float flipTime;                  // position on the timeline
    float realTime;                  // real seconds since the flip started
    float nextAutoFlip;
    float nextGhost;
    Behaviour pausedDance;           // TungDance on the same object, paused while flipping
    TrailRenderer[] swooshes;
    AudioSource speaker;
    InputAction flipAction;

    public bool IsFlipping => flipping;

    // Call this from other scripts (or a UnityEvent) to make it flip.
    public void Backflip()
    {
        if (ready && !flipping && isActiveAndEnabled) StartFlip();
    }

    void Start()
    {
        meshFilter = GetComponent<MeshFilter>();
        if (meshFilter == null || meshFilter.sharedMesh == null)
        {
            Debug.LogWarning("TungBackflip needs a MeshFilter with a mesh on the same object.", this);
            enabled = false;
            return;
        }

        // If TungDance already swapped in its dancing copy, briefly turn it off to get the real model back.
        Behaviour dance = GetComponent("TungDance") as Behaviour;
        bool danceWasOn = dance != null && dance.enabled;
        if (danceWasOn) dance.enabled = false;
        original = meshFilter.sharedMesh;
        working = ReadableCopy(original);
        if (danceWasOn) dance.enabled = true;
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
        startVertices = new Vector3[restPositions.Length];
        startNormals = new Vector3[restPositions.Length];

        speaker = gameObject.AddComponent<AudioSource>();
        speaker.playOnAwake = false;
        speaker.spatialBlend = 1f;
        speaker.dopplerLevel = 0f;
        speaker.maxDistance = 30f;

        nextAutoFlip = Time.time + (autoFlipEvery > 0f ? Mathf.Min(2f, autoFlipEvery) : 0f);
        ready = true;
        OnEnable();
    }

    void OnEnable()
    {
        if (!ready) return;
        flipAction = new InputAction("Backflip", InputActionType.Button);
        flipAction.AddBinding("<Keyboard>/b");
        flipAction.AddBinding("<XRController>{RightHand}/{SecondaryButton}");
        flipAction.Enable();
    }

    void OnDisable()
    {
        if (!ready) return;
        flipAction.Dispose();
        if (flipping) FinishFlip();
    }

    void OnDestroy()
    {
        if (working != null) Destroy(working);
        if (speaker != null) Destroy(speaker);
    }

    void Update()
    {
        if (!flipping)
        {
            bool auto = autoFlipEvery > 0f && Time.time >= nextAutoFlip;
            if (flipAction.WasPressedThisFrame() || auto) StartFlip();
            if (!flipping) return;
        }

        // Flip time runs slower around the top of the jump, so it hangs there upside down.
        float before = flipTime;
        flipTime += Time.deltaTime * (1f - slowMotion * Hang(flipTime));
        realTime += Time.deltaTime;

        Pose pose = PoseAt(flipTime);
        Apply(pose, Smooth(Mathf.Clamp01(realTime / BlendIn)));
        Effects(before, pose);

        if (flipTime >= End) FinishFlip();
    }

    void StartFlip()
    {
        // Start from the pose it's in right now (for example mid-dance), then pause TungDance.
        Mesh shown = meshFilter.sharedMesh;
        if (shown != null && shown != working && shown.isReadable && shown.vertexCount == startVertices.Length)
        {
            System.Array.Copy(shown.vertices, startVertices, startVertices.Length);
            System.Array.Copy(shown.normals, startNormals, startNormals.Length);
        }
        else
        {
            for (int i = 0; i < startVertices.Length; i++)
            {
                startVertices[i] = toMesh.MultiplyPoint3x4(restPositions[i]);
                startNormals[i] = toMesh.MultiplyVector(restNormals[i]);
            }
        }
        pausedDance = GetComponent("TungDance") as Behaviour;
        if (pausedDance != null && pausedDance.enabled) pausedDance.enabled = false;
        else pausedDance = null;

        meshFilter.sharedMesh = working;
        flipping = true;
        flipTime = 0f;
        realTime = 0f;
        nextGhost = 0f;
        if (playSounds) speaker.PlayOneShot(Sound(Sfx.Charge), 0.5f);
    }

    void FinishFlip()
    {
        flipping = false;
        meshFilter.sharedMesh = original;
        StopSwooshes();
        if (pausedDance != null) pausedDance.enabled = true; // TungDance eases back into its dance
        pausedDance = null;
        nextAutoFlip = Time.time + autoFlipEvery;
    }

    // ---------- The flip ----------

    struct Pose
    {
        public float lift;       // how high it is off the floor (fraction of its height)
        public float squash;     // + = shorter and wider, - = taller and thinner
        public float angle;      // lean forward (+) / flip backwards (-), in degrees
        public float airborne;   // 0 = turning around its feet, 1 = around its middle
        public float curl;       // upper body curls forward (fraction of its height)
        public Vector2 arms;     // x = swing forward, y = out to the side (degrees, both arms)
        public Vector2 legs;     // x = swing forward (the tuck), y = out to the side
    }

    Pose PoseAt(float t)
    {
        var p = new Pose();
        Vector2 armsBack = new Vector2(-55f, 12f), armsUp = new Vector2(170f, 20f);
        Vector2 armsTuck = new Vector2(95f, -5f), armsBalance = new Vector2(60f, 70f), victory = new Vector2(160f, 35f);
        p.airborne = Smooth(Mathf.InverseLerp(Takeoff - 0.04f, Takeoff + 0.06f, t)) *
                     (1f - Smooth(Mathf.InverseLerp(Land - 0.06f, Land + 0.02f, t)));

        if (t < CrouchEnd) // 1. crouch down, arms swing back
        {
            float c = Smooth(t / CrouchEnd);
            p.squash = 0.22f * c;
            p.angle = 12f * c;
            p.curl = 0.04f * c;
            p.arms = armsBack * c;
        }
        else if (t < Takeoff) // 2. explode upwards, arms thrown up
        {
            float l = Smooth((t - CrouchEnd) / (Takeoff - CrouchEnd));
            p.squash = Mathf.Lerp(0.22f, -0.18f, l);
            p.angle = Mathf.Lerp(12f, -10f, l);
            p.curl = Mathf.Lerp(0.04f, -0.03f, l);
            p.arms = Vector2.Lerp(armsBack, armsUp, l);
        }
        else if (t < Land) // 3. the flip: tuck tight, spin, hang upside down at the top, open up to land
        {
            float u = (t - Takeoff) / (Land - Takeoff);
            float tuck = Smooth(Mathf.InverseLerp(0.05f, 0.3f, u)) * (1f - Smooth(Mathf.InverseLerp(0.7f, 0.95f, u)));
            p.lift = jumpHeight * 4f * u * (1f - u);
            p.angle = -10f - 350f * Smooth(u);
            p.squash = -0.18f * (1f - Smooth(u / 0.2f)) - 0.08f * Smooth(Mathf.InverseLerp(0.8f, 1f, u));
            p.curl = 0.06f * tuck - 0.03f * (1f - Smooth(u / 0.15f));
            p.legs = new Vector2(115f, 8f) * tuck;
            p.arms = Vector2.Lerp(u < 0.5f ? armsUp : armsBalance, armsTuck, tuck);
        }
        else // 4. stick the landing, then a victory pose
        {
            float v = Mathf.Clamp01((t - Land) / (End - Land));
            float impact = 1f - Mathf.Exp(-40f * v);
            p.squash = 0.26f * impact * Mathf.Exp(-6f * v) * Mathf.Cos(14f * v) - 0.08f * (1f - impact);
            p.angle = 10f * (1f - Mathf.Exp(-12f * v)) * Mathf.Exp(-5f * v);
            p.curl = -0.03f * Bump(v, 0.55f, 0.3f);
            p.arms = Vector2.Lerp(armsBalance, victory, Smooth(Mathf.InverseLerp(0.15f, 0.4f, v)));
            p.arms = Vector2.Lerp(p.arms, Vector2.zero, Smooth(Mathf.InverseLerp(0.7f, 1f, v)));
        }
        return p;
    }

    // 1 around the top of the jump, 0 elsewhere: used to slow the flip down there.
    static float Hang(float t)
    {
        float apex = (Takeoff + Land) * 0.5f;
        float x = Mathf.Clamp01(1f - Mathf.Abs(t - apex) / 0.22f);
        return x * x * (3f - 2f * x);
    }

    static float Smooth(float t)
    {
        t = Mathf.Clamp01(t);
        return t * t * (3f - 2f * t);
    }

    static float Bump(float x, float center, float width) => Mathf.Clamp01(1f - Mathf.Abs(x - center) / width);

    // ---------- Moving the mesh ----------

    void Apply(Pose pose, float blendIn)
    {
        // Squash from the feet, then turn, then lift. On the floor it tips over the front of its feet
        // (leaning forward) or its heels (leaning back), so its feet never dig into the floor.
        // In the air it turns around its middle.
        Vector3 squash = new Vector3(1f + 0.5f * pose.squash, 1f - pose.squash, 1f + 0.5f * pose.squash);
        float edge = (Mathf.DeltaAngle(0f, pose.angle) > 0f ? toeZ : heelZ) * squash.z;
        Vector3 pivot = new Vector3(0f, 0.5f * height * (1f - pose.squash) * pose.airborne, edge * (1f - pose.airborne));
        Quaternion turn = Quaternion.AngleAxis(pose.angle, Vector3.right);
        Matrix4x4 body = toMesh * Matrix4x4.Translate(new Vector3(0f, pose.lift * height, 0f) + pivot) *
                         Matrix4x4.Rotate(turn) * Matrix4x4.Translate(-pivot) * Matrix4x4.Scale(squash);
        Matrix4x4 bodyNormals = body.inverse.transpose;
        Vector3 curl = new Vector3(0f, 0f, pose.curl * height);
        bodyMatrix = body;

        SetLimb(RightArm, pose.arms, 1f, body, bodyNormals, curl);
        SetLimb(LeftArm, pose.arms, -1f, body, bodyNormals, curl);
        SetLimb(RightLeg, pose.legs, 1f, body, bodyNormals, Vector3.zero);
        SetLimb(LeftLeg, pose.legs, -1f, body, bodyNormals, Vector3.zero);

        for (int i = 0; i < vertices.Length; i++)
        {
            int p = part[i];
            if (p == Body)
            {
                vertices[i] = body.MultiplyPoint3x4(restPositions[i] + curl * curlWeight[i]);
                normals[i] = bodyNormals.MultiplyVector(restNormals[i]).normalized;
            }
            else
            {
                vertices[i] = limbMatrices[p].MultiplyPoint3x4(restPositions[i]);
                normals[i] = limbNormalMatrices[p].MultiplyVector(restNormals[i]).normalized;
            }
            if (blendIn < 1f)
            {
                vertices[i] = Vector3.Lerp(startVertices[i], vertices[i], blendIn);
                normals[i] = Vector3.Lerp(startNormals[i], normals[i], blendIn).normalized;
            }
        }
        working.SetVertices(vertices);
        working.SetNormals(normals);
        working.RecalculateBounds();
    }

    // side = 1 for the right limb, -1 for the left one, so "out to the side" mirrors.
    void SetLimb(int limb, Vector2 angles, float side, Matrix4x4 body, Matrix4x4 bodyNormals, Vector3 curl)
    {
        // Out to the side first, then swing forward, so a raised arm still points outward.
        Quaternion swing = Quaternion.AngleAxis(-angles.x, Vector3.right) *
                           Quaternion.AngleAxis(side * angles.y, Vector3.forward);
        Vector3 pivot = pivots[limb];
        limbMatrices[limb] = body * Matrix4x4.Translate(pivot + curl * CurlWeight(pivot.y)) *
                             Matrix4x4.Rotate(swing) * Matrix4x4.Translate(-pivot);
        limbNormalMatrices[limb] = bodyNormals * Matrix4x4.Rotate(swing);
    }

    float CurlWeight(float y)
    {
        float t = Mathf.Clamp01((y - hipHeight) / Mathf.Max(1e-6f, height - hipHeight));
        return t * t;
    }

    // ---------- Effects ----------

    void Effects(float before, Pose pose)
    {
        Matrix4x4 toWorld = transform.localToWorldMatrix * toMesh;
        Vector3 feet = toWorld.MultiplyPoint3x4(Vector3.zero);
        float size = (toWorld.MultiplyPoint3x4(new Vector3(0f, height, 0f)) - feet).magnitude; // its height in meters

        if (Crossed(before, Takeoff))
        {
            if (dust) Dust(feet, size, 10, 0.6f, false);
            if (playSounds) speaker.PlayOneShot(Sound(Sfx.Whoosh), 0.8f);
            if (trails) StartSwooshes(size);
        }

        bool inAir = flipTime > Takeoff && flipTime < Land;
        if (trails && inAir && swooshes != null)
        {
            // The swooshes follow the top of its head and its feet.
            Matrix4x4 meshToWorld = transform.localToWorldMatrix;
            swooshes[0].transform.position = meshToWorld.MultiplyPoint3x4(
                bodyMatrix.MultiplyPoint3x4(headTop + new Vector3(0f, 0f, pose.curl * height)));
            swooshes[1].transform.position = meshToWorld.MultiplyPoint3x4(
                0.5f * (limbMatrices[RightLeg].MultiplyPoint3x4(rightFoot) + limbMatrices[LeftLeg].MultiplyPoint3x4(leftFoot)));
            if (!swooshesPlaced)
            {
                foreach (TrailRenderer trail in swooshes) trail.Clear(); // no streak from where they were made
                swooshesPlaced = true;
            }
        }
        float airborneU = Mathf.InverseLerp(Takeoff, Land, flipTime);
        if (trails && inAir && realTime >= nextGhost && airborneU > 0.06f && airborneU < 0.94f)
        {
            nextGhost = realTime + GhostEvery;
            Afterimage(Color.Lerp(afterimageColor, trailColor, airborneU));
        }

        if (Crossed(before, Land))
        {
            StopSwooshes();
            if (dust)
            {
                Dust(feet, size, 14, 1f, true);
                Shockwave(feet, size);
            }
            if (playSounds) speaker.PlayOneShot(Sound(Sfx.Land), 1f);
        }
        if (playSounds && Crossed(before, Land + 0.3f * (End - Land)))
            speaker.PlayOneShot(Sound(Sfx.TaDa), 0.6f);
    }

    bool Crossed(float before, float mark) => before < mark && flipTime >= mark;

    void StartSwooshes(float size)
    {
        StopSwooshes();
        swooshes = new TrailRenderer[2];
        for (int i = 0; i < 2; i++)
        {
            var go = new GameObject(i == 0 ? "Backflip Swoosh (head)" : "Backflip Swoosh (feet)");
            TrailRenderer trail = go.AddComponent<TrailRenderer>();
            trail.sharedMaterial = ColorMaterial(i == 0 ? trailColor : afterimageColor);
            trail.time = 0.35f;
            trail.widthMultiplier = 0.09f * size;
            trail.widthCurve = AnimationCurve.EaseInOut(0f, 1f, 1f, 0f); // thick at the front, thin at the tail
            trail.minVertexDistance = 0.01f * size;
            trail.numCapVertices = 4;
            trail.shadowCastingMode = ShadowCastingMode.Off;
            trail.receiveShadows = false;
            swooshes[i] = trail;
        }
        swooshesPlaced = false;
    }

    void StopSwooshes()
    {
        if (swooshes == null) return;
        foreach (TrailRenderer trail in swooshes)
        {
            if (trail == null) continue;
            trail.emitting = false;
            Destroy(trail.gameObject, trail.time + 0.1f); // let the tail finish fading
        }
        swooshes = null;
    }

    // A see-through copy of its current pose that fades away where it was.
    void Afterimage(Color color)
    {
        Mesh snapshot = Instantiate(working);
        snapshot.name = "Backflip afterimage";
        var white = new Color32[snapshot.vertexCount];
        for (int i = 0; i < white.Length; i++) white[i] = new Color32(255, 255, 255, 255);
        snapshot.colors32 = white;

        var go = new GameObject("Backflip Afterimage");
        go.transform.SetPositionAndRotation(transform.position, transform.rotation);
        go.transform.localScale = transform.lossyScale;
        go.AddComponent<MeshFilter>().sharedMesh = snapshot;
        MeshRenderer r = go.AddComponent<MeshRenderer>();
        var materials = new Material[snapshot.subMeshCount];
        for (int i = 0; i < materials.Length; i++) materials[i] = GhostMaterial();
        r.sharedMaterials = materials;
        r.shadowCastingMode = ShadowCastingMode.Off;
        r.receiveShadows = false;
        var block = new MaterialPropertyBlock();
        Animate(go, 0.3f, t =>
        {
            Color c = color;
            c.a = 0.5f * (1f - t) * (1f - t);
            block.SetColor(ColorId, c);
            block.SetColor(BaseColorId, c);
            r.SetPropertyBlock(block);
            if (t >= 1f) Destroy(snapshot);
        });
    }

    // Puffs of dust rolling out along the floor.
    void Dust(Vector3 feet, float size, int count, float strength, bool ring)
    {
        Material material = ColorMaterial(new Color(0.85f, 0.8f, 0.72f));
        for (int i = 0; i < count; i++)
        {
            float angle = (i + Random.value * 0.5f) / count * Mathf.PI * 2f;
            Vector3 dir = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle));
            Vector3 start = feet + dir * (0.12f * size) + Vector3.up * (0.03f * size);
            float travel = (ring ? 0.55f : 0.35f) * size * strength * Random.Range(0.8f, 1.2f);
            float puffSize = 0.09f * size * Random.Range(0.8f, 1.3f);
            Transform puff = Piece("Backflip Dust", Sphere, material, start, Vector3.zero).transform;
            Animate(puff.gameObject, Random.Range(0.45f, 0.65f), t =>
            {
                float eased = 1f - (1f - t) * (1f - t);
                puff.position = start + dir * (travel * eased) + Vector3.up * (0.1f * size * t);
                float grow = t < 0.25f ? t / 0.25f : 1f - (t - 0.25f) / 0.75f;
                puff.localScale = Vector3.one * (puffSize * Mathf.Sqrt(Mathf.Max(0f, grow)));
            });
        }
    }

    // A flat ring that races out along the floor when it lands. It's drawn from both sides so it
    // shows up from any angle.
    void Shockwave(Vector3 feet, float size)
    {
        const int segments = 48;
        var ringMesh = new Mesh { name = "Backflip Shockwave" };
        var points = new Vector3[segments * 2];
        var triangles = new int[segments * 12];
        for (int i = 0; i < segments; i++)
        {
            int a = i * 2, b = (i + 1) % segments * 2;
            int[] quad = { a, a + 1, b, b, a + 1, b + 1, a, b, a + 1, b, b + 1, a + 1 }; // top side, then bottom side
            quad.CopyTo(triangles, i * 12);
        }
        ringMesh.vertices = points;
        ringMesh.triangles = triangles;

        GameObject ring = Piece("Backflip Shockwave", ringMesh, ColorMaterial(Color.white), feet + Vector3.up * (0.01f * size), Vector3.one);
        Animate(ring, 0.45f, t =>
        {
            float radius = size * Mathf.Lerp(0.15f, 0.9f, 1f - (1f - t) * (1f - t));
            float halfWidth = 0.03f * size * (1f - t);
            for (int i = 0; i < segments; i++)
            {
                float angle = i / (float)segments * Mathf.PI * 2f;
                Vector3 dir = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle));
                points[i * 2] = dir * (radius - halfWidth);
                points[i * 2 + 1] = dir * (radius + halfWidth);
            }
            ringMesh.vertices = points;
            ringMesh.RecalculateBounds();
            if (t >= 1f) Destroy(ringMesh);
        });
    }

    static void Animate(GameObject go, float seconds, System.Action<float> step)
    {
        TungBackflipEffect effect = go.AddComponent<TungBackflipEffect>();
        effect.seconds = seconds;
        effect.step = step;
        step(0f);
    }

    static GameObject Piece(string name, Mesh mesh, Material material, Vector3 position, Vector3 scale)
    {
        var go = new GameObject(name);
        go.transform.position = position;
        go.transform.localScale = scale;
        go.AddComponent<MeshFilter>().sharedMesh = mesh;
        MeshRenderer r = go.AddComponent<MeshRenderer>();
        r.sharedMaterial = material;
        r.shadowCastingMode = ShadowCastingMode.Off;
        return go;
    }

    static Mesh sphere;
    static Mesh Sphere
    {
        get
        {
            if (sphere == null)
            {
                GameObject temp = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                sphere = temp.GetComponent<MeshFilter>().sharedMesh;
                DestroyImmediate(temp);
            }
            return sphere;
        }
    }

    static readonly int BaseColorId = Shader.PropertyToID("_BaseColor"); // URP
    static readonly int ColorId = Shader.PropertyToID("_Color");         // built-in pipeline and Sprites/Default
    static readonly Dictionary<Color, Material> colorMaterials = new Dictionary<Color, Material>();
    static Material ghostMaterial;

    // A flat, always-bright material of one color (URP, built-in pipeline, and builds).
    static Material ColorMaterial(Color color)
    {
        if (!colorMaterials.TryGetValue(color, out Material material) || material == null)
        {
            Shader shader = Shader.Find("Universal Render Pipeline/Unlit");
            if (shader == null) shader = Shader.Find("Unlit/Color");
            if (shader == null) shader = Shader.Find("Sprites/Default");
            material = new Material(shader) { color = color };
            colorMaterials[color] = material;
        }
        return material;
    }

    // A see-through material for the afterimages. Sprites/Default is always included in builds.
    static Material GhostMaterial()
    {
        if (ghostMaterial == null) ghostMaterial = new Material(Shader.Find("Sprites/Default"));
        return ghostMaterial;
    }

    // ---------- Finding the body parts (same approach as TungDance) ----------

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

        // Front: noses and toes stick out forward, so see which way the head and feet poke out further.
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
                if (most >= least) sticksOut += most + least;
            }
            if (Mathf.Abs(sticksOut) > strongest)
            {
                strongest = Mathf.Abs(sticksOut);
                front = axes[a] * (sticksOut >= 0f ? 1f : -1f);
            }
        }
        if (flipFront) front = -front;
        Vector3 right = Vector3.Cross(up, front);

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
        float[] armReach = { float.MinValue, float.MinValue };
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
            bool small = (max - min).magnitude < 0.4f * height;

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
        foreach ((List<int> piece, Vector3 center) in armCandidates)
        {
            int side = center.x > 0f ? 0 : 1;
            int which = center.y <= armReach[side] + 0.02f * height ? (side == 0 ? RightArm : LeftArm) : Body;
            foreach (int i in piece) part[i] = which;
        }

        for (int limb = RightArm; limb <= LeftLeg; limb++)
        {
            float top = float.MinValue, bottom = float.MaxValue;
            for (int i = 0; i < n; i++)
                if (part[i] == limb) { top = Mathf.Max(top, restPositions[i].y); bottom = Mathf.Min(bottom, restPositions[i].y); }
            if (top < bottom) continue;

            Vector3 sum = Vector3.zero;
            int count = 0;
            float band = top - 0.15f * (top - bottom);
            for (int i = 0; i < n; i++)
                if (part[i] == limb && restPositions[i].y >= band) { sum += restPositions[i]; count++; }
            pivots[limb] = sum / count;
            if (limb >= RightLeg) pivots[limb].y = top;
        }

        curlWeight = new float[n];
        for (int i = 0; i < n; i++)
            curlWeight[i] = part[i] == Body ? CurlWeight(restPositions[i].y) : 0f;

        // The front and back edges of its feet (everything near the floor).
        toeZ = heelZ = 0f;
        for (int i = 0; i < n; i++)
        {
            if (restPositions[i].y > 0.1f * height) continue;
            toeZ = Mathf.Max(toeZ, restPositions[i].z);
            heelZ = Mathf.Min(heelZ, restPositions[i].z);
        }

        // The points the swoosh trails follow: the top of its head and the bottom of each foot.
        headTop = new Vector3(0f, height, 0f);
        rightFoot = LowestPoint(RightLeg);
        leftFoot = LowestPoint(LeftLeg);
    }

    Vector3 LowestPoint(int limb)
    {
        Vector3 lowest = Vector3.zero;
        float best = float.MaxValue;
        for (int i = 0; i < restPositions.Length; i++)
            if (part[i] == limb && restPositions[i].y < best) { best = restPositions[i].y; lowest = restPositions[i]; }
        return lowest;
    }

    static Mesh ReadableCopy(Mesh source)
    {
        if (source.isReadable)
        {
            Mesh copy = Instantiate(source);
            copy.name = source.name + " (backflip)";
            return copy;
        }
#if UNITY_EDITOR
        // Read/Write is off for this model, which blocks scripts in Play mode. The Editor can still read it.
        using (Mesh.MeshDataArray dataArray = UnityEditor.MeshUtility.AcquireReadOnlyMeshData(source))
        {
            Mesh.MeshData data = dataArray[0];
            int count = data.vertexCount;
            var copy = new Mesh { name = source.name + " (backflip)", indexFormat = data.indexFormat };
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
        Debug.LogError($"TungBackflip can't read the mesh '{source.name}'. Select the model file and tick Read/Write in its import settings.");
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
            Debug.Log($"TungBackflip turned on Read/Write for {path} so the backflip also works in builds.", this);
        }
    }
#endif

    // ---------- Sounds, made from scratch so there are no audio files to import ----------

    enum Sfx { Charge, Whoosh, Land, TaDa }
    static AudioClip[] sounds;

    static AudioClip Sound(Sfx sfx)
    {
        if (sounds == null || sounds[0] == null)
        {
            sounds = new[]
            {
                // Charge: a quick rising "vwip" while it crouches.
                Synth(0.32f, t => 200f + 900f * t / 0.32f, t => 0.35f * Mathf.Sin(Mathf.PI * t / 0.32f), 0f, 0.15f, 1f),
                // Whoosh: air rushing past as it spins, swelling at the top.
                Synth(1.1f, t => 0f, t => Mathf.Sin(Mathf.PI * t / 1.1f) * (0.6f + 0.4f * Mathf.Sin(2f * Mathf.PI * 3f * t)), 0f, 1f, 0.12f),
                // Land: a big wooden TUNG with a thud under it.
                Synth(0.9f, t => 95f * (1f + 0.5f * Mathf.Exp(-t / 0.03f)), t => Mathf.Exp(-t / 0.22f), 0.15f, 0.25f, 0.5f),
                // Ta-da: two quick bright notes.
                Synth(0.5f, t => t < 0.12f ? 784f : 1047f, t => Mathf.Exp(-((t < 0.12f ? t : t - 0.12f) / 0.18f)) * 0.5f, 0.3f, 0f, 1f),
            };
        }
        return sounds[(int)sfx];
    }

    // A tiny synthesizer: a tone following `pitch` (Hz) at `volume`, mixed toward a square wave and
    // toward noise, with the noise smoothed by `mellow` (1 = raw hiss, smaller = softer rumble).
    static AudioClip Synth(float seconds, System.Func<float, float> pitch, System.Func<float, float> volume,
                           float square, float noise, float mellow)
    {
        const int rate = 22050;
        float[] samples = new float[Mathf.CeilToInt(seconds * rate)];
        float phase = 0f, hiss = 0f;
        for (int i = 0; i < samples.Length; i++)
        {
            float t = i / (float)rate;
            phase += 2f * Mathf.PI * pitch(t) / rate;
            float tone = Mathf.Lerp(Mathf.Sin(phase), Mathf.Sign(Mathf.Sin(phase)), square);
            hiss += (Random.Range(-1f, 1f) - hiss) * mellow;
            float rumble = 0.6f * hiss * Mathf.Sqrt((2f - mellow) / mellow); // smoothing makes noise quieter; undo that
            samples[i] = Mathf.Clamp(0.8f * volume(t) * Mathf.Lerp(tone, rumble, noise), -1f, 1f);
        }
        AudioClip clip = AudioClip.Create("Backflip", samples.Length, 1, rate, false);
        clip.SetData(samples, 0);
        return clip;
    }
}

// Plays one short effect for TungBackflip: calls step(t) every frame as t goes 0 -> 1, then removes itself.
class TungBackflipEffect : MonoBehaviour
{
    public float seconds;
    public System.Action<float> step;
    float age;

    void Update()
    {
        age += Time.deltaTime;
        step(Mathf.Clamp01(age / seconds));
        if (age >= seconds) Destroy(gameObject);
    }
}
