using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using B = SahurNightBuilder;
using T = BrainrotTextures;

// Builds the Five Nights characters out of simple shapes (spheres, cylinders, boxes, plus cones, fins, a torus
// and a frilly tutu made in code), each with its own Enchanted Flashlight materials (normal + cursed textures).
// Every character stands with its feet at its pivot and faces +Z. You can swap in a real model later with the
// "Model Override" slot on its script.
public static class BrainrotModels
{
    static readonly Dictionary<string, Material> m = new Dictionary<string, Material>();
    static Shader ghost;
    static string folder;

    public static Material Get(string name) => m[name];

    // ---------- Materials ----------

    public static void MakeMaterials(string textureFolder, Shader ghostShader)
    {
        m.Clear();
        ghost = ghostShader;
        folder = textureFolder;
        Color red = new Color(1f, 0.12f, 0.05f), cyan = new Color(0.2f, 1f, 1f), pink = new Color(1f, 0.3f, 0.65f),
              green = new Color(0.45f, 1f, 0.3f), orange = new Color(1f, 0.5f, 0.1f), gold = new Color(1f, 0.85f, 0.2f);

        // Shared bits.
        Ench("Eye White", Pair("Eye White", new Color(0.95f, 0.95f, 0.92f), T.Pattern.Plain, red, null, 1), new Color(3f, 0.3f, 0.2f), 2f, 0.6f);
        Ench("Pupil", Pair("Pupil", new Color(0.03f, 0.03f, 0.04f), T.Pattern.Plain, new Color(1f, 0.9f, 0.1f), null, 2), new Color(3f, 2.5f, 0.3f), 3f, 0.8f);
        Ench("Mouth", Pair("Mouth", new Color(0.35f, 0.05f, 0.07f), T.Pattern.Plain, orange, null, 3), new Color(2.6f, 0.8f, 0.1f), 2f, 0.5f);
        Ench("Teeth", Pair("Teeth", new Color(0.97f, 0.95f, 0.85f), T.Pattern.Plain, red, null, 4), new Color(3f, 0.3f, 0.2f), 1.5f, 0.6f);

        // Tralalero Tralala: a shark in blue Nikes.
        Ench("Shark Skin", Pair("Shark Skin", new Color(0.36f, 0.5f, 0.64f), T.Pattern.Speckles, cyan, "TRALALA", 10), new Color(0.3f, 2f, 2.4f), 1.2f, 0.45f);
        Ench("Shark Belly", Pair("Shark Belly", new Color(0.92f, 0.92f, 0.88f), T.Pattern.Plain, red, "67", 11), new Color(0.3f, 2f, 2.4f), 1.2f, 0.45f);
        Ench("Sneaker", Pair("Sneaker", new Color(0.95f, 0.95f, 0.97f), T.Pattern.Plain, orange, "TRA LA", 12,
            c => T.Swoosh(c, new Color(0.1f, 0.35f, 0.95f)), c => T.Swoosh(c, new Color(1f, 0.4f, 0.05f))), new Color(0.3f, 2f, 2.4f), 1.2f, 0.3f);
        Ench("Leg Skin", Pair("Leg Skin", new Color(0.93f, 0.74f, 0.6f), T.Pattern.Plain, red, null, 13), new Color(0.3f, 2f, 2.4f), 1f, 0.3f);

        // Ballerina Cappuccina: a ballerina with a cappuccino cup for a head.
        Ench("Tutu", Pair("Tutu", new Color(1f, 0.62f, 0.78f), T.Pattern.Ruffles, pink, "CAPPUCCINA", 20), new Color(2.4f, 0.6f, 1.4f), 1.2f, 0.3f);
        Ench("Ceramic Cup", Pair("Ceramic Cup", new Color(0.96f, 0.94f, 0.9f), T.Pattern.Plain, pink, "CAPPUCCINA", 21), new Color(2.4f, 0.6f, 1.4f), 1.2f, 0.8f);
        Ench("Latte", Pair("Latte", new Color(0.5f, 0.32f, 0.18f), T.Pattern.Plain, orange, null, 22, T.LatteHeart, T.LatteSkull), new Color(2.4f, 0.6f, 1.4f), 1.5f, 0.6f);
        Ench("Ballerina Skin", Pair("Ballerina Skin", new Color(0.98f, 0.82f, 0.72f), T.Pattern.Plain, pink, null, 23), new Color(2.4f, 0.6f, 1.4f), 1f, 0.35f);
        Ench("Satin", Pair("Satin", new Color(1f, 0.75f, 0.8f), T.Pattern.Stripes, pink, null, 24), new Color(2.4f, 0.6f, 1.4f), 1f, 0.6f);
        m["Glowing Eyes"] = B.Lit("Glowing Eyes", new Color(1f, 0.1f, 0.05f), 0f, 0.5f, true, new Color(6f, 0.4f, 0.2f));

        // Brr Brr Patapim: a tree-monkey. Invisible except in the beam (see-through Ghost shader), solid when he gets you.
        Color patapimEdge = new Color(0.6f, 2.4f, 0.4f);
        var bark = Pair("Bark", new Color(0.38f, 0.25f, 0.14f), T.Pattern.Bark, green, "BRR BRR", 30);
        var leaves = Pair("Leaves", new Color(0.2f, 0.5f, 0.18f), T.Pattern.Leaves, green, null, 31);
        var monkey = Pair("Monkey Skin", new Color(0.86f, 0.62f, 0.52f), T.Pattern.Plain, green, "PATAPIM", 32);
        Ench("Bark", bark, patapimEdge, 0.8f, 0.2f);
        Ench("Leaves", leaves, patapimEdge, 0.8f, 0.2f);
        Ench("Monkey Skin", monkey, patapimEdge, 0.8f, 0.35f);
        Ghost("Ghost Bark", bark, patapimEdge);
        Ghost("Ghost Leaves", leaves, patapimEdge);
        Ghost("Ghost Monkey Skin", monkey, patapimEdge);
        Ghost("Ghost Eye White", Pair("Eye White", new Color(0.95f, 0.95f, 0.92f), T.Pattern.Plain, red, null, 1), patapimEdge);
        Ghost("Ghost Pupil", Pair("Pupil", new Color(0.03f, 0.03f, 0.04f), T.Pattern.Plain, new Color(1f, 0.9f, 0.1f), null, 2), patapimEdge);
        Ghost("Ghost Mouth", Pair("Mouth", new Color(0.35f, 0.05f, 0.07f), T.Pattern.Plain, orange, null, 3), patapimEdge);

        // Bombardiro Crocodilo: a crocodile bomber plane.
        Color bomberEdge = new Color(2.6f, 1f, 0.15f);
        Ench("Croc Scales", Pair("Croc Scales", new Color(0.27f, 0.47f, 0.2f), T.Pattern.Scales, orange, "BOMBARDIRO", 40), bomberEdge, 1.2f, 0.4f);
        Ench("Bomber Wing", Pair("Bomber Wing", new Color(0.36f, 0.4f, 0.26f), T.Pattern.Panels, orange, "67", 41,
            c => T.Roundel(c, new Color(0.15f, 0.2f, 0.5f), new Color(0.15f, 0.2f, 0.5f)), c => T.Roundel(c, new Color(1f, 0.2f, 0.05f), Color.black)), bomberEdge, 1.2f, 0.35f);
        Ench("Bomb", Pair("Bomb", new Color(0.25f, 0.26f, 0.28f), T.Pattern.Plain, red, "BOOM", 42,
            c => T.Stencil(c, "67", new Color(1f, 0.85f, 0.1f), 9f)), bomberEdge, 1.5f, 0.6f);
        Ench("Croc Eye", Pair("Croc Eye", new Color(1f, 0.82f, 0.15f), T.Pattern.Plain, red, null, 43), bomberEdge, 2f, 0.7f);
        m["Propeller"] = B.Lit("Propeller Metal", new Color(0.15f, 0.15f, 0.16f), 0.8f, 0.5f);
        m["Nav Red"] = B.Lit("Navigation Light Red", new Color(1f, 0.1f, 0.1f), 0f, 0.5f, true, new Color(8f, 0.3f, 0.2f));
        m["Nav Green"] = B.Lit("Navigation Light Green", new Color(0.1f, 1f, 0.2f), 0f, 0.5f, true, new Color(0.3f, 8f, 0.6f));
        m["Nav White"] = B.Lit("Navigation Light White", Color.white, 0f, 0.5f, true, new Color(9f, 9f, 8f));

        // Lirili Larila: a desert elephant-cactus in sandals, with a pocket watch.
        Color liriliEdge = new Color(2.4f, 2f, 0.3f);
        Ench("Elephant Skin", Pair("Elephant Skin", new Color(0.56f, 0.56f, 0.6f), T.Pattern.Wrinkles, gold, "LIRILI", 50, null, c => T.Clocks(c, gold)), liriliEdge, 1.2f, 0.25f);
        Ench("Cactus", Pair("Cactus", new Color(0.3f, 0.55f, 0.25f), T.Pattern.Ribs, gold, "LARILA", 51), liriliEdge, 1.2f, 0.25f);
        Ench("Sandal", Pair("Sandal", new Color(0.48f, 0.3f, 0.16f), T.Pattern.Plain, gold, null, 52), liriliEdge, 1f, 0.3f);
        Ench("Gold", Pair("Gold", new Color(0.95f, 0.76f, 0.28f), T.Pattern.Plain, red, "TIME", 53), liriliEdge, 1.2f, 0.85f);

        // Scenery: the desert and Tralalero's beach hut.
        Ench("Sand", Pair("Sand", new Color(0.8f, 0.67f, 0.45f), T.Pattern.Dunes, gold, "TIME IS MINE", 60), liriliEdge, 0.8f, 0.1f);
        Ench("Beach Hut", Pair("Beach Hut", new Color(0.2f, 0.45f, 0.85f), T.Pattern.Awning, cyan, "TRALALA", 61), new Color(0.3f, 2f, 2.4f), 1f, 0.2f);
        m["Confetti"] = B.Lit("Confetti", Color.white, 0f, 0.6f);
    }

    static T.Pair Pair(string name, Color color, T.Pattern pattern, Color glow, string word, int seed,
                       System.Action<SahurNightTextures.Canvas> decal = null, System.Action<SahurNightTextures.Canvas> hiddenDecal = null)
    {
        return T.Make(folder, name, color, pattern, glow, word, seed, decal, hiddenDecal);
    }

    static void Ench(string name, T.Pair pair, Color boundary, float glow, float smoothness)
    {
        m[name] = B.Enchanted("Brainrot " + name, pair.normal, pair.hidden, boundary, glow, 0f, smoothness);
    }

    // See-through: only visible inside the beam, where it shows its normal look (plus a glowing outline).
    static void Ghost(string name, T.Pair pair, Color boundary)
    {
        m[name] = B.Enchanted("Brainrot " + name, pair.normal, pair.normal, boundary, 0.9f, 0.006f, 0.3f, ghost);
    }

    // ---------- Characters ----------

    // Tung Tung Tung Sahur: your downloaded model, wrapped so his feet are at the pivot and he faces +Z.
    public static TungEnemy BuildTung(GameObject tungModel)
    {
        var root = new GameObject("Tung Tung Tung Sahur");
        var model = (GameObject)PrefabUtility.InstantiatePrefab(tungModel);
        model.name = "Model (tung.fbx)";
        model.transform.SetParent(root.transform, false);
        model.transform.localRotation = Quaternion.Euler(0f, 180f, 0f) * model.transform.localRotation; // the fbx faces -Z
        B.ApplyTungMaterials(model);
        Bounds b = WorldBounds(model);
        model.transform.localPosition += Vector3.up * -b.min.y; // feet on the floor
        b = WorldBounds(model);

        var dance = model.AddComponent<TungDance>();
        dance.move = TungDance.Move.Dance;
        dance.enabled = false;

        var tung = root.AddComponent<TungEnemy>();
        tung.displayName = "Tung Tung Tung Sahur";
        tung.builtInModel = model.transform;
        tung.dance = dance;
        tung.face = Node("Face", root.transform, new Vector3(0f, b.max.y * 0.82f, 0.18f));
        tung.tickSeconds = 5f;
        return tung;
    }

    public static TralaleroEnemy BuildTralalero()
    {
        var root = new GameObject("Tralalero Tralala");
        Transform model = Node("Model", root.transform, Vector3.zero);
        Material skin = m["Shark Skin"];

        Transform body = Node("Body", model, new Vector3(0f, 1.15f, 0f));
        Ball("Shark Body", body, Vector3.zero, new Vector3(0.62f, 0.62f, 1.7f), skin);
        Ball("Belly", body, new Vector3(0f, -0.12f, 0.08f), new Vector3(0.5f, 0.42f, 1.4f), m["Shark Belly"]);
        Ball("Mouth", body, new Vector3(0f, -0.15f, 0.6f), new Vector3(0.36f, 0.1f, 0.3f), m["Mouth"]);
        for (int i = -3; i <= 3; i++)
            Shape("Tooth", body, Cone(0.022f, 0f, 0.06f, 8), new Vector3(i * 0.045f, -0.11f, 0.7f - Mathf.Abs(i) * 0.02f), new Vector3(180f, 0f, 0f), Vector3.one, m["Teeth"]);
        foreach (float side in new[] { -1f, 1f })
        {
            Ball(side < 0 ? "Eye L" : "Eye R", body, new Vector3(0.21f * side, 0.1f, 0.5f), Vector3.one * 0.12f, m["Eye White"]);
            Ball(side < 0 ? "Pupil L" : "Pupil R", body, new Vector3(0.24f * side, 0.11f, 0.55f), Vector3.one * 0.06f, m["Pupil"]);
            Shape("Side Fin", body, Fin(new Vector3(0f, 0f, 0.2f), new Vector3(0f, 0f, -0.05f), new Vector3(0f, -0.25f, -0.25f), 0.04f),
                  new Vector3(0.27f * side, -0.08f, 0.1f), new Vector3(0f, 0f, 40f * side), Vector3.one, skin);
        }
        Shape("Dorsal Fin", body, Fin(new Vector3(0f, 0f, 0.28f), new Vector3(0f, 0f, -0.2f), new Vector3(0f, 0.45f, -0.32f), 0.05f),
              new Vector3(0f, 0.27f, 0f), Vector3.zero, Vector3.one, skin);
        Transform tail = Node("Tail", body, new Vector3(0f, 0.02f, -0.78f));
        Ball("Tail Stalk", tail, new Vector3(0f, 0f, -0.08f), new Vector3(0.24f, 0.28f, 0.42f), skin);
        Shape("Tail Fin (top)", tail, Fin(new Vector3(0f, 0f, 0.02f), new Vector3(0f, 0f, -0.14f), new Vector3(0f, 0.45f, -0.4f), 0.05f), new Vector3(0f, 0f, -0.15f), Vector3.zero, Vector3.one, skin);
        Shape("Tail Fin (bottom)", tail, Fin(new Vector3(0f, 0f, 0.02f), new Vector3(0f, 0f, -0.14f), new Vector3(0f, -0.3f, -0.32f), 0.05f), new Vector3(0f, 0f, -0.15f), Vector3.zero, Vector3.one, skin);

        // Three legs in sneakers (two in front, one behind).
        var legs = new List<Transform>();
        Vector3[] hips = { new Vector3(-0.2f, 0.85f, 0.32f), new Vector3(0.2f, 0.85f, 0.32f), new Vector3(0f, 0.85f, -0.45f) };
        for (int i = 0; i < hips.Length; i++)
        {
            Transform leg = Node($"Leg {i + 1}", model, hips[i]);
            Tube("Thigh", leg, new Vector3(0f, -0.38f, 0f), 0.13f, 0.76f, m["Leg Skin"]);
            Block("Sneaker", leg, new Vector3(0f, -0.79f, 0.07f), new Vector3(0.15f, 0.1f, 0.3f), m["Sneaker"]);
            Block("Sole", leg, new Vector3(0f, -0.835f, 0.07f), new Vector3(0.16f, 0.03f, 0.31f), m["Teeth"]);
            legs.Add(leg);
        }

        var shark = root.AddComponent<TralaleroEnemy>();
        shark.displayName = "Tralalero Tralala";
        shark.builtInModel = model;
        shark.body = body;
        shark.tail = tail;
        shark.legs = legs.ToArray();
        shark.face = Node("Face", root.transform, new Vector3(0f, 1.15f, 0.86f));
        shark.tickSeconds = 5f;
        return shark;
    }

    public static BallerinaEnemy BuildBallerina()
    {
        var root = new GameObject("Ballerina Cappuccina");
        Transform model = Node("Model", root.transform, Vector3.zero);
        foreach (float side in new[] { -1f, 1f })
        {
            Tube(side < 0 ? "Leg L" : "Leg R", model, new Vector3(0.07f * side, 0.46f, 0f), 0.075f, 0.82f, m["Satin"]);
            Ball(side < 0 ? "Pointe Shoe L" : "Pointe Shoe R", model, new Vector3(0.07f * side, 0.04f, 0.03f), new Vector3(0.08f, 0.07f, 0.13f), m["Satin"]);
        }
        Shape("Tutu", model, Ruffle(0.1f, 0.46f, 0.07f, 14, 0.025f), new Vector3(0f, 0.9f, 0f), Vector3.zero, Vector3.one, m["Tutu"]);
        Shape("Tutu (under)", model, Ruffle(0.1f, 0.4f, 0.1f, 11, 0.03f), new Vector3(0f, 0.86f, 0f), new Vector3(0f, 15f, 0f), Vector3.one, m["Tutu"]);
        B.Prim(PrimitiveType.Capsule, "Body", model, new Vector3(0f, 1.12f, 0f), Vector3.zero, new Vector3(0.24f, 0.25f, 0.17f), m["Tutu"]);
        Tube("Neck", model, new Vector3(0f, 1.39f, 0f), 0.07f, 0.08f, m["Ballerina Skin"]);

        // The head: a cappuccino cup on a saucer.
        Tube("Saucer", model, new Vector3(0f, 1.43f, 0f), 0.42f, 0.018f, m["Ceramic Cup"]);
        Shape("Cup", model, Cone(0.13f, 0.18f, 0.25f, 28), new Vector3(0f, 1.44f, 0f), Vector3.zero, Vector3.one, m["Ceramic Cup"]);
        Tube("Latte", model, new Vector3(0f, 1.682f, 0f), 0.335f, 0.008f, m["Latte"]);
        Shape("Handle", model, Torus(0.065f, 0.017f, 230f), new Vector3(0.2f, 1.56f, 0f), new Vector3(0f, 0f, -115f), Vector3.one, m["Ceramic Cup"]);
        var glowing = new List<Renderer>();
        foreach (float side in new[] { -1f, 1f })
        {
            Ball(side < 0 ? "Eye L" : "Eye R", model, new Vector3(0.055f * side, 1.58f, 0.155f), new Vector3(0.04f, 0.055f, 0.02f), m["Pupil"]);
            GameObject glow = Ball(side < 0 ? "Glowing Eye L" : "Glowing Eye R", model, new Vector3(0.055f * side, 1.58f, 0.168f), new Vector3(0.05f, 0.06f, 0.02f), m["Glowing Eyes"]);
            glow.GetComponent<Renderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            glowing.Add(glow.GetComponent<Renderer>());
        }
        for (int i = -2; i <= 2; i++) // a little smile
            Ball("Smile", model, new Vector3(i * 0.022f, 1.51f + 0.006f * i * i, 0.148f + 0.004f * Mathf.Abs(i)), Vector3.one * 0.018f, m["Mouth"]);

        // Arms up over her head, fingertips touching (ballet "fifth position").
        var arms = new List<Transform>();
        foreach (float side in new[] { -1f, 1f })
        {
            Transform sway = Node(side < 0 ? "Arm L (sway)" : "Arm R (sway)", model, new Vector3(0.14f * side, 1.3f, 0f));
            Transform upper = Node("Upper Arm", sway, Vector3.zero, new Vector3(0f, 0f, -22f * side));
            Tube("Upper Arm", upper, new Vector3(0f, 0.15f, 0f), 0.05f, 0.3f, m["Ballerina Skin"]);
            Transform elbow = Node("Elbow", upper, new Vector3(0f, 0.3f, 0f), new Vector3(0f, 0f, 68f * side));
            Tube("Forearm", elbow, new Vector3(0f, 0.13f, 0f), 0.045f, 0.26f, m["Ballerina Skin"]);
            Ball("Hand", elbow, new Vector3(0f, 0.28f, 0f), Vector3.one * 0.06f, m["Ballerina Skin"]);
            arms.Add(sway);
        }

        var ballerina = root.AddComponent<BallerinaEnemy>();
        ballerina.displayName = "Ballerina Cappuccina";
        ballerina.builtInModel = model;
        ballerina.arms = arms.ToArray();
        ballerina.eyes = glowing.ToArray();
        ballerina.face = Node("Face", root.transform, new Vector3(0f, 1.56f, 0.2f));
        ballerina.tickSeconds = 4f;
        return ballerina;
    }

    public static PatapimEnemy BuildPatapim()
    {
        var root = new GameObject("Brr Brr Patapim");
        Transform model = Node("Model", root.transform, Vector3.zero);
        var feet = new List<Transform>();
        var arms = new List<Transform>();
        foreach (float side in new[] { -1f, 1f })
        {
            Transform foot = Node(side < 0 ? "Foot L" : "Foot R", model, new Vector3(0.2f * side, 0f, 0f));
            Ball("Foot", foot, new Vector3(0f, 0.09f, 0.14f), new Vector3(0.3f, 0.18f, 0.52f), m["Ghost Monkey Skin"]);
            for (int t = 0; t < 4; t++)
                Ball("Toe", foot, new Vector3((t - 1.5f) * 0.065f, 0.06f, 0.4f), Vector3.one * 0.07f, m["Ghost Monkey Skin"]);
            Tube("Leg", model, new Vector3(0.17f * side, 0.32f, 0f), 0.14f, 0.36f, m["Ghost Bark"]);
            feet.Add(foot);

            Transform arm = Node(side < 0 ? "Branch Arm L" : "Branch Arm R", model, new Vector3(0.27f * side, 1.22f, 0f));
            Transform tilt = Node("Tilt", arm, Vector3.zero, new Vector3(0f, 0f, -55f * side));
            Tube("Branch", tilt, new Vector3(0f, 0.25f, 0f), 0.07f, 0.5f, m["Ghost Bark"]);
            Tube("Twig", tilt, new Vector3(0.06f * side, 0.5f, 0f), 0.035f, 0.2f, m["Ghost Bark"], new Vector3(0f, 0f, -35f * side));
            Tube("Twig", tilt, new Vector3(-0.05f * side, 0.52f, 0f), 0.035f, 0.18f, m["Ghost Bark"], new Vector3(0f, 0f, 30f * side));
            Ball("Leaf Tuft", tilt, new Vector3(0f, 0.62f, 0f), new Vector3(0.18f, 0.12f, 0.18f), m["Ghost Leaves"]);
            arms.Add(arm);
        }
        Shape("Trunk", model, Cone(0.3f, 0.24f, 1.0f, 20), new Vector3(0f, 0.45f, 0f), Vector3.zero, Vector3.one, m["Ghost Bark"]);
        foreach (float side in new[] { -1f, 1f })
        {
            Ball("Eye", model, new Vector3(0.1f * side, 1.27f, 0.22f), Vector3.one * 0.13f, m["Ghost Eye White"]);
            Ball("Pupil", model, new Vector3(0.1f * side, 1.27f, 0.285f), Vector3.one * 0.065f, m["Ghost Pupil"]);
        }
        Ball("Big Nose", model, new Vector3(0f, 1.12f, 0.33f), new Vector3(0.19f, 0.16f, 0.3f), m["Ghost Monkey Skin"]);
        Ball("Mouth", model, new Vector3(0f, 0.97f, 0.26f), new Vector3(0.2f, 0.05f, 0.06f), m["Ghost Mouth"]);
        Vector3[] canopy = { new Vector3(0f, 1.66f, 0f), new Vector3(-0.22f, 1.55f, 0.05f), new Vector3(0.22f, 1.57f, -0.04f), new Vector3(0.05f, 1.6f, -0.2f), new Vector3(-0.05f, 1.8f, 0.02f) };
        for (int i = 0; i < canopy.Length; i++)
            Ball("Leaves", model, canopy[i], Vector3.one * (i == 0 ? 0.5f : 0.36f), m["Ghost Leaves"]);

        // Which solid material each see-through part turns into when he gets you.
        var parts = new List<Renderer>();
        var solids = new List<Material>();
        foreach (Renderer r in model.GetComponentsInChildren<Renderer>())
        {
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            string ghostName = r.sharedMaterial.name.Replace("Brainrot Ghost ", "");
            parts.Add(r);
            solids.Add(m.TryGetValue(ghostName, out Material solid) ? solid : r.sharedMaterial);
        }

        var patapim = root.AddComponent<PatapimEnemy>();
        patapim.displayName = "Brr Brr Patapim";
        patapim.builtInModel = model;
        patapim.feet = feet.ToArray();
        patapim.arms = arms.ToArray();
        patapim.ghostParts = parts.ToArray();
        patapim.solidMaterials = solids.ToArray();
        patapim.face = Node("Face", root.transform, new Vector3(0f, 1.15f, 0.42f));
        patapim.tickSeconds = 4.5f;
        return patapim;
    }

    public static BombardiroEnemy BuildBombardiro()
    {
        var root = new GameObject("Bombardiro Crocodilo");
        Transform model = Node("Model", root.transform, Vector3.zero);
        model.localScale = Vector3.one * 0.7f;
        Material scales = m["Croc Scales"], wing = m["Bomber Wing"];

        Ball("Fuselage", model, Vector3.zero, new Vector3(0.9f, 0.8f, 3f), scales);
        // The crocodile head at the front, mouth open, lots of teeth.
        Ball("Upper Jaw", model, new Vector3(0f, 0.12f, 1.75f), new Vector3(0.6f, 0.28f, 1.3f), scales);
        Ball("Lower Jaw", model, new Vector3(0f, -0.14f, 1.68f), new Vector3(0.55f, 0.2f, 1.2f), scales);
        Ball("Mouth", model, new Vector3(0f, -0.01f, 1.72f), new Vector3(0.5f, 0.12f, 1.1f), m["Mouth"]);
        for (int i = 0; i < 6; i++)
        {
            float z = Mathf.Lerp(1.3f, 2.25f, i / 5f), x = Mathf.Lerp(0.25f, 0.14f, i / 5f);
            foreach (float side in new[] { -1f, 1f })
            {
                Shape("Tooth", model, Cone(0.03f, 0f, 0.1f, 6), new Vector3(x * side, 0.0f, z), new Vector3(180f, 0f, 0f), Vector3.one, m["Teeth"]);
                Shape("Tooth", model, Cone(0.028f, 0f, 0.08f, 6), new Vector3(x * 0.9f * side, -0.07f, z - 0.05f), Vector3.zero, Vector3.one, m["Teeth"]);
            }
        }
        foreach (float side in new[] { -1f, 1f })
        {
            Ball("Eye", model, new Vector3(0.2f * side, 0.36f, 1.25f), new Vector3(0.2f, 0.18f, 0.2f), m["Croc Eye"]);
            Block("Slit Pupil", model, new Vector3(0.2f * side, 0.37f, 1.345f), new Vector3(0.03f, 0.12f, 0.015f), m["Pupil"]);
            Ball("Nostril", model, new Vector3(0.08f * side, 0.27f, 2.35f), Vector3.one * 0.06f, m["Pupil"]);
        }
        // Wings, engines, propellers, bombs, tail.
        Block("Wings", model, new Vector3(0f, -0.02f, 0.1f), new Vector3(5f, 0.08f, 0.9f), wing);
        var propellers = new List<Transform>();
        foreach (float side in new[] { -1f, 1f })
        {
            Tube("Engine", model, new Vector3(1.3f * side, -0.05f, 0.55f), 0.3f, 0.75f, wing, new Vector3(90f, 0f, 0f));
            Ball("Spinner", model, new Vector3(1.3f * side, -0.05f, 0.95f), Vector3.one * 0.16f, m["Propeller"]);
            Transform prop = Node("Propeller", model, new Vector3(1.3f * side, -0.05f, 0.98f));
            for (int b = 0; b < 3; b++)
                Block("Blade", prop, Vector3.zero, new Vector3(0.09f, 0.75f, 0.02f), m["Propeller"], new Vector3(0f, 0f, b * 120f)).transform.localPosition = Quaternion.Euler(0f, 0f, b * 120f) * new Vector3(0f, 0.36f, 0f);
            propellers.Add(prop);
            B.Prim(PrimitiveType.Capsule, "Bomb", model, new Vector3(0.7f * side, -0.32f, 0.1f), new Vector3(90f, 0f, 0f), new Vector3(0.22f, 0.36f, 0.22f), m["Bomb"]);
        }
        Block("Tail Wing", model, new Vector3(0f, 0.05f, -2.6f), new Vector3(1.7f, 0.06f, 0.45f), wing);
        // Navigation lights: red on the left wingtip, green on the right, a blinking white one on the tail.
        Ball("Nav Light (left)", model, new Vector3(-2.5f, 0f, 0.1f), Vector3.one * 0.6f, m["Nav Red"]);
        Ball("Nav Light (right)", model, new Vector3(2.5f, 0f, 0.1f), Vector3.one * 0.6f, m["Nav Green"]);
        GameObject beacon = Ball("Beacon (tail)", model, new Vector3(0f, 1.15f, -2.95f), Vector3.one * 0.4f, m["Nav White"]);
        // A searchlight under his chin, sweeping the yard while he circles.
        Light searchlight = B.MakeLight("Searchlight", LightType.Spot, Vector3.zero, Quaternion.identity, new Color(1f, 0.92f, 0.75f), 60f, 45f, 22f, LightShadows.None, model);
        searchlight.transform.localPosition = new Vector3(0f, -0.4f, 1.6f);
        searchlight.transform.localRotation = Quaternion.Euler(65f, 0f, 0f);
        searchlight.enabled = false;
        // Glowing engines light up his body, so you can see him against the night sky.
        Light glow = B.MakeLight("Engine Glow", LightType.Point, Vector3.zero, Quaternion.identity, new Color(1f, 0.55f, 0.25f), 4f, 6f, 0f, LightShadows.None, model);
        glow.transform.localPosition = new Vector3(0f, -1.2f, 0.6f);
        glow.enabled = false;
        Shape("Tail Fin", model, Fin(new Vector3(0f, 0f, 0.1f), new Vector3(0f, 0f, -0.6f), new Vector3(0f, 0.8f, -0.75f), 0.07f), new Vector3(0f, 0.3f, -2.3f), Vector3.zero, Vector3.one, scales);
        for (int i = 0; i < 7; i++) // crocodile spikes along the back
            Shape("Spike", model, Cone(0.06f, 0f, 0.14f, 6), new Vector3(0f, 0.38f - i * 0.02f, 0.9f - i * 0.4f), Vector3.zero, Vector3.one, scales);

        var hitPoints = new[]
        {
            Node("Hit: Nose", model, new Vector3(0f, 0.1f, 2.3f)),
            Node("Hit: Middle", model, Vector3.zero),
            Node("Hit: Tail", model, new Vector3(0f, 0.1f, -2.4f)),
            Node("Hit: Left Wing", model, new Vector3(-1.8f, 0f, 0.1f)),
            Node("Hit: Right Wing", model, new Vector3(1.8f, 0f, 0.1f)),
        };

        var croc = root.AddComponent<BombardiroEnemy>();
        croc.displayName = "Bombardiro Crocodilo";
        croc.builtInModel = model;
        croc.propellers = propellers.ToArray();
        croc.hitPoints = hitPoints;
        croc.beacon = beacon.GetComponent<Renderer>();
        croc.searchlight = searchlight;
        croc.engineGlow = glow;
        croc.face = Node("Face", root.transform, new Vector3(0f, 0.15f, 1.45f));
        croc.tickSeconds = 7f;
        return croc;
    }

    public static LiriliEnemy BuildLirili()
    {
        var root = new GameObject("Lirili Larila");
        Transform model = Node("Model", root.transform, Vector3.zero);
        Material elephant = m["Elephant Skin"], cactus = m["Cactus"];
        var legs = new List<Transform>();
        foreach (float side in new[] { -1f, 1f })
        {
            Transform leg = Node(side < 0 ? "Leg L" : "Leg R", model, new Vector3(0.16f * side, 0.7f, 0f));
            Tube("Elephant Leg", leg, new Vector3(0f, -0.34f, 0f), 0.2f, 0.68f, elephant);
            Block("Sandal", leg, new Vector3(0f, -0.69f, 0.05f), new Vector3(0.22f, 0.03f, 0.36f), m["Sandal"]);
            Block("Strap", leg, new Vector3(0f, -0.64f, 0.1f), new Vector3(0.23f, 0.025f, 0.05f), m["Sandal"]);
            Block("Strap", leg, new Vector3(0f, -0.64f, -0.02f), new Vector3(0.23f, 0.025f, 0.05f), m["Sandal"]);
            legs.Add(leg);
        }
        // A saguaro cactus body with its arms up.
        Tube("Cactus Body", model, new Vector3(0f, 1.08f, 0f), 0.52f, 0.9f, cactus);
        Ball("Cactus Top", model, new Vector3(0f, 1.52f, 0f), new Vector3(0.52f, 0.3f, 0.52f), cactus);
        foreach (float side in new[] { -1f, 1f })
        {
            Tube("Cactus Arm", model, new Vector3(0.36f * side, 1.08f, 0f), 0.15f, 0.22f, cactus, new Vector3(0f, 0f, 90f));
            Tube("Cactus Arm", model, new Vector3(0.45f * side, 1.27f, 0f), 0.15f, 0.42f, cactus);
            Ball("Cactus Arm Top", model, new Vector3(0.45f * side, 1.48f, 0f), Vector3.one * 0.15f, cactus);
        }
        // The elephant head, with big ears, tusks and a trunk.
        Ball("Head", model, new Vector3(0f, 1.75f, 0.08f), new Vector3(0.48f, 0.42f, 0.45f), elephant);
        foreach (float side in new[] { -1f, 1f })
        {
            Ball("Ear", model, new Vector3(0.3f * side, 1.76f, -0.02f), new Vector3(0.07f, 0.42f, 0.34f), elephant, new Vector3(0f, -25f * side, 0f));
            Ball("Eye", model, new Vector3(0.12f * side, 1.83f, 0.28f), Vector3.one * 0.075f, m["Eye White"]);
            Ball("Pupil", model, new Vector3(0.125f * side, 1.83f, 0.31f), Vector3.one * 0.04f, m["Pupil"]);
            Shape("Tusk", model, Cone(0.025f, 0.005f, 0.16f, 8), new Vector3(0.1f * side, 1.64f, 0.27f), new Vector3(110f, 0f, 0f), Vector3.one, m["Teeth"]);
        }
        Transform trunk = Node("Trunk", model, new Vector3(0f, 1.68f, 0.28f));
        Vector3[] trunkBalls = { new Vector3(0f, -0.04f, 0.04f), new Vector3(0f, -0.13f, 0.1f), new Vector3(0f, -0.24f, 0.13f), new Vector3(0f, -0.35f, 0.12f), new Vector3(0f, -0.44f, 0.08f) };
        for (int i = 0; i < trunkBalls.Length; i++)
            Ball("Trunk", trunk, trunkBalls[i], Vector3.one * Mathf.Lerp(0.14f, 0.07f, i / 4f), elephant);
        // His pocket watch, hanging from a cactus arm.
        Transform watch = Node("Pocket Watch", model, new Vector3(0.45f, 1.32f, 0.11f));
        Tube("Watch", watch, Vector3.zero, 0.17f, 0.03f, m["Gold"], new Vector3(90f, 0f, 0f));
        Tube("Watch Face", watch, new Vector3(0f, 0f, 0.016f), 0.14f, 0.004f, m["Eye White"], new Vector3(90f, 0f, 0f));
        Block("Hour Hand", watch, new Vector3(0f, 0.02f, 0.02f), new Vector3(0.01f, 0.045f, 0.004f), m["Pupil"]);
        Block("Minute Hand", watch, new Vector3(0.025f, 0f, 0.02f), new Vector3(0.055f, 0.008f, 0.004f), m["Pupil"]);

        var lirili = root.AddComponent<LiriliEnemy>();
        lirili.displayName = "Lirili Larila";
        lirili.builtInModel = model;
        lirili.legs = legs.ToArray();
        lirili.trunk = trunk;
        lirili.watch = watch;
        lirili.face = Node("Face", root.transform, new Vector3(0f, 1.72f, 0.38f));
        lirili.tickSeconds = 6f;
        return lirili;
    }

    // ---------- Parts ----------

    static Transform Node(string name, Transform parent, Vector3 position, Vector3 euler = default)
    {
        var t = new GameObject(name).transform;
        t.SetParent(parent, false);
        t.localPosition = position;
        t.localEulerAngles = euler;
        return t;
    }

    // `size` = diameters.
    static GameObject Ball(string name, Transform parent, Vector3 position, Vector3 size, Material material, Vector3 euler = default)
        => B.Prim(PrimitiveType.Sphere, name, parent, position, euler, size, material);

    static GameObject Tube(string name, Transform parent, Vector3 center, float diameter, float length, Material material, Vector3 euler = default)
        => B.Prim(PrimitiveType.Cylinder, name, parent, center, euler, new Vector3(diameter, length * 0.5f, diameter), material);

    static GameObject Block(string name, Transform parent, Vector3 center, Vector3 size, Material material, Vector3 euler = default)
        => B.Prim(PrimitiveType.Cube, name, parent, center, euler, size, material);

    static GameObject Shape(string name, Transform parent, Mesh mesh, Vector3 position, Vector3 euler, Vector3 scale, Material material)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        go.transform.localPosition = position;
        go.transform.localEulerAngles = euler;
        go.transform.localScale = scale;
        go.AddComponent<MeshFilter>().sharedMesh = mesh;
        go.AddComponent<MeshRenderer>().sharedMaterial = material;
        return go;
    }

    static Bounds WorldBounds(GameObject go)
    {
        var renderers = go.GetComponentsInChildren<Renderer>();
        Bounds b = renderers.Length > 0 ? renderers[0].bounds : new Bounds(go.transform.position, Vector3.zero);
        foreach (Renderer r in renderers) b.Encapsulate(r.bounds);
        return b;
    }

    // ---------- Meshes made in code ----------

    // Collects vertices and triangles; every triangle is turned to face the way its vertex normals point.
    class MeshMaker
    {
        readonly List<Vector3> v = new List<Vector3>();
        readonly List<Vector3> n = new List<Vector3>();
        readonly List<Vector2> uv = new List<Vector2>();
        readonly List<int> tris = new List<int>();

        public int Add(Vector3 position, Vector3 normal, Vector2 texture)
        {
            v.Add(position); n.Add(normal.normalized); uv.Add(texture);
            return v.Count - 1;
        }

        public void Tri(int a, int b, int c)
        {
            if (Vector3.Dot(Vector3.Cross(v[b] - v[a], v[c] - v[a]), n[a] + n[b] + n[c]) < 0f) (b, c) = (c, b);
            tris.Add(a); tris.Add(b); tris.Add(c);
        }

        public void Quad(int a, int b, int c, int d) { Tri(a, b, c); Tri(a, c, d); }

        public Mesh Make(string name)
        {
            var mesh = new Mesh { name = name };
            mesh.SetVertices(v);
            mesh.SetNormals(n);
            mesh.SetUVs(0, uv);
            mesh.SetTriangles(tris, 0);
            mesh.RecalculateBounds();
            mesh.RecalculateTangents();
            return mesh;
        }
    }

    // A cone or cylinder along +Y, from radius `bottom` at y = 0 to radius `top` at y = `height`, with caps.
    static Mesh Cone(float bottom, float top, float height, int segments)
    {
        var mm = new MeshMaker();
        float slope = (bottom - top) / Mathf.Max(height, 1e-4f);
        for (int i = 0; i < segments; i++)
        {
            float a0 = i * 2f * Mathf.PI / segments, a1 = (i + 1) * 2f * Mathf.PI / segments;
            Vector3 d0 = new Vector3(Mathf.Cos(a0), 0f, Mathf.Sin(a0)), d1 = new Vector3(Mathf.Cos(a1), 0f, Mathf.Sin(a1));
            Vector3 n0 = d0 + Vector3.up * slope, n1 = d1 + Vector3.up * slope;
            int b0 = mm.Add(d0 * bottom, n0, new Vector2(i / (float)segments, 0f));
            int b1 = mm.Add(d1 * bottom, n1, new Vector2((i + 1) / (float)segments, 0f));
            int t0 = mm.Add(d0 * top + Vector3.up * height, n0, new Vector2(i / (float)segments, 1f));
            int t1 = mm.Add(d1 * top + Vector3.up * height, n1, new Vector2((i + 1) / (float)segments, 1f));
            mm.Quad(b0, t0, t1, b1);
            if (top > 1e-4f)
                mm.Tri(mm.Add(Vector3.up * height, Vector3.up, new Vector2(0.5f, 0.5f)),
                       mm.Add(d0 * top + Vector3.up * height, Vector3.up, new Vector2(0.5f + 0.5f * d0.x, 0.5f + 0.5f * d0.z)),
                       mm.Add(d1 * top + Vector3.up * height, Vector3.up, new Vector2(0.5f + 0.5f * d1.x, 0.5f + 0.5f * d1.z)));
            if (bottom > 1e-4f)
                mm.Tri(mm.Add(Vector3.zero, Vector3.down, new Vector2(0.5f, 0.5f)),
                       mm.Add(d0 * bottom, Vector3.down, new Vector2(0.5f + 0.5f * d0.x, 0.5f + 0.5f * d0.z)),
                       mm.Add(d1 * bottom, Vector3.down, new Vector2(0.5f + 0.5f * d1.x, 0.5f + 0.5f * d1.z)));
        }
        return mm.Make("Cone");
    }

    // Part of a ring (a cup handle): `arc` degrees of a torus lying in the XY plane.
    static Mesh Torus(float radius, float thickness, float arc, int segments = 20, int sides = 10)
    {
        var mm = new MeshMaker();
        var grid = new int[segments + 1, sides + 1];
        for (int i = 0; i <= segments; i++)
        {
            float u = arc * Mathf.Deg2Rad * i / segments;
            Vector3 center = new Vector3(Mathf.Cos(u), Mathf.Sin(u), 0f) * radius;
            for (int j = 0; j <= sides; j++)
            {
                float w = 2f * Mathf.PI * j / sides;
                Vector3 normal = new Vector3(Mathf.Cos(u) * Mathf.Cos(w), Mathf.Sin(u) * Mathf.Cos(w), Mathf.Sin(w));
                grid[i, j] = mm.Add(center + normal * thickness, normal, new Vector2(i / (float)segments, j / (float)sides));
            }
        }
        for (int i = 0; i < segments; i++)
            for (int j = 0; j < sides; j++)
                mm.Quad(grid[i, j], grid[i + 1, j], grid[i + 1, j + 1], grid[i, j + 1]);
        return mm.Make("Torus");
    }

    // A fin: the triangle a-b-c (in the YZ plane) made `thickness` thick along X.
    static Mesh Fin(Vector3 a, Vector3 b, Vector3 c, float thickness)
    {
        var mm = new MeshMaker();
        Vector3 half = Vector3.right * (thickness * 0.5f), mid = (a + b + c) / 3f;
        Vector3[] p = { a, b, c };
        mm.Tri(mm.Add(a + half, Vector3.right, new Vector2(0f, 0f)), mm.Add(b + half, Vector3.right, new Vector2(1f, 0f)), mm.Add(c + half, Vector3.right, new Vector2(0.5f, 1f)));
        mm.Tri(mm.Add(a - half, Vector3.left, new Vector2(0f, 0f)), mm.Add(b - half, Vector3.left, new Vector2(1f, 0f)), mm.Add(c - half, Vector3.left, new Vector2(0.5f, 1f)));
        for (int i = 0; i < 3; i++)
        {
            Vector3 p0 = p[i], p1 = p[(i + 1) % 3];
            Vector3 outward = (p0 + p1) * 0.5f - mid;
            mm.Quad(mm.Add(p0 + half, outward, new Vector2(0f, 0f)), mm.Add(p1 + half, outward, new Vector2(1f, 0f)),
                    mm.Add(p1 - half, outward, new Vector2(1f, 1f)), mm.Add(p0 - half, outward, new Vector2(0f, 1f)));
        }
        return mm.Make("Fin");
    }

    // A frilly tutu: a ring from `inner` to a wavy `outer` edge that droops down, visible from above and below.
    static Mesh Ruffle(float inner, float outer, float droop, int waves, float wave, int segments = 72)
    {
        var mm = new MeshMaker();
        for (int side = 0; side < 2; side++)
        {
            Vector3 normal = side == 0 ? Vector3.up : Vector3.down;
            int first = -1;
            for (int i = 0; i <= segments; i++)
            {
                float a = i * 2f * Mathf.PI / segments;
                Vector3 d = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a));
                float r = outer * (1f + 0.07f * Mathf.Sin(waves * a));
                int innerIndex = mm.Add(d * inner, normal, new Vector2(i / (float)segments, 0f));
                int outerIndex = mm.Add(d * r + Vector3.up * (-droop + wave * Mathf.Sin(waves * a)), normal, new Vector2(i / (float)segments, 1f));
                if (first >= 0) mm.Quad(first, first + 1, outerIndex, innerIndex);
                first = innerIndex;
            }
        }
        return mm.Make("Ruffle");
    }
}
