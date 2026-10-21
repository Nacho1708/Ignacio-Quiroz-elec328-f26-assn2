using System.Collections.Generic;
using UnityEngine;
using Canvas = SahurNightTextures.Canvas;

// Paints the textures for the Five Nights characters: a normal one and a "cursed" one (only visible in the
// flashlight beam) for every material: shark skin, Nike sneakers, tutu, cappuccino foam, bark, crocodile scales,
// bomber wings, cactus, elephant skin, sand...
public static class BrainrotTextures
{
    const int Size = 256;

    public enum Pattern { Plain, Speckles, Stripes, Scales, Bark, Panels, Ruffles, Wrinkles, Ribs, Leaves, Dunes, Awning }

    public class Pair
    {
        public Texture2D normal, hidden;
    }

    // A texture pair: `color` in a `pattern`, plus an optional picture painted on top (`decal`).
    // The hidden version is a darker copy with glowing cracks and the word `word` scrawled on it in `glow`.
    public static Pair Make(string folder, string name, Color color, Pattern pattern, Color glow, string word, int seed,
                            System.Action<Canvas> decal = null, System.Action<Canvas> hiddenDecal = null)
    {
        var c = new Canvas(Size, Size, color, seed);
        Paint(c, pattern, color);
        decal?.Invoke(c);
        var pair = new Pair { normal = c.Save($"{folder}/{name}.png") };

        var h = new Canvas(Size, Size, Color.black, seed + 1000);
        System.Array.Copy(c.Px, h.Px, c.Px.Length);
        h.Darken(0.4f, Color.white);
        for (int i = 0; i < 7; i++) // glowing cracks
        {
            float x = h.Rand(0, Size), y = h.Rand(0, Size), a = h.Rand(0, Mathf.PI * 2f);
            for (int k = 0; k < 6; k++)
            {
                float nx = x + Mathf.Cos(a) * 9f, ny = y + Mathf.Sin(a) * 9f;
                h.Line(x, y, nx, ny, 2f, glow, 0.9f);
                x = nx; y = ny; a += h.Rand(-0.8f, 0.8f);
            }
        }
        if (!string.IsNullOrEmpty(word))
        {
            float cell = Mathf.Clamp(220f / (word.Length * 6f), 3f, 7f);
            h.Write(word, 12, 175, cell, glow, 1f, 1f, true);
            h.Write(word, 40, 95, cell, glow, 1f, 1f, true);
            h.Write(word, 8, 15, cell, glow, 1f, 1f, false);
        }
        hiddenDecal?.Invoke(h);
        pair.hidden = h.Save($"{folder}/{name} Cursed.png");
        return pair;
    }

    static void Paint(Canvas c, Pattern pattern, Color color)
    {
        Color dark = color * 0.6f, light = Color.Lerp(color, Color.white, 0.35f);
        dark.a = light.a = 1f;
        switch (pattern)
        {
            case Pattern.Speckles:
                for (int i = 0; i < 500; i++) c.Disc(c.Rand(0, Size), c.Rand(0, Size), 1.6f, 1.6f, c.Rand(0f, 1f) < 0.5f ? light : dark, 0.6f);
                break;
            case Pattern.Stripes:
                for (int y = 0; y < Size; y++) if ((y / 16) % 2 == 0) for (int x = 0; x < Size; x++) c.Blend(x, y, light, 0.5f);
                break;
            case Pattern.Scales:
                for (int row = 0; row < Size / 20 + 1; row++)
                    for (int col = 0; col < Size / 26 + 2; col++)
                    {
                        float x = col * 26 + (row % 2) * 13, y = row * 20;
                        c.Disc(x, y, 13f, 11f, dark, 0.9f);
                        c.Disc(x, y + 1.5f, 10.5f, 8.5f, Color.Lerp(color, light, c.Rand(0f, 0.6f)), 1f);
                    }
                break;
            case Pattern.Bark:
                for (int i = 0; i < 26; i++)
                {
                    float x = c.Rand(0, Size), wobble = c.Rand(4f, 9f), phase = c.Rand(0f, 6f);
                    for (int y = 0; y < Size; y += 3)
                        c.Disc(x + wobble * Mathf.Sin(y * 0.05f + phase), y, 2f, 2.5f, dark, 0.7f);
                }
                for (int i = 0; i < 5; i++) c.Disc(c.Rand(0, Size), c.Rand(0, Size), 9f, 13f, dark * 0.8f, 0.9f);
                break;
            case Pattern.Panels:
                for (int y = 0; y < Size; y++) for (int x = 0; x < Size; x++) if (x % 64 < 2 || y % 64 < 2) c.Blend(x, y, dark, 1f);
                for (int y = 8; y < Size; y += 64) for (int x = 8; x < Size; x += 16) c.Disc(x, y, 1.6f, 1.6f, light, 0.9f);
                break;
            case Pattern.Ruffles:
                for (int y = 0; y < Size; y++)
                    for (int x = 0; x < Size; x++)
                        c.Blend(x, y, Mathf.Sin(x * 0.2f) > 0f ? light : dark, 0.25f * Mathf.Abs(Mathf.Sin(x * 0.2f)));
                break;
            case Pattern.Wrinkles:
                for (int i = 0; i < 30; i++)
                {
                    float y = c.Rand(0, Size), x0 = c.Rand(0, Size), length = c.Rand(20f, 60f);
                    for (float x = x0; x < x0 + length; x += 2f) c.Disc(x, y + 3f * Mathf.Sin(x * 0.15f), 1.2f, 1.2f, dark, 0.7f);
                }
                break;
            case Pattern.Ribs:
                for (int x = 0; x < Size; x++) for (int y = 0; y < Size; y++) if (x % 32 < 3) c.Blend(x, y, dark, 0.9f);
                for (int x = 16; x < Size; x += 32) for (int y = 6; y < Size; y += 14) c.Disc(x + 15, y, 1.4f, 1.4f, new Color(0.95f, 0.95f, 0.8f), 1f);
                break;
            case Pattern.Leaves:
                for (int i = 0; i < 260; i++) c.Disc(c.Rand(0, Size), c.Rand(0, Size), c.Rand(5f, 10f), c.Rand(3f, 6f), Color.Lerp(dark, light, c.Rand(0f, 1f)), 0.9f, c.Rand(0f, 3f));
                break;
            case Pattern.Dunes:
                for (int y = 0; y < Size; y++)
                    for (int x = 0; x < Size; x++)
                        c.Blend(x, y, Mathf.Sin(y * 0.12f + 2f * Mathf.Sin(x * 0.03f)) > 0.6f ? light : color, 0.5f);
                break;
            case Pattern.Awning:
                for (int x = 0; x < Size; x++) if ((x / 32) % 2 == 0) for (int y = 0; y < Size; y++) c.Blend(x, y, Color.white, 1f);
                break;
        }
        if (pattern != Pattern.Plain) c.Noise(0.18f, 4f, c.Rand(0f, 50f));
        else c.Noise(0.08f, 3f, c.Rand(0f, 50f));
    }

    // ---------- Decals ----------

    // A sneaker swoosh.
    public static void Swoosh(Canvas c, Color color)
    {
        for (int i = 0; i <= 40; i++)
        {
            float t = i / 40f;
            float x = Mathf.Lerp(40f, 220f, t), y = 120f - 50f * Mathf.Sin(t * Mathf.PI * 0.85f) + 40f * t;
            c.Disc(x, y, 3f + 9f * Mathf.Sin(t * Mathf.PI), 3f + 9f * Mathf.Sin(t * Mathf.PI), color, 1f);
        }
    }

    // Latte art: a heart (or a skull, for the cursed one).
    public static void LatteHeart(Canvas c)
    {
        Color foam = new Color(0.96f, 0.9f, 0.78f);
        c.Disc(128, 128, 110, 110, new Color(0.75f, 0.55f, 0.35f), 1f);
        c.Disc(100, 145, 38, 38, foam, 1f);
        c.Disc(156, 145, 38, 38, foam, 1f);
        for (int i = 0; i < 30; i++) c.Disc(128, 145 - i * 2.4f, 52 - i * 1.7f, 4f, foam, 1f);
    }

    public static void LatteSkull(Canvas c)
    {
        Color glow = new Color(1f, 0.3f, 0.1f);
        c.Disc(128, 140, 60, 55, glow, 1f);
        c.Disc(128, 95, 35, 25, glow, 1f);
        c.Disc(105, 145, 15, 18, Color.black, 1f);
        c.Disc(151, 145, 15, 18, Color.black, 1f);
        c.Disc(128, 118, 6, 8, Color.black, 1f);
        for (int i = -2; i <= 2; i++) c.Line(128 + i * 9, 92, 128 + i * 9, 78, 3f, Color.black, 1f);
    }

    // A bomber's star roundel.
    public static void Roundel(Canvas c, Color ring, Color star)
    {
        c.Disc(128, 128, 70, 70, ring, 1f);
        c.Disc(128, 128, 56, 56, Color.white, 1f);
        for (int i = 0; i < 5; i++)
        {
            float a = Mathf.PI / 2f + i * 2f * Mathf.PI / 5f;
            c.Line(128, 128, 128 + Mathf.Cos(a) * 52f, 128 + Mathf.Sin(a) * 52f, 14f, star, 1f);
        }
        c.Disc(128, 128, 18, 18, star, 1f);
    }

    public static void Stencil(Canvas c, string text, Color color, float cell)
    {
        c.Write(text, 128 - text.Length * cell * 3f, 110, cell, color, 1f, 0.3f, false);
    }

    // Wall clock faces (Lirili's cursed skin).
    public static void Clocks(Canvas c, Color glow)
    {
        for (int i = 0; i < 4; i++)
        {
            float x = 40 + (i % 2) * 130 + c.Rand(-10, 10), y = 60 + (i / 2) * 120 + c.Rand(-10, 10);
            for (int k = 0; k < 24; k++)
            {
                float a = k / 24f * Mathf.PI * 2f;
                c.Disc(x + Mathf.Cos(a) * 28f, y + Mathf.Sin(a) * 28f, 2.5f, 2.5f, glow, 1f);
            }
            c.Line(x, y, x + 14f, y + 10f, 3f, glow, 1f);
            c.Line(x, y, x - 4f, y + 22f, 2f, glow, 1f);
        }
    }

    // A small noise texture for the camera static.
    public static Texture2D StaticNoise(string folder)
    {
        var c = new Canvas(128, 128, Color.gray, 77);
        for (int y = 0; y < 128; y++)
            for (int x = 0; x < 128; x++)
            {
                float v = c.Rand(0f, 1f);
                c.Px[y * 128 + x] = new Color(v, v, v, 0.85f);
            }
        return c.Save($"{folder}/Camera Static.png");
    }
}
