using UnityEngine;

// A light that flickers now and then, and a lot more when the game says things are getting "nervous".
[RequireComponent(typeof(Light))]
public class FlickerLight : MonoBehaviour
{
    [Tooltip("How often it flickers on its own (0 = steady).")]
    [Range(0f, 1f)] public float jitter = 0.15f;
    [Tooltip("Set by the game: 0 = calm, 1 = flickering wildly (Sahur is right outside).")]
    [Range(0f, 1f)] public float nervous;

    Light lamp;
    float baseIntensity;
    float seed;

    void Awake()
    {
        lamp = GetComponent<Light>();
        baseIntensity = lamp.intensity;
        seed = Random.value * 100f;
    }

    void Update()
    {
        float amount = Mathf.Max(jitter, nervous);
        float noise = Mathf.PerlinNoise(seed, Time.time * (4f + 18f * nervous));
        float dip = noise < amount * 0.6f ? Mathf.Lerp(0.05f, 0.6f, noise / Mathf.Max(0.01f, amount * 0.6f)) : 1f;
        lamp.intensity = baseIntensity * dip;
    }
}
