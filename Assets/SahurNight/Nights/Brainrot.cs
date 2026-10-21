using System.Collections;
using UnityEngine;

// Base for every character in Five Nights at Tung's.
// Five Nights at Freddy's-style AI: every few seconds (a "tick") a character gets a chance to act. It acts when a
// random roll from 1 to 20 is at or below its level, so level 0 never moves and level 20 moves every time.
// The game sets the levels each night (Nights 1-5, or your own in Custom Night).
public abstract class Brainrot : MonoBehaviour
{
    [Header("Character")]
    public string displayName = "Brainrot";
    [Tooltip("A point at the middle of its face. Jumpscares put this right in front of your eyes.")]
    public Transform face;
    [Tooltip("Optional: drag in your own model (a prefab) to replace the built-in one made of simple shapes. " +
             "Its feet should be at its pivot and it should face +Z (blue arrow).")]
    public GameObject modelOverride;
    [Tooltip("The built-in model (hidden when you use your own).")]
    public Transform builtInModel;

    [Header("AI (the game sets the level every night)")]
    [Range(0, 20)] public int level;
    [Tooltip("Seconds between chances to act.")]
    public float tickSeconds = 5f;

    public NightsGame Game { get; private set; }
    public bool Active { get; private set; }
    // 0 to 1: how close it is to getting you (the room lights flicker more as this goes up).
    public virtual float Danger => 0f;
    // Can it end the night? (Lirili Larila only steals time.)
    public virtual bool Lethal => true;
    public float Height { get; private set; } = 1.8f;
    public virtual AudioClip ScareSound => NightsSounds.AirHorn;
    // How far in front of your eyes its face pops up in a jumpscare (meters).
    protected virtual float JumpscareDistance => 0.6f;

    protected Transform Model { get; private set; }
    protected AudioSource Voice { get; private set; }
    protected EnchantedFlashlight Flashlight => Game.flashlight;
    protected float Level01 => level / 20f;
    protected bool UsingOwnModel => Model != builtInModel;

    Renderer[] renderers;
    float tickTimer;
    bool celebrating;
    float celebrateClock;

    public void Setup(NightsGame game)
    {
        Game = game;
        Model = builtInModel != null ? builtInModel : transform;
        if (modelOverride != null)
        {
            if (builtInModel != null) builtInModel.gameObject.SetActive(false);
            Model = Instantiate(modelOverride, transform).transform;
            Model.localPosition = Vector3.zero;
            Model.localRotation = Quaternion.identity;
        }
        renderers = GetComponentsInChildren<Renderer>();

        Voice = gameObject.AddComponent<AudioSource>();
        Voice.playOnAwake = false;
        Voice.spatialBlend = 1f;           // 3D: you hear where it is
        Voice.rolloffMode = AudioRolloffMode.Linear;
        Voice.minDistance = 1f;
        Voice.maxDistance = 25f;
        Voice.dopplerLevel = 0f;

        // Measure it, standing at its spot.
        float top = transform.position.y + 0.5f;
        foreach (Renderer r in renderers) top = Mathf.Max(top, r.bounds.max.y);
        Height = top - transform.position.y;
        if (face == null)
        {
            face = new GameObject("Face").transform;
            face.SetParent(transform, false);
            face.localPosition = new Vector3(0f, Height * 0.85f, 0.15f);
        }
        OnSetup();
        Show(false);
    }

    protected virtual void OnSetup() { }

    // The night starts: this character plays at `startLevel` (0 = sits this night out).
    public void StartNight(int startLevel)
    {
        StopNight();
        level = Mathf.Clamp(startLevel, 0, 20);
        Active = level > 0;
        tickTimer = Random.Range(0.5f, 1.2f) * tickSeconds;
        if (Active) OnStart();
    }

    public void StopNight()
    {
        Active = false;
        celebrating = false;
        StopAllCoroutines();
        transform.localScale = Vector3.one;
        if (Voice != null) Voice.Stop();
        OnStop();
        Show(false);
    }

    // Stay put (it just caught you).
    public void Freeze() => Active = false;

    protected abstract void OnStart();
    protected virtual void OnStop() { }
    // A chance to act (use Roll()).
    protected abstract void Tick();
    // Every frame while the night is on.
    protected abstract void Run();
    // The 6-7 panic move: run away right now.
    public abstract void Scare();

    protected virtual void Update()
    {
        if (celebrating)
        {
            celebrateClock += Time.deltaTime;
            Celebrate(celebrateClock);
            return;
        }
        if (!Active || Game == null || !Game.Running) return;
        tickTimer -= Time.deltaTime;
        if (tickTimer <= 0f)
        {
            tickTimer += Mathf.Max(0.5f, tickSeconds);
            Tick();
        }
        Run();
    }

    protected bool Roll() => Random.Range(1, 21) <= level;

    protected void CatchPlayer() => Game.Caught(this);

    // ---------- The flashlight ----------

    // Is this point lit by the flashlight: inside the beam, close enough, with nothing solid in between?
    protected bool Lit(Vector3 point)
    {
        EnchantedFlashlight f = Flashlight;
        if (f == null || !f.IsOn) return false;
        Vector3 toPoint = point - f.Origin;
        float distance = toPoint.magnitude;
        if (distance < 0.01f || distance > f.range) return false;
        if (Vector3.Angle(f.Direction, toPoint) > f.coneAngle * 0.5f + 2f) return false;
        if (Physics.Raycast(f.Origin, toPoint / distance, out RaycastHit hit, distance, ~0, QueryTriggerInteraction.Ignore) &&
            !hit.transform.IsChildOf(transform))
            return false; // something's in the way
        return true;
    }

    // Lit anywhere from its knees to its head, standing with its feet at `feet`.
    protected bool LitBody(Vector3 feet)
    {
        return Lit(feet + Vector3.up * (Height * 0.35f)) || Lit(feet + Vector3.up * (Height * 0.6f)) || Lit(feet + Vector3.up * (Height * 0.85f));
    }

    // ---------- Looks ----------

    protected void Show(bool visible)
    {
        if (renderers == null) return;
        foreach (Renderer r in renderers) if (r != null) r.enabled = visible;
        OnShown(visible);
    }

    // After showing or hiding it (for parts that are only sometimes visible).
    protected virtual void OnShown(bool visible) { }

    protected static Vector3 PlayerPosition()
    {
        Camera cam = Camera.main;
        return cam != null ? cam.transform.position : new Vector3(0f, 1.6f, 0f);
    }

    // Turn (around the vertical axis only) to face the player.
    protected void FacePlayer()
    {
        Vector3 look = PlayerPosition() - transform.position;
        look.y = 0f;
        if (look.sqrMagnitude > 1e-4f) transform.rotation = Quaternion.LookRotation(look);
    }

    protected static Vector3 Flat(Vector3 v)
    {
        v.y = 0f;
        return v.sqrMagnitude > 1e-6f ? v.normalized : Vector3.forward;
    }

    protected void PlaySound(AudioClip clip, float volume = 1f)
    {
        if (Voice != null && clip != null) Voice.PlayOneShot(clip, volume);
    }

    // ---------- Getting you ----------

    // Jumpscare (when they're turned on): pop up right in front of your face with a silly sound. No camera shake.
    public virtual IEnumerator Jumpscare(Transform view)
    {
        Show(true);
        transform.localScale = Vector3.one;
        Vector3 forward = Flat(view.forward);
        Quaternion look = Quaternion.LookRotation(-forward);
        transform.rotation = look;
        Vector3 faceLocal = transform.InverseTransformPoint(face.position);
        Vector3 target = view.position + forward * JumpscareDistance + Vector3.down * 0.06f;
        NightsSounds.Play(ScareSound, target, 1f);
        const float length = 1.3f;
        for (float t = 0f; t < length; t += Time.deltaTime)
        {
            float pop = Pop(t);
            float wobble = Mathf.Sin(t * 38f) * 5f * (1f - t / length);
            transform.rotation = look * Quaternion.Euler(0f, 0f, wobble);
            transform.localScale = Vector3.one * pop;
            transform.position = target - transform.rotation * (faceLocal * pop);
            OnJumpscareFrame(t);
            yield return null;
        }
        transform.localScale = Vector3.one;
    }

    protected virtual void OnJumpscareFrame(float t) { }

    // Springs from small to a bit too big and settles at full size.
    protected static float Pop(float t)
    {
        float k = Mathf.Clamp01(t / 0.12f);
        if (t < 0.12f) return Mathf.Lerp(0.3f, 1.15f, k * k);
        return 1f + 0.15f * Mathf.Exp(-(t - 0.12f) / 0.08f) * Mathf.Cos((t - 0.12f) * 30f);
    }

    // After getting you: stand at `spot` facing you and do something goofy until the next night starts.
    public void StartCelebrating(Vector3 spot)
    {
        Show(true);
        transform.localScale = Vector3.one;
        transform.position = spot;
        FacePlayer();
        celebrating = true;
        celebrateClock = 0f;
        OnCelebrateStart();
    }

    protected virtual void OnCelebrateStart() { }

    // The goofy celebration (default: happy hops and spins). `t` = seconds since it started.
    protected virtual void Celebrate(float t)
    {
        float hop = Mathf.Abs(Mathf.Sin(t * 5f)) * 0.25f;
        Vector3 p = transform.position;
        p.y = hop;
        transform.position = p;
        transform.rotation = Quaternion.LookRotation(Flat(PlayerPosition() - p)) * Quaternion.Euler(0f, Mathf.Sin(t * 2.5f) * 40f, 0f);
    }
}

// One way in for characters that walk station to station (from far away to right outside the office).
[System.Serializable]
public class BrainrotPath
{
    public string name;
    [Tooltip("Stations from far away to right outside the office.")]
    public Transform[] stations;
    [Tooltip("Comes in through the office door, so a closed door stops it.")]
    public bool throughDoor;
    [Tooltip("Which security camera shows this path (0-3), or -1 for none.")]
    public int securityCamera = -1;
}

// A character that walks along paths, station by station.
public abstract class PathWalker : Brainrot
{
    [Header("Paths")]
    public BrainrotPath[] paths;

    protected int PathIndex { get; private set; }
    protected int Station { get; private set; }
    protected Vector3 Rest;          // where its feet are
    protected bool Walking { get; private set; }
    Vector3 walkTarget;

    protected BrainrotPath CurrentPath => paths[PathIndex];
    protected bool AtLast => Station == CurrentPath.stations.Length - 1;
    protected float Progress => Station / (float)Mathf.Max(1, CurrentPath.stations.Length - 1);
    protected bool DoorBlocks => CurrentPath.throughDoor && Game.DoorClosed;
    protected bool OnCamera => CurrentPath.securityCamera >= 0 && Game.cameras != null && Game.cameras.IsWatching(CurrentPath.securityCamera);

    protected void Appear(int path, int station = 0)
    {
        PathIndex = Mathf.Clamp(path, 0, paths.Length - 1);
        Station = Mathf.Clamp(station, 0, CurrentPath.stations.Length - 1);
        Rest = CurrentPath.stations[Station].position;
        Walking = false;
        Show(true);
        Place(Rest);
    }

    protected void StartWalking()
    {
        if (AtLast) return;
        walkTarget = CurrentPath.stations[Station + 1].position;
        Walking = true;
    }

    // Move toward the next station. True when it gets there.
    protected bool Step(float speed)
    {
        if (!Walking) return false;
        Rest = Vector3.MoveTowards(Rest, walkTarget, speed * Time.deltaTime);
        if ((Rest - walkTarget).sqrMagnitude > 1e-4f) return false;
        Rest = walkTarget;
        Station++;
        Walking = false;
        return true;
    }

    protected int RandomPath() => Random.Range(0, paths.Length);

    protected int OtherPath()
    {
        if (paths.Length < 2) return 0;
        int p = Random.Range(0, paths.Length - 1);
        return p >= PathIndex ? p + 1 : p;
    }

    // Stand with its feet at `feet`, facing the player.
    protected void Place(Vector3 feet)
    {
        transform.position = feet;
        FacePlayer();
    }
}
