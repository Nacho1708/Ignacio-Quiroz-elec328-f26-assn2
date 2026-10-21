using System.Collections.Generic;
using UnityEngine;

// Every sound in Five Nights at Tung's, made from scratch in code (no audio files to import).
public static class NightsSounds
{
    const int Rate = 22050;
    static readonly Dictionary<string, AudioClip> cache = new Dictionary<string, AudioClip>();

    // ----- Bombardiro Crocodilo -----
    public static AudioClip Siren => Get("Air Raid Siren", 4f, Siren1);
    public static AudioClip Engine => Get("Propeller Engine", 2f, Engine1);
    public static AudioClip Whoosh => Get("Whoosh", 1.2f, Whoosh1);
    public static AudioClip Boom => Get("Boom", 1.6f, Boom1);
    // ----- Tralalero Tralala -----
    public static AudioClip Squeak => Get("Sneaker Squeak", 0.13f, Squeak1);
    public static AudioClip Tralalero => Get("Tra La Le Ro", 1.15f, Tralalero1);
    public static AudioClip Bonk => Get("Bonk", 0.75f, Bonk1);
    // ----- Ballerina Cappuccina -----
    public static AudioClip MusicBox => Get("Music Box", 6f, MusicBox1);
    // ----- Brr Brr Patapim -----
    public static AudioClip Brr => Get("Brr Brr", 1f, Brr1);
    // ----- Lirili Larila -----
    public static AudioClip Trumpet => Get("Elephant Trumpet", 1.2f, Trumpet1);
    public static AudioClip FlipFlop => Get("Flip Flop", 0.09f, FlipFlop1);
    public static AudioClip Rewind => Get("Clock Rewind", 1.3f, Rewind1);
    // ----- The office -----
    public static AudioClip DoorSlam => Get("Door Slam", 0.6f, DoorSlam1);
    public static AudioClip DoorCreak => Get("Door Creak", 0.55f, DoorCreak1);
    public static AudioClip Static => Get("Static", 0.25f, Static1);
    public static AudioClip Beep => Get("Beep", 0.08f, t => 0.5f * Mathf.Sign(Mathf.Sin(2f * Mathf.PI * 1250f * t)) * Fade(t, 0.08f, 0.01f));
    public static AudioClip PowerDown => Get("Power Down", 2f, PowerDown1);
    public static AudioClip SixSeven => Get("Six Seven", 1.4f, SixSeven1);
    // ----- Silly jumpscares -----
    public static AudioClip AirHorn => Get("Air Horn", 1f, AirHorn1);
    public static AudioClip Kazoo => Get("Kazoo Scream", 1f, Kazoo1);
    public static AudioClip SlideWhistle => Get("Slide Whistle", 0.9f, SlideWhistle1);
    public static AudioClip Boing => Get("Boing", 0.8f, Boing1);

    public static void Play(AudioClip clip, Vector3 position, float volume)
    {
        if (clip != null) AudioSource.PlayClipAtPoint(clip, position, volume);
    }

    // ---------- The sounds ----------
    // Each returns a function from time (seconds) to a sample; it's called once per sample, in order,
    // so oscillators can keep their phase between calls.

    static System.Func<float, float> Siren1()
    {
        var a = new Osc(); var b = new Osc();
        return t =>
        {
            float sweep = 0.5f - 0.5f * Mathf.Cos(2f * Mathf.PI * t / 2f); // wails up and down every 2 s
            float hz = 360f + 360f * sweep;
            return 0.45f * (a.Saw(hz) * 0.6f + b.Saw(hz * 1.006f) * 0.4f) * Fade(t, 4f, 0.3f, 0.4f);
        };
    }

    static System.Func<float, float> Engine1()
    {
        var a = new Osc(); var noise = new Noise(5);
        return t =>
        {
            float propeller = 0.6f + 0.4f * Mathf.Sin(2f * Mathf.PI * 18f * t); // 36 beats in 2 s: loops cleanly
            return 0.5f * propeller * (0.7f * a.Square(70f) * 0.5f + 0.5f * noise.Low(0.08f));
        };
    }

    static System.Func<float, float> Whoosh1()
    {
        var noise = new Noise(7);
        return t =>
        {
            float bell = Mathf.Sin(Mathf.PI * Mathf.Clamp01(t / 1.2f));
            return 0.9f * bell * bell * noise.Low(Mathf.Lerp(0.04f, 0.4f, bell));
        };
    }

    static System.Func<float, float> Boom1()
    {
        var noise = new Noise(9); var thump = new Osc();
        return t => 1.4f * Mathf.Exp(-t / 0.35f) * (0.75f * noise.Low(0.06f) * 2.5f + 0.5f * thump.Sin(Mathf.Lerp(70f, 35f, t / 1.6f)));
    }

    static System.Func<float, float> Squeak1()
    {
        var a = new Osc();
        return t => 0.45f * a.Sin(Mathf.Lerp(1700f, 2700f, t / 0.13f)) * Fade(t, 0.13f, 0.01f, 0.04f);
    }

    static System.Func<float, float> Tralalero1()
    {
        // "Tra - la - le - ro!" sung on four notes, a bit nasal.
        float[] notes = { 392f, 494f, 587f, 784f };
        var a = new Osc(); var b = new Osc();
        return t =>
        {
            int i = Mathf.Min(3, (int)(t / 0.26f));
            float local = t - i * 0.26f;
            float length = i == 3 ? 0.37f : 0.26f;
            float hz = notes[i] * (1f + 0.015f * Mathf.Sin(2f * Mathf.PI * 6f * t));
            float voice = 0.6f * a.Square(hz) * 0.4f + 0.4f * b.Sin(hz * 2f);
            return 0.5f * voice * Fade(local, length, 0.015f, 0.06f);
        };
    }

    static System.Func<float, float> Bonk1()
    {
        var a = new Osc(); var b = new Osc(); var noise = new Noise(11);
        return t =>
        {
            float bonk = a.Sin(Mathf.Lerp(520f, 140f, Mathf.Clamp01(t / 0.25f))) * Mathf.Exp(-t / 0.12f);
            float click = noise.Next() * Mathf.Exp(-t / 0.01f);
            float stars = t > 0.25f ? b.Sin(t < 0.45f ? 1568f : 2093f) * 0.35f * Mathf.Exp(-((t - 0.25f) % 0.2f) / 0.08f) : 0f;
            return 0.8f * (bonk + 0.5f * click) + stars;
        };
    }

    static System.Func<float, float> MusicBox1()
    {
        // A wobbly little waltz in A minor, 12 notes of half a second (loops every 6 s).
        float[] notes = { 659f, 880f, 1047f, 988f, 880f, 659f, 698f, 880f, 1175f, 1047f, 988f, 831f };
        var a = new Osc(); var b = new Osc();
        float lastNote = -1f; int playing = 0;
        return t =>
        {
            int i = (int)(t / 0.5f) % notes.Length;
            if (i != playing || lastNote < 0f) { playing = i; lastNote = i * 0.5f; }
            float local = t - lastNote;
            float hz = notes[i] * (1f + 0.004f * Mathf.Sin(2f * Mathf.PI * 0.7f * t)); // slightly out of tune
            return 0.45f * Mathf.Exp(-local / 0.35f) * (a.Sin(hz) + 0.3f * b.Sin(hz * 4.02f)) * Fade(t, 6f, 0.004f, 0.004f);
        };
    }

    static System.Func<float, float> Brr1()
    {
        // "brrr... brrr": a whispered rumble, twice.
        var noise = new Noise(13); var hum = new Osc();
        return t =>
        {
            float burst = t < 0.4f ? Fade(t, 0.4f, 0.03f, 0.1f) : t > 0.5f ? Fade(t - 0.5f, 0.45f, 0.03f, 0.12f) : 0f;
            float rattle = 0.55f + 0.45f * Mathf.Sin(2f * Mathf.PI * 32f * t);
            return 0.9f * burst * rattle * (noise.Low(0.15f) * 1.8f + 0.25f * hum.Sin(95f));
        };
    }

    static System.Func<float, float> Trumpet1()
    {
        var a = new Osc(); var b = new Osc();
        return t =>
        {
            float glide = t < 0.35f ? Mathf.Lerp(260f, 560f, t / 0.35f) : Mathf.Lerp(560f, 410f, (t - 0.35f) / 0.85f);
            float hz = glide * (1f + 0.03f * Mathf.Sin(2f * Mathf.PI * 7f * t));
            return 0.4f * (0.7f * a.Saw(hz) + 0.3f * b.Square(hz * 0.5f)) * Fade(t, 1.2f, 0.04f, 0.3f);
        };
    }

    static System.Func<float, float> FlipFlop1()
    {
        var noise = new Noise(17); var a = new Osc();
        return t => 0.8f * Mathf.Exp(-t / 0.015f) * (noise.Next() * 0.7f + 0.5f * a.Sin(320f));
    }

    static System.Func<float, float> Rewind1()
    {
        // Ticks that speed up and drop in pitch: the clock spinning backwards.
        var a = new Osc(); float nextTick = 0f, tickStart = 0f; int ticks = 0;
        return t =>
        {
            if (t >= nextTick) { tickStart = t; ticks++; nextTick = t + Mathf.Lerp(0.16f, 0.035f, t / 1.3f); }
            float local = t - tickStart;
            return 0.6f * Mathf.Exp(-local / 0.008f) * a.Square(Mathf.Lerp(2400f, 900f, t / 1.3f));
        };
    }

    static System.Func<float, float> DoorSlam1()
    {
        var noise = new Noise(19); var ring = new Osc();
        return t => 1.3f * (noise.Low(0.07f) * 2.5f * Mathf.Exp(-t / 0.07f) + 0.25f * ring.Sin(185f) * Mathf.Exp(-t / 0.2f));
    }

    static System.Func<float, float> DoorCreak1()
    {
        var a = new Osc();
        return t => 0.3f * a.Saw(220f + 40f * Mathf.Sin(2f * Mathf.PI * 9f * t) + 60f * t) * Fade(t, 0.55f, 0.05f, 0.15f);
    }

    static System.Func<float, float> Static1()
    {
        var noise = new Noise(23);
        return t => 0.35f * noise.Next() * Fade(t, 0.25f, 0.005f, 0.05f);
    }

    static System.Func<float, float> PowerDown1()
    {
        var a = new Osc(); var b = new Osc();
        return t =>
        {
            float hz = Mathf.Lerp(240f, 30f, Mathf.Pow(t / 2f, 0.6f));
            return 0.55f * (a.Sin(hz) + 0.3f * b.Square(hz * 0.5f) * 0.5f) * Fade(t, 2f, 0.01f, 0.6f);
        };
    }

    static System.Func<float, float> SixSeven1()
    {
        // "SIX!" "SEVEN!" chanted, then a whoomp.
        var a = new Osc(); var b = new Osc(); var noise = new Noise(29);
        return t =>
        {
            float six = t < 0.3f ? Fade(t, 0.3f, 0.01f, 0.08f) * (0.6f * a.Square(330f) * 0.5f + 0.4f * noise.Next() * Mathf.Exp(-t / 0.05f)) : 0f;
            float seven = t >= 0.35f && t < 0.85f ? Fade(t - 0.35f, 0.5f, 0.01f, 0.15f) * 0.6f * a.Square(Mathf.Lerp(440f, 392f, (t - 0.35f) / 0.5f)) * 0.5f : 0f;
            float whoomp = t >= 0.8f ? Mathf.Exp(-(t - 0.8f) / 0.2f) * b.Sin(Mathf.Lerp(120f, 40f, (t - 0.8f) / 0.6f)) : 0f;
            return 0.7f * (six + seven) + 0.8f * whoomp;
        };
    }

    static System.Func<float, float> AirHorn1()
    {
        var a = new Osc(); var b = new Osc(); var c = new Osc();
        return t =>
        {
            float stutter = t < 0.3f ? (Mathf.Repeat(t, 0.1f) < 0.07f ? 1f : 0f) : 1f; // BWAH-BWAH-BWAAAAH
            return 0.28f * stutter * (a.Square(440f) + b.Square(554f) + c.Square(659f)) * Fade(t, 1f, 0.01f, 0.2f);
        };
    }

    static System.Func<float, float> Kazoo1()
    {
        var a = new Osc(); var b = new Osc();
        return t =>
        {
            float hz = Mathf.Lerp(280f, 900f, Mathf.Clamp01(t / 0.25f)) * (1f + 0.06f * Mathf.Sin(2f * Mathf.PI * 11f * t));
            return 0.45f * (0.6f * a.Saw(hz) + 0.4f * b.Square(hz * 1.5f) * 0.5f) * Fade(t, 1f, 0.01f, 0.3f);
        };
    }

    static System.Func<float, float> SlideWhistle1()
    {
        var a = new Osc();
        return t =>
        {
            float hz = t < 0.45f ? Mathf.Lerp(500f, 1600f, t / 0.45f) : Mathf.Lerp(1600f, 600f, (t - 0.45f) / 0.45f);
            return 0.5f * a.Sin(hz) * Fade(t, 0.9f, 0.02f, 0.1f);
        };
    }

    static System.Func<float, float> Boing1()
    {
        var a = new Osc();
        return t => 0.6f * Mathf.Exp(-t / 0.35f) * a.Sin(180f + 90f * Mathf.Sin(2f * Mathf.PI * 13f * t) * Mathf.Exp(-t / 0.4f));
    }

    // ---------- Tiny synthesizer ----------

    static AudioClip Get(string name, float seconds, System.Func<System.Func<float, float>> make)
    {
        if (cache.TryGetValue(name, out AudioClip clip) && clip != null) return clip;
        System.Func<float, float> sample = make();
        var data = new float[Mathf.CeilToInt(seconds * Rate)];
        for (int i = 0; i < data.Length; i++) data[i] = Mathf.Clamp(sample(i / (float)Rate), -1f, 1f);
        clip = AudioClip.Create(name, data.Length, 1, Rate, false);
        clip.SetData(data, 0);
        cache[name] = clip;
        return clip;
    }

    static AudioClip Get(string name, float seconds, System.Func<float, float> sample) => Get(name, seconds, () => sample);

    // Fade in over `attack` and out over `release` (seconds), for a sound `length` long.
    static float Fade(float t, float length, float attack, float release = 0.01f)
    {
        return Mathf.Clamp01(t / Mathf.Max(attack, 1e-4f)) * Mathf.Clamp01((length - t) / Mathf.Max(release, 1e-4f));
    }

    class Osc
    {
        float phase;
        float Step(float hz) { phase += hz / Rate; phase -= Mathf.Floor(phase); return phase; }
        public float Sin(float hz) => Mathf.Sin(2f * Mathf.PI * Step(hz));
        public float Square(float hz) => Step(hz) < 0.5f ? 1f : -1f;
        public float Saw(float hz) => Step(hz) * 2f - 1f;
    }

    class Noise
    {
        readonly System.Random random;
        float low;
        public Noise(int seed) { random = new System.Random(seed); }
        public float Next() => (float)random.NextDouble() * 2f - 1f;
        public float Low(float smooth) { low += (Next() - low) * smooth; return low; } // softer, rumblier noise
    }
}
