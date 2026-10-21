using UnityEngine;

// Ballerina Cappuccina: the opposite of Tung. She only dances closer while she's IN your light (she loves the spotlight),
// and freezes in the dark. Leave her in the dark long enough and she gets bored and pirouettes away.
// Her music box gets louder as she gets closer. She uses the same ways in as Tung, so flashing everything is a bad idea.
// When the power runs out, she's the one who comes for you.
public class BallerinaEnemy : PathWalker
{
    [Tooltip("Arms (raised over her head), swayed while she dances.")]
    public Transform[] arms;
    [Tooltip("Her glowing eyes, shown in the dark when the power is out.")]
    public Renderer[] eyes;

    enum State { Waiting, Dancing, Leaving, Gone }
    State state;
    bool lit;
    float litWindow;   // seconds lit since the last tick
    float dark;        // seconds in the dark
    float litAtLast;   // seconds lit at the last station
    float timer, spin;
    bool glowingEyes;
    AudioSource musicBox;

    float BoredSeconds => Mathf.Lerp(12f, 26f, Level01);
    float GraceSeconds => Mathf.Lerp(2.6f, 1.2f, Level01);
    float Speed => Mathf.Lerp(1.0f, 2.0f, Level01);

    public override float Danger => state == State.Waiting && AtLast ? Mathf.Clamp01(litAtLast / GraceSeconds) : 0f;
    public override AudioClip ScareSound => NightsSounds.SlideWhistle;

    protected override void OnSetup()
    {
        musicBox = gameObject.AddComponent<AudioSource>();
        musicBox.clip = NightsSounds.MusicBox;
        musicBox.loop = true;
        musicBox.playOnAwake = false;
        musicBox.spatialBlend = 1f;
        musicBox.rolloffMode = AudioRolloffMode.Linear;
        musicBox.minDistance = 1f;
        musicBox.maxDistance = 20f;
        musicBox.dopplerLevel = 0f;
    }

    protected override void OnStart()
    {
        Appear(RandomPath());
        state = State.Waiting;
        litWindow = dark = litAtLast = 0f;
        musicBox.volume = 0.2f;
        musicBox.Play();
    }

    protected override void OnStop()
    {
        if (musicBox != null) musicBox.Stop();
        glowingEyes = false;
    }

    protected override void Tick()
    {
        if (state == State.Waiting && !AtLast && litWindow >= 0.35f && Roll())
        {
            StartWalking();
            state = State.Dancing;
        }
        litWindow = 0f;
    }

    protected override void Run()
    {
        switch (state)
        {
            case State.Leaving:
                timer -= Time.deltaTime;
                spin += 900f * Time.deltaTime;
                transform.position = Rest + Vector3.down * (Height * (1f - Mathf.Clamp01(timer / 0.8f)));
                transform.rotation = Quaternion.Euler(0f, spin, 0f);
                if (timer <= 0f) { state = State.Gone; timer = Random.Range(3f, 6f); Show(false); musicBox.Stop(); }
                return;
            case State.Gone:
                timer -= Time.deltaTime;
                if (timer <= 0f)
                {
                    Appear(OtherPath());
                    state = State.Waiting;
                    litWindow = dark = litAtLast = 0f;
                    musicBox.Play();
                }
                return;
        }

        lit = LitBody(Rest);
        if (lit) { litWindow += Time.deltaTime; dark = 0f; }
        else dark += Time.deltaTime;

        // Louder as she gets closer, a little faster while she dances in the light.
        musicBox.volume = 0.2f + 0.6f * Progress + (lit ? 0.2f : 0f);
        musicBox.pitch = lit ? 1.06f : 1f;

        if (state == State.Dancing)
        {
            if (lit)
            {
                spin += 540f * Time.deltaTime;
                if (Step(Speed)) state = State.Waiting;
            }
        }
        else if (AtLast && lit)
        {
            litAtLast += Time.deltaTime;
            if (litAtLast >= GraceSeconds) { CatchPlayer(); return; }
        }
        else if (!lit) litAtLast = Mathf.Max(0f, litAtLast - 0.5f * Time.deltaTime);

        if (dark >= BoredSeconds) { Scare(); return; } // bored: off she goes

        Place(Rest);
        if (state == State.Dancing && lit) transform.rotation = Quaternion.Euler(0f, spin, 0f); // pirouette
        Pose(lit ? 1f : 0.25f);
    }

    void Pose(float amount)
    {
        if (arms == null) return;
        for (int i = 0; i < arms.Length; i++)
            if (arms[i] != null)
                arms[i].localRotation = Quaternion.Euler(0f, 0f, (i == 0 ? 1f : -1f) * 12f * amount * Mathf.Sin(Time.time * 4f));
    }

    public override void Scare()
    {
        if (state == State.Leaving || state == State.Gone) return;
        state = State.Leaving;
        timer = 0.8f;
    }

    // When the power runs out: she appears in the doorway with glowing eyes and her music box playing.
    public void PowerOutVisit(Vector3 feet)
    {
        StopAllCoroutines();
        glowingEyes = true;
        Show(true);
        Rest = feet;
        Place(feet);
        musicBox.volume = 1f;
        musicBox.pitch = 0.94f;
        musicBox.Play();
    }

    // The glowing eyes only show up when the power is out.
    protected override void OnShown(bool visible)
    {
        if (eyes == null) return;
        foreach (Renderer r in eyes) if (r != null) r.enabled = visible && glowingEyes;
    }

    protected override void Celebrate(float t)
    {
        // A very fast pirouette with fluttering arms.
        transform.rotation = Quaternion.Euler(0f, t * 720f, 0f);
        Vector3 p = transform.position;
        p.y = Mathf.Abs(Mathf.Sin(t * 3f)) * 0.08f;
        transform.position = p;
        Pose(1f);
        if (!musicBox.isPlaying) { musicBox.volume = 0.8f; musicBox.Play(); }
    }
}
