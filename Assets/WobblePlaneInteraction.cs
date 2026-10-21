using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.XR;

// Put this on a separate, ordinary GameObject in your scene.
public class WobblePlaneInteraction : MonoBehaviour
{
    public Camera playerCamera;             // Optional; finds Main Camera at runtime
    public Transform rightController;       // Optional; aim with controller if assigned
    public InputActionReference triggerAction; // Optional: your right trigger Input Action
    public float maxDistance = 15f;
    public LayerMask hitLayers = ~0;

    bool wasPressed;

    void Update()
    {
        Camera cameraToUse = playerCamera != null ? playerCamera : Camera.main;

        // Mouse testing in the Game tab.
        Mouse mouse = Mouse.current;
        if (mouse != null && mouse.leftButton.wasPressedThisFrame && cameraToUse != null)
            TryWobble(cameraToUse.ScreenPointToRay(mouse.position.ReadValue()));

        // Physical XR trigger, or an action from the XR Interaction Toolkit.
        UnityEngine.XR.InputDevice hand = InputDevices.GetDeviceAtXRNode(XRNode.RightHand);
        bool devicePressed = hand.isValid &&
            hand.TryGetFeatureValue(UnityEngine.XR.CommonUsages.triggerButton, out bool button) && button;
        bool actionPressed = triggerAction != null && triggerAction.action != null &&
            triggerAction.action.IsPressed();
        bool pressed = devicePressed || actionPressed;

        if (pressed && !wasPressed)
        {
            if (rightController != null)
                TryWobble(new Ray(rightController.position, rightController.forward));
            else if (cameraToUse != null)
                TryWobble(new Ray(cameraToUse.transform.position,
                    cameraToUse.transform.forward)); // Head-gaze fallback
        }

        wasPressed = pressed;
    }

    void TryWobble(Ray ray)
    {
        if (!Physics.Raycast(ray, out RaycastHit hit, maxDistance, hitLayers,
            QueryTriggerInteraction.Ignore)) return;

        WobblePlane plane = hit.collider.GetComponent<WobblePlane>();
        if (plane != null) plane.WobbleAtWorldPoint(hit.point);
    }
}
