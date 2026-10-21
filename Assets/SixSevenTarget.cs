using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.XR;
using UnityEngine.Rendering;

// Drag this onto any character and press Play. Nothing else to set up.
// Look at the character and do the "6 7" with your VR controllers (right hand up + left hand down, then swap):
// every 6-7 fires lasers from your hands at it and knocks one block off its lifebar.
// When the lifebar is empty it wobbles, puffs up and explodes into confetti, then pops back a few seconds later.
// No headset? Click in the Game view and press 6, 7, 6, 7 on the keyboard.
[DisallowMultipleComponent]
public class SixSevenTarget : MonoBehaviour
{
    [Header("Health")]
    [Tooltip("How many 67 lasers it takes to blow it up.")]
    [Range(1, 20)] public int hitsToExplode = 7;
    [Tooltip("Seconds until it comes back after exploding. 0 = it stays gone.")]
    [Range(0f, 30f)] public float respawnSeconds = 3f;

    [Header("Laser")]
    [Tooltip("How close to the middle of your view it has to be to get hit, in degrees.")]
    [Range(5f, 90f)] public float aimAngle = 35f;
    public Color laserColor = new Color(1f, 0.15f, 0.6f);

    [Header("Feedback")]
    public bool playSounds = true;
    public bool vibrateControllers = true;

    const float WindUpSeconds = 0.9f;

    static readonly Color HitColor = new Color(1f, 0.1f, 0.1f);
    static readonly Color Green = new Color(0.3f, 0.9f, 0.3f);
    static readonly Color Yellow = new Color(1f, 0.8f, 0.15f);
    static readonly Color Red = new Color(1f, 0.25f, 0.2f);
    static readonly Color EmptyBlock = new Color(0.25f, 0.25f, 0.28f);
    static readonly Color BarBack = new Color(0.05f, 0.05f, 0.07f);
    static readonly Color PuffColor = new Color(0.95f, 0.95f, 0.92f);
    static readonly Color[] Confetti =
    {
        new Color(1f, 0.2f, 0.3f), new Color(1f, 0.8f, 0.1f), new Color(0.2f, 0.85f, 0.3f),
        new Color(0.2f, 0.6f, 1f), new Color(0.75f, 0.3f, 1f), new Color(1f, 0.5f, 0.1f),
    };
    static readonly int BaseColorId = Shader.PropertyToID("_BaseColor"); // URP
    static readonly int ColorId = Shader.PropertyToID("_Color");         // built-in pipeline

    int hits;
    bool exploding;
    float lastHitTime = float.NegativeInfinity;
    bool tinted;
    Renderer[] renderers;
    Collider[] colliders;
    bool[] rendererWasOn;
    bool[] colliderWasOn;
    MaterialPropertyBlock tint;
    Transform bar;
    Transform barBack;
    Renderer[] barBlocks;
    Transform clone;
    readonly List<Mesh> cloneMeshes = new List<Mesh>();

    bool CanBeHit => isActiveAndEnabled && !exploding;

    void Awake()
    {
        var found = new List<Renderer>();
        foreach (Renderer r in GetComponentsInChildren<Renderer>())
            if (r is MeshRenderer || r is SkinnedMeshRenderer || r is SpriteRenderer)
                found.Add(r);
        renderers = found.ToArray();
        colliders = GetComponentsInChildren<Collider>();
        tint = new MaterialPropertyBlock();
    }

    void OnEnable()
    {
        targets.Add(this);
    }

    void OnDisable()
    {
        targets.Remove(this);
        if (exploding)
        {
            // Turned off mid-explosion: put the character back.
            StopAllCoroutines();
            DestroyClone();
            SetVisible(true);
            exploding = false;
            hits = 0;
        }
        Tint(renderers, 0f);
        tinted = false;
        if (bar != null) bar.gameObject.SetActive(false);
    }

    void OnDestroy()
    {
        if (bar != null) Destroy(bar.gameObject);
        DestroyClone();
    }

    void Update()
    {
        // The first target to update each frame checks the gesture for all of them.
        if (checkedFrame != Time.frameCount)
        {
            checkedFrame = Time.frameCount;
            CheckGesture();
        }

        // Flash red for a moment after a hit.
        float flash = 1f - (Time.time - lastHitTime) / 0.25f;
        if (!exploding && (flash > 0f || tinted))
        {
            Tint(renderers, Mathf.Max(flash, 0f));
            tinted = flash > 0f;
        }

        UpdateBar();
    }

    void TakeHit(Vector3 point)
    {
        if (!CanBeHit) return;

        hits++;
        lastHitTime = Time.time;
        Sparks(point, laserColor, Size(CurrentBounds));
        if (hits >= hitsToExplode)
            StartCoroutine(Explode());
    }

    // ---------- Lifebar: one block per hit it can take, floating above its head ----------

    void UpdateBar()
    {
        int blocks = Mathf.Max(1, hitsToExplode);
        if (bar == null || barBlocks.Length != blocks)
            BuildBar(blocks);
        bar.gameObject.SetActive(!exploding);
        if (exploding) return;

        Bounds b = CurrentBounds;
        float width = Mathf.Clamp(b.size.y * 0.35f, 0.25f, 1.2f);
        float height = width * 0.12f;
        float gap = height * 0.25f;
        bar.position = new Vector3(b.center.x, b.max.y + height * 2f, b.center.z);

        // Turn to face you.
        Camera cam = Camera.main;
        if (cam != null)
        {
            Vector3 away = bar.position - cam.transform.position;
            away.y = 0f;
            if (away.sqrMagnitude > 1e-6f) bar.rotation = Quaternion.LookRotation(away);
        }

        barBack.localScale = new Vector3(width + 2f * gap, height + 2f * gap, height * 0.5f);
        int remaining = hitsToExplode - hits;
        Material full = ColorMaterial(remaining > blocks * 0.6f ? Green : remaining > blocks * 0.3f ? Yellow : Red);
        float blockWidth = (width - gap * (blocks - 1)) / blocks;
        for (int i = 0; i < blocks; i++)
        {
            Transform block = barBlocks[i].transform;
            block.localPosition = new Vector3(-0.5f * width + (i + 0.5f) * blockWidth + i * gap, 0f, -0.3f * height);
            block.localScale = new Vector3(blockWidth, height, 0.5f * height);
            barBlocks[i].sharedMaterial = i < remaining ? full : ColorMaterial(EmptyBlock);
        }
    }

    void BuildBar(int blocks)
    {
        if (bar != null) Destroy(bar.gameObject);
        bar = new GameObject(name + " Lifebar").transform;
        barBack = Piece("Back", Cube, ColorMaterial(BarBack), Vector3.zero, Vector3.one).transform;
        barBack.SetParent(bar, false);
        barBlocks = new Renderer[blocks];
        for (int i = 0; i < blocks; i++)
        {
            GameObject block = Piece("Block", Cube, ColorMaterial(Green), Vector3.zero, Vector3.one);
            block.transform.SetParent(bar, false);
            barBlocks[i] = block.GetComponent<Renderer>();
        }
    }

    // ---------- The explosion ----------

    IEnumerator Explode()
    {
        exploding = true;
        Bounds b = CurrentBounds;
        float size = Size(b);
        Vector3 feet = new Vector3(b.center.x, b.min.y, b.center.z);

        // Animate a look-alike copy instead of the real character, so other scripts on it are left alone.
        MakeClone(feet);
        SetVisible(false);
        if (bar != null) bar.gameObject.SetActive(false);
        Tint(renderers, 0f);
        tinted = false;
        Renderer[] cloneRenderers = clone.GetComponentsInChildren<Renderer>();
        if (playSounds) PlaySound(Sfx.WindUp, b.center, 0.8f);

        // Wind-up: wobble faster and faster, puff up, shake and flash red.
        for (float t = 0f; t < 1f; t += Time.deltaTime / WindUpSeconds)
        {
            float seconds = t * WindUpSeconds;
            float phase = 2f * Mathf.PI * (3f * seconds + 6f * seconds * seconds / WindUpSeconds); // 3 -> 15 wobbles/s
            float squash = (0.05f + 0.2f * t) * Mathf.Sin(phase);
            float puff = 1f + 0.4f * t * t;
            clone.localScale = new Vector3(puff * (1f + squash), puff * (1f - squash), puff * (1f + squash));
            clone.position = feet + Random.insideUnitSphere * (0.02f * size * t);
            Tint(cloneRenderers, Mathf.Sin(2f * phase) > 0f ? 0.25f + 0.6f * t : 0f);
            yield return null;
        }

        // POP!
        DestroyClone();
        Burst(b, size);
        if (playSounds) PlaySound(Sfx.Boom, b.center, 1f);
        if (vibrateControllers) Buzz(1f, 0.35f);

        if (respawnSeconds <= 0f) yield break; // gone for good
        yield return new WaitForSeconds(respawnSeconds);

        // Pop back in with a springy bounce.
        MakeClone(feet);
        if (playSounds) PlaySound(Sfx.Boing, b.center, 0.8f);
        for (float t = 0f; t < 1f; t += Time.deltaTime / 0.45f)
        {
            clone.localScale = Vector3.one * (1f - Mathf.Exp(-6f * t) * Mathf.Cos(3f * Mathf.PI * t));
            yield return null;
        }
        DestroyClone();
        hits = 0;
        SetVisible(true);
        exploding = false;
    }

    // Chunks of the character, confetti and puffs of smoke flying everywhere.
    void Burst(Bounds b, float size)
    {
        float scale = Mathf.Clamp(size / 1.8f, 0.3f, 3f); // tuned for a person-sized character
        Material chunkMaterial = ColorMaterial(Color.gray);
        foreach (Renderer r in renderers)
            if (r != null && r.sharedMaterial != null) { chunkMaterial = r.sharedMaterial; break; }

        for (int i = 0; i < 24; i++)
        {
            Vector3 start = b.center + Vector3.Scale(Random.insideUnitSphere, b.extents);
            Vector3 velocity = ((start - b.center).normalized * Random.Range(1.5f, 4f) + Vector3.up * Random.Range(2f, 4.5f)) * scale;
            Debris(Cube, chunkMaterial, start, velocity, Vector3.one * (Random.Range(0.05f, 0.1f) * size),
                   b.min.y, Random.Range(1.3f, 2f), 9.81f, 0.3f);
        }
        for (int i = 0; i < 60; i++)
        {
            Vector3 velocity = (Random.onUnitSphere * Random.Range(2f, 5f) + Vector3.up * 3f) * scale;
            Debris(Cube, ColorMaterial(Confetti[i % Confetti.Length]), b.center, velocity,
                   new Vector3(0.03f, 0.003f, 0.02f) * size, b.min.y, Random.Range(2f, 3f), 3f, 2.5f);
        }
        for (int i = 0; i < 7; i++)
            Puff(b.center + Vector3.Scale(Random.insideUnitSphere, b.extents) * 0.6f, size * Random.Range(0.25f, 0.4f));
    }

    // A copy of how the character looks right now, scaled from `pivot` (its feet).
    void MakeClone(Vector3 pivot)
    {
        clone = new GameObject(name + " (exploding)").transform;
        clone.position = pivot;
        foreach (Renderer r in renderers)
        {
            if (r == null) continue;
            Mesh mesh = null;
            if (r is SkinnedMeshRenderer skinned)
            {
                mesh = new Mesh();
                skinned.BakeMesh(mesh); // freeze its current pose
                cloneMeshes.Add(mesh);
            }
            else if (r.TryGetComponent(out MeshFilter filter))
            {
                mesh = filter.sharedMesh;
            }
            if (mesh == null) continue;

            var piece = new GameObject(r.name);
            piece.transform.SetPositionAndRotation(r.transform.position, r.transform.rotation);
            piece.transform.localScale = r.transform.lossyScale;
            piece.transform.SetParent(clone, true);
            piece.AddComponent<MeshFilter>().sharedMesh = mesh;
            piece.AddComponent<MeshRenderer>().sharedMaterials = r.sharedMaterials;
        }
    }

    void DestroyClone()
    {
        if (clone != null) Destroy(clone.gameObject);
        clone = null;
        foreach (Mesh mesh in cloneMeshes) Destroy(mesh);
        cloneMeshes.Clear();
    }

    void SetVisible(bool visible)
    {
        if (!visible)
        {
            rendererWasOn = new bool[renderers.Length];
            for (int i = 0; i < renderers.Length; i++)
            {
                if (renderers[i] == null) continue;
                rendererWasOn[i] = renderers[i].enabled;
                renderers[i].enabled = false;
            }
            colliderWasOn = new bool[colliders.Length];
            for (int i = 0; i < colliders.Length; i++)
            {
                if (colliders[i] == null) continue;
                colliderWasOn[i] = colliders[i].enabled;
                colliders[i].enabled = false;
            }
        }
        else if (rendererWasOn != null)
        {
            for (int i = 0; i < renderers.Length; i++)
                if (renderers[i] != null) renderers[i].enabled = rendererWasOn[i];
            for (int i = 0; i < colliders.Length; i++)
                if (colliders[i] != null) colliders[i].enabled = colliderWasOn[i];
            rendererWasOn = null;
        }
    }

    // Tints renderers toward red (0 = their normal colors).
    void Tint(Renderer[] tinting, float amount)
    {
        foreach (Renderer r in tinting)
        {
            if (r == null) continue;
            Material[] materials = r.sharedMaterials;
            for (int i = 0; i < materials.Length; i++)
            {
                tint.Clear();
                if (amount > 0f && materials[i] != null)
                {
                    Material m = materials[i];
                    Color normal = m.HasProperty(BaseColorId) ? m.GetColor(BaseColorId)
                        : m.HasProperty(ColorId) ? m.GetColor(ColorId) : Color.white;
                    Color color = Color.Lerp(normal, HitColor, amount);
                    tint.SetColor(BaseColorId, color);
                    tint.SetColor(ColorId, color);
                }
                r.SetPropertyBlock(tint, i);
            }
        }
    }

    Bounds CurrentBounds
    {
        get
        {
            bool found = false;
            Bounds b = new Bounds(transform.position, Vector3.one);
            foreach (Renderer r in renderers)
            {
                if (r == null || !r.enabled) continue;
                if (found) b.Encapsulate(r.bounds);
                else b = r.bounds;
                found = true;
            }
            return b;
        }
    }

    static float Size(Bounds b)
    {
        return Mathf.Max(0.1f, b.size.x, b.size.y, b.size.z);
    }

    // ---------- The 6-7 gesture (same rules as SixSevenPulse), checked once per frame for all targets ----------

    const float HeightDifference = 0.1f; // meters one controller has to be above the other
    const float MaxPause = 1f;           // seconds allowed between swaps

    static readonly List<SixSevenTarget> targets = new List<SixSevenTarget>();
    static int checkedFrame = -1;
    static int pose;  // 1: right hand up, -1: left hand up, 0: level
    static int swaps; // swaps in a row so far
    static float lastSwapTime = float.NegativeInfinity;
    static bool tracking;
    static float rightLow, rightHigh, leftLow, leftHigh; // hand heights since the hands were last level

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics()
    {
        targets.Clear();
        checkedFrame = -1;
        pose = swaps = 0;
        lastSwapTime = float.NegativeInfinity;
        tracking = false;
        colorMaterials.Clear();
        sounds = null;
    }

    static void CheckGesture()
    {
        Keyboard keyboard = Keyboard.current;
        if (keyboard != null)
        {
            if (keyboard.digit6Key.wasPressedThisFrame && pose != 1) Swap(1);
            if (keyboard.digit7Key.wasPressedThisFrame && pose != -1) Swap(-1);
        }

        XRController right = XRController.rightHand;
        XRController left = XRController.leftHand;
        if (!IsTracked(right) || !IsTracked(left))
        {
            tracking = false;
            return;
        }

        // Heights in the headset's tracking space, which is in real-world meters.
        float rightY = right.devicePosition.ReadValue().y;
        float leftY = left.devicePosition.ReadValue().y;
        float difference = rightY - leftY;
        if (!tracking)
        {
            tracking = true;
            pose = 0;
            ResetHandRange(rightY, leftY);
        }

        if (Mathf.Abs(difference) < 0.5f * HeightDifference)
        {
            // Hands about level: ready for the next swap, and hand movement is measured from here.
            pose = 0;
            ResetHandRange(rightY, leftY);
            return;
        }
        rightLow = Mathf.Min(rightLow, rightY);
        rightHigh = Mathf.Max(rightHigh, rightY);
        leftLow = Mathf.Min(leftLow, leftY);
        leftHigh = Mathf.Max(leftHigh, leftY);

        // Both hands must really move (one up, one down), so waving just one hand doesn't count.
        float minMove = 0.25f * HeightDifference;
        if (pose != 1 && difference > HeightDifference &&
            rightY - rightLow > minMove && leftHigh - leftY > minMove)
        {
            Swap(1);
        }
        else if (pose != -1 && difference < -HeightDifference &&
                 rightHigh - rightY > minMove && leftY - leftLow > minMove)
        {
            Swap(-1);
        }
    }

    static void ResetHandRange(float rightY, float leftY)
    {
        rightLow = rightHigh = rightY;
        leftLow = leftHigh = leftY;
    }

    static bool IsTracked(XRController hand)
    {
        return hand != null && hand.isTracked != null && hand.isTracked.isPressed;
    }

    static void Swap(int newPose)
    {
        pose = newPose;
        if (Time.time - lastSwapTime > MaxPause) swaps = 0; // too slow: this starts a new streak
        swaps++;
        lastSwapTime = Time.time;
        if (swaps % 2 == 0) Shoot(); // a 6 and a 7 make one "67"
    }

    // ---------- Lasers ----------

    static void Shoot()
    {
        Camera cam = Camera.main;
        if (cam == null || targets.Count == 0) return;
        Transform head = cam.transform;

        // Hit whichever target is closest to the middle of your view.
        SixSevenTarget target = null;
        float bestAngle = float.MaxValue;
        foreach (SixSevenTarget t in targets)
        {
            if (!t.CanBeHit) continue;
            float angle = Vector3.Angle(head.forward, t.CurrentBounds.center - head.position);
            if (angle <= t.aimAngle && angle < bestAngle)
            {
                target = t;
                bestAngle = angle;
            }
        }

        SixSevenTarget style = target != null ? target : targets[0]; // whose color and settings to use
        Vector3 aim = target != null ? target.CurrentBounds.center : head.position + head.forward * 25f;
        HandPositions(head, out Vector3 rightHand, out Vector3 leftHand);
        Laser(rightHand, aim, style.laserColor, null);
        Laser(leftHand, aim, style.laserColor, () => { if (target != null) target.TakeHit(aim); });
        if (style.playSounds) PlaySound(Sfx.Pew, (rightHand + leftHand) * 0.5f, 0.6f);
        if (style.vibrateControllers) Buzz(0.4f, 0.08f);
    }

    // Where your hands are: the tracked controllers, or just below the camera without a headset.
    static void HandPositions(Transform head, out Vector3 right, out Vector3 left)
    {
        right = head.position + head.forward * 0.3f + head.right * 0.2f - head.up * 0.3f;
        left = head.position + head.forward * 0.3f - head.right * 0.2f - head.up * 0.3f;

        // Controller poses are relative to the XR rig (the camera's parent), not the world.
        Transform rig = head.parent;
        XRController rightController = XRController.rightHand;
        XRController leftController = XRController.leftHand;
        if (IsTracked(rightController)) right = ToWorld(rig, rightController.devicePosition.ReadValue());
        if (IsTracked(leftController)) left = ToWorld(rig, leftController.devicePosition.ReadValue());
    }

    static Vector3 ToWorld(Transform rig, Vector3 point)
    {
        return rig != null ? rig.TransformPoint(point) : point;
    }

    static void Laser(Vector3 from, Vector3 to, Color color, System.Action onArrive)
    {
        var go = new GameObject("67 Laser");
        LineRenderer line = go.AddComponent<LineRenderer>();
        line.sharedMaterial = ColorMaterial(color);
        line.shadowCastingMode = ShadowCastingMode.Off;
        line.receiveShadows = false;
        line.numCapVertices = 4;
        line.positionCount = 2;
        line.SetPosition(0, from);
        bool arrived = false;
        Animate(go, 0.35f, t =>
        {
            float reach = Mathf.Clamp01(t / 0.3f); // shoots out over the first 0.1 s...
            line.SetPosition(1, Vector3.Lerp(from, to, reach));
            line.widthMultiplier = 0.03f * (1f - Mathf.Clamp01((t - 0.3f) / 0.7f)); // ...then thins away
            if (reach >= 1f && !arrived)
            {
                arrived = true;
                onArrive?.Invoke();
            }
        });
    }

    // A little burst where a laser lands.
    static void Sparks(Vector3 point, Color color, float size)
    {
        float scale = Mathf.Clamp(size / 1.8f, 0.3f, 3f);
        Material material = ColorMaterial(color);
        for (int i = 0; i < 10; i++)
            Debris(Cube, material, point, Random.onUnitSphere * (Random.Range(1f, 3f) * scale),
                   Vector3.one * (0.025f * scale), float.NegativeInfinity, Random.Range(0.2f, 0.4f), 4f, 2f);

        GameObject flash = Piece("67 Flash", Sphere, material, point, Vector3.zero);
        Transform flashTransform = flash.transform;
        Animate(flash, 0.18f, t => flashTransform.localScale = Vector3.one * (0.3f * scale * Mathf.Sin(Mathf.PI * t)));
    }

    // A piece that flies off, spins, bounces on the floor and shrinks away.
    static void Debris(Mesh mesh, Material material, Vector3 position, Vector3 velocity, Vector3 scale,
                       float ground, float seconds, float gravity, float drag)
    {
        Transform piece = Piece("67 Debris", mesh, material, position, scale).transform;
        piece.rotation = Random.rotation;
        Vector3 spin = Random.insideUnitSphere * 900f; // degrees per second
        Animate(piece.gameObject, seconds, t =>
        {
            float dt = Time.deltaTime;
            velocity += Vector3.down * (gravity * dt);
            velocity *= Mathf.Exp(-drag * dt);
            position += velocity * dt;
            if (position.y < ground)
            {
                position.y = ground;
                velocity = new Vector3(velocity.x * 0.6f, -velocity.y * 0.4f, velocity.z * 0.6f);
                spin *= 0.5f;
            }
            piece.position = position;
            piece.Rotate(spin * dt, Space.World);
            piece.localScale = scale * Mathf.Clamp01((1f - t) / 0.25f); // shrink away at the end
        });
    }

    // A cartoon smoke puff: a ball that pops out, drifts up and shrinks away.
    static void Puff(Vector3 position, float size)
    {
        Transform puff = Piece("67 Puff", Sphere, ColorMaterial(PuffColor), position, Vector3.zero).transform;
        Animate(puff.gameObject, 0.8f, t =>
        {
            float grow = t < 0.2f ? t / 0.2f : 1f - (t - 0.2f) / 0.8f;
            puff.localScale = Vector3.one * (size * Mathf.SmoothStep(0f, 1f, grow));
            puff.position = position + Vector3.up * (0.3f * size * t);
        });
    }

    // ---------- Small helpers ----------

    // Runs step(t) every frame as t goes from 0 to 1 over `seconds`, then removes the object.
    static void Animate(GameObject go, float seconds, System.Action<float> step)
    {
        SixSevenEffect effect = go.AddComponent<SixSevenEffect>();
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

    static Mesh cube, sphere;
    static Mesh Cube => cube != null ? cube : (cube = PrimitiveMesh(PrimitiveType.Cube));
    static Mesh Sphere => sphere != null ? sphere : (sphere = PrimitiveMesh(PrimitiveType.Sphere));

    static Mesh PrimitiveMesh(PrimitiveType type)
    {
        GameObject temp = GameObject.CreatePrimitive(type);
        Mesh mesh = temp.GetComponent<MeshFilter>().sharedMesh;
        DestroyImmediate(temp);
        return mesh;
    }

    static readonly Dictionary<Color, Material> colorMaterials = new Dictionary<Color, Material>();

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

    static void Buzz(float strength, float seconds)
    {
        UnityEngine.XR.InputDevices.GetDeviceAtXRNode(UnityEngine.XR.XRNode.RightHand).SendHapticImpulse(0u, strength, seconds);
        UnityEngine.XR.InputDevices.GetDeviceAtXRNode(UnityEngine.XR.XRNode.LeftHand).SendHapticImpulse(0u, strength, seconds);
    }

    // ---------- Sounds, made from scratch so there are no audio files to import ----------

    enum Sfx { Pew, WindUp, Boom, Boing }
    static AudioClip[] sounds;

    static void PlaySound(Sfx sfx, Vector3 position, float volume)
    {
        if (sounds == null)
        {
            sounds = new[]
            {
                Synth(0.22f, t => 1500f * Mathf.Pow(0.12f, t / 0.22f), t => Mathf.Exp(-t / 0.08f), 0.5f, 0.05f),
                Synth(0.9f, t => 180f * Mathf.Pow(5f, t / 0.9f),
                      t => (0.3f + 0.7f * t / 0.9f) * (0.7f + 0.3f * Mathf.Sin(2f * Mathf.PI * 12f * t)), 0.3f, 0f),
                Synth(0.8f, t => 110f * Mathf.Pow(0.3f, t / 0.8f), t => Mathf.Exp(-t / 0.2f), 0f, 0.55f),
                Synth(0.5f, t => 180f + 250f * t + 40f * Mathf.Sin(2f * Mathf.PI * 16f * t),
                      t => Mathf.Min(1f, t / 0.01f) * (1f - t / 0.5f), 0f, 0f),
            };
        }
        AudioSource.PlayClipAtPoint(sounds[(int)sfx], position, volume);
    }

    // A tiny synthesizer: a tone following `pitch` (Hz) at `volume`, mixed toward a square wave and noise.
    static AudioClip Synth(float seconds, System.Func<float, float> pitch, System.Func<float, float> volume,
                           float square, float noise)
    {
        const int rate = 22050;
        float[] samples = new float[Mathf.CeilToInt(seconds * rate)];
        float phase = 0f;
        for (int i = 0; i < samples.Length; i++)
        {
            float t = i / (float)rate;
            phase += 2f * Mathf.PI * pitch(t) / rate;
            float tone = Mathf.Lerp(Mathf.Sin(phase), Mathf.Sign(Mathf.Sin(phase)), square);
            samples[i] = 0.8f * volume(t) * Mathf.Lerp(tone, Random.Range(-1f, 1f), noise);
        }
        AudioClip clip = AudioClip.Create("67 Sound", samples.Length, 1, rate, false);
        clip.SetData(samples, 0);
        return clip;
    }
}

// Plays one short effect for SixSevenTarget: calls step(t) every frame as t goes 0 -> 1, then removes itself.
class SixSevenEffect : MonoBehaviour
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
