using UnityEngine;

// Put this on the small horizontal plane that should ripple.
[RequireComponent(typeof(MeshFilter), typeof(MeshRenderer), typeof(MeshCollider))]
public class WobblePlane : MonoBehaviour
{
    [Range(4, 100)] public int subdivisions = 40;
    [Min(0f)] public float amplitude = 0.18f; // Height in world meters
    [Min(0f)] public float falloff = 1.1f;
    [Min(0f)] public float damping = 2f;
    public float waveNumber = 8f;
    public float waveSpeed = 12f;
    [Min(0.1f)] public float duration = 2.5f;

    Mesh wobbleMesh;
    Vector3[] restVertices;
    Vector3[] movingVertices;
    Vector3 ripplePoint;
    float startTime;
    bool rippling;

    void Awake()
    {
        MeshFilter filter = GetComponent<MeshFilter>();
        Mesh originalMesh = filter.sharedMesh;
        if (originalMesh == null || originalMesh.bounds.size.x < 0.001f ||
            originalMesh.bounds.size.z < 0.001f)
        {
            Debug.LogError("WobblePlane needs a horizontal plane mesh.", this);
            enabled = false;
            return;
        }

        // A built-in Unity Plane has too few vertices for a small ripple.
        // Replace its visual mesh with a denser grid of the same size.
        int n = Mathf.Clamp(subdivisions, 4, 100);
        Bounds bounds = originalMesh.bounds;
        restVertices = new Vector3[(n + 1) * (n + 1)];
        movingVertices = new Vector3[restVertices.Length];
        Vector2[] uv = new Vector2[restVertices.Length];
        int[] triangles = new int[n * n * 6];

        for (int z = 0; z <= n; z++)
        {
            for (int x = 0; x <= n; x++)
            {
                int i = z * (n + 1) + x;
                float u = x / (float)n;
                float v = z / (float)n;
                restVertices[i] = new Vector3(
                    Mathf.Lerp(bounds.min.x, bounds.max.x, u),
                    bounds.center.y,
                    Mathf.Lerp(bounds.min.z, bounds.max.z, v));
                movingVertices[i] = restVertices[i];
                uv[i] = new Vector2(u, v);
            }
        }

        int t = 0;
        for (int z = 0; z < n; z++)
        {
            for (int x = 0; x < n; x++)
            {
                int a = z * (n + 1) + x;
                int b = a + 1;
                int c = a + n + 1;
                int d = c + 1;
                triangles[t++] = a; triangles[t++] = c; triangles[t++] = b;
                triangles[t++] = b; triangles[t++] = c; triangles[t++] = d;
            }
        }

        wobbleMesh = new Mesh { name = "Wobble Plane (runtime)" };
        wobbleMesh.MarkDynamic();
        wobbleMesh.vertices = movingVertices;
        wobbleMesh.uv = uv;
        wobbleMesh.triangles = triangles;
        wobbleMesh.RecalculateNormals();
        wobbleMesh.RecalculateBounds();
        filter.mesh = wobbleMesh;

        // The collider deliberately remains flat. Raycasts and proximity still
        // work, without rebuilding an expensive MeshCollider every frame.
        GetComponent<MeshCollider>().sharedMesh = originalMesh;
    }

    public void WobbleAtWorldPoint(Vector3 point)
    {
        if (wobbleMesh == null) return;
        ripplePoint = point;
        startTime = Time.time;
        rippling = true;
    }

    void Update()
    {
        if (!rippling) return;

        float elapsed = Time.time - startTime;
        if (elapsed >= duration)
        {
            System.Array.Copy(restVertices, movingVertices, restVertices.Length);
            rippling = false;
        }
        else
        {
            // Convert one world meter upward into this object's local space.
            Vector3 localUpPerMeter = transform.InverseTransformVector(transform.up);
            for (int i = 0; i < restVertices.Length; i++)
            {
                float distance = Vector3.Distance(
                    transform.TransformPoint(restVertices[i]), ripplePoint);
                float height = amplitude * Mathf.Exp(-falloff * distance - damping * elapsed)
                    * Mathf.Sin(distance * waveNumber - elapsed * waveSpeed);
                movingVertices[i] = restVertices[i] + localUpPerMeter * height;
            }
        }

        wobbleMesh.vertices = movingVertices;
        wobbleMesh.RecalculateNormals();
        wobbleMesh.RecalculateBounds();
    }

    void OnDestroy()
    {
        if (wobbleMesh != null) Destroy(wobbleMesh);
    }
}
