using UnityEngine;

// Tung Tung Tung Sahur: only moves in the dark, station by station, from the yard, the hallway or the side hall.
// Hold the light on him and he sinks into the floor and comes back another way.
// At the last station you have a few seconds to light him (or have the door shut, if he's at the door).
public class TungEnemy : PathWalker
{
    [Tooltip("His drum dance (on the model), played when he gets you.")]
    public TungDance dance;

    enum State { Waiting, Walking, Fleeing, Gone }
    State state;
    bool lit;
    float repel, grace, timer, stepTimer;

    float RepelSeconds => Mathf.Lerp(1.0f, 1.8f, Level01);
    float GraceSeconds => Mathf.Lerp(6f, 2.5f, Level01);
    float Speed => Mathf.Lerp(0.7f, 1.5f, Level01);

    public override float Danger => state == State.Waiting && AtLast ? Mathf.Clamp01(grace / GraceSeconds) : 0f;
    public override AudioClip ScareSound => NightsSounds.AirHorn;

    protected override void OnStart()
    {
        Appear(RandomPath());
        state = State.Waiting;
        repel = grace = 0f;
    }

    protected override void OnStop()
    {
        if (dance != null) dance.enabled = false;
    }

    protected override void Tick()
    {
        if (state == State.Waiting && !AtLast && !lit && Roll())
        {
            StartWalking();
            state = State.Walking;
            stepTimer = 0f;
        }
    }

    protected override void Run()
    {
        switch (state)
        {
            case State.Fleeing:
                timer -= Time.deltaTime;
                transform.position = Rest + Vector3.down * (Height * (1f - Mathf.Clamp01(timer / 0.6f))); // sinks into the floor
                if (timer <= 0f) { state = State.Gone; timer = Random.Range(2f, 4f); Show(false); }
                return;
            case State.Gone:
                timer -= Time.deltaTime;
                if (timer <= 0f) { Appear(OtherPath()); state = State.Waiting; repel = grace = 0f; }
                return;
        }

        lit = LitBody(Rest);
        repel = lit ? repel + Time.deltaTime : Mathf.Max(0f, repel - 0.5f * Time.deltaTime);
        if (repel >= RepelSeconds) { Scare(); return; }

        if (state == State.Walking && !lit)
        {
            stepTimer -= Time.deltaTime;
            if (stepTimer <= 0f) { stepTimer = 0.55f / Speed; PlaySound(SahurNightSounds.Step, 0.9f); }
            if (Step(Speed)) { state = State.Waiting; grace = 0f; }
        }
        else if (state == State.Waiting && AtLast && !lit)
        {
            grace += Time.deltaTime;
            if (grace >= GraceSeconds)
            {
                if (DoorBlocks) { PlaySound(NightsSounds.Bonk, 0.7f); Scare(); } // knocks on the closed door and gives up
                else CatchPlayer();
                return;
            }
        }

        // Bob while walking, shake while lit.
        Vector3 feet = Rest;
        if (state == State.Walking && !lit) feet += Vector3.up * (0.04f * Mathf.Abs(Mathf.Sin(Time.time * 6f * Speed)));
        if (lit) feet += new Vector3(Mathf.Sin(Time.time * 53f), 0f, Mathf.Cos(Time.time * 47f)) * (0.025f * (0.4f + repel / RepelSeconds));
        Place(feet);
    }

    public override void Scare()
    {
        if (state == State.Fleeing || state == State.Gone) return;
        state = State.Fleeing;
        timer = 0.6f;
        repel = 0f;
        PlaySound(SahurNightSounds.Retreat, 1f);
    }

    protected override void OnCelebrateStart()
    {
        if (dance != null) { dance.move = TungDance.Move.Dance; dance.enabled = true; }
    }

    protected override void Celebrate(float t) { } // his drum dance does it all
}
