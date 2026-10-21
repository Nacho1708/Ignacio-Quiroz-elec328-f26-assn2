using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.XR;

[RequireComponent(typeof(MeshFilter), typeof(MeshRenderer), typeof(MeshCollider))]
public class MeshDeformation : MonoBehaviour
{
    // Number of points in the mesh
    public int width = 25;
    public int height = 25;

    // Actual size of the surface in Unity units
    public float surfaceWidth = 4f;
    public float surfaceLength = 6f;

    // Drag your tracked right-hand controller here in the Inspector
    public Transform rightController;

    Vector3[] vertices;
    Mesh m;
    MeshCollider meshCollider;

    float startTime;
    Vector3 clickPoint;
    bool hasRipple;
    bool triggerWasPressed;

    void Start()
    {
        width = Mathf.Max(width, 2);
        height = Mathf.Max(height, 2);

        m = new Mesh();
        vertices = new Vector3[width * height];

        // Build a horizontal surface, centered on this object's position.
        for (int i = 0; i < vertices.Length; i++)
        {
            float x = (i % width) / (float)(width - 1);
            float z = (i / width) / (float)(height - 1);

            vertices[i] = new Vector3(
                (x - 0.5f) * surfaceWidth,
                0f,
                (z - 0.5f) * surfaceLength
            );
        }

        int[] triangles = new int[(width - 1) * (height - 1) * 6];

        for (int j = 0; j < height - 1; j++)
        {
            for (int i = 0; i < width - 1; i++)
            {
                int t = (j * (width - 1) + i) * 6;

                int A = j * width + i;
                int B = (j + 1) * width + i;
                int C = (j + 1) * width + i + 1;
                int D = j * width + i + 1;

                triangles[t]     = A;
                triangles[t + 1] = B;
                triangles[t + 2] = C;

                triangles[t + 3] = A;
                triangles[t + 4] = C;
                triangles[t + 5] = D;
            }
        }


        m.vertices = vertices;
        m.triangles = triangles;
        m.RecalculateNormals();

        GetComponent<MeshFilter>().mesh = m;

        meshCollider = GetComponent<MeshCollider>();
        meshCollider.sharedMesh = m;
    }

    void Update()
    {
        // Keep mouse clicking available while testing in the Editor.
        if (Mouse.current != null &&
            Mouse.current.leftButton.wasPressedThisFrame &&
            Camera.main != null)
        {
            Ray mouseRay = Camera.main.ScreenPointToRay(
                Mouse.current.position.ReadValue()
            );
            TryStartRipple(mouseRay);
        }

        // Check whether the right VR controller's trigger was just pressed.
        var hand = InputDevices.GetDeviceAtXRNode(XRNode.RightHand);
        bool triggerPressed = false;

        if (hand.isValid)
            hand.TryGetFeatureValue(UnityEngine.XR.CommonUsages.triggerButton,
                                    out triggerPressed);

        if (triggerPressed && !triggerWasPressed && rightController != null)
        {
            Ray controllerRay = new Ray(
                rightController.forward,
                rightController.position
            );
            TryStartRipple(controllerRay);
        }

        triggerWasPressed = triggerPressed;

        if (!hasRipple)
            return;

        float elapsedTime = Time.time - startTime;

        for (int i = 0; i < vertices.Length; i++)
        {
            float distance = Vector2.Distance(
                new Vector2(vertices[i].x, vertices[i].z),
                new Vector2(clickPoint.x, clickPoint.z)
            );

            float wobble =
                0.3f *
                Mathf.Exp(-elapsedTime / 3f) *
                Mathf.Exp(-distance * 2f) *
                Mathf.Sin(elapsedTime * 7f);

            vertices[i].y = wobble;
        }

        m.vertices = vertices;
        m.RecalculateNormals();
        m.RecalculateBounds();
    }

    void TryStartRipple(Ray ray)
    {
        if (meshCollider.Raycast(ray, out RaycastHit hit, 20f))
        {
            clickPoint = transform.InverseTransformPoint(hit.point);
            startTime = Time.time;
            hasRipple = true;
        }
    }

}
