using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

// The enchanted flashlight. Every frame it sends the beam's world-space origin, direction and cone angle
// to every Enchanted Flashlight material (as global shader values), so surfaces inside the beam reveal
// their hidden texture. A real spot light with the same cone lights the scene.
// VR: grab it and pull the trigger to switch it on and off.
// No headset: it follows the mouse; left click switches it on and off.
[RequireComponent(typeof(XRGrabInteractable))]
public class EnchantedFlashlight : MonoBehaviour
{
    [Tooltip("The front of the flashlight. The beam starts here and points along its blue (Z) axis.")]
    public Transform lens;
    [Tooltip("The real light. Its cone is kept the same as the shader's.")]
    public Light spotLight;
    [Tooltip("Full width of the beam, in degrees.")]
    [Range(5f, 90f)] public float coneAngle = 34f;
    [Tooltip("How far the beam reaches, in meters.")]
    public float range = 18f;
    [Tooltip("Spot light brightness when fully on.")]
    public float lightIntensity = 12f;

    [Header("Battery")]
    [Tooltip("Seconds of light from a full battery.")]
    public float batterySeconds = 150f;
    [Tooltip("How fast it recharges while off, compared to how fast it drains while on.")]
    [Range(0f, 1f)] public float rechargeRate = 0.35f;
    [Tooltip("On: the game powers the flashlight from its shared power meter (SetCharge) instead of this battery.")]
    public bool sharedPower;
    [Tooltip("Little lights on the handle, lit left to right as the battery fills.")]
    public Renderer[] batteryBars;
    [Tooltip("The glass at the front, glows while on.")]
    public Renderer lensGlass;
    [Tooltip("If it's dropped on the floor (or thrown far away), it floats back to where it started after this many seconds.")]
    public float returnAfter = 3f;

    static readonly int OriginId = Shader.PropertyToID("_Flashlight_Origin");
    static readonly int DirectionId = Shader.PropertyToID("_Flashlight_Direction");
    static readonly int ConeId = Shader.PropertyToID("_Flashlight_ConeAngle");
    static readonly int OnId = Shader.PropertyToID("_Flashlight_On");
    static readonly int RangeId = Shader.PropertyToID("_Flashlight_Range");
    static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
    static readonly int EmissionId = Shader.PropertyToID("_EmissionColor");

    bool switchedOn;
    float battery = 1f;
    float forcedOff;       // seconds the light is forced off (e.g. when Sahur catches you)
    XRGrabInteractable grab;
    MaterialPropertyBlock block;
    Vector3 homePosition;
    Quaternion homeRotation;
    float lostTime;

    public bool IsOn => Brightness > 0.25f;
    public float Battery => battery;                 // 0..1
    public float Brightness { get; private set; }    // 0..1, including flicker
    public Vector3 Origin => lens.position;
    public Vector3 Direction => lens.forward;
    public bool Desktop => !UnityEngine.XR.XRSettings.isDeviceActive;

    void Awake()
    {
        grab = GetComponent<XRGrabInteractable>();
        grab.activated.AddListener(_ => Toggle());
        block = new MaterialPropertyBlock();
        if (lens == null) lens = transform;
        homePosition = transform.position;
        homeRotation = transform.rotation;
    }

    public void Toggle()
    {
        switchedOn = !switchedOn;
        SahurNightSounds.Play(SahurNightSounds.Click, lens.position, 0.5f);
    }

    public void SwitchOn(bool on) => switchedOn = on;

    public void Recharge()
    {
        battery = 1f;
        forcedOff = 0f;
    }

    // Shared power: the game sets how much charge is left (0..1). At 0 the light can't turn on.
    public void SetCharge(float charge) => battery = Mathf.Clamp01(charge);

    public void ForceOff(float seconds) => forcedOff = Mathf.Max(forcedOff, seconds);

    void Update()
    {
        if (Desktop && !grab.isSelected) FollowMouse();
        else ReturnIfDropped();

        // Battery: drains while on, slowly recharges while off. Empty = off until it has some charge again.
        if (!sharedPower)
        {
            if (switchedOn && battery > 0f) battery = Mathf.Max(0f, battery - Time.deltaTime / batterySeconds);
            else if (!switchedOn) battery = Mathf.Min(1f, battery + rechargeRate * Time.deltaTime / batterySeconds);
        }
        if (battery <= 0f) switchedOn = false;
        forcedOff = Mathf.Max(0f, forcedOff - Time.deltaTime);

        // Flicker when the battery is low.
        float target = switchedOn && forcedOff <= 0f ? 1f : 0f;
        if (target > 0f && battery < 0.15f)
            target = Mathf.PerlinNoise(Time.time * 12f, 0.3f) > 0.35f ? 1f : 0.15f;
        Brightness = target;

        // Objective A: tell every enchanted material where the beam is, every frame.
        Shader.SetGlobalVector(OriginId, lens.position);
        Shader.SetGlobalVector(DirectionId, lens.forward);
        Shader.SetGlobalFloat(ConeId, coneAngle);
        Shader.SetGlobalFloat(OnId, Brightness);
        Shader.SetGlobalFloat(RangeId, range);

        if (spotLight != null)
        {
            spotLight.enabled = Brightness > 0f;
            spotLight.intensity = lightIntensity * Brightness;
            spotLight.spotAngle = coneAngle;
            spotLight.innerSpotAngle = coneAngle * 0.7f;
            spotLight.range = range;
        }
        ShowBattery();
    }

    // Without a headset: hold the flashlight at the lower right of the view and point it at the mouse.
    void FollowMouse()
    {
        Camera cam = Camera.main;
        if (cam == null) return;
        if (TryGetComponent(out Rigidbody body)) body.isKinematic = true;
        Transform view = cam.transform;
        transform.position = view.position + view.right * 0.22f - view.up * 0.2f + view.forward * 0.35f;
        Vector2 mouse = Mouse.current != null ? Mouse.current.position.ReadValue() : new Vector2(Screen.width, Screen.height) * 0.5f;
        Ray ray = cam.ScreenPointToRay(mouse);
        Vector3 target = Physics.Raycast(ray, out RaycastHit hit, 50f) ? hit.point : ray.GetPoint(20f);
        transform.rotation = Quaternion.LookRotation(target - transform.position, view.up);
        if (Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame) Toggle();
        if (Keyboard.current != null && Keyboard.current.fKey.wasPressedThisFrame) Toggle();
    }

    // In VR: if it ends up on the floor or far away, put it back on its shelf.
    void ReturnIfDropped()
    {
        bool lost = !grab.isSelected && (transform.position.y < 0.3f || (transform.position - homePosition).sqrMagnitude > 9f);
        lostTime = lost ? lostTime + Time.deltaTime : 0f;
        if (lostTime < returnAfter) return;
        lostTime = 0f;
        if (TryGetComponent(out Rigidbody body) && !body.isKinematic)
        {
            body.linearVelocity = Vector3.zero;
            body.angularVelocity = Vector3.zero;
        }
        transform.SetPositionAndRotation(homePosition, homeRotation);
    }

    void ShowBattery()
    {
        if (batteryBars != null)
        {
            Color color = battery > 0.5f ? new Color(0.2f, 1f, 0.3f) : battery > 0.2f ? new Color(1f, 0.8f, 0.1f) : new Color(1f, 0.15f, 0.1f);
            for (int i = 0; i < batteryBars.Length; i++)
            {
                bool lit = battery > (i + 0.01f) / batteryBars.Length;
                block.SetColor(BaseColorId, lit ? color : new Color(0.08f, 0.08f, 0.08f));
                block.SetColor(EmissionId, lit ? color * 2f : Color.black);
                batteryBars[i].SetPropertyBlock(block);
            }
        }
        if (lensGlass != null)
        {
            block.SetColor(BaseColorId, Color.Lerp(new Color(0.3f, 0.3f, 0.3f), new Color(1f, 0.95f, 0.8f), Brightness));
            block.SetColor(EmissionId, new Color(1f, 0.92f, 0.75f) * (3f * Brightness));
            lensGlass.SetPropertyBlock(block);
        }
    }
}
