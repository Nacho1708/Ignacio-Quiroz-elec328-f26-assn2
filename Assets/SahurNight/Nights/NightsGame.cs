using System.Collections;
using System.Collections.Generic;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.Rendering.Universal;
using UnityEngine.XR.Interaction.Toolkit.UI;

// Five Nights at Tung's: runs the menu, the nights (12 AM to 6 AM), the shared power meter, the door, the cameras,
// the 6-7 panic move, getting caught (with optional silly jumpscares) and the 6 AM sunrise.
//
// Desktop (no headset): mouse aims the flashlight, left click / F = light, right-drag = look around,
// 1-4 = cameras, Tab = monitor on/off, Space = door, 6 7 6 7 = panic move, R / N / M on the end screen.
public class NightsGame : MonoBehaviour
{
    public const int TungIndex = 0, BallerinaIndex = 1, LiriliIndex = 2, TralaleroIndex = 3, PatapimIndex = 4, BombardiroIndex = 5;

    [System.Serializable]
    public class Night
    {
        [Tooltip("Starting levels (0-20): Tung, Ballerina, Lirili, Tralalero, Patapim, Bombardiro.")]
        public int[] levels = new int[6];
    }

    [Header("Nights")]
    [Tooltip("Real seconds per in-game hour (12 AM to 6 AM is 6 hours).")]
    public float secondsPerHour = 45f;
    [Tooltip("Seconds the night card (with tips) shows before the night starts.")]
    public float introSeconds = 7f;
    [Tooltip("Nights 1 to 5: who plays and how aggressive they start (everyone gets +1 at 2, 3 and 4 AM).")]
    public Night[] nights =
    {
        new Night { levels = new[] { 5, 0, 0, 0, 0, 0 } },
        new Night { levels = new[] { 7, 4, 4, 0, 0, 0 } },
        new Night { levels = new[] { 9, 6, 6, 5, 0, 0 } },
        new Night { levels = new[] { 11, 8, 8, 7, 6, 0 } },
        new Night { levels = new[] { 13, 10, 10, 10, 9, 8 } },
    };
    [Tooltip("Custom Night levels (set from the menu).")]
    public int[] customLevels = { 10, 10, 10, 10, 10, 10 };

    [Header("Power (percent per second)")]
    public float baseDrain = 0.05f;
    [Tooltip("Extra base drain for each night after Night 1.")]
    public float drainPerNight = 0.015f;
    public float flashlightDrain = 0.22f;
    public float doorDrain = 0.35f;
    public float monitorDrain = 0.18f;

    [Header("Scene")]
    public EnchantedFlashlight flashlight;
    public OfficeDoor door;
    public SecurityCameras cameras;
    public NightsMenu menu;
    [Tooltip("Tung, Ballerina, Lirili, Tralalero, Patapim, Bombardiro (in this order).")]
    public Brainrot[] cast;
    public TMP_Text[] clocks;
    public TMP_Text[] powerDisplays;
    public FlickerLight[] roomLights;
    [Tooltip("Lights that go out when the power runs out.")]
    public Light[] officeLights;
    [Tooltip("Glowing things that go dark when the power runs out.")]
    public Renderer[] officeGlows;
    public Light moon;
    public Light sun;
    public Light[] dayLights;
    public AudioSource ambience;
    [Tooltip("Where Ballerina shows up when the power runs out.")]
    public Transform doorway;
    [Tooltip("Material for the confetti (random colors are set per piece).")]
    public Material confettiMaterial;

    public enum Phase { Menu, Intro, Night, PowerOut, Caught, Won }
    public Phase CurrentPhase { get; private set; } = Phase.Menu;
    public bool Running => CurrentPhase == Phase.Night;
    public int NightNumber { get; private set; } = 1; // 1-5, 6 = Custom Night
    public int Hour => Mathf.Clamp(Mathf.FloorToInt(clock / secondsPerHour), 0, 6);
    public float Power { get; private set; } = 100f;
    public bool DoorClosed => door != null && door.Closed;
    public bool JumpscaresOn { get; set; }
    public bool PanicReady { get; private set; }

    static readonly string[] Names = { "Tung Tung Tung Sahur", "Ballerina Cappuccina", "Lirili Larila", "Tralalero Tralala", "Brr Brr Patapim", "Bombardiro Crocodilo" };
    static readonly string[] Tips =
    {
        "<b>Tung Tung Tung Sahur</b> only moves in the dark. Keep your light on him to send him back.",
        "<b>Ballerina Cappuccina</b> only dances closer while she's IN your light. Keep it off her and she gets bored.",
        "<b>Lirili Larila</b> steals an hour if he reaches the window. He freezes while you watch him (CAM 1 or your light).",
        "<b>Tralalero Tralala</b> sprints down the hallway behind you. Hear the sneakers? Shut the DOOR! Watching him on CAM 3 holds him back.",
        "<b>Brr Brr Patapim</b> is invisible except in your flashlight. Listen for \"brr brr\" and sweep the room.",
        "<b>Bombardiro Crocodilo</b> dives through the skylight after the air-raid siren. Look UP and hit him with the beam!",
    };

    float clock;
    int lastHour;
    int[] levels = new int[6];
    Material sky;
    Color nightAmbient, nightFog;
    float nightFogDensity, nightSkyExposure;
    float stolenFlash;
    UniversalAdditionalCameraData cameraData;
    bool cameraHadPostProcessing;
    static readonly int EmissionColor = Shader.PropertyToID("_EmissionColor");

    void Awake()
    {
        // World-space menus need an EventSystem with the XR UI input module (the Roost brings its own).
        if (FindAnyObjectByType<EventSystem>() == null)
        {
            var events = new GameObject("EventSystem (Five Nights)");
            events.AddComponent<EventSystem>();
            events.AddComponent<XRUIInputModule>();
        }
    }

    void Start()
    {
        // Work on a copy of the skybox so the sunrise doesn't change the asset itself.
        if (RenderSettings.skybox != null)
        {
            sky = new Material(RenderSettings.skybox);
            RenderSettings.skybox = sky;
            nightSkyExposure = sky.HasProperty("_Exposure") ? sky.GetFloat("_Exposure") : 1f;
        }
        nightAmbient = RenderSettings.ambientLight;
        nightFog = RenderSettings.fogColor;
        nightFogDensity = RenderSettings.fogDensity;

        // Bloom, vignette and film grain need post-processing on the camera (the Roost's camera has it off).
        if (Camera.main != null && Camera.main.TryGetComponent(out cameraData))
        {
            cameraHadPostProcessing = cameraData.renderPostProcessing;
            cameraData.renderPostProcessing = true;
        }
        foreach (Canvas c in GetComponentsInChildren<Canvas>(true)) if (c.worldCamera == null) c.worldCamera = Camera.main;
        foreach (Canvas c in FindObjectsByType<Canvas>(FindObjectsInactive.Include)) if (c.worldCamera == null) c.worldCamera = Camera.main;

        foreach (Brainrot b in cast) if (b != null) b.Setup(this);
        flashlight.sharedPower = true;
        if (ambience != null) { ambience.clip = SahurNightSounds.Drone; ambience.loop = true; }
        ShowMenu();
    }

    void OnDestroy()
    {
        if (cameraData != null) cameraData.renderPostProcessing = cameraHadPostProcessing;
    }

    // ---------- Menu ----------

    public void ShowMenu()
    {
        StopAllCoroutines();
        ResetOffice();
        CurrentPhase = Phase.Menu;
        menu.ShowMain();
    }

    public void Retry() => StartNight(NightNumber);
    public void NextNight() => StartNight(Mathf.Min(5, NightNumber + 1));

    public void ChangeCustomLevel(int who, int delta) => customLevels[who] = Mathf.Clamp(customLevels[who] + delta, 0, 20);

    public void SetAllCustomLevels(int level)
    {
        for (int i = 0; i < customLevels.Length; i++) customLevels[i] = level;
    }

    // 1-5 = Nights 1 to 5, 6 = Custom Night.
    public void StartNight(int number)
    {
        StopAllCoroutines();
        ResetOffice();
        NightNumber = Mathf.Clamp(number, 1, 6);
        int[] start = NightNumber <= nights.Length ? nights[NightNumber - 1].levels : customLevels;
        for (int i = 0; i < levels.Length; i++) levels[i] = i < start.Length ? start[i] : 0;
        CurrentPhase = Phase.Intro;
        clock = 0f;
        menu.ShowCard(NightCard());
        StartCoroutine(Intro());
    }

    string NightCard()
    {
        var text = new StringBuilder();
        text.Append(NightNumber <= 5 ? $"<size=150%>NIGHT {NightNumber}</size>\n" : "<size=150%>CUSTOM NIGHT</size>\n");
        text.Append("<color=#FFD54A>12 AM</color>  Survive until 6 AM\n\n");
        int[] before = NightNumber >= 2 && NightNumber <= nights.Length ? nights[NightNumber - 2].levels : null;
        for (int i = 0; i < levels.Length; i++)
        {
            if (levels[i] <= 0) continue;
            bool isNew = before == null || before[i] <= 0;
            if (isNew || NightNumber == 6) text.Append(isNew && NightNumber <= 5 ? "<color=#FF7A5C>NEW!</color> " : "").Append(Tips[i]).Append('\n');
        }
        if (NightNumber == 1) text.Append("\nYour flashlight, the door and the monitor share one power meter. Don't run out!");
        if (NightNumber == 2) text.Append("\nCameras: buttons on the front rail. Panicking? Do the 6-7 once per night to blast everyone back.");
        return text.ToString();
    }

    IEnumerator Intro()
    {
        yield return new WaitForSeconds(introSeconds);
        menu.HideBoard();
        CurrentPhase = Phase.Night;
        lastHour = 0;
        if (ambience != null) ambience.Play();
        for (int i = 0; i < cast.Length; i++) if (cast[i] != null) cast[i].StartNight(levels[i]);
    }

    // Everything back to the start of a night.
    void ResetOffice()
    {
        foreach (Brainrot b in cast) if (b != null) b.StopNight();
        Power = 100f;
        PanicReady = true;
        stolenFlash = 0f;
        SetDaylight(0f);
        SetOfficeLights(true);
        SetNervous(0f);
        if (door != null) door.Snap(false);
        if (cameras != null) { cameras.Powered = true; cameras.TurnOff(); }
        flashlight.Recharge();
        flashlight.SwitchOn(false);
        if (ambience != null) ambience.Stop();
        clock = 0f;
        UpdateDisplays();
    }

    // ---------- The night ----------

    void Update()
    {
        if (!UnityEngine.XR.XRSettings.isDeviceActive) DesktopLook();
        ReadKeyboard();

        if (CurrentPhase == Phase.Night || CurrentPhase == Phase.PowerOut)
        {
            clock += Time.deltaTime;
            int hour = Hour;
            if (hour != lastHour)
            {
                if (hour >= 6) { StopAllCoroutines(); StartCoroutine(Win()); return; } // 6 AM beats everything, even a power-out
                if (hour > lastHour)
                {
                    SahurNightSounds.Play(SahurNightSounds.Chime, Eyes(), 0.6f);
                    if (hour >= 2 && hour <= 4) // everyone gets a bit more aggressive at 2, 3 and 4 AM
                        foreach (Brainrot b in cast) if (b != null && b.Active) b.level = Mathf.Min(20, b.level + 1);
                }
                lastHour = hour;
            }
        }

        if (CurrentPhase == Phase.Night)
        {
            float drain = baseDrain + drainPerNight * (Mathf.Min(NightNumber, 5) - 1);
            if (flashlight.IsOn) drain += flashlightDrain;
            if (door != null && door.Closing) drain += doorDrain;
            if (cameras != null && cameras.MonitorOn) drain += monitorDrain;
            DrainPower(drain * Time.deltaTime);
            if (Power <= 0f) { StartCoroutine(PowerOut()); return; }

            // The lights get nervous when someone's about to get you.
            float danger = 0f;
            foreach (Brainrot b in cast) if (b != null && b.Active) danger = Mathf.Max(danger, b.Danger);
            SetNervous(danger);
        }
        flashlight.SetCharge(Power / 100f);
        stolenFlash = Mathf.Max(0f, stolenFlash - Time.deltaTime);
        UpdateDisplays();
    }

    public void DrainPower(float percent) => Power = Mathf.Max(0f, Power - percent);

    // Lirili Larila got to the window: the clock goes back an hour (at 12 AM he takes power instead).
    public void StealTime(Brainrot thief)
    {
        if (clock >= secondsPerHour) { clock -= secondsPerHour; lastHour = Hour; }
        else DrainPower(8f);
        stolenFlash = 2.5f;
    }

    // The 6-7: everyone back to the start, once per night.
    public void Panic()
    {
        if (!Running || !PanicReady) return;
        PanicReady = false;
        Vector3 eyes = Eyes();
        NightsSounds.Play(NightsSounds.SixSeven, eyes, 1f);
        foreach (Brainrot b in cast) if (b != null && b.Active) b.Scare();
        Camera cam = Camera.main;
        Confetti(eyes + (cam != null ? Flat(cam.transform.forward) : Vector3.forward) * 0.8f, 40);
    }

    public void SelectCamera(int feed) { if (Running && cameras != null) cameras.Select(feed); }
    public void ToggleMonitor() { if (Running && cameras != null) cameras.ToggleMonitor(); }
    public void ToggleDoor() { if (Running && door != null) door.Toggle(); }

    // ---------- Endings ----------

    public void Caught(Brainrot who)
    {
        if (who == null || !who.Lethal || (CurrentPhase != Phase.Night && CurrentPhase != Phase.PowerOut)) return;
        StopAllCoroutines();
        StartCoroutine(CaughtSequence(who));
    }

    IEnumerator CaughtSequence(Brainrot who)
    {
        CurrentPhase = Phase.Caught;
        int caughtAt = Hour;
        foreach (Brainrot b in cast) if (b != null && b != who) b.StopNight();
        who.Freeze();
        flashlight.ForceOff(4f);
        if (cameras != null) cameras.TurnOff();
        if (ambience != null) ambience.Stop();
        SetNervous(1f);
        SahurNightSounds.Play(SahurNightSounds.Buzz, Eyes(), 0.6f);
        yield return new WaitForSeconds(0.6f);

        Transform view = Camera.main != null ? Camera.main.transform : transform;
        if (JumpscaresOn)
        {
            SetOfficeLights(true);
            yield return who.Jumpscare(view);
        }
        else
        {
            // Lights out for a moment... and then they're just standing there.
            SetOfficeLights(false);
            yield return new WaitForSeconds(0.9f);
        }
        SetOfficeLights(true);
        who.StartCelebrating(CelebrationSpot(view));
        SetNervous(0.2f);
        menu.ShowEnd($"<size=130%><color=#FF5A3C>{who.displayName.ToUpper()}</color></size>\ngot you at {HourName(caughtAt)}!\n\n<size=70%>Pull the trigger on a button (or press R to retry, M for the menu)</size>", false);
    }

    // About 1.7 m in front of the player, inside the office.
    Vector3 CelebrationSpot(Transform view)
    {
        Vector3 spot = new Vector3(view.position.x, 0f, view.position.z) + Flat(view.forward) * 1.7f;
        spot.x = Mathf.Clamp(spot.x, -1.9f, 1.9f);
        spot.z = Mathf.Clamp(spot.z, -1.9f, 1.3f); // stays in front of the desk
        return spot;
    }

    IEnumerator PowerOut()
    {
        CurrentPhase = Phase.PowerOut;
        Power = 0f;
        foreach (Brainrot b in cast) if (b != null) b.StopNight();
        SetOfficeLights(false);
        SetNervous(0f);
        flashlight.SetCharge(0f);
        if (cameras != null) { cameras.Powered = false; cameras.TurnOff(); }
        if (door != null) door.SetClosed(false);
        if (ambience != null) ambience.Stop();
        NightsSounds.Play(NightsSounds.PowerDown, Eyes(), 1f);
        yield return new WaitForSeconds(Random.Range(3f, 5f));

        // In the dark, a music box starts playing at the door...
        var ballerina = cast.Length > BallerinaIndex ? cast[BallerinaIndex] as BallerinaEnemy : null;
        if (ballerina != null && doorway != null) ballerina.PowerOutVisit(doorway.position);
        yield return new WaitForSeconds(Random.Range(6f, 14f));
        if (ballerina != null) Caught(ballerina);
    }

    IEnumerator Win()
    {
        CurrentPhase = Phase.Won;
        foreach (Brainrot b in cast)
        {
            if (b != null && b.Active) SahurNightSounds.Play(SahurNightSounds.Retreat, b.transform.position, 0.6f);
            if (b != null) b.StopNight();
        }
        if (cameras != null) cameras.TurnOff();
        if (door != null) door.SetClosed(false, true);
        SetNervous(0f);
        Vector3 eyes = Eyes();
        SahurNightSounds.Play(SahurNightSounds.Chime, eyes, 0.7f);
        yield return new WaitForSeconds(0.6f);
        SahurNightSounds.Play(SahurNightSounds.Win, eyes, 0.8f);
        if (ambience != null) ambience.Stop();
        SetOfficeLights(true);
        for (float t = 0f; t < 4f; t += Time.deltaTime) { SetDaylight(t / 4f); yield return null; }
        SetDaylight(1f);
        Confetti(eyes + Vector3.up * 0.5f + (Camera.main != null ? Flat(Camera.main.transform.forward) : Vector3.forward), 50);
        string night = NightNumber <= 5 ? $"Night {NightNumber}" : "Custom Night";
        bool next = NightNumber < 5;
        menu.ShowEnd($"<size=150%><color=#FFD54A>6 AM!</color></size>\nYou survived {night}.\n\n<size=70%>{(next ? "N = next night, " : "")}R = play again, M = menu</size>", next);
    }

    // ---------- Lights, displays, effects ----------

    void SetNervous(float amount)
    {
        foreach (FlickerLight l in roomLights) if (l != null) l.nervous = amount;
    }

    void SetOfficeLights(bool on)
    {
        foreach (Light l in officeLights) if (l != null) l.enabled = on;
        var block = new MaterialPropertyBlock();
        foreach (Renderer r in officeGlows)
        {
            if (r == null) continue;
            if (on) r.SetPropertyBlock(null);
            else { block.SetColor(EmissionColor, Color.black); r.SetPropertyBlock(block); }
        }
    }

    // 0 = night, 1 = morning: sun up, room lights on, sky brighter, fog thinner.
    void SetDaylight(float t)
    {
        t = Mathf.SmoothStep(0f, 1f, t);
        if (sun != null) { sun.enabled = t > 0f; sun.intensity = 1.4f * t; }
        if (moon != null) moon.intensity = Mathf.Lerp(0.25f, 0f, t);
        foreach (Light l in dayLights) if (l != null) { l.enabled = t > 0f; l.intensity = 1.2f * t; }
        RenderSettings.ambientLight = Color.Lerp(nightAmbient, new Color(0.55f, 0.52f, 0.48f), t);
        RenderSettings.fogColor = Color.Lerp(nightFog, new Color(0.75f, 0.68f, 0.6f), t);
        RenderSettings.fogDensity = Mathf.Lerp(nightFogDensity, 0.01f, t);
        if (sky != null)
        {
            if (sky.HasProperty("_Exposure")) sky.SetFloat("_Exposure", Mathf.Lerp(nightSkyExposure, 1.6f, t));
            if (sky.HasProperty("_Tint")) sky.SetColor("_Tint", Color.Lerp(new Color(0.5f, 0.5f, 0.5f), new Color(0.95f, 0.65f, 0.45f), t));
        }
    }

    void UpdateDisplays()
    {
        string time = CurrentPhase == Phase.Menu ? "12 AM" : HourName(Hour);
        if (stolenFlash > 0f && Mathf.Repeat(stolenFlash, 0.5f) < 0.3f) time = "<color=#FF4040>TIME STOLEN!</color>";
        foreach (TMP_Text c in clocks) if (c != null && c.text != time) c.text = time;

        int usage = 1 + (flashlight.IsOn ? 1 : 0) + (door != null && door.Closing ? 1 : 0) + (cameras != null && cameras.MonitorOn ? 1 : 0);
        string bars = $"<color=#FFD54A>{new string('I', usage * 2)}</color><color=#555555>{new string('I', (4 - usage) * 2)}</color>";
        string color = Power > 50f ? "#7CFF7C" : Power > 20f ? "#FFD54A" : "#FF5050";
        string power = CurrentPhase == Phase.PowerOut ? "<color=#FF5050>POWER OUT</color>" : $"POWER <color={color}>{Mathf.CeilToInt(Power)}%</color>\nUSAGE {bars}";
        foreach (TMP_Text p in powerDisplays) if (p != null && p.text != power) p.text = power;
    }

    // Confetti: little colored cubes that pop out and tumble onto the floor.
    public void Confetti(Vector3 position, int count)
    {
        if (confettiMaterial == null) return;
        var block = new MaterialPropertyBlock();
        for (int i = 0; i < count; i++)
        {
            GameObject piece = GameObject.CreatePrimitive(PrimitiveType.Cube);
            piece.name = "Confetti";
            piece.transform.position = position + Random.insideUnitSphere * 0.15f;
            piece.transform.rotation = Random.rotation;
            piece.transform.localScale = new Vector3(0.04f, 0.005f, 0.03f);
            var r = piece.GetComponent<Renderer>();
            r.sharedMaterial = confettiMaterial;
            block.SetColor("_BaseColor", Color.HSVToRGB(Random.value, 0.85f, 1f));
            r.SetPropertyBlock(block);
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            var body = piece.AddComponent<Rigidbody>();
            body.mass = 0.01f;
            body.linearDamping = 2f;
            body.angularDamping = 0.5f;
            body.linearVelocity = Random.insideUnitSphere * 3f + Vector3.up * 2.5f;
            body.angularVelocity = Random.insideUnitSphere * 20f;
            Destroy(piece, Random.Range(3f, 5f));
        }
    }

    // ---------- Desktop (no headset) ----------

    void ReadKeyboard()
    {
        Keyboard k = Keyboard.current;
        if (k == null) return;
        switch (CurrentPhase)
        {
            case Phase.Menu:
                if (menu.CustomOpen)
                {
                    if (k.enterKey.wasPressedThisFrame) StartNight(6);
                    if (k.escapeKey.wasPressedThisFrame) menu.ShowMain();
                    break;
                }
                for (int i = 0; i < 5; i++)
                    if (k[(Key)((int)Key.Digit1 + i)].wasPressedThisFrame) StartNight(i + 1);
                if (k.cKey.wasPressedThisFrame) menu.ShowCustom();
                if (k.jKey.wasPressedThisFrame) JumpscaresOn = !JumpscaresOn;
                break;
            case Phase.Night:
                for (int i = 0; i < 4; i++)
                    if (k[(Key)((int)Key.Digit1 + i)].wasPressedThisFrame) SelectCamera(i);
                if (k.tabKey.wasPressedThisFrame) ToggleMonitor();
                if (k.spaceKey.wasPressedThisFrame) ToggleDoor();
                if (k.escapeKey.wasPressedThisFrame) ShowMenu();
                break;
            case Phase.Caught:
            case Phase.Won:
                if (!menu.board.activeSelf) break; // wait for the end screen
                if (k.rKey.wasPressedThisFrame) Retry();
                if (k.nKey.wasPressedThisFrame && CurrentPhase == Phase.Won && NightNumber < 5) NextNight();
                if (k.mKey.wasPressedThisFrame || k.escapeKey.wasPressedThisFrame) ShowMenu();
                break;
        }
    }

    // Hold the right mouse button and drag to look around.
    float yaw, pitch;
    void DesktopLook()
    {
        Camera cam = Camera.main;
        if (cam == null || Mouse.current == null) return;
        if (Mouse.current.rightButton.isPressed)
        {
            Vector2 delta = Mouse.current.delta.ReadValue();
            yaw += delta.x * 0.15f;
            pitch = Mathf.Clamp(pitch - delta.y * 0.15f, -80f, 80f);
        }
        cam.transform.localRotation = Quaternion.Euler(pitch, yaw, 0f);
    }

    // ---------- Helpers ----------

    public static string HourName(int hour) => (hour <= 0 ? 12 : hour) + " AM";

    static Vector3 Eyes() => Camera.main != null ? Camera.main.transform.position : new Vector3(0f, 1.6f, 0f);

    static Vector3 Flat(Vector3 v)
    {
        v.y = 0f;
        return v.sqrMagnitude > 1e-6f ? v.normalized : Vector3.forward;
    }
}
