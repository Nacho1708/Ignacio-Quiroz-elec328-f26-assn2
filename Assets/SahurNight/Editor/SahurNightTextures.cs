using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

// Paints the textures for Sahur Night in code: the normal-looking surfaces, and their hidden "cursed"
// versions that only show up inside the flashlight beam (scrawled TUNG TUNG TUNG, handprints, footprints...).
public static class SahurNightTextures
{
    const int Size = 512;
    public const float MoonElevation = 15f;
    // Where the moon sits on the panorama (Skybox/Panoramic, latitude-longitude): u = 0.25 is straight ahead (+Z).
    const float MoonU = 0.25f, MoonV = 1f - (90f - MoonElevation) / 180f;

    public class Canvas
    {
        public readonly int W, H;
        public readonly Color[] Px;
        readonly System.Random random;

        public Canvas(int w, int h, Color fill, int seed = 1)
        {
            W = w; H = h; Px = new Color[w * h];
            for (int i = 0; i < Px.Length; i++) Px[i] = fill;
            random = new System.Random(seed);
        }

        public float Rand(float a, float b) => a + (float)random.NextDouble() * (b - a);

        public void Blend(int x, int y, Color c, float alpha)
        {
            x = ((x % W) + W) % W; y = ((y % H) + H) % H; // wraps, so tiled textures have no seams
            int i = y * W + x;
            Px[i] = Color.Lerp(Px[i], c, Mathf.Clamp01(alpha));
        }

        public void Noise(float amount, float scale, float seedOffset)
        {
            for (int y = 0; y < H; y++)
                for (int x = 0; x < W; x++)
                {
                    // Tileable noise: sample around a torus.
                    float u = x / (float)W * Mathf.PI * 2f, v = y / (float)H * Mathf.PI * 2f;
                    float n = Mathf.PerlinNoise(seedOffset + Mathf.Cos(u) * scale, seedOffset + Mathf.Sin(u) * scale + Mathf.Cos(v) * scale)
                              * Mathf.PerlinNoise(seedOffset + 7f + Mathf.Sin(v) * scale, seedOffset + 3f + Mathf.Cos(v) * scale);
                    Color p = Px[y * W + x];
                    Px[y * W + x] = p * (1f - amount + amount * 2.2f * n);
                }
        }

        public void Darken(float factor, Color tint)
        {
            for (int i = 0; i < Px.Length; i++) Px[i] = Px[i] * factor * tint;
        }

        public void Disc(float cx, float cy, float rx, float ry, Color c, float alpha, float angle = 0f)
        {
            float cos = Mathf.Cos(angle), sin = Mathf.Sin(angle);
            int r = Mathf.CeilToInt(Mathf.Max(rx, ry)) + 1;
            for (int y = -r; y <= r; y++)
                for (int x = -r; x <= r; x++)
                {
                    float lx = (x * cos + y * sin) / rx, ly = (-x * sin + y * cos) / ry;
                    float d = lx * lx + ly * ly;
                    if (d <= 1f) Blend(Mathf.RoundToInt(cx) + x, Mathf.RoundToInt(cy) + y, c, alpha * Mathf.Clamp01((1f - d) * 4f));
                }
        }

        public void Line(float x0, float y0, float x1, float y1, float width, Color c, float alpha)
        {
            float len = Vector2.Distance(new Vector2(x0, y0), new Vector2(x1, y1));
            int steps = Mathf.Max(1, Mathf.CeilToInt(len / Mathf.Max(0.5f, width * 0.4f)));
            for (int i = 0; i <= steps; i++)
            {
                float t = i / (float)steps;
                Disc(Mathf.Lerp(x0, x1, t), Mathf.Lerp(y0, y1, t), width * 0.5f, width * 0.5f, c, alpha);
            }
        }

        // Scrawled block letters (a little shaky), optionally dripping like wet paint.
        public void Write(string text, float x, float y, float cell, Color c, float alpha, float shake, bool drips)
        {
            float cx = x;
            foreach (char ch in text.ToUpperInvariant())
            {
                if (ch == ' ') { cx += cell * 4f; continue; }
                if (!Font.TryGetValue(ch, out string[] rows)) { cx += cell * 6f; continue; }
                for (int row = 0; row < 7; row++)
                    for (int col = 0; col < 5; col++)
                    {
                        if (rows[row][col] != '1') continue;
                        float px = cx + col * cell + Rand(-shake, shake), py = y + (6 - row) * cell + Rand(-shake, shake);
                        Disc(px, py, cell * 0.62f, cell * 0.62f, c, alpha);
                        if (drips && row == 6 && Rand(0f, 1f) < 0.45f)
                            Line(px, py, px + Rand(-1f, 1f), py - Rand(cell, cell * 5f), cell * 0.35f, c, alpha * 0.9f);
                    }
                cx += cell * 6f;
            }
        }

        public void Handprint(float x, float y, float s, float angle, Color c, float alpha)
        {
            Disc(x, y, 18f * s, 22f * s, c, alpha, angle);
            float cos = Mathf.Cos(angle), sin = Mathf.Sin(angle);
            Vector2 Rot(float lx, float ly) => new Vector2(x + lx * cos - ly * sin, y + lx * sin + ly * cos);
            float[] fx = { -13f, -4.5f, 4.5f, 13f }, flen = { 20f, 26f, 27f, 22f };
            for (int i = 0; i < 4; i++)
            {
                Vector2 a = Rot(fx[i] * s, 18f * s), b = Rot(fx[i] * 1.2f * s, (18f + flen[i]) * s);
                Line(a.x, a.y, b.x, b.y, 8f * s, c, alpha);
            }
            Vector2 t0 = Rot(-16f * s, -2f * s), t1 = Rot(-34f * s, 14f * s);
            Line(t0.x, t0.y, t1.x, t1.y, 9f * s, c, alpha);
        }

        public void Footprint(float x, float y, float s, float angle, Color c, float alpha)
        {
            Disc(x, y, 13f * s, 24f * s, c, alpha, angle);
            float cos = Mathf.Cos(angle), sin = Mathf.Sin(angle);
            for (int i = -1; i <= 1; i++)
            {
                float lx = i * 9f * s, ly = 31f * s;
                Disc(x + lx * cos - ly * sin, y + lx * sin + ly * cos, 5f * s, 6f * s, c, alpha);
            }
        }

        public void Scratch(float x, float y, float length, float angle, Color c, float alpha)
        {
            for (int i = 0; i < 3; i++)
            {
                float ox = Mathf.Cos(angle + Mathf.PI / 2f) * i * 9f, oy = Mathf.Sin(angle + Mathf.PI / 2f) * i * 9f;
                Line(x + ox, y + oy, x + ox + Mathf.Cos(angle) * length, y + oy + Mathf.Sin(angle) * length, 3.5f, c, alpha);
            }
        }

        public Texture2D Save(string path, bool tiled = true, bool panorama = false)
        {
            var tex = new Texture2D(W, H, TextureFormat.RGBA32, false);
            tex.SetPixels(Px);
            tex.Apply();
            File.WriteAllBytes(path, tex.EncodeToPNG());
            Object.DestroyImmediate(tex);
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            importer.wrapMode = tiled ? TextureWrapMode.Repeat : TextureWrapMode.Clamp;
            importer.anisoLevel = 4;
            if (panorama)
            {
                importer.wrapModeU = TextureWrapMode.Repeat; // wraps around horizontally
                importer.wrapModeV = TextureWrapMode.Clamp;
                importer.mipmapEnabled = false;              // no seam where the panorama wraps
            }
            importer.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }
    }

    public static readonly Color Blood = new Color(0.78f, 0.04f, 0.03f);
    public static readonly Color Glow = new Color(1f, 0.15f, 0.05f);

    public static Dictionary<string, Texture2D> Make(string folder, Texture2D sahurAlbedo)
    {
        var t = new Dictionary<string, Texture2D>();

        // Wallpaper: faded green stripes with little diamonds.
        var wall = new Canvas(Size, Size, new Color(0.42f, 0.5f, 0.44f), 11);
        for (int y = 0; y < Size; y++)
            for (int x = 0; x < Size; x++)
                if ((x / 32) % 2 == 0) wall.Blend(x, y, new Color(0.36f, 0.43f, 0.38f), 1f);
        for (int y = 32; y < Size; y += 64)
            for (int x = 16; x < Size; x += 64) wall.Disc(x, y, 6f, 9f, new Color(0.55f, 0.6f, 0.5f), 0.8f);
        wall.Noise(0.25f, 3f, 1f);
        t["wall"] = wall.Save($"{folder}/Wall.png");
        var wallCursed = new Canvas(Size, Size, Color.black, 12);
        System.Array.Copy(wall.Px, wallCursed.Px, wall.Px.Length);
        wallCursed.Darken(0.45f, new Color(1f, 0.85f, 0.85f));
        wallCursed.Write("TUNG", 40, 380, 9f, Blood, 0.95f, 2.5f, true);
        wallCursed.Write("TUNG", 210, 300, 9f, Blood, 0.95f, 2.5f, true);
        wallCursed.Write("TUNG", 70, 210, 9f, Blood, 0.95f, 2.5f, true);
        wallCursed.Write("SAHUR", 220, 110, 8f, Glow, 0.9f, 2f, true);
        wallCursed.Handprint(430, 420, 1f, -0.3f, Blood, 0.85f);
        wallCursed.Handprint(470, 250, 0.9f, 0.4f, Blood, 0.85f);
        wallCursed.Handprint(60, 60, 1.1f, 0.1f, Blood, 0.85f);
        t["wallCursed"] = wallCursed.Save($"{folder}/Wall Cursed.png");

        // Floor: checkered tiles.
        var floor = new Canvas(Size, Size, new Color(0.78f, 0.75f, 0.66f), 21);
        for (int y = 0; y < Size; y++)
            for (int x = 0; x < Size; x++)
            {
                if (((x / 128) + (y / 128)) % 2 == 1) floor.Blend(x, y, new Color(0.17f, 0.27f, 0.26f), 1f);
                if (x % 128 < 3 || y % 128 < 3) floor.Blend(x, y, new Color(0.25f, 0.24f, 0.22f), 0.8f);
            }
        floor.Noise(0.3f, 4f, 5f);
        t["floor"] = floor.Save($"{folder}/Floor.png");
        var floorCursed = new Canvas(Size, Size, Color.black, 22);
        System.Array.Copy(floor.Px, floorCursed.Px, floor.Px.Length);
        floorCursed.Darken(0.4f, Color.white);
        for (int i = 0; i < 6; i++) // two trails of big blobby Sahur feet
        {
            floorCursed.Footprint(70 + (i % 2) * 70 + i * 40, 20 + i * 85, 1.3f, -0.35f, Blood, 0.9f);
            floorCursed.Footprint(330 + (i % 2) * 60 - i * 25, 60 + i * 85, 1.1f, 0.3f, Blood, 0.85f);
        }
        floorCursed.Write("67", 180, 300, 12f, Glow, 0.9f, 2f, false);
        floorCursed.Write("67", 400, 30, 9f, Glow, 0.9f, 2f, false);
        t["floorCursed"] = floorCursed.Save($"{folder}/Floor Cursed.png");

        // Wood (desk, door, fence): planks with grain.
        var wood = new Canvas(Size, Size, new Color(0.42f, 0.27f, 0.15f), 31);
        for (int y = 0; y < Size; y++)
            for (int x = 0; x < Size; x++)
            {
                float grain = Mathf.Sin((x + 12f * Mathf.Sin(y * 0.02f + (x / 128) * 2f)) * 0.35f);
                wood.Blend(x, y, new Color(0.3f, 0.18f, 0.09f), 0.35f * (grain * 0.5f + 0.5f));
                if (x % 128 < 2) wood.Blend(x, y, new Color(0.12f, 0.07f, 0.03f), 1f);
            }
        wood.Noise(0.2f, 5f, 9f);
        t["wood"] = wood.Save($"{folder}/Wood.png");
        var woodCursed = new Canvas(Size, Size, Color.black, 32);
        System.Array.Copy(wood.Px, woodCursed.Px, wood.Px.Length);
        woodCursed.Darken(0.5f, Color.white);
        woodCursed.Scratch(60, 420, 170, -0.5f, new Color(0.9f, 0.75f, 0.55f), 0.9f);
        woodCursed.Scratch(300, 200, 150, 0.3f, new Color(0.9f, 0.75f, 0.55f), 0.9f);
        woodCursed.Write("SAHUR", 60, 60, 11f, Glow, 0.95f, 1.5f, false);
        t["woodCursed"] = woodCursed.Save($"{folder}/Wood Cursed.png");

        // Ceiling: acoustic tiles.
        var ceiling = new Canvas(Size, Size, new Color(0.7f, 0.7f, 0.68f), 41);
        for (int i = 0; i < 2500; i++) ceiling.Disc(ceiling.Rand(0, Size), ceiling.Rand(0, Size), 1.6f, 1.6f, new Color(0.45f, 0.45f, 0.43f), 0.8f);
        for (int y = 0; y < Size; y++) for (int x = 0; x < Size; x++) if (x % 256 < 4 || y % 256 < 4) ceiling.Blend(x, y, new Color(0.35f, 0.35f, 0.35f), 1f);
        ceiling.Noise(0.2f, 3f, 13f);
        t["ceiling"] = ceiling.Save($"{folder}/Ceiling.png");
        var ceilingCursed = new Canvas(Size, Size, Color.black, 42);
        System.Array.Copy(ceiling.Px, ceilingCursed.Px, ceiling.Px.Length);
        ceilingCursed.Darken(0.35f, Color.white);
        ceilingCursed.Write("6 7 6 7", 40, 300, 10f, Blood, 0.9f, 2f, true);
        ceilingCursed.Handprint(380, 120, 1.2f, 2.5f, Blood, 0.8f);
        t["ceilingCursed"] = ceilingCursed.Save($"{folder}/Ceiling Cursed.png");

        // Metal (lockers, frames).
        var metal = new Canvas(Size, Size, new Color(0.45f, 0.48f, 0.5f), 51);
        for (int y = 0; y < Size; y++) for (int x = 0; x < Size; x++) if (x % 128 < 3) metal.Blend(x, y, new Color(0.2f, 0.22f, 0.24f), 1f);
        for (int y = 30; y < Size; y += 120) for (int x = 20; x < Size; x += 128) { metal.Disc(x, y, 4f, 4f, new Color(0.7f, 0.7f, 0.7f), 0.9f); metal.Disc(x + 88, y, 4f, 4f, new Color(0.7f, 0.7f, 0.7f), 0.9f); }
        metal.Noise(0.25f, 6f, 17f);
        t["metal"] = metal.Save($"{folder}/Metal.png");
        var metalCursed = new Canvas(Size, Size, Color.black, 52);
        System.Array.Copy(metal.Px, metalCursed.Px, metal.Px.Length);
        metalCursed.Darken(0.4f, Color.white);
        metalCursed.Write("DONT", 50, 360, 10f, Blood, 0.95f, 2f, true);
        metalCursed.Write("LET HIM", 50, 250, 10f, Blood, 0.95f, 2f, true);
        metalCursed.Write("IN", 50, 140, 10f, Blood, 0.95f, 2f, true);
        t["metalCursed"] = metalCursed.Save($"{folder}/Metal Cursed.png");

        // Poster: "Employee of the month" ... or is it?
        var poster = new Canvas(Size, Size, new Color(0.92f, 0.88f, 0.75f), 61);
        poster.Write("EMPLOYEE", 70, 440, 9f, new Color(0.2f, 0.3f, 0.6f), 1f, 0.3f, false);
        poster.Write("OF THE MONTH", 40, 380, 6.5f, new Color(0.2f, 0.3f, 0.6f), 1f, 0.3f, false);
        poster.Disc(256, 200, 110, 110, new Color(1f, 0.85f, 0.3f), 1f);
        poster.Disc(215, 235, 12, 16, Color.black, 1f); poster.Disc(297, 235, 12, 16, Color.black, 1f);
        for (int i = 0; i <= 20; i++) { float a = Mathf.PI * (1.15f + 0.7f * i / 20f); poster.Disc(256 + Mathf.Cos(a) * 60, 200 + Mathf.Sin(a) * 60, 6, 6, Color.black, 1f); }
        poster.Noise(0.12f, 4f, 21f);
        t["poster"] = poster.Save($"{folder}/Poster.png", false);
        var posterCursed = new Canvas(Size, Size, new Color(0.25f, 0.2f, 0.15f), 62);
        posterCursed.Write("WANTED", 80, 440, 10f, Blood, 1f, 1f, true);
        posterCursed.Disc(256, 210, 70, 140, new Color(0.45f, 0.28f, 0.14f), 1f);   // a log with a face
        posterCursed.Disc(228, 270, 14, 18, Glow, 1f); posterCursed.Disc(284, 270, 14, 18, Glow, 1f);
        posterCursed.Line(220, 190, 292, 190, 8, Color.black, 1f);
        posterCursed.Write("TUNG TUNG TUNG", 40, 40, 5.5f, Glow, 1f, 0.8f, false);
        t["posterCursed"] = posterCursed.Save($"{folder}/Poster Cursed.png", false);

        // Muddy yard outside the window.
        var yard = new Canvas(Size, Size, new Color(0.16f, 0.2f, 0.11f), 71);
        for (int i = 0; i < 4000; i++) yard.Disc(yard.Rand(0, Size), yard.Rand(0, Size), 1.5f, 5f, new Color(0.22f, 0.3f, 0.14f), 0.7f, yard.Rand(-0.4f, 0.4f));
        yard.Noise(0.35f, 3f, 25f);
        t["yard"] = yard.Save($"{folder}/Yard.png");
        var yardCursed = new Canvas(Size, Size, Color.black, 72);
        System.Array.Copy(yard.Px, yardCursed.Px, yard.Px.Length);
        yardCursed.Darken(0.5f, Color.white);
        for (int i = 0; i < 5; i++) yardCursed.Footprint(150 + (i % 2) * 80, 40 + i * 100, 1.5f, 0.1f, new Color(0.5f, 0.05f, 0.03f), 0.85f);
        t["yardCursed"] = yardCursed.Save($"{folder}/Yard Cursed.png");

        // Sahur himself: in the beam his wood cracks open and glows.
        var sahurCursed = new Canvas(Size, Size, Color.black, 81);
        if (sahurAlbedo != null)
        {
            Texture2D scaled = Resize(sahurAlbedo, Size);
            System.Array.Copy(scaled.GetPixels(), sahurCursed.Px, sahurCursed.Px.Length);
            Object.DestroyImmediate(scaled);
        }
        sahurCursed.Darken(0.35f, new Color(1f, 0.8f, 0.8f));
        for (int i = 0; i < 40; i++) // glowing cracks
        {
            float x = sahurCursed.Rand(0, Size), y = sahurCursed.Rand(0, Size), a = sahurCursed.Rand(0, Mathf.PI * 2f);
            for (int k = 0; k < 8; k++)
            {
                float nx = x + Mathf.Cos(a) * 14f, ny = y + Mathf.Sin(a) * 14f;
                sahurCursed.Line(x, y, nx, ny, 2.5f, Glow, 0.95f);
                x = nx; y = ny; a += sahurCursed.Rand(-0.7f, 0.7f);
            }
        }
        t["sahurCursed"] = sahurCursed.Save($"{folder}/Sahur Cursed.png", false);
        var eyes = new Canvas(64, 64, new Color(1f, 0.08f, 0.02f), 82);
        eyes.Noise(0.4f, 2f, 3f);
        t["eyesCursed"] = eyes.Save($"{folder}/Sahur Eyes Cursed.png", false);

        // EXIT sign.
        var exit = new Canvas(256, 128, new Color(0.05f, 0.02f, 0.02f), 91);
        exit.Write("EXIT", 30, 30, 11f, new Color(1f, 0.2f, 0.15f), 1f, 0f, false);
        t["exit"] = exit.Save($"{folder}/Exit Sign.png", false);

        // Night sky panorama: stars and a moon.
        var sky = new Canvas(2048, 1024, Color.black, 101);
        for (int y = 0; y < 1024; y++)
        {
            float h = y / 1023f; // 0 = straight down, 1 = straight up
            Color c = h < 0.5f ? new Color(0.02f, 0.02f, 0.03f) : Color.Lerp(new Color(0.09f, 0.06f, 0.14f), new Color(0.005f, 0.008f, 0.025f), Mathf.Pow((h - 0.5f) * 2f, 0.6f));
            for (int x = 0; x < 2048; x++) sky.Px[y * 2048 + x] = c;
        }
        for (int i = 0; i < 3000; i++)
        {
            float y = sky.Rand(540, 1024), b = Mathf.Pow(sky.Rand(0f, 1f), 3f);
            sky.Disc(sky.Rand(0, 2048), y, 0.8f + b, 0.8f + b, new Color(0.85f, 0.88f, 1f), 0.4f + 0.6f * b);
        }
        // The moon: straight ahead through the office window, 15 degrees up (matches the moonlight's direction).
        float moonX = MoonU * 2048f, moonY = MoonV * 1024f;
        sky.Disc(moonX, moonY, 60, 60, new Color(0.3f, 0.32f, 0.45f), 0.35f);   // glow
        sky.Disc(moonX, moonY, 26, 26, new Color(0.95f, 0.95f, 0.88f), 1f);
        sky.Disc(moonX - 8, moonY + 8, 6, 6, new Color(0.75f, 0.75f, 0.7f), 0.8f);
        sky.Disc(moonX + 10, moonY - 8, 4, 4, new Color(0.75f, 0.75f, 0.7f), 0.8f);
        t["sky"] = sky.Save($"{folder}/Night Sky.png", false, true);
        return t;
    }

    static Texture2D Resize(Texture2D source, int size)
    {
        var rt = RenderTexture.GetTemporary(size, size, 0, RenderTextureFormat.ARGB32);
        Graphics.Blit(source, rt);
        var previous = RenderTexture.active;
        RenderTexture.active = rt;
        var result = new Texture2D(size, size, TextureFormat.RGBA32, false);
        result.ReadPixels(new Rect(0, 0, size, size), 0, 0);
        result.Apply();
        RenderTexture.active = previous;
        RenderTexture.ReleaseTemporary(rt);
        return result;
    }

    // A 5x7 pixel font: enough letters for the scrawled messages.
    static readonly Dictionary<char, string[]> Font = new Dictionary<char, string[]>
    {
        ['A'] = new[] { "01110", "10001", "10001", "11111", "10001", "10001", "10001" },
        ['B'] = new[] { "11110", "10001", "10001", "11110", "10001", "10001", "11110" },
        ['C'] = new[] { "01110", "10001", "10000", "10000", "10000", "10001", "01110" },
        ['D'] = new[] { "11110", "10001", "10001", "10001", "10001", "10001", "11110" },
        ['E'] = new[] { "11111", "10000", "10000", "11110", "10000", "10000", "11111" },
        ['F'] = new[] { "11111", "10000", "10000", "11110", "10000", "10000", "10000" },
        ['G'] = new[] { "01110", "10001", "10000", "10111", "10001", "10001", "01111" },
        ['H'] = new[] { "10001", "10001", "10001", "11111", "10001", "10001", "10001" },
        ['I'] = new[] { "01110", "00100", "00100", "00100", "00100", "00100", "01110" },
        ['K'] = new[] { "10001", "10010", "10100", "11000", "10100", "10010", "10001" },
        ['L'] = new[] { "10000", "10000", "10000", "10000", "10000", "10000", "11111" },
        ['M'] = new[] { "10001", "11011", "10101", "10101", "10001", "10001", "10001" },
        ['N'] = new[] { "10001", "10001", "11001", "10101", "10011", "10001", "10001" },
        ['O'] = new[] { "01110", "10001", "10001", "10001", "10001", "10001", "01110" },
        ['P'] = new[] { "11110", "10001", "10001", "11110", "10000", "10000", "10000" },
        ['R'] = new[] { "11110", "10001", "10001", "11110", "10100", "10010", "10001" },
        ['S'] = new[] { "01111", "10000", "10000", "01110", "00001", "00001", "11110" },
        ['T'] = new[] { "11111", "00100", "00100", "00100", "00100", "00100", "00100" },
        ['U'] = new[] { "10001", "10001", "10001", "10001", "10001", "10001", "01110" },
        ['W'] = new[] { "10001", "10001", "10001", "10101", "10101", "10101", "01010" },
        ['X'] = new[] { "10001", "10001", "01010", "00100", "01010", "10001", "10001" },
        ['Y'] = new[] { "10001", "10001", "01010", "00100", "00100", "00100", "00100" },
        ['6'] = new[] { "00110", "01000", "10000", "11110", "10001", "10001", "01110" },
        ['7'] = new[] { "11111", "00001", "00010", "00100", "01000", "01000", "01000" },
        ['!'] = new[] { "00100", "00100", "00100", "00100", "00100", "00000", "00100" },
    };
}
