using TMPro;
using UnityEngine;

// The security monitor on the desk: four cameras (yard, roof, hallway, side hall), one shown at a time.
// Switching shows a burst of static. The monitor uses power while it's on, and some characters
// behave differently when you watch them on camera (Lirili Larila freezes, Tralalero can't get ready).
public class SecurityCameras : MonoBehaviour
{
    public Camera[] feeds;
    public string[] feedNames = { "YARD", "ROOF", "HALLWAY", "SIDE HALL" };
    [Tooltip("The monitor's screen (a quad). Its material shows the camera picture.")]
    public Renderer screen;
    [Tooltip("Shown on the screen while the monitor is on.")]
    public TextMeshPro label;
    [Tooltip("Shown on the screen while it's off.")]
    public TextMeshPro offLabel;
    [Tooltip("A quad over the screen showing static while switching.")]
    public Renderer staticNoise;
    public Vector2Int resolution = new Vector2Int(640, 400);
    [Tooltip("How much brighter than real life the cameras see (night vision).")]
    public Color nightVision = new Color(1.6f, 2.3f, 1.7f);

    public bool MonitorOn { get; private set; }
    public int Feed { get; private set; }
    public bool Powered { get; set; } = true;

    RenderTexture picture;
    Material screenMaterial;
    float staticTime;
    static readonly int BaseMap = Shader.PropertyToID("_BaseMap");
    static readonly int BaseColor = Shader.PropertyToID("_BaseColor");

    public bool IsWatching(int feed) => MonitorOn && Powered && Feed == feed && staticTime <= 0f;

    void Awake()
    {
        picture = new RenderTexture(resolution.x, resolution.y, 24) { name = "Security Camera Picture" };
        foreach (Camera c in feeds)
        {
            if (c == null) continue;
            c.targetTexture = picture;
            c.enabled = false;
        }
        if (screen != null)
        {
            screenMaterial = screen.material; // its own copy
            screenMaterial.SetTexture(BaseMap, picture);
            screenMaterial.SetColor(BaseColor, nightVision);
        }
        Refresh();
    }

    void OnDestroy()
    {
        if (picture != null) picture.Release();
    }

    public void Select(int feed)
    {
        if (!Powered || feeds == null || feeds.Length == 0) return;
        Feed = Mathf.Clamp(feed, 0, feeds.Length - 1);
        MonitorOn = true;
        staticTime = 0.25f;
        SahurNightSounds.Play(NightsSounds.Static, screen != null ? screen.transform.position : transform.position, 0.5f);
        Refresh();
    }

    public void ToggleMonitor()
    {
        if (!Powered) return;
        MonitorOn = !MonitorOn;
        if (MonitorOn) staticTime = 0.25f;
        SahurNightSounds.Play(NightsSounds.Beep, screen != null ? screen.transform.position : transform.position, 0.4f);
        Refresh();
    }

    public void TurnOff()
    {
        MonitorOn = false;
        Refresh();
    }

    void Update()
    {
        staticTime = Mathf.Max(0f, staticTime - Time.deltaTime);
        if (staticNoise != null)
        {
            staticNoise.enabled = MonitorOn && Powered && staticTime > 0f;
            if (staticNoise.enabled) staticNoise.material.mainTextureOffset = new Vector2(Random.value, Random.value);
        }
        if (label != null && MonitorOn && Powered)
        {
            bool rec = Mathf.Repeat(Time.time, 1f) < 0.6f;
            label.text = $"CAM {Feed + 1}  {feedNames[Mathf.Min(Feed, feedNames.Length - 1)]}" + (rec ? "   <color=#FF3030>● REC</color>" : "");
        }
        Refresh();
    }

    void Refresh()
    {
        bool on = MonitorOn && Powered;
        for (int i = 0; i < feeds.Length; i++)
            if (feeds[i] != null) feeds[i].enabled = on && i == Feed;
        if (screen != null) screen.enabled = on;
        if (label != null) label.enabled = on;
        if (offLabel != null) offLabel.enabled = !on && Powered;
    }
}
