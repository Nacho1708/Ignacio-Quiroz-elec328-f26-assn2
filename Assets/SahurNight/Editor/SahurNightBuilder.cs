using System.Collections.Generic;
using System.IO;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.Build.Profile;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

// Tools > Sahur Night > Build Scene
// Builds the whole Sahur Night scene: the night-shift office around the Roost rails, the yard outside the window,
// the hallway behind the door and the side hall on the left, the enchanted flashlight, Tung Tung Tung Sahur and
// his three paths, the lights, the sky and the post-processing. Safe to run again: it rebuilds the scene from scratch.
// (Five Nights at Tung's reuses the office, lights and flashlight from here: see FiveNightsBuilder.)
//
// Layout (meters, looking from above, you stand at the origin facing +Z inside the Roost rails):
//   front (+Z): desk on the window rail, window, moonlit yard
//   back  (-Z): door to a long dark hallway        left (-X): opening to the side hall        right (+X): lockers
public static class SahurNightBuilder
{
    internal const string Folder = "Assets/SahurNight";
    internal const string Generated = Folder + "/Generated";
    const string ScenePath = Folder + "/SahurNight.unity";
    const string ShaderPath = Folder + "/Shaders/EnchantedFlashlight.shadergraph";
    internal const string TungGuid = "3909191f4400249e78d3e9c2288838ad"; // Assets/Brainrot/tttsahur/tung.fbx
    const string RoostRailsGuid = "b79712b0b77bb794b9f0d330c5107b45";  // Reality Roost SDK: Roost Rails.prefab

    // Room size.
    internal const float Half = 2.4f, Wall = 0.2f, Ceiling = 2.8f;
    internal const float WindowLeft = -1.3f, WindowRight = 1.3f, WindowBottom = 1.0f, WindowTop = 2.3f;
    internal const float DoorHalf = 0.55f, DoorTop = 2.1f, HallHalf = 0.6f, SkylightHalf = 0.65f;
    static readonly Vector3 YardLamp = new Vector3(-1.6f, 0f, 13.2f);

    static Shader enchanted;
    static Transform parent;
    static readonly Dictionary<string, Material> mats = new Dictionary<string, Material>();
    internal static Dictionary<string, Material> Mats => mats;
    internal static Transform Parent { get => parent; set => parent = value; }

    // What the shared office, yard and hallway code builds. Sahur Night uses the defaults (all off).
    internal class WorldOptions
    {
        public bool skylight;      // a hole in the ceiling above you (for Bombardiro Crocodilo)
        public bool closableDoor;  // leave out the fixed open door (Five Nights adds a hinged one)
        public bool securityDesk;  // leave out the desk monitor and keyboard (Five Nights adds a security monitor)
        public bool alcove;        // an opening in the hallway's right wall near the far end (Tralalero's hideout)
    }

    // Lights and glowing things the games turn on, off and flicker.
    internal class World
    {
        public Light moon, sun, morning, tube, lamp, exitGlow;
        public readonly List<Renderer> glows = new List<Renderer>(); // ceiling tube, lamp bulb, exit sign
    }

    [MenuItem("Tools/Sahur Night/Build Scene")]
    public static void BuildScene()
    {
        if (!Application.isBatchMode)
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            if (File.Exists(ScenePath) && !EditorUtility.DisplayDialog("Sahur Night",
                    "Rebuild SahurNight.unity from scratch? Changes you made by hand in that scene will be replaced.", "Rebuild", "Cancel"))
                return;
        }

        try
        {
            if (!Prepare("Sahur Night", out GameObject tungModel)) return;
            EditorUtility.DisplayProgressBar("Sahur Night", "Building the office...", 0.7f);
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            BuildEverything(tungModel);
            EditorSceneManager.SaveScene(scene, ScenePath);
            AddToBuildLists(ScenePath);
            AssetDatabase.SaveAssets();
            Debug.Log("Sahur Night: built " + ScenePath + ". Press Play to test it here, or load it from RR_Boot in the Roost.");
        }
        finally
        {
            EditorUtility.ClearProgressBar();
        }
    }

    // Finds the shader graph and Tung, paints the textures and makes the materials.
    internal static bool Prepare(string title, out GameObject tungModel)
    {
        tungModel = null;
        enchanted = AssetDatabase.LoadAssetAtPath<Shader>(ShaderPath);
        if (enchanted == null) { Debug.LogError($"{title}: can't find the shader graph at {ShaderPath}."); return false; }
        string tungPath = AssetDatabase.GUIDToAssetPath(TungGuid);
        if (string.IsNullOrEmpty(tungPath) || AssetDatabase.LoadAssetAtPath<GameObject>(tungPath) == null)
            tungPath = AssetDatabase.FindAssets("tung t:Model").Select(AssetDatabase.GUIDToAssetPath).FirstOrDefault();
        if (string.IsNullOrEmpty(tungPath)) { Debug.LogError($"{title}: can't find the Tung Tung Tung Sahur model (tung.fbx)."); return false; }

        EditorUtility.DisplayProgressBar(title, "Painting textures...", 0.1f);
        MakeFolder(Generated); MakeFolder(Generated + "/Textures"); MakeFolder(Generated + "/Materials");
        MakeTungReadable(tungPath);
        tungModel = AssetDatabase.LoadAssetAtPath<GameObject>(tungPath);
        Texture2D albedo = FindAlbedo(tungModel, tungPath);
        Dictionary<string, Texture2D> tex = SahurNightTextures.Make(Generated + "/Textures", albedo);

        EditorUtility.DisplayProgressBar(title, "Making materials...", 0.4f);
        MakeMaterials(tex, albedo);
        return true;
    }

    // ---------- Assets ----------

    internal static void MakeFolder(string path)
    {
        if (AssetDatabase.IsValidFolder(path)) return;
        AssetDatabase.CreateFolder(Path.GetDirectoryName(path).Replace('\\', '/'), Path.GetFileName(path));
    }

    // Tung's mesh has to be readable so his dance can move its vertices in a build too.
    static void MakeTungReadable(string path)
    {
        if (AssetImporter.GetAtPath(path) is ModelImporter importer && !importer.isReadable)
        {
            importer.isReadable = true;
            importer.SaveAndReimport();
        }
    }

    static Texture2D FindAlbedo(GameObject model, string modelPath)
    {
        foreach (Renderer r in model.GetComponentsInChildren<Renderer>())
            foreach (Material m in r.sharedMaterials)
                if (m != null && m.mainTexture is Texture2D t) return t;
        return AssetDatabase.LoadAssetAtPath<Texture2D>(Path.GetDirectoryName(modelPath).Replace('\\', '/') + "/albedo.png");
    }

    internal static Material SaveMaterial(Material m)
    {
        string path = $"{Generated}/Materials/{m.name}.mat";
        Material existing = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (existing != null)
        {
            EditorUtility.CopySerialized(m, existing); // keep the same asset, so nothing else loses it
            Object.DestroyImmediate(m);
            mats[existing.name] = existing;
            return existing;
        }
        AssetDatabase.CreateAsset(m, path);
        mats[m.name] = m;
        return m;
    }

    // One material on the Enchanted Flashlight shader graph (or another graph with the same properties).
    // Each has its own normal texture, hidden texture and beam outline color (Objective F).
    internal static Material Enchanted(string name, Texture2D baseMap, Texture2D hidden, Color boundary, float glow = 0.6f,
                                       float wiggle = 0f, float smoothness = 0.2f, Shader shader = null)
    {
        var m = new Material(shader != null ? shader : enchanted) { name = name };
        if (baseMap != null) m.SetTexture("_BaseMap", baseMap);
        m.SetTexture("_HiddenTexture", hidden);
        m.SetColor("_BaseColor", Color.white);
        m.SetColor("_BoundaryColor", boundary);
        m.SetFloat("_BoundaryWidth", 1.2f);
        m.SetFloat("_EdgeFeather", 0.4f);
        m.SetFloat("_HiddenGlow", glow);
        m.SetFloat("_WiggleAmount", wiggle);
        m.SetFloat("_Smoothness", smoothness);
        return SaveMaterial(m);
    }

    // A plain URP Lit material for props that aren't enchanted (the flashlight itself, lamps...).
    internal static Material Lit(string name, Color color, float metallic = 0f, float smoothness = 0.4f, bool emissive = false, Color emission = default)
    {
        var m = new Material(Shader.Find("Universal Render Pipeline/Lit")) { name = name };
        m.SetColor("_BaseColor", color);
        m.SetFloat("_Metallic", metallic);
        m.SetFloat("_Smoothness", smoothness);
        if (emissive)
        {
            m.EnableKeyword("_EMISSION");
            m.SetColor("_EmissionColor", emission);
            m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.None;
        }
        return SaveMaterial(m);
    }

    static void MakeMaterials(Dictionary<string, Texture2D> t, Texture2D albedo)
    {
        mats.Clear();
        // HDR outline colors (above 1 so they glow with bloom).
        Enchanted("Enchanted Wall", t["wall"], t["wallCursed"], new Color(0.2f, 1.6f, 2.2f));
        Enchanted("Enchanted Floor", t["floor"], t["floorCursed"], new Color(2.2f, 1.2f, 0.15f));
        Enchanted("Enchanted Wood", t["wood"], t["woodCursed"], new Color(2.4f, 0.6f, 0.1f), 0.8f);
        Enchanted("Enchanted Ceiling", t["ceiling"], t["ceilingCursed"], new Color(1.2f, 0.5f, 2.4f));
        Enchanted("Enchanted Metal", t["metal"], t["metalCursed"], new Color(0.4f, 2.2f, 0.6f), 0.6f, 0f, 0.45f);
        Enchanted("Enchanted Poster", t["poster"], t["posterCursed"], new Color(2.4f, 1.9f, 0.4f), 0.9f);
        Enchanted("Enchanted Yard", t["yard"], t["yardCursed"], new Color(2.4f, 0.25f, 0.2f), 0.7f, 0f, 0.05f);
        Enchanted("Enchanted Sahur", albedo, t["sahurCursed"], new Color(3f, 0.15f, 0.05f), 1.3f, 0.012f, 0.3f);
        Enchanted("Enchanted Sahur Eyes", null, t["eyesCursed"], new Color(3f, 0.15f, 0.05f), 2.5f, 0f, 0.6f);

        Lit("Flashlight Body", new Color(0.12f, 0.12f, 0.13f), 0.6f, 0.55f);
        Lit("Flashlight Head", new Color(0.55f, 0.08f, 0.06f), 0.7f, 0.6f);
        Lit("Flashlight Glow", new Color(0.3f, 0.3f, 0.3f), 0f, 0.9f, true, Color.black);
        Lit("Dark Plastic", new Color(0.05f, 0.05f, 0.055f), 0f, 0.5f);
        Lit("Lamp Shade", new Color(0.1f, 0.25f, 0.15f), 0.3f, 0.5f, true, new Color(0.25f, 0.2f, 0.1f));
        Lit("Fluorescent Tube", new Color(0.9f, 1f, 0.95f), 0f, 0.5f, true, new Color(1.6f, 1.9f, 1.7f));
        Lit("Lamp Bulb", new Color(1f, 0.85f, 0.6f), 0f, 0.5f, true, new Color(4f, 2.6f, 1.2f));
        Lit("Screen", new Color(0.01f, 0.03f, 0.02f), 0f, 0.95f, true, new Color(0.01f, 0.06f, 0.03f));

        var exit = new Material(Shader.Find("Universal Render Pipeline/Lit")) { name = "Exit Sign" };
        exit.SetTexture("_BaseMap", t["exit"]);
        exit.EnableKeyword("_EMISSION");
        exit.SetTexture("_EmissionMap", t["exit"]);
        exit.SetColor("_EmissionColor", new Color(1.8f, 1.8f, 1.8f));
        exit.globalIlluminationFlags = MaterialGlobalIlluminationFlags.None;
        SaveMaterial(exit);

        var sky = new Material(Shader.Find("Skybox/Panoramic")) { name = "Night Sky" };
        sky.SetTexture("_MainTex", t["sky"]);
        sky.SetFloat("_Exposure", 0.9f);
        sky.SetColor("_Tint", new Color(0.5f, 0.5f, 0.5f));
        SaveMaterial(sky);
    }

    // ---------- The scene ----------

    static void BuildEverything(GameObject tungModel)
    {
        World world = BuildWorld(new WorldOptions());
        EnchantedFlashlight flashlight = BuildFlashlight();
        SahurStalker sahur = BuildSahur(tungModel, flashlight);

        // Clocks and the message board.
        Transform ui = Group("Clocks and Messages");
        TextMeshPro monitorClock = Text("Monitor Clock", "12 AM", new Vector3(0.9f, 1.22f, 1.955f), Quaternion.identity,
            1.5f, new Color(0.45f, 1f, 0.55f), new Vector2(0.5f, 0.3f), ui);
        TextMeshPro wallClock = Text("Wall Clock", "12 AM", new Vector3(Half - 0.01f, 2.35f, -0.9f), Quaternion.Euler(0f, 90f, 0f),
            2.6f, new Color(1f, 0.25f, 0.2f), new Vector2(1.2f, 0.4f), ui);
        TextMeshPro message = Text("Message", "", new Vector3(0f, 1.95f, 1.9f), Quaternion.identity,
            0.85f, new Color(0.93f, 0.9f, 0.85f), new Vector2(2.3f, 1.3f), ui);
        // A dark see-through card behind the message so it's readable against the moon and the walls.
        var backdrop = new Material(Shader.Find("Sprites/Default")) { name = "Message Backdrop", color = new Color(0f, 0f, 0f, 0.65f) };
        GameObject card = Prim(PrimitiveType.Quad, "Backdrop", message.transform, new Vector3(0f, 0f, 0.02f), Vector3.zero, new Vector3(2.45f, 1.45f, 1f), SaveMaterial(backdrop));
        card.GetComponent<Renderer>().shadowCastingMode = ShadowCastingMode.Off;

        var gameObject = new GameObject("Sahur Night Game");
        var game = gameObject.AddComponent<SahurNightGame>();
        game.flashlight = flashlight;
        game.sahur = sahur;
        game.clocks = new[] { monitorClock, wallClock };
        game.message = message;
        game.roomLights = new[] { world.tube.GetComponent<FlickerLight>(), world.lamp.GetComponent<FlickerLight>() };
        game.moon = world.moon;
        game.sun = world.sun;
        game.dayLights = new[] { world.morning };
        game.windowZ = Half + Wall;
        var ambience = gameObject.AddComponent<AudioSource>();
        ambience.playOnAwake = false;
        ambience.spatialBlend = 0f;
        ambience.volume = 0.35f;
        game.ambience = ambience;

        BuildTestRig();
    }

    // The sky, the office, yard and hallways, the lights, post-processing and the Roost rails.
    internal static World BuildWorld(WorldOptions options)
    {
        var world = new World();

        // Sky, fog and the faint blue ambient light of a night with a moon.
        RenderSettings.skybox = mats["Night Sky"];
        RenderSettings.ambientMode = AmbientMode.Flat;
        RenderSettings.ambientLight = new Color(0.045f, 0.05f, 0.075f);
        RenderSettings.fog = true;
        RenderSettings.fogMode = FogMode.ExponentialSquared;
        RenderSettings.fogColor = new Color(0.015f, 0.02f, 0.035f);
        RenderSettings.fogDensity = 0.045f;
        RenderSettings.defaultReflectionMode = DefaultReflectionMode.Skybox;
        RenderSettings.reflectionIntensity = 0.3f;

        Transform env = Group("Environment");
        Transform office = Group("Office", env);
        BuildOffice(office, options, world);
        BuildYard(Group("Yard (outside the window)", env));
        BuildHallway(Group("Hallway (behind the door)", env), options);
        BuildSideHall(Group("Side Hall (left)", env));

        Transform lights = Group("Lights");
        world.moon = MakeLight("Moonlight", LightType.Directional, Vector3.up * 10f,
            Quaternion.LookRotation(-MoonDirection()), new Color(0.55f, 0.65f, 1f), 0.25f, 0f, 0f, LightShadows.Soft, lights);
        RenderSettings.sun = world.moon;
        world.sun = MakeLight("Sunrise (6 AM)", LightType.Directional, Vector3.up * 10f,
            Quaternion.LookRotation(-new Vector3(0.35f, 0.22f, 1f).normalized), new Color(1f, 0.78f, 0.55f), 0f, 0f, 0f, LightShadows.Soft, lights);
        world.sun.enabled = false;
        world.morning = MakeLight("Morning Room Light (6 AM)", LightType.Point, new Vector3(0f, 2.5f, 0f), Quaternion.identity,
            new Color(1f, 0.92f, 0.8f), 0f, 9f, 0f, LightShadows.None, lights);
        world.morning.enabled = false;

        // Room lights: a buzzing ceiling tube and a desk lamp. They flicker when someone is close.
        world.tube = MakeLight("Ceiling Light", LightType.Point, new Vector3(0f, Ceiling - 0.2f, options.skylight ? 1.3f : 0.3f), Quaternion.identity,
            new Color(0.75f, 0.92f, 0.82f), 0.7f, 6f, 0f, LightShadows.None, lights);
        world.tube.gameObject.AddComponent<FlickerLight>().jitter = 0.12f;
        world.lamp = MakeLight("Desk Lamp", LightType.Spot, new Vector3(-1.05f, 1.42f, 1.95f),
            Quaternion.LookRotation(new Vector3(0.35f, -1f, -0.25f)), new Color(1f, 0.75f, 0.45f), 2.2f, 3.5f, 95f, LightShadows.None, lights);
        world.lamp.gameObject.AddComponent<FlickerLight>().jitter = 0.04f;
        world.exitGlow = MakeLight("Exit Sign Glow", LightType.Point, new Vector3(0f, 2.3f, -Half + 0.2f), Quaternion.identity,
            new Color(1f, 0.15f, 0.1f), 0.5f, 3f, 0f, LightShadows.None, lights);
        Light hallEnd = MakeLight("Hallway Light", LightType.Point, new Vector3(0f, 2.4f, -10.2f), Quaternion.identity,
            new Color(0.6f, 0.75f, 1f), 0.8f, 6f, 0f, LightShadows.None, lights);
        hallEnd.gameObject.AddComponent<FlickerLight>().jitter = 0.3f;
        Light sideEnd = MakeLight("Side Hall Light", LightType.Point, new Vector3(-10.2f, 2.4f, 0f), Quaternion.identity,
            new Color(0.95f, 0.8f, 0.5f), 0.7f, 6f, 0f, LightShadows.None, lights);
        sideEnd.gameObject.AddComponent<FlickerLight>().jitter = 0.45f;
        Light yardLamp = MakeLight("Yard Lamp", LightType.Point, new Vector3(YardLamp.x, 3.1f, YardLamp.z), Quaternion.identity,
            new Color(1f, 0.6f, 0.25f), 2.2f, 11f, 0f, LightShadows.None, lights);
        yardLamp.gameObject.AddComponent<FlickerLight>().jitter = 0.1f;

        BuildPostProcessing();

        // The Roost rails (the real rails in the room). You play inside them.
        BuildRails();
        return world;
    }

    static void BuildOffice(Transform p, WorldOptions options, World world)
    {
        parent = p;
        Material wall = mats["Enchanted Wall"], floor = mats["Enchanted Floor"], wood = mats["Enchanted Wood"],
                 ceiling = mats["Enchanted Ceiling"], metal = mats["Enchanted Metal"];
        float o = Half + Wall; // outer edge of the walls

        Box("Floor", new Vector3(-o, -0.1f, -o), new Vector3(o, 0f, o), floor, 2f);
        if (!options.skylight)
            Box("Ceiling", new Vector3(-o, Ceiling, -o), new Vector3(o, Ceiling + 0.1f, o), ceiling, 2.4f);
        else
        {
            // A square skylight right above the middle of the Roost: look up!
            float s = SkylightHalf, top = Ceiling + 0.1f;
            Box("Ceiling (front)", new Vector3(-o, Ceiling, s), new Vector3(o, top, o), ceiling, 2.4f);
            Box("Ceiling (back)", new Vector3(-o, Ceiling, -o), new Vector3(o, top, -s), ceiling, 2.4f);
            Box("Ceiling (left)", new Vector3(-o, Ceiling, -s), new Vector3(-s, top, s), ceiling, 2.4f);
            Box("Ceiling (right)", new Vector3(s, Ceiling, -s), new Vector3(o, top, s), ceiling, 2.4f);
            // A short shaft up through the roof, with a wooden frame.
            float shaft = top + 0.45f;
            Box("Skylight Shaft (front)", new Vector3(-s - 0.08f, top, s), new Vector3(s + 0.08f, shaft, s + 0.08f), wall, 1.6f);
            Box("Skylight Shaft (back)", new Vector3(-s - 0.08f, top, -s - 0.08f), new Vector3(s + 0.08f, shaft, -s), wall, 1.6f);
            Box("Skylight Shaft (left)", new Vector3(-s - 0.08f, top, -s), new Vector3(-s, shaft, s), wall, 1.6f);
            Box("Skylight Shaft (right)", new Vector3(s, top, -s), new Vector3(s + 0.08f, shaft, s), wall, 1.6f);
            Box("Skylight Frame (front)", new Vector3(-s - 0.06f, Ceiling - 0.06f, s), new Vector3(s + 0.06f, Ceiling, s + 0.06f), wood, 1.2f, false);
            Box("Skylight Frame (back)", new Vector3(-s - 0.06f, Ceiling - 0.06f, -s - 0.06f), new Vector3(s + 0.06f, Ceiling, -s), wood, 1.2f, false);
            Box("Skylight Frame (left)", new Vector3(-s - 0.06f, Ceiling - 0.06f, -s), new Vector3(-s, Ceiling, s), wood, 1.2f, false);
            Box("Skylight Frame (right)", new Vector3(s, Ceiling - 0.06f, -s), new Vector3(s + 0.06f, Ceiling, s), wood, 1.2f, false);
        }

        // Front wall with the window.
        Box("Front Wall (below window)", new Vector3(-o, 0f, Half), new Vector3(o, WindowBottom, o), wall, 1.6f);
        Box("Front Wall (above window)", new Vector3(-o, WindowTop, Half), new Vector3(o, Ceiling, o), wall, 1.6f);
        Box("Front Wall (left of window)", new Vector3(-o, WindowBottom, Half), new Vector3(WindowLeft, WindowTop, o), wall, 1.6f);
        Box("Front Wall (right of window)", new Vector3(WindowRight, WindowBottom, Half), new Vector3(o, WindowTop, o), wall, 1.6f);
        Box("Window Sill", new Vector3(WindowLeft - 0.05f, WindowBottom - 0.03f, Half - 0.08f), new Vector3(WindowRight + 0.05f, WindowBottom, o + 0.05f), wood, 1.2f);
        Box("Window Frame Top", new Vector3(WindowLeft - 0.05f, WindowTop, Half - 0.03f), new Vector3(WindowRight + 0.05f, WindowTop + 0.06f, Half), wood, 1.2f, false);
        Box("Window Frame Left", new Vector3(WindowLeft - 0.06f, WindowBottom, Half - 0.03f), new Vector3(WindowLeft, WindowTop, Half), wood, 1.2f, false);
        Box("Window Frame Right", new Vector3(WindowRight, WindowBottom, Half - 0.03f), new Vector3(WindowRight + 0.06f, WindowTop, Half), wood, 1.2f, false);

        // Back wall with the door.
        Box("Back Wall (left of door)", new Vector3(-o, 0f, -o), new Vector3(-DoorHalf, Ceiling, -Half), wall, 1.6f);
        Box("Back Wall (right of door)", new Vector3(DoorHalf, 0f, -o), new Vector3(o, Ceiling, -Half), wall, 1.6f);
        Box("Back Wall (above door)", new Vector3(-DoorHalf, DoorTop, -o), new Vector3(DoorHalf, Ceiling, -Half), wall, 1.6f);
        Box("Door Frame Top", new Vector3(-DoorHalf - 0.06f, DoorTop, -Half), new Vector3(DoorHalf + 0.06f, DoorTop + 0.07f, -Half + 0.03f), wood, 1.2f, false);
        Box("Door Frame Left", new Vector3(-DoorHalf - 0.06f, 0f, -Half), new Vector3(-DoorHalf, DoorTop, -Half + 0.03f), wood, 1.2f, false);
        Box("Door Frame Right", new Vector3(DoorHalf, 0f, -Half), new Vector3(DoorHalf + 0.06f, DoorTop, -Half + 0.03f), wood, 1.2f, false);
        if (!options.closableDoor)
            Box("Door (open)", new Vector3(DoorHalf - 0.07f, 0f, -o - 1.02f), new Vector3(DoorHalf - 0.02f, DoorTop - 0.02f, -o), wood, 1.2f);

        // Left wall with the opening to the side hall.
        Box("Left Wall (front)", new Vector3(-o, 0f, HallHalf), new Vector3(-Half, Ceiling, o), wall, 1.6f);
        Box("Left Wall (back)", new Vector3(-o, 0f, -o), new Vector3(-Half, Ceiling, -HallHalf), wall, 1.6f);
        Box("Left Wall (above opening)", new Vector3(-o, DoorTop, -HallHalf), new Vector3(-Half, Ceiling, HallHalf), wall, 1.6f);

        // Right wall: solid, with lockers and the employee of the month.
        Box("Right Wall", new Vector3(Half, 0f, -o), new Vector3(o, Ceiling, o), wall, 1.6f);
        for (int i = 0; i < 4; i++)
            Box($"Locker {i + 1}", new Vector3(Half - 0.45f, 0f, -2.2f + i * 0.42f), new Vector3(Half, 1.95f, -2.2f + i * 0.42f + 0.4f), metal, 1.6f);
        Box("Employee of the Month", new Vector3(Half - 0.02f, 1.15f, 0.55f), new Vector3(Half, 1.95f, 1.15f), mats["Enchanted Poster"], 0f, false);
        Box("Filing Cabinet", new Vector3(Half - 0.5f, 0f, 1.6f), new Vector3(Half, 1.25f, 2.3f), metal, 1.6f);

        // Desk under the window. Its front edge sits on the Roost's window rail, so the real rail is the desk.
        Box("Desk Top", new Vector3(-1.7f, 0.93f, 1.43f), new Vector3(1.7f, 0.965f, 2.3f), wood, 1.2f);
        Box("Desk Back", new Vector3(-1.7f, 0f, 2.25f), new Vector3(1.7f, 0.93f, 2.3f), wood, 1.2f);
        Box("Desk Side (left)", new Vector3(-1.7f, 0f, 1.5f), new Vector3(-1.64f, 0.93f, 2.25f), wood, 1.2f);
        Box("Desk Side (right)", new Vector3(1.64f, 0f, 1.5f), new Vector3(1.7f, 0.93f, 2.25f), wood, 1.2f);
        Box("Drawers", new Vector3(1.0f, 0.05f, 1.55f), new Vector3(1.64f, 0.9f, 2.25f), wood, 1.2f);

        // Props (no colliders, so they never block the flashlight's line of sight).
        Transform props = Group("Desk Props", p);
        if (!options.securityDesk)
        {
            Box("Monitor", new Vector3(0.6f, 1.02f, 1.97f), new Vector3(1.2f, 1.42f, 2.04f), mats["Dark Plastic"], 1f, false, props);
            Box("Monitor Screen", new Vector3(0.63f, 1.05f, 1.962f), new Vector3(1.17f, 1.39f, 1.97f), mats["Screen"], 1f, false, props);
            Box("Monitor Stand", new Vector3(0.85f, 0.965f, 1.98f), new Vector3(0.95f, 1.02f, 2.08f), mats["Dark Plastic"], 1f, false, props);
            Box("Keyboard", new Vector3(0.65f, 0.965f, 1.62f), new Vector3(1.13f, 0.985f, 1.78f), mats["Dark Plastic"], 1f, false, props);
        }
        Prim(PrimitiveType.Cylinder, "Lamp Base", props, new Vector3(-1.2f, 0.975f, 2.05f), Vector3.zero, new Vector3(0.16f, 0.01f, 0.16f), mats["Dark Plastic"]);
        Prim(PrimitiveType.Cylinder, "Lamp Pole", props, new Vector3(-1.2f, 1.2f, 2.05f), Vector3.zero, new Vector3(0.025f, 0.23f, 0.025f), mats["Dark Plastic"]);
        Prim(PrimitiveType.Cylinder, "Lamp Shade", props, new Vector3(-1.1f, 1.45f, 1.98f), new Vector3(-25f, 0f, -20f), new Vector3(0.2f, 0.07f, 0.2f), mats["Lamp Shade"]);
        world.glows.Add(Prim(PrimitiveType.Sphere, "Lamp Bulb", props, new Vector3(-1.07f, 1.41f, 1.96f), Vector3.zero, Vector3.one * 0.05f, mats["Lamp Bulb"]).GetComponent<Renderer>());
        Prim(PrimitiveType.Cylinder, "Coffee Mug", props, new Vector3(-0.35f, 1.01f, 1.7f), Vector3.zero, new Vector3(0.08f, 0.05f, 0.08f), mats["Flashlight Head"]);

        float tubeZ = options.skylight ? 1.3f : 0.3f; // with a skylight, the light tube moves in front of it
        world.glows.Add(Box("Ceiling Light Tube", new Vector3(-0.6f, Ceiling - 0.05f, tubeZ - 0.12f), new Vector3(0.6f, Ceiling, tubeZ + 0.12f), mats["Fluorescent Tube"], 1f, false, props).GetComponent<Renderer>());
        world.glows.Add(Box("Exit Sign", new Vector3(-0.25f, 2.22f, -Half + 0.03f), new Vector3(0.25f, 2.47f, -Half + 0.08f), mats["Exit Sign"], 0f, false, props).GetComponent<Renderer>());
    }

    static void BuildYard(Transform p)
    {
        parent = p;
        Material yard = mats["Enchanted Yard"], wood = mats["Enchanted Wood"], metal = mats["Enchanted Metal"];
        Box("Yard Ground", new Vector3(-18f, -0.1f, Half + Wall), new Vector3(18f, 0f, 24f), yard, 3f);
        Box("Building Wall (left)", new Vector3(-18f, 0f, Half), new Vector3(-Half - Wall, 4f, Half + Wall), mats["Enchanted Wall"], 1.6f);
        Box("Building Wall (right)", new Vector3(Half + Wall, 0f, Half), new Vector3(18f, 4f, Half + Wall), mats["Enchanted Wall"], 1.6f);

        // A wooden fence at the back of the yard.
        for (float x = -17f; x <= 17f; x += 2f)
            Box("Fence Post", new Vector3(x - 0.07f, 0f, 20f), new Vector3(x + 0.07f, 1.8f, 20.14f), wood, 1.2f);
        Box("Fence Rail (low)", new Vector3(-17.5f, 0.4f, 19.9f), new Vector3(17.5f, 0.75f, 20f), wood, 1.2f);
        Box("Fence Rail (high)", new Vector3(-17.5f, 1.1f, 19.9f), new Vector3(17.5f, 1.45f, 20f), wood, 1.2f);

        // Dead trees.
        Vector3[] trees = { new Vector3(-6.5f, 0f, 10f), new Vector3(7.5f, 0f, 7.5f), new Vector3(-10f, 0f, 16f), new Vector3(3.5f, 0f, 17.5f), new Vector3(11f, 0f, 15f) };
        for (int i = 0; i < trees.Length; i++)
        {
            Transform tree = Group($"Dead Tree {i + 1}", p);
            tree.position = trees[i];
            float h = 3.5f + i % 3;
            Prim(PrimitiveType.Cylinder, "Trunk", tree, new Vector3(0f, h * 0.5f, 0f), Vector3.zero, new Vector3(0.3f, h * 0.5f, 0.3f), wood, true);
            for (int b = 0; b < 3; b++)
            {
                float y = h * (0.55f + 0.15f * b), turn = i * 70f + b * 130f;
                Quaternion dir = Quaternion.Euler(0f, turn, 0f);
                Prim(PrimitiveType.Cylinder, "Branch", tree, new Vector3(0f, y, 0f) + dir * new Vector3(0.45f, 0.35f, 0f),
                     (dir * Quaternion.Euler(0f, 0f, -50f)).eulerAngles, new Vector3(0.09f, 0.6f, 0.09f), wood);
            }
        }

        // A yard lamp post near his path, so you can just make him out out there (its light is in Lights).
        Prim(PrimitiveType.Cylinder, "Lamp Post", p, new Vector3(YardLamp.x, 1.6f, YardLamp.z), Vector3.zero, new Vector3(0.12f, 1.6f, 0.12f), metal, true);
        Prim(PrimitiveType.Sphere, "Lamp Post Bulb", p, new Vector3(YardLamp.x, 3.25f, YardLamp.z), Vector3.zero, Vector3.one * 0.3f, mats["Lamp Bulb"]);
    }

    static void BuildHallway(Transform p, WorldOptions options)
    {
        parent = p;
        Material wall = mats["Enchanted Wall"];
        float o = Half + Wall, end = -11f;
        Box("Hallway Floor", new Vector3(-1.4f, -0.1f, end - 0.2f), new Vector3(1.4f, 0f, -o), mats["Enchanted Floor"], 2f);
        Box("Hallway Ceiling", new Vector3(-1.4f, Ceiling, end - 0.2f), new Vector3(1.4f, Ceiling + 0.1f, -o), mats["Enchanted Ceiling"], 2.4f);
        Box("Hallway Wall (left)", new Vector3(-1.4f, 0f, end), new Vector3(-1.2f, Ceiling, -o), wall, 1.6f);
        if (!options.alcove)
            Box("Hallway Wall (right)", new Vector3(1.2f, 0f, end), new Vector3(1.4f, Ceiling, -o), wall, 1.6f);
        else
        {
            // An opening near the far end of the right wall (Five Nights builds the room behind it).
            Box("Hallway Wall (right, far)", new Vector3(1.2f, 0f, end), new Vector3(1.4f, Ceiling, -10.6f), wall, 1.6f);
            Box("Hallway Wall (right)", new Vector3(1.2f, 0f, -9.2f), new Vector3(1.4f, Ceiling, -o), wall, 1.6f);
            Box("Hallway Wall (above opening)", new Vector3(1.2f, DoorTop, -10.6f), new Vector3(1.4f, Ceiling, -9.2f), wall, 1.6f);
        }
        Box("Hallway End", new Vector3(-1.4f, 0f, end - 0.2f), new Vector3(1.4f, Ceiling, end), wall, 1.6f);
        for (int i = 0; i < 3; i++)
            Box($"Hallway Locker {i + 1}", new Vector3(-1.2f, 0f, -6.6f + i * 0.42f), new Vector3(-0.85f, 1.95f, -6.6f + i * 0.42f + 0.4f), mats["Enchanted Metal"], 1.6f);
        Box("Hallway Poster", new Vector3(1.18f, 1.1f, -8.6f), new Vector3(1.2f, 1.9f, -8f), mats["Enchanted Poster"], 0f, false);
    }

    static void BuildSideHall(Transform p)
    {
        parent = p;
        Material wall = mats["Enchanted Wall"];
        float o = Half + Wall, end = -11f;
        Box("Side Hall Floor", new Vector3(end - 0.2f, -0.1f, -1.4f), new Vector3(-o, 0f, 1.4f), mats["Enchanted Floor"], 2f);
        Box("Side Hall Ceiling", new Vector3(end - 0.2f, Ceiling, -1.4f), new Vector3(-o, Ceiling + 0.1f, 1.4f), mats["Enchanted Ceiling"], 2.4f);
        Box("Side Hall Wall (front)", new Vector3(end, 0f, 1.2f), new Vector3(-o, Ceiling, 1.4f), wall, 1.6f);
        Box("Side Hall Wall (back)", new Vector3(end, 0f, -1.4f), new Vector3(-o, Ceiling, -1.2f), wall, 1.6f);
        Box("Side Hall End", new Vector3(end - 0.2f, 0f, -1.4f), new Vector3(end, Ceiling, 1.4f), wall, 1.6f);
        Box("Side Hall Bench", new Vector3(-8.4f, 0f, 0.8f), new Vector3(-7f, 0.45f, 1.2f), mats["Enchanted Wood"], 1.2f);
        Box("Side Hall Poster", new Vector3(-6.3f, 1.1f, -1.2f), new Vector3(-5.7f, 1.9f, -1.18f), mats["Enchanted Poster"], 0f, false);
    }

    static void BuildRails()
    {
        string path = AssetDatabase.GUIDToAssetPath(RoostRailsGuid);
        GameObject prefab = string.IsNullOrEmpty(path) ? null : AssetDatabase.LoadAssetAtPath<GameObject>(path);
        if (prefab != null)
        {
            var rails = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            rails.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
            return;
        }
        // No Reality Roost package: stand-in rails with the same size, so the layout still makes sense.
        Debug.LogWarning("Sahur Night: Roost Rails prefab not found, adding stand-in rails.");
        parent = Group("Roost Rails (stand-in)");
        Material rail = Lit("Stand-in Rail", new Color(0.5f, 0.5f, 0.55f), 0.8f, 0.6f);
        Box("Window Rail", new Vector3(-0.9144f, 0.94f, 1.397f), new Vector3(0.9144f, 0.99f, 1.447f), rail, 1f);
        Box("Door Rail", new Vector3(-0.9144f, 0.94f, -1.4224f), new Vector3(0.9144f, 0.99f, -1.3716f), rail, 1f);
        Box("Left Rail", new Vector3(-0.9652f, 0.94f, -1.4224f), new Vector3(-0.9144f, 0.99f, 1.447f), rail, 1f);
        Box("Right Rail", new Vector3(0.9144f, 0.94f, -1.4224f), new Vector3(0.9652f, 0.99f, 1.447f), rail, 1f);
    }

    // ---------- The enchanted flashlight ----------

    internal static EnchantedFlashlight BuildFlashlight()
    {
        // A little shelf on the right rail holds it.
        parent = Group("Flashlight Shelf");
        Box("Shelf", new Vector3(0.83f, 0.99f, 0.05f), new Vector3(1.05f, 1.01f, 0.5f), mats["Enchanted Wood"], 1.2f);

        var root = new GameObject("Enchanted Flashlight");
        root.transform.SetPositionAndRotation(new Vector3(0.94f, 1.045f, 0.24f), Quaternion.identity);
        Transform t = root.transform;

        // Model: points along its blue (Z) axis.
        Prim(PrimitiveType.Cylinder, "Handle", t, new Vector3(0f, 0f, -0.02f), new Vector3(90f, 0f, 0f), new Vector3(0.04f, 0.085f, 0.04f), mats["Flashlight Body"]);
        Prim(PrimitiveType.Cylinder, "Head", t, new Vector3(0f, 0f, 0.09f), new Vector3(90f, 0f, 0f), new Vector3(0.062f, 0.03f, 0.062f), mats["Flashlight Head"]);
        Prim(PrimitiveType.Cylinder, "Tail Cap", t, new Vector3(0f, 0f, -0.108f), new Vector3(90f, 0f, 0f), new Vector3(0.044f, 0.006f, 0.044f), mats["Flashlight Head"]);
        GameObject glass = Prim(PrimitiveType.Cylinder, "Lens Glass", t, new Vector3(0f, 0f, 0.1205f), new Vector3(90f, 0f, 0f), new Vector3(0.052f, 0.002f, 0.052f), mats["Flashlight Glow"]);
        var bars = new Renderer[3];
        for (int i = 0; i < 3; i++)
            bars[i] = Prim(PrimitiveType.Cube, $"Battery Bar {i + 1}", t, new Vector3(0f, 0.0205f, -0.075f + i * 0.022f), Vector3.zero,
                           new Vector3(0.012f, 0.004f, 0.016f), mats["Flashlight Glow"]).GetComponent<Renderer>();
        foreach (Renderer r in root.GetComponentsInChildren<Renderer>()) r.shadowCastingMode = ShadowCastingMode.Off;

        var lens = new GameObject("Lens (beam origin)").transform;
        lens.SetParent(t, false);
        lens.localPosition = new Vector3(0f, 0f, 0.123f);
        var grip = new GameObject("Grip").transform;
        grip.SetParent(t, false);
        grip.localPosition = new Vector3(0f, 0f, -0.03f);

        Light spot = MakeLight("Beam", LightType.Spot, lens.TransformPoint(0f, 0f, 0.005f), lens.rotation, new Color(1f, 0.95f, 0.82f), 12f, 18f, 34f, LightShadows.Soft, lens);
        spot.renderMode = LightRenderMode.ForcePixel;
        spot.shadowNearPlane = 0.1f;
        spot.enabled = false;

        var collider = root.AddComponent<BoxCollider>();
        collider.center = new Vector3(0f, 0f, 0.005f);
        collider.size = new Vector3(0.065f, 0.065f, 0.245f);
        var body = root.AddComponent<Rigidbody>();
        body.mass = 0.4f;
        body.interpolation = RigidbodyInterpolation.Interpolate;
        body.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;

        var grab = root.AddComponent<XRGrabInteractable>();
        grab.movementType = XRBaseInteractable.MovementType.Instantaneous;
        grab.attachTransform = grip;
        grab.useDynamicAttach = false;
        grab.throwOnDetach = false;

        var flashlight = root.AddComponent<EnchantedFlashlight>();
        flashlight.lens = lens;
        flashlight.spotLight = spot;
        flashlight.batteryBars = bars;
        flashlight.lensGlass = glass.GetComponent<Renderer>();
        return flashlight;
    }

    // ---------- Tung Tung Tung Sahur ----------

    static SahurStalker BuildSahur(GameObject model, EnchantedFlashlight flashlight)
    {
        // His paths: stations from far away to right outside the office.
        Transform pathRoot = Group("Sahur Paths");
        SahurStalker.Path MakePath(string name, params Vector3[] points)
        {
            return new SahurStalker.Path { name = name, stations = Stations(name, pathRoot, points) };
        }
        float o = Half + Wall;
        SahurStalker.Path window = MakePath("Window Path (yard)", new Vector3(-4f, 0f, 15.5f), new Vector3(2.4f, 0f, 9.5f), new Vector3(-0.7f, 0f, 5.8f), new Vector3(-0.5f, 0f, o + 0.75f));
        SahurStalker.Path door = MakePath("Door Path (hallway)", new Vector3(0.3f, 0f, -10.2f), new Vector3(-0.35f, 0f, -7.6f), new Vector3(0.4f, 0f, -5.2f), new Vector3(0f, 0f, -o - 0.75f));
        SahurStalker.Path hall = MakePath("Hall Path (left)", new Vector3(-10.2f, 0f, -0.3f), new Vector3(-7.6f, 0f, 0.45f), new Vector3(-5.2f, 0f, -0.35f), new Vector3(-o - 0.75f, 0f, 0f));

        var tung = (GameObject)PrefabUtility.InstantiatePrefab(model);
        tung.name = "Tung Tung Tung Sahur";
        tung.transform.position = window.stations[0].position;
        ApplyTungMaterials(tung);

        // An upright capsule to block things, sized from the mesh (the model itself is rotated and scaled by 100).
        Bounds bounds = new Bounds(tung.transform.position, Vector3.zero);
        foreach (Renderer r in tung.GetComponentsInChildren<Renderer>()) bounds.Encapsulate(r.bounds);
        var hitBox = new GameObject("Hit Box");
        hitBox.transform.SetParent(tung.transform, false);
        hitBox.transform.rotation = Quaternion.identity;
        hitBox.transform.position = tung.transform.position;
        hitBox.transform.localScale = Vector3.one / tung.transform.lossyScale.x;
        var capsule = hitBox.AddComponent<CapsuleCollider>();
        capsule.direction = 1;
        capsule.height = bounds.size.y;
        capsule.radius = Mathf.Min(0.3f, bounds.size.y * 0.5f);
        capsule.center = bounds.center - tung.transform.position;

        var dance = tung.AddComponent<TungDance>();
        dance.move = TungDance.Move.Dance;
        dance.enabled = false; // the game turns this on when he catches you

        var stalker = tung.AddComponent<SahurStalker>();
        stalker.paths = new[] { window, door, hall };
        stalker.flashlight = flashlight;
        return stalker;
    }

    // Enchanted materials: his body cracks open and glows in the beam, his eyes turn red.
    internal static void ApplyTungMaterials(GameObject tung)
    {
        foreach (Renderer r in tung.GetComponentsInChildren<Renderer>())
        {
            Material[] slots = r.sharedMaterials;
            int body = System.Array.FindIndex(slots, m => m != null && m.mainTexture != null); // the one with the wood texture
            if (body < 0) body = 0;
            for (int i = 0; i < slots.Length; i++)
            {
                if (i != body && slots[i] != null) // the eyes keep their color outside the beam
                    mats["Enchanted Sahur Eyes"].SetColor("_BaseColor", slots[i].HasProperty("_BaseColor") ? slots[i].GetColor("_BaseColor") : slots[i].color);
                slots[i] = i == body ? mats["Enchanted Sahur"] : mats["Enchanted Sahur Eyes"];
            }
            r.sharedMaterials = slots;
        }
    }

    // Empty marker objects along a path (stations from far away to right outside the office).
    internal static Transform[] Stations(string name, Transform under, params Vector3[] points)
    {
        Transform p = Group(name, under);
        var stations = new Transform[points.Length];
        for (int i = 0; i < points.Length; i++)
        {
            stations[i] = new GameObject(i == points.Length - 1 ? "Station (last)" : $"Station {i + 1}").transform;
            stations[i].SetParent(p, false);
            stations[i].position = points[i];
        }
        return stations;
    }

    // ---------- Post-processing, test rig, build lists ----------

    static void BuildPostProcessing()
    {
        string path = Generated + "/Sahur Night Post Processing.asset";
        // Both scenes share this profile: reuse it if it's already there (so the other scene keeps its link).
        var profile = AssetDatabase.LoadAssetAtPath<VolumeProfile>(path);
        if (profile != null && profile.components.Count > 0)
        {
            AddVolume(profile);
            return;
        }
        AssetDatabase.DeleteAsset(path);
        profile = ScriptableObject.CreateInstance<VolumeProfile>();
        AssetDatabase.CreateAsset(profile, path);

        var bloom = profile.Add<Bloom>(true);
        bloom.threshold.Override(0.85f);
        bloom.intensity.Override(0.9f);
        bloom.scatter.Override(0.65f);
        var tone = profile.Add<Tonemapping>(true);
        tone.mode.Override(TonemappingMode.Neutral);
        var color = profile.Add<ColorAdjustments>(true);
        color.postExposure.Override(0.3f);
        color.saturation.Override(-12f);
        color.contrast.Override(8f);
        var vignette = profile.Add<Vignette>(true);
        vignette.intensity.Override(0.32f);
        vignette.smoothness.Override(0.5f);
        var grain = profile.Add<FilmGrain>(true);
        grain.type.Override(FilmGrainLookup.Medium3);
        grain.intensity.Override(0.3f);
        foreach (VolumeComponent c in profile.components)
        {
            c.hideFlags = HideFlags.HideInInspector | HideFlags.HideInHierarchy;
            AssetDatabase.AddObjectToAsset(c, profile);
        }
        EditorUtility.SetDirty(profile);
        AddVolume(profile);
    }

    static void AddVolume(VolumeProfile profile)
    {
        var go = new GameObject("Post Processing");
        var volume = go.AddComponent<Volume>();
        volume.isGlobal = true;
        volume.priority = 1f;
        volume.sharedProfile = profile;
    }

    // An XR rig so you can press Play on this scene directly. When RR_Boot loads the scene, the Roost brings
    // its own rig and this one removes itself (TestRigGuard).
    internal static void BuildTestRig()
    {
        string path = AssetDatabase.FindAssets("XR Origin (XR Rig) t:Prefab").Select(AssetDatabase.GUIDToAssetPath)
            .OrderByDescending(p => p.Contains("Starter Assets")).FirstOrDefault(p => Path.GetFileNameWithoutExtension(p) == "XR Origin (XR Rig)");
        GameObject rig;
        if (path != null)
        {
            rig = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(path));
            rig.name = "XR Origin (test rig, removed in the Roost)";
        }
        else
        {
            // No XRI starter rig: just a camera (desktop testing still works).
            rig = new GameObject("Test Camera (removed in the Roost)");
            var cam = new GameObject("Main Camera") { tag = "MainCamera" };
            cam.transform.SetParent(rig.transform, false);
            cam.transform.localPosition = new Vector3(0f, 1.6f, 0f);
            cam.AddComponent<Camera>().nearClipPlane = 0.02f;
            cam.AddComponent<AudioListener>();
            rig.AddComponent<Unity.XR.CoreUtils.XROrigin>().Camera = cam.GetComponent<Camera>();
        }
        rig.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
        rig.AddComponent<TestRigGuard>();
        foreach (Camera cam in rig.GetComponentsInChildren<Camera>(true))
        {
            var data = cam.GetUniversalAdditionalCameraData();
            data.renderPostProcessing = true;
            cam.clearFlags = CameraClearFlags.Skybox;
        }
    }

    // Add the scene to Build Settings and to the Reality Roost build profile(s), so the Roost menu lists it.
    internal static void AddToBuildLists(string scenePath)
    {
        List<EditorBuildSettingsScene> scenes = EditorBuildSettings.scenes.ToList();
        if (!scenes.Any(s => s.path == scenePath))
        {
            scenes.Add(new EditorBuildSettingsScene(scenePath, true));
            EditorBuildSettings.scenes = scenes.ToArray();
        }
        foreach (string guid in AssetDatabase.FindAssets("t:BuildProfile"))
        {
            var profile = AssetDatabase.LoadAssetAtPath<BuildProfile>(AssetDatabase.GUIDToAssetPath(guid));
            if (profile == null || profile.scenes == null) continue;
            List<EditorBuildSettingsScene> list = profile.scenes.ToList();
            if (!list.Any(s => s.path.EndsWith("RR_Boot.unity")) || list.Any(s => s.path == scenePath)) continue;
            list.Add(new EditorBuildSettingsScene(scenePath, true));
            profile.scenes = list.ToArray();
            EditorUtility.SetDirty(profile);
            Debug.Log($"Sahur Night: added {Path.GetFileNameWithoutExtension(scenePath)} to the {profile.name} build profile (it's #{list.Count - 1} in the Roost menu).");
        }
    }

    // ---------- Helpers ----------

    internal static Vector3 MoonDirection()
    {
        float e = SahurNightTextures.MoonElevation * Mathf.Deg2Rad;
        return new Vector3(0f, Mathf.Sin(e), Mathf.Cos(e));
    }

    internal static Transform Group(string name, Transform under = null)
    {
        var t = new GameObject(name).transform;
        t.SetParent(under, false);
        return t;
    }

    internal static Light MakeLight(string name, LightType type, Vector3 position, Quaternion rotation, Color color, float intensity,
                                    float range, float spotAngle, LightShadows shadows, Transform under)
    {
        var go = new GameObject(name);
        go.transform.SetParent(under, false);
        go.transform.SetPositionAndRotation(position, rotation);
        var light = go.AddComponent<Light>();
        light.type = type;
        light.color = color;
        light.intensity = intensity;
        if (type != LightType.Directional) light.range = range;
        if (type == LightType.Spot) { light.spotAngle = spotAngle; light.innerSpotAngle = spotAngle * 0.6f; }
        light.shadows = shadows;
        return light;
    }

    internal static TextMeshPro Text(string name, string text, Vector3 position, Quaternion rotation, float size, Color color, Vector2 box, Transform under)
    {
        var go = new GameObject(name);
        go.transform.SetParent(under, false);
        go.transform.SetPositionAndRotation(position, rotation);
        var tmp = go.AddComponent<TextMeshPro>();
        tmp.rectTransform.sizeDelta = box;
        tmp.text = text;
        tmp.fontSize = size;
        tmp.color = color;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.textWrappingMode = TextWrappingModes.Normal;
        return tmp;
    }

    internal static GameObject Prim(PrimitiveType type, string name, Transform under, Vector3 localPosition, Vector3 localEuler, Vector3 scale,
                                    Material material, bool keepCollider = false)
    {
        GameObject go = GameObject.CreatePrimitive(type);
        go.name = name;
        go.transform.SetParent(under, false);
        go.transform.localPosition = localPosition;
        go.transform.localEulerAngles = localEuler;
        go.transform.localScale = scale;
        go.GetComponent<Renderer>().sharedMaterial = material;
        if (!keepCollider) Object.DestroyImmediate(go.GetComponent<Collider>());
        return go;
    }

    // An axis-aligned box from `min` to `max`. Its UVs follow world position (one texture repeat every `tile`
    // meters), so textures line up across neighboring boxes and never stretch. tile = 0 fits the texture once per face.
    internal static GameObject Box(string name, Vector3 min, Vector3 max, Material material, float tile, bool collide = true, Transform under = null)
    {
        var go = new GameObject(name);
        go.transform.SetParent(under != null ? under : parent, false);
        Vector3 center = (min + max) * 0.5f, size = max - min;
        go.transform.position = center;
        go.AddComponent<MeshFilter>().sharedMesh = BoxMesh(name, center, size, tile);
        go.AddComponent<MeshRenderer>().sharedMaterial = material;
        if (collide) go.AddComponent<BoxCollider>().size = size;
        return go;
    }

    static Mesh BoxMesh(string name, Vector3 center, Vector3 size, float tile)
    {
        var vertices = new List<Vector3>();
        var normals = new List<Vector3>();
        var uvs = new List<Vector2>();
        var triangles = new List<int>();
        Vector3 half = size * 0.5f;
        foreach (Vector3 n in new[] { Vector3.right, Vector3.left, Vector3.up, Vector3.down, Vector3.forward, Vector3.back })
        {
            // Texture axes as seen by someone looking at this face: u to their right, v up.
            Vector3 up = Mathf.Abs(n.y) > 0.5f ? Vector3.forward : Vector3.up;
            Vector3 right = Vector3.Cross(n, up);
            float er = Mathf.Abs(Vector3.Dot(right, half)), eu = Mathf.Abs(Vector3.Dot(up, half));
            int first = vertices.Count;
            foreach (Vector2 corner in new[] { new Vector2(-1f, -1f), new Vector2(-1f, 1f), new Vector2(1f, 1f), new Vector2(1f, -1f) })
            {
                Vector3 local = Vector3.Scale(n, half) + right * (corner.x * er) + up * (corner.y * eu);
                vertices.Add(local);
                normals.Add(n);
                Vector3 world = center + local;
                uvs.Add(tile > 0f ? new Vector2(Vector3.Dot(world, right), Vector3.Dot(world, up)) / tile
                                  : new Vector2(corner.x * 0.5f + 0.5f, corner.y * 0.5f + 0.5f));
            }
            triangles.AddRange(new[] { first, first + 1, first + 2, first, first + 2, first + 3 });
        }
        var mesh = new Mesh { name = name };
        mesh.SetVertices(vertices);
        mesh.SetNormals(normals);
        mesh.SetUVs(0, uvs);
        mesh.SetTriangles(triangles, 0);
        mesh.RecalculateBounds();
        mesh.RecalculateTangents();
        return mesh;
    }
}
