using UnityEngine;

// Every sound in Sahur Night, made from scratch in code so there are no audio files to import.
public static class SahurNightSounds
{
    static AudioClip step, click, retreat, chime, win, drone, buzz;

    // A wooden "tung" footstep.
    public static AudioClip Step => step != null ? step : step = Synth("Tung step", 0.35f,
        t => 150f * (1f + 0.4f * Mathf.Exp(-t / 0.02f)), t => Mathf.Exp(-t / 0.09f), 0f, 0.12f, 0.6f);

    // Flashlight switch.
    public static AudioClip Click => click != null ? click : click = Synth("Click", 0.05f,
        t => 2400f, t => Mathf.Exp(-t / 0.008f), 0.6f, 0.5f, 1f);

    // Sahur getting chased off: a "tung" that falls away.
    public static AudioClip Retreat => retreat != null ? retreat : retreat = Synth("Retreat", 0.9f,
        t => 260f * Mathf.Pow(0.35f, t / 0.9f), t => Mathf.Exp(-t / 0.35f) * 0.8f, 0.2f, 0.25f, 0.2f);

    // The clock striking a new hour: two soft bell notes.
    public static AudioClip Chime => chime != null ? chime : chime = Synth("Chime", 1.6f,
        t => t < 0.5f ? 660f : 495f, t => Mathf.Exp(-(t < 0.5f ? t : t - 0.5f) / 0.45f) * 0.5f, 0f, 0f, 1f);

    // 6 AM: three rising notes.
    public static AudioClip Win => win != null ? win : win = Synth("Win", 1.4f,
        t => t < 0.25f ? 523f : t < 0.5f ? 659f : 784f, t => Mathf.Exp(-(t % 0.25f) / 0.3f) * (t < 0.5f ? 0.45f : 0.6f), 0.2f, 0f, 1f);

    // The background hum of the building at night (loops).
    public static AudioClip Drone => drone != null ? drone : drone = Synth("Drone", 8f,
        t => 55f + 1.5f * Mathf.Sin(t * Mathf.PI * 0.25f), t => 0.35f + 0.15f * Mathf.Sin(t * Mathf.PI * 0.5f), 0.1f, 0.35f, 0.05f);

    // A light buzzing while it flickers.
    public static AudioClip Buzz => buzz != null ? buzz : buzz = Synth("Buzz", 0.6f,
        t => 120f, t => 0.25f * (Mathf.Sin(t * 60f) > 0f ? 1f : 0.3f), 0.8f, 0.2f, 1f);

    public static void Play(AudioClip clip, Vector3 position, float volume)
    {
        AudioSource.PlayClipAtPoint(clip, position, volume);
    }

    // A tiny synthesizer: a tone following `pitch` (Hz) at `volume`, mixed toward a square wave and toward
    // noise; `mellow` smooths the noise (1 = raw hiss, smaller = softer rumble).
    public static AudioClip Synth(string name, float seconds, System.Func<float, float> pitch, System.Func<float, float> volume,
                           float square, float noise, float mellow)
    {
        const int rate = 22050;
        float[] samples = new float[Mathf.CeilToInt(seconds * rate)];
        float phase = 0f, hiss = 0f;
        var random = new System.Random(67);
        for (int i = 0; i < samples.Length; i++)
        {
            float t = i / (float)rate;
            phase += 2f * Mathf.PI * pitch(t) / rate;
            float tone = Mathf.Lerp(Mathf.Sin(phase), Mathf.Sign(Mathf.Sin(phase)), square);
            hiss += ((float)random.NextDouble() * 2f - 1f - hiss) * mellow;
            float rumble = 0.6f * hiss * Mathf.Sqrt((2f - mellow) / mellow);
            samples[i] = Mathf.Clamp(0.8f * volume(t) * Mathf.Lerp(tone, rumble, noise), -1f, 1f);
        }
        // Fade the last few milliseconds so loops and endings don't click.
        int fade = Mathf.Min(samples.Length, rate / 100);
        for (int i = 0; i < fade; i++) samples[samples.Length - 1 - i] *= i / (float)fade;
        AudioClip clip = AudioClip.Create(name, samples.Length, 1, rate, false);
        clip.SetData(samples, 0);
        return clip;
    }
}
