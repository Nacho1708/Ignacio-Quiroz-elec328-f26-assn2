using UnityEngine;

[RequireComponent(typeof(MeshFilter))]
public class FoodRippleBasics : MonoBehaviour
{
    [Range(0f, 0.15f)] public float rippleHeight = 0.055f;
    public float rippleSpeed = 3f;
    public float ringSpacing = 1.6f;

    private Mesh mesh;
    private Vector3[] originalVertices;
    private Vector3[] changedVertices;

    void Start()
    {
        // .mesh gives this object its own copy of the Plane's mesh.
        mesh = GetComponent<MeshFilter>().mesh;
        originalVertices = mesh.vertices;
        changedVertices = new Vector3[originalVertices.Length];
    }

    void Update()
    {
        for (int i = 0; i < originalVertices.Length; i++)
        {
            Vector3 point = originalVertices[i];

            // A Unity Plane lies in its local XZ plane. Y points upward.
            float distance = Mathf.Sqrt(point.x * point.x + point.z * point.z);
            float fadeAtEdge = Mathf.Clamp01(1f - distance / 5f);
            float ripple = 0.5f + 0.5f * Mathf.Sin(
                ringSpacing * distance - rippleSpeed * Time.time);

            point.y += rippleHeight * fadeAtEdge * ripple;
            changedVertices[i] = point;
        }

        mesh.vertices = changedVertices;
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
    }
}
