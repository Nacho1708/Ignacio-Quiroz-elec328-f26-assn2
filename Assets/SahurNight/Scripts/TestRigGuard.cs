using Unity.XR.CoreUtils;
using UnityEngine;

// This scene has its own XR rig so you can press Play on it directly.
// When the Reality Roost boot scene loads it instead, the Roost already brings its own rig, so this one removes itself.
public class TestRigGuard : MonoBehaviour
{
    void Awake()
    {
        foreach (XROrigin origin in FindObjectsByType<XROrigin>(FindObjectsInactive.Exclude))
        {
            if (!origin.transform.IsChildOf(transform))
            {
                gameObject.SetActive(false);
                Destroy(gameObject);
                return;
            }
        }
    }
}
