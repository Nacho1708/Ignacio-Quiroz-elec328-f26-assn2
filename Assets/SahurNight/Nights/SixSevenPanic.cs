using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.XR;

// The 6-7 panic move (from Homework 1): right hand up + left hand down, then swap, a few times quickly.
// Once per night, it blasts every character back to where they started.
// No headset: press 6, 7, 6, 7 on the keyboard.
public class SixSevenPanic : MonoBehaviour
{
    public NightsGame game;
    [Tooltip("Hand swaps in a row (6, 7, 6, 7...) needed.")]
    [Range(2, 8)] public int swapsNeeded = 4;
    [Tooltip("Longest pause between swaps, in seconds.")]
    public float maxPause = 0.9f;
    [Tooltip("How much higher one controller has to be than the other, in meters.")]
    public float heightDifference = 0.12f;

    int pose;   // 1: right hand up, -1: left hand up, 0: level
    int swaps;
    float lastSwap = float.NegativeInfinity;
    bool tracking;
    float rightLow, rightHigh, leftLow, leftHigh;

    void Update()
    {
        Keyboard keyboard = Keyboard.current;
        if (keyboard != null)
        {
            if (keyboard.digit6Key.wasPressedThisFrame && pose != 1) Swap(1);
            if (keyboard.digit7Key.wasPressedThisFrame && pose != -1) Swap(-1);
        }
        ReadControllers();
    }

    void Swap(int newPose)
    {
        pose = newPose;
        if (Time.time - lastSwap > maxPause) swaps = 0;
        swaps++;
        lastSwap = Time.time;
        if (swaps >= swapsNeeded)
        {
            swaps = 0;
            if (game != null) game.Panic();
        }
    }

    void ReadControllers()
    {
        XRController right = XRController.rightHand, left = XRController.leftHand;
        if (!Tracked(right) || !Tracked(left)) { tracking = false; return; }
        float rightY = right.devicePosition.ReadValue().y, leftY = left.devicePosition.ReadValue().y;
        float difference = rightY - leftY;
        if (!tracking || Mathf.Abs(difference) < 0.5f * heightDifference)
        {
            // Hands about level: ready for the next swap.
            tracking = true;
            pose = 0;
            rightLow = rightHigh = rightY;
            leftLow = leftHigh = leftY;
            return;
        }
        rightLow = Mathf.Min(rightLow, rightY); rightHigh = Mathf.Max(rightHigh, rightY);
        leftLow = Mathf.Min(leftLow, leftY); leftHigh = Mathf.Max(leftHigh, leftY);
        float minMove = 0.25f * heightDifference; // both hands have to move, not just one
        if (pose != 1 && difference > heightDifference && rightY - rightLow > minMove && leftHigh - leftY > minMove) Swap(1);
        else if (pose != -1 && difference < -heightDifference && rightHigh - rightY > minMove && leftY - leftLow > minMove) Swap(-1);
    }

    static bool Tracked(XRController hand) => hand != null && hand.isTracked != null && hand.isTracked.isPressed;
}
