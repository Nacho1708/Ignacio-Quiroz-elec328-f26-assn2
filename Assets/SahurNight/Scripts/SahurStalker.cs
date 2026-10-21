using UnityEngine;

// Tung Tung Tung Sahur, creeping toward the office.
// He walks along one of several paths (window, door, side hall), stopping at each station.
// He only moves while he's NOT in the flashlight beam, and every step goes "tung" so you can hear where he is.
// Keep the beam on him for a moment and he runs off, then comes back along a random path.
// If he reaches the last station of his path and stays unlit too long, he catches you.
public class SahurStalker : MonoBehaviour
{
    [System.Serializable]
    public class Path
    {
        public string name;
        [Tooltip("Stations from far away to right outside the office.")]
        public Transform[] stations;
    }

    public Path[] paths;
    public EnchantedFlashlight flashlight;

    [Header("Difficulty (the game changes these every hour)")]
    [Tooltip("Seconds he waits at a station before moving closer (random between x and y).")]
    public Vector2 waitTime = new Vector2(9f, 13f);
    [Tooltip("Walking speed, in meters per second.")]
    public float walkSpeed = 0.8f;
    [Tooltip("Seconds of light it takes to chase him off.")]
    public float repelTime = 1.3f;
    [Tooltip("Seconds you have to light him once he's at the last station.")]
    public float graceTime = 5f;

    public enum State { Waiting, Walking, Fleeing, Gone, Caught, Asleep }
    public State CurrentState { get; private set; } = State.Asleep;
    public int PathIndex { get; private set; }
    public int Station { get; private set; }
    public bool IsLit { get; private set; }
    public bool AtLastStation => CurrentState == State.Waiting && Station == paths[PathIndex].stations.Length - 1;
    public float Danger => AtLastStation ? Mathf.Clamp01(lastStationTimer / graceTime) : 0f;
    public event System.Action<SahurStalker> Caught;

    Renderer[] renderers;
    AudioSource voice;
    float timer;
    float repel;
    float lastStationTimer;
    float stepTimer;
    Vector3 walkTo, rest;      // rest = the point on the floor he stands on
    float height = 1.8f;
    float footOffset;          // how far his pivot is above his feet (Tung's pivot is a bit below them)

    void Awake()
    {
        renderers = GetComponentsInChildren<Renderer>();
        voice = gameObject.AddComponent<AudioSource>();
        voice.spatialBlend = 1f;      // 3D: you hear which way he is
        voice.rolloffMode = AudioRolloffMode.Linear;
        voice.minDistance = 1f;
        voice.maxDistance = 22f;
        voice.dopplerLevel = 0f;
        Bounds b = MeshBounds();
        height = Mathf.Max(0.5f, b.size.y);
        footOffset = transform.position.y - b.min.y;
        SetVisible(false);
    }

    // Start stalking from the far end of a random path.
    public void Begin()
    {
        enabled = true;
        AppearAtFarStation(Random.Range(0, paths.Length));
    }

    public void Sleep()
    {
        CurrentState = State.Asleep;
        SetVisible(false);
    }

    // Stop where he is (used when he catches you).
    public void Freeze() => CurrentState = State.Caught;

    void Update()
    {
        if (CurrentState == State.Asleep || CurrentState == State.Caught) return;

        IsLit = CurrentState != State.Gone && InBeam();
        repel = IsLit ? repel + Time.deltaTime : Mathf.Max(0f, repel - 0.5f * Time.deltaTime);

        switch (CurrentState)
        {
            case State.Waiting:
                if (repel >= repelTime) { Flee(); break; }
                if (IsLit) break; // frozen in the light
                if (Station == paths[PathIndex].stations.Length - 1)
                {
                    lastStationTimer += Time.deltaTime;
                    if (lastStationTimer >= graceTime) { CurrentState = State.Caught; Caught?.Invoke(this); }
                    break;
                }
                timer -= Time.deltaTime;
                if (timer <= 0f) StartWalking();
                break;

            case State.Walking:
                if (repel >= repelTime) { Flee(); break; }
                if (IsLit) break; // stops mid-step while lit
                Vector3 next = Vector3.MoveTowards(rest, walkTo, walkSpeed * Time.deltaTime);
                rest = next;
                stepTimer -= Time.deltaTime;
                if (stepTimer <= 0f) { stepTimer = 0.55f / Mathf.Max(0.3f, walkSpeed); voice.PlayOneShot(SahurNightSounds.Step, 0.9f); }
                if ((rest - walkTo).sqrMagnitude < 1e-4f) Arrive();
                break;

            case State.Fleeing:
                timer -= Time.deltaTime;
                float sink = 1f - Mathf.Clamp01(timer / 0.5f);
                transform.position = rest + Vector3.up * (footOffset - height * sink); // sinks into the floor
                if (timer <= 0f) { CurrentState = State.Gone; timer = Random.Range(2f, 4f); SetVisible(false); }
                return;

            case State.Gone:
                timer -= Time.deltaTime;
                if (timer <= 0f) AppearAtFarStation(OtherPath());
                return;
        }
        Pose();
    }

    // Is he inside the flashlight beam, with nothing (like a wall) in between?
    bool InBeam()
    {
        if (flashlight == null || !flashlight.IsOn) return false;
        Vector3 origin = flashlight.Origin;
        foreach (float h in new[] { 0.35f, 0.6f, 0.85f })
        {
            Vector3 point = rest + Vector3.up * (height * h);
            Vector3 toPoint = point - origin;
            float dist = toPoint.magnitude;
            if (dist > flashlight.range) continue;
            if (Vector3.Angle(flashlight.Direction, toPoint) > flashlight.coneAngle * 0.5f + 2f) continue;
            if (Physics.Raycast(origin, toPoint / dist, out RaycastHit hit, dist, ~0, QueryTriggerInteraction.Ignore) &&
                !hit.transform.IsChildOf(transform))
                continue; // something's in the way
            return true;
        }
        return false;
    }

    void StartWalking()
    {
        Transform[] stations = paths[PathIndex].stations;
        walkTo = stations[Station + 1].position;
        CurrentState = State.Walking;
        stepTimer = 0f;
    }

    void Arrive()
    {
        Station++;
        CurrentState = State.Waiting;
        timer = Random.Range(waitTime.x, waitTime.y);
        lastStationTimer = 0f;
    }

    void Flee()
    {
        CurrentState = State.Fleeing;
        timer = 0.6f;
        repel = 0f;
        voice.PlayOneShot(SahurNightSounds.Retreat, 1f);
    }

    void AppearAtFarStation(int path)
    {
        PathIndex = path;
        Station = 0;
        rest = paths[path].stations[0].position;
        CurrentState = State.Waiting;
        timer = Random.Range(waitTime.x, waitTime.y);
        lastStationTimer = 0f;
        repel = 0f;
        SetVisible(true);
        Pose();
    }

    int OtherPath()
    {
        if (paths.Length < 2) return 0;
        int p = Random.Range(0, paths.Length - 1);
        return p >= PathIndex ? p + 1 : p;
    }

    // Stand at `rest`, face the player, bob while walking and shake while lit.
    void Pose()
    {
        Vector3 position = rest;
        if (CurrentState == State.Walking && !IsLit)
            position += Vector3.up * (0.04f * Mathf.Abs(Mathf.Sin(Time.time * 6f * walkSpeed)));
        if (IsLit)
            position += new Vector3(Mathf.Sin(Time.time * 53f), 0f, Mathf.Cos(Time.time * 47f)) * (0.025f * (0.4f + repel / repelTime));
        transform.position = position + Vector3.up * footOffset;

        Camera cam = Camera.main;
        if (cam != null)
        {
            Vector3 look = cam.transform.position - rest;
            look.y = 0f;
            if (look.sqrMagnitude > 0.01f)
                transform.rotation = Quaternion.LookRotation(-look) * Quaternion.Euler(270f, 0f, 0f); // the model's front is its -Z
        }
    }

    // Walk calmly up to `spot` (used for the caught ending).
    public void WalkTo(Vector3 spot)
    {
        rest = spot;
        Pose();
    }

    // His size in the world, measured from the mesh itself (works even while he's hidden).
    Bounds MeshBounds()
    {
        Bounds world = new Bounds(transform.position, Vector3.zero);
        bool first = true;
        foreach (MeshFilter filter in GetComponentsInChildren<MeshFilter>())
        {
            if (filter.sharedMesh == null) continue;
            Bounds local = filter.sharedMesh.bounds;
            for (int i = 0; i < 8; i++)
            {
                Vector3 corner = local.center + Vector3.Scale(local.extents, new Vector3(i % 2 * 2 - 1, i / 2 % 2 * 2 - 1, i / 4 * 2 - 1));
                Vector3 point = filter.transform.TransformPoint(corner);
                if (first) { world = new Bounds(point, Vector3.zero); first = false; }
                else world.Encapsulate(point);
            }
        }
        if (first) foreach (Renderer r in renderers) world.Encapsulate(r.bounds);
        return world;
    }

    void SetVisible(bool visible)
    {
        foreach (Renderer r in renderers) r.enabled = visible;
        foreach (Collider c in GetComponentsInChildren<Collider>()) c.enabled = visible;
    }
}
