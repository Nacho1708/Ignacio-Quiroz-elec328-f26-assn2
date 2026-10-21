using System.Collections;
using UnityEngine;

// Tralalero Tralala: a shark in sneakers who hides in the beach hut at the far end of the hallway.
// He gets ready in steps (hiding, peeking, standing in the hallway squeaking his sneakers), and he can't get ready
// while you watch him (hallway camera or flashlight). Then he SPRINTS: shut the door before he gets there!
// Bonk: he hits the door (that costs some power) and goes back to his hut. Door open: he gets you.
public class TralaleroEnemy : Brainrot
{
    [Header("Spots")]
    public Transform hideSpot, peekSpot, readySpot, doorSpot;
    [Tooltip("The hallway security camera.")]
    public int hallwayCamera = 2;

    [Header("Model parts")]
    public Transform body;
    public Transform tail;
    public Transform[] legs;

    enum Stage { Hiding, Peeking, Ready, Sprinting, Bonked }
    Stage stage;
    Vector3 feet;
    Quaternion facing;
    float runClock, squeakTimer;
    bool watched;

    float SprintSpeed => Mathf.Lerp(4.2f, 6.5f, Level01);
    public override float Danger => stage == Stage.Ready ? 0.5f : stage == Stage.Sprinting ? 1f : 0f;
    public override AudioClip ScareSound => NightsSounds.Kazoo;

    protected override void OnStart() => GoHome();

    void GoHome()
    {
        stage = Stage.Hiding;
        Show(true);
        SetSpot(hideSpot);
    }

    void SetSpot(Transform spot)
    {
        feet = spot.position;
        facing = spot.rotation;
        transform.SetPositionAndRotation(feet, facing);
    }

    protected override void Tick()
    {
        if (stage > Stage.Ready || watched || !Roll()) return;
        if (stage == Stage.Hiding) { stage = Stage.Peeking; SetSpot(peekSpot); PlaySound(NightsSounds.Squeak, 0.6f); }
        else if (stage == Stage.Peeking) { stage = Stage.Ready; SetSpot(readySpot); squeakTimer = 0.5f; }
        else StartCoroutine(Sprint());
    }

    protected override void Run()
    {
        if (stage == Stage.Sprinting || stage == Stage.Bonked) return;
        watched = (Game.cameras != null && Game.cameras.IsWatching(hallwayCamera)) || LitBody(feet);

        // Waiting in the hallway: stretching, sneakers squeaking now and then.
        if (stage == Stage.Ready)
        {
            squeakTimer -= Time.deltaTime;
            if (squeakTimer <= 0f) { squeakTimer = Random.Range(1.2f, 2.5f); PlaySound(NightsSounds.Squeak, 0.8f); }
            Animate(0.25f, 3f);
        }
        else Animate(0f, 1.5f);
    }

    IEnumerator Sprint()
    {
        stage = Stage.Sprinting;
        PlaySound(NightsSounds.Tralalero, 1f);
        // Wind up...
        for (float t = 0f; t < 0.7f; t += Time.deltaTime)
        {
            if (body != null) body.localRotation = Quaternion.Euler(12f * Mathf.Sin(Mathf.PI * t / 0.7f), 0f, 0f);
            Animate(0.15f, 8f);
            yield return null;
        }
        // ...and run!
        Vector3 from = feet, to = doorSpot.position;
        transform.rotation = Quaternion.LookRotation(Flat(to - from));
        float squeak = 0f;
        for (float d = 0f; d < 1f;)
        {
            d = Mathf.Min(1f, d + SprintSpeed * Time.deltaTime / Mathf.Max(0.1f, Vector3.Distance(from, to)));
            feet = Vector3.Lerp(from, to, d);
            transform.position = feet + Vector3.up * (0.06f * Mathf.Abs(Mathf.Sin(runClock * 18f)));
            squeak -= Time.deltaTime;
            if (squeak <= 0f) { squeak = 0.13f; PlaySound(NightsSounds.Squeak, 1f); }
            Animate(1f, 18f);
            yield return null;
        }
        if (Game.DoorClosed)
        {
            // BONK. He falls on his back, gets up, and walks back to his hut.
            stage = Stage.Bonked;
            PlaySound(NightsSounds.Bonk, 1f);
            Game.DrainPower(4f);
            for (float t = 0f; t < 1.6f; t += Time.deltaTime)
            {
                float fall = t < 0.25f ? t / 0.25f : t > 1.2f ? 1f - (t - 1.2f) / 0.4f : 1f;
                if (body != null) body.localRotation = Quaternion.Euler(-70f * fall, 0f, 0f);
                yield return null;
            }
            if (body != null) body.localRotation = Quaternion.identity;
            yield return new WaitForSeconds(0.5f);
            GoHome();
        }
        else CatchPlayer();
    }

    void Animate(float run, float speed)
    {
        runClock += Time.deltaTime * speed;
        if (legs != null)
            for (int i = 0; i < legs.Length; i++)
                if (legs[i] != null)
                    legs[i].localRotation = Quaternion.Euler(run * 45f * Mathf.Sin(runClock + i * 2.1f), 0f, 0f);
        if (tail != null) tail.localRotation = Quaternion.Euler(0f, (10f + 25f * run) * Mathf.Sin(runClock * 0.6f + 1f), 0f);
    }

    public override void Scare()
    {
        if (stage == Stage.Bonked) return;
        StopAllCoroutines();
        if (body != null) body.localRotation = Quaternion.identity;
        PlaySound(NightsSounds.Squeak, 1f);
        GoHome();
    }

    protected override void Celebrate(float t)
    {
        // Breakdance: spinning on the spot, legs kicking.
        transform.rotation = Quaternion.Euler(0f, t * 600f, 0f);
        Vector3 p = transform.position;
        p.y = Mathf.Abs(Mathf.Sin(t * 6f)) * 0.15f;
        transform.position = p;
        if (body != null) body.localRotation = Quaternion.Euler(0f, 0f, 25f * Mathf.Sin(t * 6f));
        runClock = t * 14f;
        if (legs != null)
            for (int i = 0; i < legs.Length; i++)
                if (legs[i] != null) legs[i].localRotation = Quaternion.Euler(60f * Mathf.Sin(runClock + i * 2.1f), 0f, 0f);
    }
}
