using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;
using UnityEngine.InputSystem.XR;

// Drag this onto a sphere (GameObject > 3D Object > Sphere) and press Play. Nothing else to set up.
// Click the sphere with the mouse, or point a VR controller at it and pull the trigger,
// and a wave ripples out across the surface from the spot you pressed.
[RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
public class WobbleSphere : MonoBehaviour
{
    [Tooltip("Vertices around the sphere. Higher looks smoother but costs more CPU. Applied when Play starts.")]
    [Range(16, 256)] public int resolution = 96;

    [Header("Wave (distances are in sphere radii; the far side is 2 away)")]
    [Tooltip("How far the surface moves in and out.")]
    [Range(0f, 0.3f)] public float amplitude = 0.05f;
    [Tooltip("Distance between wave crests.")]
    [Range(0.2f, 3f)] public float wavelength = 0.6f;
    [Tooltip("How fast the wave spreads, in radii per second.")]
    [Range(0.2f, 10f)] public float waveSpeed = 2f;
    [Tooltip("Seconds for the wobbling at any one spot to die down.")]
    [Range(0.05f, 5f)] public float fadeTime = 0.5f;
    [Tooltip("How far the wave travels before it fades.")]
    [Range(0.2f, 10f)] public float fadeDistance = 2f;
    [Tooltip("Width of the dent where you press.")]
    [Range(0.01f, 0.5f)] public float pressSize = 0.12f;

    [Header("Pressing")]
    [Tooltip("How far away (in meters) a mouse click or controller can press the sphere from.")]
    public float maxPressDistance = 50f;

    struct Ripple
    {
        public Vector3 direction; // where it was pressed: unit vector from the center, in local space
        public float startTime;
    }

    const int MaxRipples = 8;
    readonly List<Ripple> ripples = new List<Ripple>();

    Mesh mesh;
    Collider sphereCollider;
    Vector3 center;
    float radius;
    int segments;             // vertices around each ring (plus one duplicate for the texture seam)
    int rings;                // rings from the bottom pole to the top pole
    Vector3[] restDirections; // unit vector from the center to each vertex (also its normal at rest)
    Vector3[] restVertices;
    Vector3[] vertices;
    readonly List<Vector3> normals = new List<Vector3>();
    bool isDeformed;

    InputAction leftTrigger;
    InputAction rightTrigger;

    void Awake()
    {
        MeshFilter meshFilter = GetComponent<MeshFilter>();

        // Keep the size of the sphere this was dropped on (Unity's sphere has radius 0.5).
        center = Vector3.zero;
        radius = 0.5f;
        if (meshFilter.sharedMesh != null)
        {
            Bounds bounds = meshFilter.sharedMesh.bounds;
            center = bounds.center;
            radius = Mathf.Max(bounds.extents.x, bounds.extents.y, bounds.extents.z);
            if (radius <= 0f) radius = 0.5f;
        }

        // Unity's built-in sphere is too coarse for waves, so swap in a finer one.
        mesh = BuildSphere();
        meshFilter.sharedMesh = mesh;

        // Presses are detected with a collider. Unity's sphere already has one.
        sphereCollider = GetComponent<Collider>();
        if (sphereCollider == null)
        {
            SphereCollider added = gameObject.AddComponent<SphereCollider>();
            added.center = center;
            added.radius = radius;
            sphereCollider = added;
        }
    }

    void OnEnable()
    {
        leftTrigger = new InputAction("Left Trigger", InputActionType.Button,
            "<XRController>{LeftHand}/{TriggerButton}");
        rightTrigger = new InputAction("Right Trigger", InputActionType.Button,
            "<XRController>{RightHand}/{TriggerButton}");
        leftTrigger.Enable();
        rightTrigger.Enable();
    }

    void OnDisable()
    {
        leftTrigger.Dispose();
        rightTrigger.Dispose();
    }

    void OnDestroy()
    {
        if (mesh != null) Destroy(mesh);
    }

    void Update()
    {
        // Mouse click (handy for testing in the Game view).
        Mouse mouse = Mouse.current;
        if (mouse != null && mouse.leftButton.wasPressedThisFrame)
        {
            Camera cam = Camera.main != null ? Camera.main
                : Camera.allCamerasCount > 0 ? Camera.allCameras[0] : null;
            if (cam != null)
                TryPress(cam.ScreenPointToRay(mouse.position.ReadValue()));
        }

        // VR: point either controller at the sphere and pull its trigger.
        if (rightTrigger.WasPressedThisFrame())
            TryPress(XRController.rightHand);
        if (leftTrigger.WasPressedThisFrame())
            TryPress(XRController.leftHand);

        // A ripple is over once its wave has crossed the whole sphere and died down.
        float lifetime = 2f / waveSpeed + 5f * fadeTime;
        for (int r = ripples.Count - 1; r >= 0; r--)
            if (Time.time - ripples[r].startTime > lifetime)
                ripples.RemoveAt(r);

        if (ripples.Count > 0)
        {
            Deform();
            isDeformed = true;
        }
        else if (isDeformed)
        {
            // All ripples are done: snap back to the exact round shape.
            mesh.vertices = restVertices;
            mesh.normals = restDirections;
            mesh.RecalculateBounds();
            isDeformed = false;
        }
    }

    // Starts a ripple at the spot on the sphere nearest to worldPoint.
    // Other scripts can call this too (for example when something hits the sphere).
    public void WobbleAt(Vector3 worldPoint)
    {
        Vector3 direction = (transform.InverseTransformPoint(worldPoint) - center).normalized;
        if (direction == Vector3.zero) return;

        if (ripples.Count == MaxRipples) ripples.RemoveAt(0); // the oldest ripple makes room
        ripples.Add(new Ripple { direction = direction, startTime = Time.time });
    }

    void TryPress(XRController hand)
    {
        if (hand == null) return;

        // Aim like a laser pointer, from the controller's pointer pose (its grip pose if there is none).
        Vector3Control position = hand.TryGetChildControl<Vector3Control>("pointerPosition") ?? hand.devicePosition;
        QuaternionControl rotation = hand.TryGetChildControl<QuaternionControl>("pointerRotation") ?? hand.deviceRotation;
        if (position == null || rotation == null) return;

        Vector3 origin = position.ReadValue();
        Vector3 direction = rotation.ReadValue() * Vector3.forward;

        // Controller poses are relative to the XR rig (the camera's parent), not the world.
        Transform rig = Camera.main != null ? Camera.main.transform.parent : null;
        if (rig != null)
        {
            origin = rig.TransformPoint(origin);
            direction = rig.TransformDirection(direction);
        }

        TryPress(new Ray(origin, direction));
    }

    void TryPress(Ray ray)
    {
        // Only the first thing the ray hits counts, so a sphere hidden behind something is not pressed.
        if (Physics.Raycast(ray, out RaycastHit hit, maxPressDistance,
                Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore) &&
            hit.collider == sphereCollider)
        {
            WobbleAt(hit.point);
        }
    }

    void Deform()
    {
        for (int i = 0; i < restDirections.Length; i++)
        {
            float height = 0f; // outward displacement, in radii
            foreach (Ripple ripple in ripples)
                height += Wave(restDirections[i], ripple);

            vertices[i] = center + restDirections[i] * (radius * (1f + height));
        }

        mesh.vertices = vertices;
        mesh.RecalculateNormals();
        SmoothSeam();
        mesh.RecalculateBounds();
    }

    // Height of one ripple (in radii) at the vertex pointing in `direction` from the center.
    // The pressed spot bobs in and out and calms down, and that motion travels outward at waveSpeed.
    float Wave(Vector3 direction, Ripple ripple)
    {
        // Distance from the pressed spot (0 to 2 radii), rounded off under the
        // fingertip so the dent is smooth there instead of pointed.
        float distance = Mathf.Sqrt((direction - ripple.direction).sqrMagnitude + pressSize * pressSize) - pressSize;
        float t = Time.time - ripple.startTime - distance / waveSpeed; // time since the wave reached this spot
        if (t <= 0f) return 0f;                                           // it hasn't yet

        float period = wavelength / waveSpeed; // seconds per bob
        float envelope = (1f - Mathf.Exp(-6f * t / period)) * Mathf.Exp(-t / fadeTime); // smooth start, then dies down
        float falloff = Mathf.Exp(-distance / fadeDistance);                             // weaker further away

        // Minus sign: the pressed spot dips in first, like a finger pushing on it.
        return -amplitude * falloff * envelope * Mathf.Sin(2f * Mathf.PI * t / period);
    }

    // RecalculateNormals doesn't know that the vertices along the texture seam and at the poles
    // are doubled up, so those would show as creases while the sphere is deformed. Average them.
    void SmoothSeam()
    {
        mesh.GetNormals(normals);
        for (int ring = 0; ring <= rings; ring++)
        {
            int first = ring * (segments + 1);
            int last = first + segments;
            // At a pole the whole ring is one point; elsewhere only the ring's two ends meet.
            int step = ring == 0 || ring == rings ? 1 : segments;

            Vector3 sum = Vector3.zero;
            for (int i = first; i <= last; i += step) sum += normals[i];
            Vector3 average = sum.normalized;
            for (int i = first; i <= last; i += step) normals[i] = average;
        }
        mesh.SetNormals(normals);
    }

    Mesh BuildSphere()
    {
        segments = Mathf.Clamp(resolution, 16, 256);
        rings = segments / 2;
        int count = (segments + 1) * (rings + 1);

        restDirections = new Vector3[count];
        restVertices = new Vector3[count];
        Vector2[] uv = new Vector2[count];
        Vector4[] tangents = new Vector4[count];

        for (int ring = 0, i = 0; ring <= rings; ring++)
        {
            float v = ring / (float)rings;
            float polar = Mathf.PI * v; // 0 at the bottom, PI at the top
            for (int seg = 0; seg <= segments; seg++, i++)
            {
                float u = seg / (float)segments;
                float azimuth = 2f * Mathf.PI * u + Mathf.PI; // seam on the -X side, like Unity's sphere
                Vector3 dir = new Vector3(
                    Mathf.Sin(polar) * Mathf.Cos(azimuth),
                    -Mathf.Cos(polar),
                    Mathf.Sin(polar) * Mathf.Sin(azimuth));

                restDirections[i] = dir;
                restVertices[i] = center + dir * radius;
                uv[i] = new Vector2(u, v);
                tangents[i] = new Vector4(-Mathf.Sin(azimuth), 0f, Mathf.Cos(azimuth), -1f);
            }
        }
        vertices = (Vector3[])restVertices.Clone();

        // Two triangles per grid square, wound clockwise as seen from outside.
        // The squares touching a pole collapse to one triangle.
        List<int> triangles = new List<int>(segments * rings * 6);
        for (int ring = 0; ring < rings; ring++)
        {
            for (int seg = 0; seg < segments; seg++)
            {
                int a = ring * (segments + 1) + seg; // bottom left
                int b = a + 1;                        // bottom right
                int c = a + segments + 1;             // top left
                int d = c + 1;                        // top right

                if (ring < rings - 1) { triangles.Add(a); triangles.Add(c); triangles.Add(d); }
                if (ring > 0) { triangles.Add(a); triangles.Add(d); triangles.Add(b); }
            }
        }

        Mesh sphere = new Mesh { name = "Wobble Sphere" };
        sphere.MarkDynamic();
        sphere.vertices = restVertices;
        sphere.normals = restDirections; // a sphere's normals point straight out
        sphere.tangents = tangents;
        sphere.uv = uv;
        sphere.SetTriangles(triangles, 0);
        sphere.RecalculateBounds();
        return sphere;
    }
}
