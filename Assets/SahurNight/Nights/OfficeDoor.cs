using UnityEngine;

// The office door behind you. The DOOR button on the back rail slams it shut or opens it again.
// Closed, it stops anyone coming down the hallway (and your light), but it eats power.
public class OfficeDoor : MonoBehaviour
{
    [Tooltip("The hinge the door swings around (the door is its child).")]
    public Transform hinge;
    [Tooltip("Hinge angle when open (closed = 0).")]
    public float openAngle = -95f;
    public float swingSeconds = 0.25f;

    float amount;      // 0 = open, 1 = closed
    bool closing;

    public bool Closed => amount > 0.9f;
    public bool Closing => closing;

    public void Toggle() => SetClosed(!closing);

    public void SetClosed(bool closed, bool silent = false)
    {
        if (closed == closing) return;
        closing = closed;
        if (!silent) SahurNightSounds.Play(closed ? NightsSounds.DoorSlam : NightsSounds.DoorCreak, transform.position, closed ? 1f : 0.6f);
    }

    public void Snap(bool closed)
    {
        closing = closed;
        amount = closed ? 1f : 0f;
        Apply();
    }

    void Update()
    {
        amount = Mathf.MoveTowards(amount, closing ? 1f : 0f, Time.deltaTime / Mathf.Max(0.01f, swingSeconds));
        Apply();
    }

    void Apply()
    {
        if (hinge != null) hinge.localRotation = Quaternion.Euler(0f, Mathf.Lerp(openAngle, 0f, amount * amount), 0f);
    }
}
