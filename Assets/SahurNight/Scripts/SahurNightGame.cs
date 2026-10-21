using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering.Universal;

// Runs the night: the clock from 12 AM to 6 AM, Sahur getting faster every hour (and a second one at 3 AM),
// the "caught" ending (no jump scare: the lights flicker, he walks in and does his drum dance), and the 6 AM sunrise.
// Pull the trigger (or press R) on the end screen to play again.
public class SahurNightGame : MonoBehaviour
{
    [Header("Night")]
    [Tooltip("Real seconds per in-game hour (12 AM to 6 AM is 6 hours).")]
    public float secondsPerHour = 40f;
    [Tooltip("Seconds of instructions before Sahur starts moving.")]
    public float introSeconds = 12f;
    [Tooltip("Hour when a second Sahur joins (1-5). 0 = never.")]
    [Range(0, 5)] public int secondSahurAt = 3;
    [Tooltip("Where the window wall is (z). If he catches you from out there he stays outside the window.")]
    public float windowZ = 2.6f;

    [Header("Scene")]
    public EnchantedFlashlight flashlight;
    public SahurStalker sahur;
    public TextMeshPro[] clocks;
    public TextMeshPro message;
    public FlickerLight[] roomLights;
    public Light moon;
    public Light sun;
    public Light[] dayLights;
    public AudioSource ambience;

    enum Phase { Intro, Night, Caught, Won }
    Phase phase;
    float clock;
    int hour = -1;
    readonly List<SahurStalker> stalkers = new List<SahurStalker>();
    InputAction restart;
    Material sky;
    Color nightAmbient, nightFog;
    float nightFogDensity, nightSkyExposure;
    Vector3 messageHome;
    Quaternion messageHomeRotation;
    UniversalAdditionalCameraData cameraData;
    bool cameraHadPostProcessing;

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
        messageHome = message.transform.position;
        messageHomeRotation = message.transform.rotation;

        restart = new InputAction("Restart", InputActionType.Button);
        restart.AddBinding("<XRController>{RightHand}/{TriggerButton}");
        restart.AddBinding("<XRController>{LeftHand}/{TriggerButton}");
        restart.AddBinding("<Keyboard>/r");
        restart.Enable();

        sahur.Caught += OnCaught;
        stalkers.Add(sahur);
        if (ambience != null) { ambience.clip = SahurNightSounds.Drone; ambience.loop = true; ambience.Play(); }
        StartNight();
    }

    void OnDestroy()
    {
        restart?.Dispose();
        if (cameraData != null) cameraData.renderPostProcessing = cameraHadPostProcessing;
    }

    void StartNight()
    {
        StopAllCoroutines();
        for (int i = stalkers.Count - 1; i >= 1; i--) { Destroy(stalkers[i].gameObject); stalkers.RemoveAt(i); }
        foreach (SahurStalker s in stalkers) s.Sleep();
        TungDance dance = sahur.GetComponent<TungDance>();
        if (dance != null) dance.enabled = false;

        phase = Phase.Intro;
        clock = 0f;
        hour = -1;
        flashlight.Recharge();
        flashlight.SwitchOn(false);
        SetDaylight(0f);
        if (ambience != null && !ambience.isPlaying) ambience.Play();
        message.transform.SetPositionAndRotation(messageHome, messageHomeRotation);
        message.transform.localScale = Vector3.one;
        message.text = "<size=140%>NIGHT SHIFT</size>\n\nTung Tung Tung Sahur is coming.\n" +
                       "Grab the flashlight on the right rail. Trigger = light on/off.\n" +
                       "Keep the light on him to send him away. Listen for his steps...\n" +
                       "Watch the window, the door behind you, and the hall to your left.\n\n" +
                       "Survive until <color=#FFD54A>6 AM</color>.";
        message.gameObject.SetActive(true);
    }

    void Update()
    {
        if (!UnityEngine.XR.XRSettings.isDeviceActive) DesktopLook();

        switch (phase)
        {
            case Phase.Intro:
                clock += Time.deltaTime;
                ShowClock(0);
                if (clock >= introSeconds)
                {
                    phase = Phase.Night;
                    clock = 0f;
                    message.gameObject.SetActive(false);
                    sahur.Begin();
                }
                break;

            case Phase.Night:
                clock += Time.deltaTime;
                int h = Mathf.FloorToInt(clock / secondsPerHour);
                if (h != hour)
                {
                    hour = h;
                    if (hour >= 6) { StartCoroutine(Win()); break; }
                    if (hour > 0) SahurNightSounds.Play(SahurNightSounds.Chime, Camera.main != null ? Camera.main.transform.position : Vector3.zero, 0.6f);
                    if (secondSahurAt > 0 && hour == secondSahurAt) AddSahur();
                    foreach (SahurStalker s in stalkers) SetDifficulty(s, hour);
                }
                ShowClock(hour);

                // The lights get nervous when he's right outside.
                float danger = 0f;
                foreach (SahurStalker s in stalkers) danger = Mathf.Max(danger, s.Danger);
                foreach (FlickerLight l in roomLights) if (l != null) l.nervous = danger;
                break;

            case Phase.Caught:
            case Phase.Won:
                if (restart.WasPressedThisFrame() && message.gameObject.activeSelf) StartNight();
                break;
        }
    }

    static void SetDifficulty(SahurStalker s, int hour)
    {
        float t = Mathf.Clamp01(hour / 5f);
        s.waitTime = Vector2.Lerp(new Vector2(9f, 13f), new Vector2(3.5f, 5.5f), t);
        s.walkSpeed = Mathf.Lerp(0.7f, 1.3f, t);
        s.graceTime = Mathf.Lerp(5f, 3f, t);
    }

    void AddSahur()
    {
        SahurStalker twin = Instantiate(sahur, sahur.transform.parent);
        twin.name = "Tung Tung Tung Sahur (2)";
        twin.Caught += OnCaught;
        stalkers.Add(twin);
        SetDifficulty(twin, hour);
        twin.Begin();
    }

    void ShowClock(int h)
    {
        string text = (h <= 0 ? 12 : h) + " AM";
        foreach (TextMeshPro c in clocks) if (c != null && c.text != text) c.text = text;
    }

    void OnCaught(SahurStalker catcher)
    {
        if (phase != Phase.Night) return;
        phase = Phase.Caught;
        StartCoroutine(Caught(catcher));
    }

    // No jump scare: the lights flicker out, he walks in calmly and does his "tung tung tung sahur" drum dance.
    IEnumerator Caught(SahurStalker catcher)
    {
        int caughtHour = Mathf.Max(0, hour);
        foreach (SahurStalker s in stalkers) if (s != catcher) s.Sleep();
        catcher.Freeze();
        flashlight.ForceOff(4f);
        if (ambience != null) ambience.Stop();
        foreach (FlickerLight l in roomLights) if (l != null) l.nervous = 1f;
        SahurNightSounds.Play(SahurNightSounds.Buzz, catcher.transform.position, 0.6f);
        yield return new WaitForSeconds(1.2f);

        // Walk in through the door or the hall to about 1.5 m from the player
        // (at the window he just walks right up to the glass: the desk is in the way).
        Transform view = Camera.main != null ? Camera.main.transform : transform;
        Vector3 player = new Vector3(view.position.x, 0f, view.position.z);
        Vector3 from = catcher.transform.position; from.y = 0f;
        Vector3 to = from.z > windowZ ? new Vector3(Mathf.Clamp(from.x, -1f, 1f), 0f, windowZ + 0.25f)
                                      : player + (from - player).normalized * 1.5f;
        float walk = Vector3.Distance(from, to) / 1.1f;
        float step = 0f;
        for (float t = 0f; t < walk; t += Time.deltaTime)
        {
            catcher.WalkTo(Vector3.Lerp(from, to, t / walk));
            step -= Time.deltaTime;
            if (step <= 0f) { step = 0.5f; SahurNightSounds.Play(SahurNightSounds.Step, catcher.transform.position, 0.9f); }
            yield return null;
        }
        catcher.WalkTo(to);
        foreach (FlickerLight l in roomLights) if (l != null) l.nervous = 0.2f;

        TungDance dance = catcher.GetComponent<TungDance>();
        if (dance != null) { dance.move = TungDance.Move.Dance; dance.enabled = true; }

        // The message floats beside him at eye level, so it doesn't cover the dance.
        Vector3 toward = (to - player).normalized;
        ShowEndMessage($"<size=150%><color=#FF5A3C>TUNG TUNG TUNG SAHUR!</color></size>\n\nHe caught you at {(caughtHour == 0 ? 12 : caughtHour)} AM.\n\nPull the trigger (or press R) to try again.",
                       player + toward * 1.0f + Vector3.Cross(Vector3.up, toward) * 0.85f + Vector3.up * Mathf.Max(1.2f, view.position.y - 0.05f), 0.55f);
    }

    IEnumerator Win()
    {
        phase = Phase.Won;
        ShowClock(6);
        foreach (TextMeshPro c in clocks) if (c != null) c.text = "6 AM";
        foreach (SahurStalker s in stalkers)
        {
            if (s.CurrentState != SahurStalker.State.Asleep) SahurNightSounds.Play(SahurNightSounds.Retreat, s.transform.position, 0.8f);
            s.Sleep();
        }
        Vector3 ears = Camera.main != null ? Camera.main.transform.position : Vector3.zero;
        SahurNightSounds.Play(SahurNightSounds.Chime, ears, 0.7f);
        yield return new WaitForSeconds(0.6f);
        SahurNightSounds.Play(SahurNightSounds.Win, ears, 0.8f);
        if (ambience != null) ambience.Stop();
        for (float t = 0f; t < 4f; t += Time.deltaTime) { SetDaylight(t / 4f); yield return null; }
        SetDaylight(1f);
        Transform view = Camera.main != null ? Camera.main.transform : transform;
        ShowEndMessage("<size=160%><color=#FFD54A>6 AM!</color></size>\n\nYou survived the night shift.\n\nPull the trigger (or press R) to play again.",
                       view.position + Flat(view.forward) * 1.8f + Vector3.up * 0.1f, 0.8f);
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
        foreach (FlickerLight l in roomLights) if (l != null) l.nervous = 0f;
    }

    void ShowEndMessage(string text, Vector3 position, float scale)
    {
        message.transform.localScale = Vector3.one * scale;
        Transform view = Camera.main != null ? Camera.main.transform : transform;
        message.text = text;
        message.transform.position = position;
        message.transform.rotation = Quaternion.LookRotation(Flat(position - view.position));
        message.gameObject.SetActive(true);
    }

    static Vector3 Flat(Vector3 v)
    {
        v.y = 0f;
        return v.sqrMagnitude > 1e-4f ? v.normalized : Vector3.forward;
    }

    // Without a headset: hold the right mouse button and drag to look around.
    float yaw, pitch;
    void DesktopLook()
    {
        Camera cam = Camera.main;
        if (cam == null || Mouse.current == null) return;
        if (Mouse.current.rightButton.isPressed)
        {
            Vector2 delta = Mouse.current.delta.ReadValue();
            yaw += delta.x * 0.15f;
            pitch = Mathf.Clamp(pitch - delta.y * 0.15f, -70f, 70f);
        }
        cam.transform.localRotation = Quaternion.Euler(pitch, yaw, 0f);
    }
}
