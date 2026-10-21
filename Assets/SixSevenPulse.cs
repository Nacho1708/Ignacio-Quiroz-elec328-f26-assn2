using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.XR;

// Drag this onto any GameObject and press Play. Nothing else to set up.
// Do the "6 7" with your VR controllers: right hand up + left hand down, then swap, over and over.
// After a few swaps the object starts shrinking and growing in time with you (keeping its proportions).
// Stop, and it springs back to its normal size.
// No headset? Click in the Game view and press 6, 7, 6, 7 on the keyboard.
public class SixSevenPulse : MonoBehaviour
{
    public enum ScalePoint { Bottom, Center, Pivot }

    [Header("Gesture")]
    [Tooltip("How many hand swaps in a row (6, 7, 6, 7, ...) it takes to start.")]
    [Range(1, 10)] public int swapsToStart = 4;
    [Tooltip("Longest pause between swaps, in seconds. Pause longer and it stops.")]
    [Range(0.2f, 3f)] public float maxPause = 1f;
    [Tooltip("How much higher one controller has to be than the other, in meters.")]
    [Range(0.03f, 0.5f)] public float heightDifference = 0.1f;

    [Header("Scaling")]
    [Tooltip("Size on the down beats, as a fraction of the original. Bounciness can overshoot it a little.")]
    [Range(0.1f, 1f)] public float smallest = 0.7f;
    [Tooltip("Size on the up beats, as a fraction of the original. Bounciness can overshoot it a little.")]
    [Range(1f, 3f)] public float biggest = 1.3f;
    [Tooltip("0 = smooth, 1 = very springy.")]
    [Range(0f, 1f)] public float bounciness = 0.5f;
    [Tooltip("The point that stays still while it scales. Bottom keeps it standing on the floor. Applied when Play starts.")]
    public ScalePoint scaleFrom = ScalePoint.Bottom;

    const float Step = 1f / 240f; // spring simulation step, in seconds

    bool started;
    Vector3 baseScale;
    Vector3 anchor; // the point that stays still, in this object's own space
    float size = 1f;
    float sizeSpeed;
    float targetSize = 1f;

    int pose;  // 1: right hand up, -1: left hand up, 0: level
    int swaps; // swaps in a row so far
    float lastSwapTime = float.NegativeInfinity;
    bool tracking;
    float rightLow, rightHigh, leftLow, leftHigh; // hand heights since the hands were last level

    void Start()
    {
        baseScale = transform.localScale;
        anchor = FindAnchor();
        started = true;
    }

    void OnDisable()
    {
        if (!started) return;

        // Put it back to its normal size.
        size = targetSize = 1f;
        sizeSpeed = 0f;
        ApplySize();
        pose = swaps = 0;
        tracking = false;
    }

    void Update()
    {
        ReadKeyboard();
        ReadControllers();

        // Too long since the last swap: stop and go back to normal size.
        if (Time.time - lastSwapTime > maxPause)
            targetSize = 1f;

        Animate();
    }

    void Swap(int newPose)
    {
        pose = newPose;
        if (Time.time - lastSwapTime > maxPause) swaps = 0; // too slow: this starts a new streak
        swaps++;
        lastSwapTime = Time.time;

        // From the swapsToStart-th swap on: down, up, down, up, ...
        if (swaps >= swapsToStart)
            targetSize = (swaps - swapsToStart) % 2 == 0 ? smallest : biggest;
    }

    void ReadKeyboard()
    {
        Keyboard keyboard = Keyboard.current;
        if (keyboard == null) return;

        if (keyboard.digit6Key.wasPressedThisFrame && pose != 1) Swap(1);
        if (keyboard.digit7Key.wasPressedThisFrame && pose != -1) Swap(-1);
    }

    void ReadControllers()
    {
        XRController right = XRController.rightHand;
        XRController left = XRController.leftHand;
        if (!IsTracked(right) || !IsTracked(left))
        {
            tracking = false;
            return;
        }

        // Heights in the headset's tracking space, which is in real-world meters.
        float rightY = right.devicePosition.ReadValue().y;
        float leftY = left.devicePosition.ReadValue().y;
        float difference = rightY - leftY;
        if (!tracking)
        {
            tracking = true;
            pose = 0;
            ResetHandRange(rightY, leftY);
        }

        if (Mathf.Abs(difference) < 0.5f * heightDifference)
        {
            // Hands about level: ready for the next swap, and hand movement is measured from here.
            pose = 0;
            ResetHandRange(rightY, leftY);
            return;
        }
        rightLow = Mathf.Min(rightLow, rightY);
        rightHigh = Mathf.Max(rightHigh, rightY);
        leftLow = Mathf.Min(leftLow, leftY);
        leftHigh = Mathf.Max(leftHigh, leftY);

        // Both hands must really move (one up, one down), so waving just one hand doesn't count.
        float minMove = 0.25f * heightDifference;
        if (pose != 1 && difference > heightDifference &&
            rightY - rightLow > minMove && leftHigh - leftY > minMove)
        {
            Swap(1);
        }
        else if (pose != -1 && difference < -heightDifference &&
                 rightHigh - rightY > minMove && leftY - leftLow > minMove)
        {
            Swap(-1);
        }
    }

    void ResetHandRange(float rightY, float leftY)
    {
        rightLow = rightHigh = rightY;
        leftLow = leftHigh = leftY;
    }

    static bool IsTracked(XRController hand)
    {
        return hand != null && hand.isTracked != null && hand.isTracked.isPressed;
    }

    void Animate()
    {
        // Resting at normal size: leave the transform alone.
        if (targetSize == 1f && size == 1f && sizeSpeed == 0f) return;

        // A spring pulls the size toward targetSize. Less damping = more overshoot and wobble.
        float omega = 2f * Mathf.PI * 4f;                  // springs back about 4 times a second
        float damping = Mathf.Lerp(1f, 0.15f, bounciness); // 1 = no overshoot at all
        for (float remaining = Time.deltaTime; remaining > 0f; remaining -= Step)
        {
            float dt = Mathf.Min(Step, remaining); // small steps keep the spring stable
            sizeSpeed += (omega * omega * (targetSize - size) - 2f * damping * omega * sizeSpeed) * dt;
            size = Mathf.Max(size + sizeSpeed * dt, 0.05f);
        }

        // Settled back at normal size: snap exactly and stop.
        if (targetSize == 1f && Mathf.Abs(size - 1f) < 1e-4f && Mathf.Abs(sizeSpeed) < 1e-3f)
        {
            size = 1f;
            sizeSpeed = 0f;
        }
        ApplySize();
    }

    // Scales the whole object evenly, then shifts it so the anchor point (e.g. its feet) stays put.
    void ApplySize()
    {
        Vector3 anchorBefore = transform.TransformPoint(anchor);
        transform.localScale = baseScale * size;
        transform.position += anchorBefore - transform.TransformPoint(anchor);
    }

    Vector3 FindAnchor()
    {
        if (scaleFrom == ScalePoint.Pivot) return Vector3.zero;

        // A box around everything visible on this object and its children.
        bool found = false;
        Bounds bounds = default;
        foreach (Renderer r in GetComponentsInChildren<Renderer>())
        {
            if (!(r is MeshRenderer || r is SkinnedMeshRenderer || r is SpriteRenderer)) continue;
            if (found) bounds.Encapsulate(r.bounds);
            else bounds = r.bounds;
            found = true;
        }
        if (!found) return Vector3.zero;

        Vector3 point = scaleFrom == ScalePoint.Bottom
            ? new Vector3(bounds.center.x, bounds.min.y, bounds.center.z)
            : bounds.center;
        return transform.InverseTransformPoint(point);
    }
}
