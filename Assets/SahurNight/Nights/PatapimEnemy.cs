using UnityEngine;

// Brr Brr Patapim: a tree-monkey with giant feet who creeps around INSIDE the office.
// He's invisible except inside your flashlight beam (his materials use the see-through Enchanted Ghost shader),
// and he doesn't show up on the cameras. Listen for "brr brr" and sweep the room with the light to find him.
// Keep the beam on him and he burrows away. Leave him alone right next to the rails and he gets you.
public class PatapimEnemy : Brainrot
{
    [System.Serializable]
    public class Lane
    {
        public Transform far, mid, near;
    }

    [Tooltip("Ways he creeps toward you: from a far corner, to the middle, to right next to the rails.")]
    public Lane[] lanes;

    [Header("Model parts")]
    public Transform[] feet;
    public Transform[] arms;
    [Tooltip("His see-through parts, and the solid materials they switch to when he's done hiding (when he gets you).")]
    public Renderer[] ghostParts;
    public Material[] solidMaterials;

    enum State { Waiting, Creeping, Burrowing, Gone }
    State state;
    int lane, spot;      // spot: 0 far, 1 mid, 2 near
    Vector3 feetAt, target;
    bool lit;
    float repel, grace, timer, whisper, walkClock;
    Material[][] ghostMaterials;

    float RepelSeconds => Mathf.Lerp(0.8f, 1.5f, Level01);
    float GraceSeconds => Mathf.Lerp(6f, 3f, Level01);
    public override float Danger => state == State.Waiting && spot == 2 ? Mathf.Clamp01(grace / GraceSeconds) : 0f;
    public override AudioClip ScareSound => NightsSounds.Boing;

    protected override void OnSetup()
    {
        ghostMaterials = new Material[ghostParts.Length][];
        for (int i = 0; i < ghostParts.Length; i++) ghostMaterials[i] = ghostParts[i].sharedMaterials;
    }

    protected override void OnStart()
    {
        SetSolid(false);
        AppearAt(Random.Range(0, lanes.Length));
    }

    protected override void OnStop() => SetSolid(false);

    void AppearAt(int newLane)
    {
        lane = newLane;
        spot = 0;
        feetAt = Spot(0).position;
        state = State.Waiting;
        repel = grace = 0f;
        whisper = Random.Range(3f, 7f);
        Show(true);
        Place();
    }

    Transform Spot(int i) => i == 0 ? lanes[lane].far : i == 1 ? lanes[lane].mid : lanes[lane].near;

    protected override void Tick()
    {
        if (state == State.Waiting && spot < 2 && !lit && Roll())
        {
            target = Spot(spot + 1).position;
            state = State.Creeping;
        }
    }

    protected override void Run()
    {
        switch (state)
        {
            case State.Burrowing:
                timer -= Time.deltaTime;
                transform.position = feetAt + Vector3.down * (Height * (1f - Mathf.Clamp01(timer / 0.7f)));
                if (timer <= 0f) { state = State.Gone; timer = Random.Range(3f, 6f); Show(false); }
                return;
            case State.Gone:
                timer -= Time.deltaTime;
                if (timer <= 0f)
                {
                    int next = Random.Range(0, lanes.Length - 1);
                    AppearAt(next >= lane ? next + 1 : next);
                }
                return;
        }

        lit = LitBody(feetAt);
        repel = lit ? repel + Time.deltaTime : Mathf.Max(0f, repel - 0.5f * Time.deltaTime);
        if (repel >= RepelSeconds) { Scare(); return; }

        if (state == State.Creeping && !lit)
        {
            walkClock += Time.deltaTime;
            feetAt = Vector3.MoveTowards(feetAt, target, 0.9f * Time.deltaTime);
            if ((feetAt - target).sqrMagnitude < 1e-4f)
            {
                spot++;
                state = State.Waiting;
                grace = 0f;
                PlaySound(NightsSounds.Brr, 0.9f);
            }
        }
        else if (state == State.Waiting && spot == 2 && !lit)
        {
            grace += Time.deltaTime;
            if (grace >= GraceSeconds) { CatchPlayer(); return; }
        }

        // Every now and then: "brr brr..."
        whisper -= Time.deltaTime;
        if (whisper <= 0f) { whisper = Random.Range(6f, 11f); PlaySound(NightsSounds.Brr, 0.7f); }

        Place();
    }

    void Place()
    {
        Vector3 shiver = lit ? new Vector3(Mathf.Sin(Time.time * 61f), 0f, Mathf.Cos(Time.time * 53f)) * 0.02f : Vector3.zero;
        transform.position = feetAt + shiver;
        FacePlayer();
        bool moving = state == State.Creeping && !lit;
        if (feet != null)
            for (int i = 0; i < feet.Length; i++)
                if (feet[i] != null)
                    feet[i].localPosition = new Vector3(feet[i].localPosition.x, moving ? Mathf.Max(0f, 0.08f * Mathf.Sin(walkClock * 7f + i * Mathf.PI)) : 0f, feet[i].localPosition.z);
        if (arms != null)
            for (int i = 0; i < arms.Length; i++)
                if (arms[i] != null)
                    arms[i].localRotation = Quaternion.Euler(0f, 0f, (i == 0 ? 1f : -1f) * (moving ? 15f : 5f) * Mathf.Sin(Time.time * 3f + i));
    }

    public override void Scare()
    {
        if (state == State.Burrowing || state == State.Gone) return;
        state = State.Burrowing;
        timer = 0.7f;
        repel = 0f;
        PlaySound(SahurNightSounds.Retreat, 0.9f);
    }

    // When he gets you he stops hiding: his see-through materials switch to solid ones.
    void SetSolid(bool solid)
    {
        if (ghostParts == null || ghostMaterials == null) return;
        for (int i = 0; i < ghostParts.Length; i++)
        {
            if (ghostParts[i] == null) continue;
            if (solid && solidMaterials != null && i < solidMaterials.Length && solidMaterials[i] != null)
            {
                var slots = new Material[ghostParts[i].sharedMaterials.Length];
                for (int s = 0; s < slots.Length; s++) slots[s] = solidMaterials[i];
                ghostParts[i].sharedMaterials = slots;
            }
            else ghostParts[i].sharedMaterials = ghostMaterials[i];
        }
    }

    public override System.Collections.IEnumerator Jumpscare(Transform view)
    {
        SetSolid(true);
        yield return base.Jumpscare(view);
    }

    protected override void OnCelebrateStart() => SetSolid(true);

    protected override void Celebrate(float t)
    {
        // Stomp-hops on his giant feet.
        Vector3 p = transform.position;
        p.y = Mathf.Abs(Mathf.Sin(t * 4f)) * 0.3f;
        transform.position = p;
        FacePlayer();
        if (arms != null)
            for (int i = 0; i < arms.Length; i++)
                if (arms[i] != null) arms[i].localRotation = Quaternion.Euler(0f, 0f, (i == 0 ? 1f : -1f) * 35f * Mathf.Sin(t * 8f));
    }
}
