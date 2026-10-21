using System.Collections;
using UnityEngine;

// Lirili Larila: a desert elephant-cactus in sandals who steals time.
// He only moves when nobody is watching him: not in your flashlight, and not on the yard camera.
// If he makes it to the window he trumpets and turns the clock back an hour (or drains power at 12 AM).
// He never catches you; he just makes the night longer.
public class LiriliEnemy : PathWalker
{
    public Transform trunk;
    public Transform[] legs;
    [Tooltip("His pocket watch, which spins while he steals time.")]
    public Transform watch;

    enum State { Waiting, Walking, Stealing, Leaving, Gone }
    State state;
    bool watched;
    float timer, stepTimer, walkClock;

    float Speed => Mathf.Lerp(0.55f, 1.0f, Level01);
    public override bool Lethal => false;

    protected override void OnStart()
    {
        Appear(0);
        state = State.Waiting;
    }

    protected override void Tick()
    {
        if (state != State.Waiting || watched || !Roll()) return;
        if (AtLast) StartCoroutine(StealTime());
        else { StartWalking(); state = State.Walking; }
    }

    protected override void Run()
    {
        switch (state)
        {
            case State.Stealing:
                return;
            case State.Leaving:
                timer -= Time.deltaTime;
                transform.position = Rest + Vector3.down * (Height * (1f - Mathf.Clamp01(timer / 0.8f)));
                if (timer <= 0f) { state = State.Gone; timer = Random.Range(5f, 9f); Show(false); }
                return;
            case State.Gone:
                timer -= Time.deltaTime;
                if (timer <= 0f) { Appear(0); state = State.Waiting; }
                return;
        }

        watched = OnCamera || LitBody(Rest);
        if (state == State.Walking && !watched)
        {
            walkClock += Time.deltaTime;
            stepTimer -= Time.deltaTime;
            if (stepTimer <= 0f) { stepTimer = 0.45f / Speed; PlaySound(NightsSounds.FlipFlop, 0.9f); }
            if (Step(Speed)) state = State.Waiting;
        }
        Place(Rest);
        Animate(state == State.Walking && !watched);
    }

    void Animate(bool walking)
    {
        if (legs != null)
            for (int i = 0; i < legs.Length; i++)
                if (legs[i] != null)
                    legs[i].localRotation = Quaternion.Euler(walking ? 25f * Mathf.Sin(walkClock * 6f + i * Mathf.PI) : 0f, 0f, 0f);
        if (trunk != null) trunk.localRotation = Quaternion.Euler(10f * Mathf.Sin(Time.time * 1.7f), 0f, 8f * Mathf.Sin(Time.time * 1.1f));
    }

    IEnumerator StealTime()
    {
        state = State.Stealing;
        PlaySound(NightsSounds.Trumpet, 1f);
        // Trunk up, watch spinning backwards.
        for (float t = 0f; t < 1.4f; t += Time.deltaTime)
        {
            if (trunk != null) trunk.localRotation = Quaternion.Euler(-70f * Mathf.Sin(Mathf.PI * Mathf.Clamp01(t / 1.4f)), 0f, 0f);
            if (watch != null) watch.localRotation = Quaternion.Euler(0f, 0f, t * 720f);
            yield return null;
        }
        PlaySound(NightsSounds.Rewind, 1f);
        Game.StealTime(this);
        yield return new WaitForSeconds(1f);
        Scare();
    }

    public override void Scare()
    {
        if (state == State.Leaving || state == State.Gone) return;
        StopAllCoroutines();
        state = State.Leaving;
        timer = 0.8f;
    }
}
