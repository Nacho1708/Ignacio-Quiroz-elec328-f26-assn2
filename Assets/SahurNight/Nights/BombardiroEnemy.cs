using System.Collections;
using UnityEngine;

// Bombardiro Crocodilo: a crocodile bomber plane. He circles high above the office (you hear his propellers,
// and you can see him on the roof camera), then the air-raid siren goes off, he lines up over the skylight
// and DIVES straight at you, much faster than anyone else. Look up and hit him with the beam before he
// reaches the skylight and he pulls up and flies off. Miss, and he crashes in.
public class BombardiroEnemy : Brainrot
{
    [Header("Flying")]
    public Vector3 circleCenter = new Vector3(0f, 0f, 4f);
    public float circleRadius = 10f;
    public float circleHeight = 16f;
    [Tooltip("Meters per second while circling.")]
    public float flySpeed = 10f;
    [Tooltip("Where he dives to: the skylight above you.")]
    public Vector3 skylight = new Vector3(0f, 2.9f, 0f);
    [Tooltip("How high he lines up before diving.")]
    public float diveTop = 22f;

    [Header("Model parts")]
    public Transform[] propellers;
    [Tooltip("Where to check if the beam hits him (nose, middle, tail...).")]
    public Transform[] hitPoints;
    [Tooltip("A light on his tail that blinks (so you can spot him in the night sky).")]
    public Renderer beacon;
    [Tooltip("His searchlight, sweeping the ground while he's flying.")]
    public Light searchlight;
    [Tooltip("A light near his engines that lights up his body.")]
    public Light engineGlow;

    enum State { Away, Circling, LiningUp, Diving, PullingUp }
    State state;
    float timer, angle, litTime, cooldown, progress;
    Vector3 position, from;
    Quaternion rotation;
    AudioSource engine;

    float DiveSeconds => Mathf.Lerp(3.2f, 1.8f, Level01);
    public override float Danger => state == State.Diving ? 1f : state == State.LiningUp ? 0.6f : 0f;
    public override AudioClip ScareSound => NightsSounds.Boom;
    protected override float JumpscareDistance => 1.6f; // he's big

    protected override void OnSetup()
    {
        engine = gameObject.AddComponent<AudioSource>();
        engine.clip = NightsSounds.Engine;
        engine.loop = true;
        engine.playOnAwake = false;
        engine.spatialBlend = 1f;
        engine.rolloffMode = AudioRolloffMode.Linear;
        engine.minDistance = 4f;
        engine.maxDistance = 70f;
        engine.dopplerLevel = 0.3f;
        Voice.maxDistance = 70f;
        Voice.minDistance = 5f;
    }

    protected override void OnStart()
    {
        state = State.Away;
        cooldown = Random.Range(4f, 8f);
        Show(false);
    }

    protected override void OnStop()
    {
        if (engine != null) engine.Stop();
        if (searchlight != null) searchlight.enabled = false;
        if (engineGlow != null) engineGlow.enabled = false;
    }

    protected override void Tick()
    {
        if (state == State.Away && cooldown <= 0f && Roll())
        {
            state = State.Circling;
            timer = Random.Range(5f, 9f);
            angle = Random.Range(0f, 2f * Mathf.PI);
            Show(true);
            engine.volume = 0.8f;
            engine.Play();
        }
    }

    protected override void Run()
    {
        if (propellers != null)
            foreach (Transform p in propellers) if (p != null) p.localRotation *= Quaternion.Euler(0f, 0f, 1500f * Time.deltaTime);
        if (beacon != null && state != State.Away) beacon.enabled = Mathf.Repeat(Time.time, 1f) < 0.25f;
        if (searchlight != null) searchlight.enabled = state != State.Away;
        if (engineGlow != null) engineGlow.enabled = state != State.Away;

        switch (state)
        {
            case State.Away:
                cooldown -= Time.deltaTime;
                return;

            case State.Circling:
                angle += flySpeed / circleRadius * Time.deltaTime;
                position = circleCenter + new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * circleRadius + Vector3.up * circleHeight;
                Vector3 tangent = new Vector3(-Mathf.Sin(angle), 0f, Mathf.Cos(angle));
                rotation = Quaternion.LookRotation(tangent) * Quaternion.Euler(0f, 0f, 25f); // banking into the turn
                timer -= Time.deltaTime;
                if (timer <= 0f)
                {
                    state = State.LiningUp;
                    from = position;
                    progress = 0f;
                    PlaySound(NightsSounds.Siren, 1f);
                }
                break;

            case State.LiningUp:
                progress += Time.deltaTime / 2f;
                Vector3 top = new Vector3(skylight.x, diveTop, skylight.z);
                position = Vector3.Lerp(from, top, Mathf.SmoothStep(0f, 1f, progress));
                rotation = Quaternion.Slerp(rotation, Quaternion.LookRotation(Vector3.down, Vector3.forward), Time.deltaTime * 3f);
                if (progress >= 1f) { state = State.Diving; progress = 0f; litTime = 0f; }
                break;

            case State.Diving:
                progress += Time.deltaTime / DiveSeconds;
                float k = progress * progress; // speeds up as he falls
                position = Vector3.Lerp(new Vector3(skylight.x, diveTop, skylight.z), skylight, k);
                rotation = Quaternion.LookRotation(Vector3.down, Vector3.forward) * Quaternion.Euler(0f, 0f, progress * 540f); // spinning dive
                transform.SetPositionAndRotation(position, rotation);
                if (BeamOnHim()) litTime += Time.deltaTime;
                if (litTime >= 0.25f) { PullUp(); break; }
                if (progress >= 1f) { CatchPlayer(); return; }
                break;

            case State.PullingUp:
                progress += Time.deltaTime / 2.2f;
                Vector3 away = from + new Vector3(18f, 12f, 22f);
                Vector3 next = Vector3.Lerp(Vector3.Lerp(from, from + Vector3.up * 8f, progress), Vector3.Lerp(from + Vector3.up * 8f, away, progress), progress);
                if ((next - position).sqrMagnitude > 1e-6f) rotation = Quaternion.LookRotation(next - position);
                position = next;
                if (progress >= 1f)
                {
                    state = State.Away;
                    cooldown = Mathf.Lerp(14f, 6f, Level01);
                    Show(false);
                    engine.Stop();
                }
                break;
        }
        transform.SetPositionAndRotation(position, rotation);
    }

    bool BeamOnHim()
    {
        if (hitPoints == null || hitPoints.Length == 0) return Lit(transform.position);
        foreach (Transform p in hitPoints) if (p != null && Lit(p.position)) return true;
        return false;
    }

    void PullUp()
    {
        state = State.PullingUp;
        progress = 0f;
        from = position;
        PlaySound(NightsSounds.Whoosh, 1f);
        // He drops a dud bomb in the yard on his way out.
        NightsSounds.Play(NightsSounds.Boom, new Vector3(Random.Range(-6f, 6f), 0f, Random.Range(10f, 18f)), 0.6f);
    }

    public override void Scare()
    {
        if (state == State.Circling || state == State.LiningUp || state == State.Diving) PullUp();
    }

    public override IEnumerator Jumpscare(Transform view)
    {
        Game.Confetti(view.position + Flat(view.forward) * 1f, 60);
        NightsSounds.Play(NightsSounds.AirHorn, view.position, 0.8f);
        yield return base.Jumpscare(view);
    }

    protected override void OnCelebrateStart()
    {
        Game.Confetti(transform.position + Vector3.up, 40);
        if (engine != null) { engine.volume = 0.4f; engine.Play(); }
    }

    protected override void Celebrate(float t)
    {
        // Crash-landed, spinning on his belly with the propellers still going.
        transform.rotation = Quaternion.Euler(-8f, t * 120f, 6f * Mathf.Sin(t * 5f));
        Vector3 p = transform.position;
        p.y = 0.3f + Mathf.Abs(Mathf.Sin(t * 3f)) * 0.1f; // belly on the floor
        transform.position = p;
        if (propellers != null)
            foreach (Transform pr in propellers) if (pr != null) pr.localRotation *= Quaternion.Euler(0f, 0f, 900f * Time.deltaTime);
    }
}
