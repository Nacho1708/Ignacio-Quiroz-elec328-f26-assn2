using System.Collections.Generic;
using System.IO;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.UI;
using UnityEngine.XR.Interaction.Toolkit.UI;
using B = SahurNightBuilder;

// Tools > Sahur Night > Build Five Nights at Tung's
// The same office, now with a skylight, a door you can shut, a security desk with four cameras, button panels on
// the Roost rails, a desert outside the window and Tralalero's beach hut down the hallway, plus all six characters,
// the menus (Nights 1-5 and Custom Night) and the game. Safe to run again: it rebuilds the scene from scratch.
public static class FiveNightsBuilder
{
    const string ScenePath = B.Folder + "/FiveNightsAtTung.unity";
    const string GhostShaderPath = B.Folder + "/Shaders/EnchantedGhost.shadergraph";
    static Transform parent;

    [MenuItem("Tools/Sahur Night/Build Five Nights at Tung's")]
    public static void BuildScene()
    {
        if (!Application.isBatchMode)
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            if (File.Exists(ScenePath) && !EditorUtility.DisplayDialog("Five Nights at Tung's",
                    "Rebuild FiveNightsAtTung.unity from scratch? Changes you made by hand in that scene will be replaced.", "Rebuild", "Cancel"))
                return;
        }
        var ghost = AssetDatabase.LoadAssetAtPath<Shader>(GhostShaderPath);
        if (ghost == null) { Debug.LogError($"Five Nights: can't find the ghost shader graph at {GhostShaderPath}."); return; }

        try
        {
            if (!B.Prepare("Five Nights at Tung's", out GameObject tungModel)) return;
            EditorUtility.DisplayProgressBar("Five Nights at Tung's", "Painting the characters...", 0.5f);
            string textures = B.Generated + "/Textures/Characters";
            B.MakeFolder(textures);
            BrainrotModels.MakeMaterials(textures, ghost);

            EditorUtility.DisplayProgressBar("Five Nights at Tung's", "Building the office...", 0.75f);
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            Build(tungModel);
            EditorSceneManager.SaveScene(scene, ScenePath);
            B.AddToBuildLists(ScenePath);
            AssetDatabase.SaveAssets();
            Debug.Log("Five Nights at Tung's: built " + ScenePath + ". Press Play to test it here, or load it from RR_Boot in the Roost.");
        }
        finally
        {
            EditorUtility.ClearProgressBar();
        }
    }

    static void Build(GameObject tungModel)
    {
        var options = new B.WorldOptions { skylight = true, closableDoor = true, securityDesk = true, alcove = true };
        B.World world = B.BuildWorld(options);
        var mats = B.Mats;

        Transform extras = B.Group("Five Nights Extras");
        BuildDesert(B.Group("Desert (Lirili's side of the yard)", extras));
        BuildBeachHut(B.Group("Beach Hut (Tralalero's hideout)", extras));
        OfficeDoor door = BuildDoor(extras);
        EnchantedFlashlight flashlight = B.BuildFlashlight();

        // The security desk: a monitor showing one of four cameras.
        SecurityCameras cameras = BuildSecurity(extras, out TextMeshPro monitorClock, out TextMeshPro monitorPower);

        // Big clock and power display on the right wall.
        Transform displays = B.Group("Clock and Power", extras);
        TextMeshPro wallClock = B.Text("Wall Clock", "12 AM", new Vector3(B.Half - 0.01f, 2.35f, -0.9f), Quaternion.Euler(0f, 90f, 0f),
            2.6f, new Color(1f, 0.25f, 0.2f), new Vector2(1.4f, 0.4f), displays);
        TextMeshPro wallPower = B.Text("Wall Power", "POWER 100%", new Vector3(B.Half - 0.01f, 1.98f, -0.9f), Quaternion.Euler(0f, 90f, 0f),
            1.2f, new Color(0.9f, 0.9f, 0.85f), new Vector2(1.4f, 0.4f), displays);

        // The characters.
        Transform paths = B.Group("Brainrot Paths");
        float o = B.Half + B.Wall;
        TungEnemy tung = BrainrotModels.BuildTung(tungModel);
        tung.paths = new[]
        {
            Path("Tung: Window (yard)", paths, false, 0, new Vector3(-4f, 0f, 15.5f), new Vector3(2.4f, 0f, 9.5f), new Vector3(-0.7f, 0f, 5.8f), new Vector3(-0.45f, 0f, o + 0.75f)),
            Path("Tung: Door (hallway)", paths, true, 2, new Vector3(0.3f, 0f, -10.2f), new Vector3(-0.35f, 0f, -7.6f), new Vector3(0.4f, 0f, -5.2f), new Vector3(0f, 0f, -o - 0.75f)),
            Path("Tung: Side hall", paths, false, 3, new Vector3(-10.2f, 0f, -0.3f), new Vector3(-7.6f, 0f, 0.45f), new Vector3(-5.2f, 0f, -0.35f), new Vector3(-o - 0.75f, 0f, 0f)),
        };
        BallerinaEnemy ballerina = BrainrotModels.BuildBallerina();
        ballerina.paths = new[]
        {
            Path("Ballerina: Window (yard)", paths, false, 0, new Vector3(-2.5f, 0f, 14.5f), new Vector3(1.2f, 0f, 10.2f), new Vector3(0.6f, 0f, 6.2f), new Vector3(0.3f, 0f, o + 0.85f)),
            Path("Ballerina: Door (hallway)", paths, true, 2, new Vector3(-0.5f, 0f, -9.7f), new Vector3(0.45f, 0f, -7f), new Vector3(-0.45f, 0f, -4.8f), new Vector3(0.35f, 0f, -o - 0.8f)),
            Path("Ballerina: Side hall", paths, false, 3, new Vector3(-9.6f, 0f, 0.5f), new Vector3(-7f, 0f, -0.5f), new Vector3(-4.7f, 0f, 0.45f), new Vector3(-o - 0.8f, 0f, -0.35f)),
        };
        LiriliEnemy lirili = BrainrotModels.BuildLirili();
        lirili.paths = new[]
        {
            Path("Lirili: Desert to the window", paths, false, 0, new Vector3(11f, 0f, 16f), new Vector3(6f, 0f, 10.5f), new Vector3(2.4f, 0f, 6.5f), new Vector3(-1.05f, 0f, o + 0.9f)),
        };
        TralaleroEnemy tralalero = BrainrotModels.BuildTralalero();
        Transform sharkSpots = B.Group("Tralalero: Spots", paths);
        tralalero.hideSpot = Spot("Hiding (beach hut)", sharkSpots, new Vector3(2.5f, 0f, -9.9f), Vector3.left);
        tralalero.peekSpot = Spot("Peeking", sharkSpots, new Vector3(1.3f, 0f, -9.95f), new Vector3(-0.3f, 0f, 1f));
        tralalero.readySpot = Spot("Ready (in the hallway)", sharkSpots, new Vector3(0.35f, 0f, -8.6f), Vector3.forward);
        tralalero.doorSpot = Spot("At the door", sharkSpots, new Vector3(0f, 0f, -o - 0.45f), Vector3.forward);
        PatapimEnemy patapim = BrainrotModels.BuildPatapim();
        Transform lanes = B.Group("Patapim: Lanes (in the office)", paths);
        patapim.lanes = new[]
        {
            Lane("Back left", lanes, new Vector3(-2.0f, 0f, -2.05f), new Vector3(-1.5f, 0f, -1.85f), new Vector3(-1.15f, 0f, -1.65f)),
            Lane("Back right", lanes, new Vector3(1.55f, 0f, -2.1f), new Vector3(1.35f, 0f, -1.85f), new Vector3(1.1f, 0f, -1.65f)),
            Lane("Left", lanes, new Vector3(-2.05f, 0f, 1.05f), new Vector3(-1.6f, 0f, 0.85f), new Vector3(-1.2f, 0f, 0.65f)),
            Lane("Right", lanes, new Vector3(2.05f, 0f, 0.95f), new Vector3(1.6f, 0f, 0.8f), new Vector3(1.2f, 0f, 0.72f)),
            Lane("Front left", lanes, new Vector3(-2.05f, 0f, 1.95f), new Vector3(-1.75f, 0f, 1.3f), new Vector3(-1.2f, 0f, 1.25f)),
        };
        BombardiroEnemy bombardiro = BrainrotModels.BuildBombardiro();
        bombardiro.skylight = new Vector3(0f, B.Ceiling + 0.15f, 0f);

        Brainrot[] cast = { tung, ballerina, lirili, tralalero, patapim, bombardiro };
        Transform castRoot = B.Group("Characters");
        foreach (Brainrot b in cast) b.transform.SetParent(castRoot, true);
        tung.transform.position = tung.paths[0].stations[0].position;
        ballerina.transform.position = ballerina.paths[0].stations[0].position;
        lirili.transform.position = lirili.paths[0].stations[0].position;
        tralalero.transform.SetPositionAndRotation(tralalero.hideSpot.position, tralalero.hideSpot.rotation);
        patapim.transform.position = patapim.lanes[0].far.position;
        bombardiro.transform.position = new Vector3(0f, 22f, 17f);

        // The game.
        var gameObject = new GameObject("Five Nights Game");
        var game = gameObject.AddComponent<NightsGame>();
        var menu = BuildMenus(game, out TextMeshProUGUI doorPower);
        game.flashlight = flashlight;
        game.door = door;
        game.cameras = cameras;
        game.menu = menu;
        game.cast = cast;
        game.clocks = new TMP_Text[] { wallClock, monitorClock };
        game.powerDisplays = new TMP_Text[] { wallPower, monitorPower, doorPower };
        game.roomLights = new[] { world.tube.GetComponent<FlickerLight>(), world.lamp.GetComponent<FlickerLight>() };
        game.officeLights = new[] { world.tube, world.lamp, world.exitGlow };
        game.officeGlows = world.glows.ToArray();
        game.moon = world.moon;
        game.sun = world.sun;
        game.dayLights = new[] { world.morning };
        game.doorway = Spot("Doorway (power out)", paths, new Vector3(0f, 0f, -o - 0.35f), Vector3.forward);
        game.confettiMaterial = BrainrotModels.Get("Confetti");
        var ambience = gameObject.AddComponent<AudioSource>();
        ambience.playOnAwake = false;
        ambience.spatialBlend = 0f;
        ambience.volume = 0.35f;
        game.ambience = ambience;
        gameObject.AddComponent<SixSevenPanic>().game = game;
        flashlight.sharedPower = true;

        B.BuildTestRig();
    }

    // ---------- Places ----------

    static BrainrotPath Path(string name, Transform under, bool throughDoor, int camera, params Vector3[] points)
    {
        return new BrainrotPath { name = name, stations = B.Stations(name, under, points), throughDoor = throughDoor, securityCamera = camera };
    }

    static Transform Spot(string name, Transform under, Vector3 position, Vector3 facing)
    {
        var t = new GameObject(name).transform;
        t.SetParent(under, false);
        t.SetPositionAndRotation(position, Quaternion.LookRotation(facing));
        return t;
    }

    static PatapimEnemy.Lane Lane(string name, Transform under, Vector3 far, Vector3 mid, Vector3 near)
    {
        Transform[] s = B.Stations("Lane: " + name, under, far, mid, near);
        return new PatapimEnemy.Lane { far = s[0], mid = s[1], near = s[2] };
    }

    static void BuildDesert(Transform p)
    {
        B.Parent = p;
        Material cactus = BrainrotModels.Get("Cactus");
        B.Box("Sand", new Vector3(4.5f, -0.05f, 5f), new Vector3(16f, 0.006f, 19.5f), BrainrotModels.Get("Sand"), 3f);
        Vector3[] spots = { new Vector3(13.5f, 0f, 11f), new Vector3(8.5f, 0f, 16.8f), new Vector3(14.5f, 0f, 17f), new Vector3(9.8f, 0f, 7.2f), new Vector3(5.2f, 0f, 14.2f), new Vector3(12.2f, 0f, 6f) };
        for (int i = 0; i < spots.Length; i++)
        {
            Transform c = B.Group($"Cactus {i + 1}", p);
            c.position = spots[i];
            float h = 1.8f + (i % 3) * 0.6f;
            B.Prim(PrimitiveType.Cylinder, "Trunk", c, new Vector3(0f, h * 0.5f, 0f), Vector3.zero, new Vector3(0.38f, h * 0.5f, 0.38f), cactus, true);
            B.Prim(PrimitiveType.Sphere, "Top", c, new Vector3(0f, h, 0f), Vector3.zero, Vector3.one * 0.38f, cactus);
            float side = i % 2 == 0 ? 1f : -1f;
            B.Prim(PrimitiveType.Cylinder, "Arm", c, new Vector3(0.3f * side, h * 0.55f, 0f), new Vector3(0f, 0f, 90f), new Vector3(0.2f, 0.15f, 0.2f), cactus);
            B.Prim(PrimitiveType.Cylinder, "Arm", c, new Vector3(0.45f * side, h * 0.55f + 0.3f, 0f), Vector3.zero, new Vector3(0.2f, 0.3f, 0.2f), cactus);
        }
        // A second lamp, so you can just make out Lirili coming across the sand.
        B.Prim(PrimitiveType.Cylinder, "Desert Lamp Post", p, new Vector3(8.5f, 1.6f, 12.5f), Vector3.zero, new Vector3(0.12f, 1.6f, 0.12f), B.Mats["Enchanted Metal"], true);
        B.Prim(PrimitiveType.Sphere, "Desert Lamp Bulb", p, new Vector3(8.5f, 3.25f, 12.5f), Vector3.zero, Vector3.one * 0.3f, B.Mats["Lamp Bulb"]);
        B.MakeLight("Desert Lamp", LightType.Point, new Vector3(8.5f, 3.1f, 12.5f), Quaternion.identity, new Color(1f, 0.65f, 0.3f), 1.8f, 11f, 0f, LightShadows.None, p)
            .gameObject.AddComponent<FlickerLight>().jitter = 0.08f;
    }

    static void BuildBeachHut(Transform p)
    {
        B.Parent = p;
        Material wall = B.Mats["Enchanted Wall"], hut = BrainrotModels.Get("Beach Hut");
        // A little room behind the opening in the hallway's right wall.
        B.Box("Hut Floor", new Vector3(1.4f, -0.1f, -11f), new Vector3(3.6f, 0.005f, -8.8f), BrainrotModels.Get("Sand"), 3f);
        B.Box("Hut Ceiling", new Vector3(1.4f, B.Ceiling, -11.2f), new Vector3(3.6f, B.Ceiling + 0.1f, -8.6f), B.Mats["Enchanted Ceiling"], 2.4f);
        B.Box("Hut Wall (back)", new Vector3(3.4f, 0f, -11.2f), new Vector3(3.6f, B.Ceiling, -8.6f), hut, 1.2f);
        B.Box("Hut Wall (near)", new Vector3(1.4f, 0f, -8.8f), new Vector3(3.4f, B.Ceiling, -8.6f), hut, 1.2f);
        B.Box("Hut Wall (far)", new Vector3(1.4f, 0f, -11.2f), new Vector3(3.4f, B.Ceiling, -11f), hut, 1.2f);
        B.Box("Hut Wall (hallway side, near)", new Vector3(1.2f, 0f, -9.2f), new Vector3(1.4f, B.Ceiling, -8.6f), wall, 1.6f);
        // Beach umbrella and towel.
        B.Prim(PrimitiveType.Cylinder, "Umbrella Pole", p, new Vector3(3f, 1f, -10.5f), Vector3.zero, new Vector3(0.04f, 1f, 0.04f), B.Mats["Enchanted Metal"]);
        B.Prim(PrimitiveType.Sphere, "Umbrella", p, new Vector3(3f, 2f, -10.5f), Vector3.zero, new Vector3(1.3f, 0.3f, 1.3f), hut);
        B.Box("Beach Towel", new Vector3(2f, 0.005f, -10.7f), new Vector3(2.8f, 0.015f, -9.3f), hut, 1f, false);
        B.Text("Beach Club Sign", "TRALALA\nBEACH CLUB", new Vector3(1.19f, 2.42f, -9.9f), Quaternion.Euler(0f, 90f, 0f), 0.8f, new Color(0.3f, 0.9f, 1f), new Vector2(1.4f, 0.5f), p);
        B.MakeLight("Hut Light", LightType.Point, new Vector3(2.4f, 2.4f, -9.9f), Quaternion.identity, new Color(0.4f, 0.8f, 1f), 0.6f, 3.5f, 0f, LightShadows.None, p);
    }

    // The door: a hinge on the right side of the doorway, with the door as its child. Closed = hinge at 0 degrees.
    static OfficeDoor BuildDoor(Transform under)
    {
        Transform root = B.Group("Office Door", under);
        Transform hinge = B.Group("Hinge", root);
        hinge.position = new Vector3(B.DoorHalf, 0f, -B.Half - B.Wall * 0.5f);
        B.Parent = hinge;
        float z = -B.Half - B.Wall * 0.5f;
        B.Box("Door", new Vector3(-B.DoorHalf + 0.005f, 0f, z - 0.03f), new Vector3(B.DoorHalf - 0.005f, B.DoorTop - 0.02f, z + 0.03f), B.Mats["Enchanted Wood"], 1.2f);
        B.Prim(PrimitiveType.Sphere, "Knob (inside)", hinge, new Vector3(-0.97f, 1f, 0.05f), Vector3.zero, Vector3.one * 0.07f, B.Mats["Enchanted Metal"]);
        B.Prim(PrimitiveType.Sphere, "Knob (outside)", hinge, new Vector3(-0.97f, 1f, -0.05f), Vector3.zero, Vector3.one * 0.07f, B.Mats["Enchanted Metal"]);
        var door = root.gameObject.AddComponent<OfficeDoor>();
        door.hinge = hinge;
        hinge.localRotation = Quaternion.Euler(0f, door.openAngle, 0f); // starts open
        return door;
    }

    static SecurityCameras BuildSecurity(Transform under, out TextMeshPro clock, out TextMeshPro power)
    {
        Transform desk = B.Group("Security Desk", under);
        B.Parent = desk;
        Material plastic = B.Mats["Dark Plastic"];
        B.Box("Security Monitor", new Vector3(0.55f, 0.98f, 1.9f), new Vector3(1.25f, 1.52f, 2.2f), plastic, 1f, false);
        B.Box("Monitor Stand", new Vector3(0.8f, 0.965f, 1.95f), new Vector3(1.0f, 0.985f, 2.15f), plastic, 1f, false);
        B.Box("Keyboard", new Vector3(0.62f, 0.965f, 1.6f), new Vector3(1.1f, 0.985f, 1.76f), plastic, 1f, false);

        var screenMaterial = new Material(Shader.Find("Universal Render Pipeline/Unlit")) { name = "Security Screen" };
        GameObject screen = B.Prim(PrimitiveType.Quad, "Screen", desk, new Vector3(0.9f, 1.25f, 1.897f), Vector3.zero, new Vector3(0.62f, 0.42f, 1f), B.SaveMaterial(screenMaterial));
        var staticMaterial = new Material(Shader.Find("Sprites/Default")) { name = "Camera Static", mainTexture = BrainrotTextures.StaticNoise(B.Generated + "/Textures/Characters") };
        GameObject noise = B.Prim(PrimitiveType.Quad, "Static", desk, new Vector3(0.9f, 1.25f, 1.894f), Vector3.zero, new Vector3(0.62f, 0.42f, 1f), B.SaveMaterial(staticMaterial));
        foreach (GameObject g in new[] { screen, noise }) g.GetComponent<Renderer>().shadowCastingMode = ShadowCastingMode.Off;

        TextMeshPro label = B.Text("Camera Name", "CAM 1  YARD", new Vector3(0.9f, 1.425f, 1.891f), Quaternion.identity, 0.32f, new Color(0.5f, 1f, 0.6f), new Vector2(0.58f, 0.06f), desk);
        label.alignment = TextAlignmentOptions.Left;
        TextMeshPro off = B.Text("Monitor Off", "MONITOR OFF\n<size=70%>press a CAM button on the front rail</size>", new Vector3(0.9f, 1.25f, 1.891f), Quaternion.identity,
            0.34f, new Color(0.45f, 0.75f, 0.5f), new Vector2(0.58f, 0.3f), desk);
        clock = B.Text("Monitor Clock", "12 AM", new Vector3(0.9f, 1.425f, 1.891f), Quaternion.identity, 0.32f, new Color(1f, 0.85f, 0.4f), new Vector2(0.58f, 0.06f), desk);
        clock.alignment = TextAlignmentOptions.Right;
        power = B.Text("Monitor Power", "POWER 100%", new Vector3(0.9f, 1.085f, 1.891f), Quaternion.identity, 0.26f, new Color(0.9f, 0.9f, 0.85f), new Vector2(0.58f, 0.09f), desk);

        // The four cameras.
        Transform rigs = B.Group("Security Cameras", under);
        var feeds = new[]
        {
            SecurityCam("CAM 1 Yard", rigs, new Vector3(-7.5f, 4.4f, 3.4f), new Vector3(3.5f, 0.3f, 10f), 72f),
            SecurityCamLooking("CAM 2 Roof", rigs, new Vector3(1.6f, B.Ceiling + 0.5f, 0.6f), Quaternion.LookRotation(new Vector3(-1.6f, 12.7f, 3.4f), Vector3.forward), 95f),
            SecurityCam("CAM 3 Hallway", rigs, new Vector3(0.95f, 2.45f, -2.85f), new Vector3(0f, 0.9f, -10.5f), 65f),
            SecurityCam("CAM 4 Side Hall", rigs, new Vector3(-2.85f, 2.45f, 0.95f), new Vector3(-10.5f, 0.9f, 0f), 65f),
        };

        var cams = desk.gameObject.AddComponent<SecurityCameras>();
        cams.feeds = feeds;
        cams.screen = screen.GetComponent<Renderer>();
        cams.label = label;
        cams.offLabel = off;
        cams.staticNoise = noise.GetComponent<Renderer>();
        return cams;
    }

    static UnityEngine.Camera SecurityCam(string name, Transform under, Vector3 position, Vector3 lookAt, float fov)
        => SecurityCamLooking(name, under, position, Quaternion.LookRotation(lookAt - position), fov);

    static UnityEngine.Camera SecurityCamLooking(string name, Transform under, Vector3 position, Quaternion rotation, float fov)
    {
        var go = new GameObject(name);
        go.transform.SetParent(under, false);
        go.transform.SetPositionAndRotation(position, rotation);
        var cam = go.AddComponent<UnityEngine.Camera>();
        cam.fieldOfView = fov;
        cam.nearClipPlane = 0.2f;
        cam.farClipPlane = 60f;
        cam.clearFlags = CameraClearFlags.Skybox;
        cam.allowMSAA = false;
        cam.allowHDR = false;
        cam.depth = -10f;
        cam.enabled = false;
        var data = cam.GetUniversalAdditionalCameraData();
        data.renderPostProcessing = false;
        data.renderShadows = false;
        // A little camera body with a red light.
        B.Prim(PrimitiveType.Cube, "Camera Body", go.transform, new Vector3(0f, 0f, -0.14f), Vector3.zero, new Vector3(0.12f, 0.1f, 0.2f), B.Mats["Dark Plastic"]);
        B.Prim(PrimitiveType.Cylinder, "Lens", go.transform, new Vector3(0f, 0f, -0.03f), new Vector3(90f, 0f, 0f), new Vector3(0.07f, 0.02f, 0.07f), B.Mats["Flashlight Body"]);
        B.Prim(PrimitiveType.Sphere, "Recording Light", go.transform, new Vector3(0.04f, 0.04f, -0.03f), Vector3.zero, Vector3.one * 0.02f, BrainrotModels.Get("Glowing Eyes"));
        return cam;
    }

    // ---------- Menus and rail buttons ----------

    static readonly Color ButtonColor = new Color(0.3f, 0.07f, 0.06f, 0.95f);
    static readonly Color PanelColor = new Color(0.02f, 0.02f, 0.04f, 0.88f);

    static NightsMenu BuildMenus(NightsGame game, out TextMeshProUGUI doorPower)
    {
        Transform ui = B.Group("Menus and Rail Buttons");
        var menu = ui.gameObject.AddComponent<NightsMenu>();
        menu.game = game;

        // The big board in front of you.
        UnityEngine.Canvas board = WorldCanvas("Menu Board", ui, new Vector3(0f, 1.62f, 1.8f), Quaternion.identity, new Vector2(1500f, 950f));
        menu.board = board.gameObject;
        Panel("Background", board.transform, Vector2.zero, new Vector2(1500f, 950f), PanelColor);

        // Main menu.
        RectTransform main = Rect("Main", board.transform, Vector2.zero, new Vector2(1500f, 950f));
        menu.mainPanel = main.gameObject;
        Label("Title", main, "FIVE NIGHTS AT TUNG'S", new Vector2(0f, 385f), new Vector2(1440f, 120f), 92f, new Color(1f, 0.35f, 0.25f));
        Label("Subtitle", main, "Survive from 12 AM to 6 AM. Grab the flashlight on the right rail!", new Vector2(0f, 300f), new Vector2(1440f, 60f), 36f, new Color(0.9f, 0.88f, 0.8f));
        string[] captions = { "Tung", "+ Ballerina, Lirili", "+ Tralalero", "+ Patapim", "+ Bombardiro" };
        menu.nightButtons = new Button[5];
        for (int i = 0; i < 5; i++)
            menu.nightButtons[i] = MakeButton($"Night {i + 1}", main, $"NIGHT {i + 1}\n<size=50%>{captions[i]}</size>", new Vector2(-560f + 280f * i, 150f), new Vector2(255f, 160f), 46f, ButtonColor, out _);
        menu.customButton = MakeButton("Custom Night", main, "CUSTOM NIGHT", new Vector2(-260f, -40f), new Vector2(470f, 105f), 44f, new Color(0.12f, 0.14f, 0.35f, 0.95f), out _);
        menu.jumpscareButton = MakeButton("Jumpscares", main, "JUMPSCARES: OFF", new Vector2(260f, -40f), new Vector2(470f, 105f), 44f, new Color(0.25f, 0.1f, 0.25f, 0.95f), out TextMeshProUGUI jumpscareLabel);
        menu.jumpscareLabel = jumpscareLabel;
        Label("Controls", main,
            "<b>VR:</b> grab the flashlight (grip) and pull the trigger for light. Point at buttons and pull the trigger (or poke them).\n" +
            "Cameras: front rail.   Door: back rail.   Panic: do the 6-7 with both controllers (once per night).\n" +
            "<b>Desktop:</b> mouse aims, click/F = light, right-drag = look, 1-4 = cameras, Tab = monitor, Space = door, 6 7 6 7 = panic.",
            new Vector2(0f, -275f), new Vector2(1440f, 260f), 27f, new Color(0.8f, 0.8f, 0.75f));

        // Custom Night.
        RectTransform custom = Rect("Custom Night", board.transform, Vector2.zero, new Vector2(1500f, 950f));
        menu.customPanel = custom.gameObject;
        Label("Title", custom, "CUSTOM NIGHT", new Vector2(0f, 395f), new Vector2(1400f, 100f), 72f, new Color(1f, 0.35f, 0.25f));
        Label("Hint", custom, "Set how aggressive each character is: 0 = stays home, 20 = BRAINROT.", new Vector2(0f, 325f), new Vector2(1400f, 50f), 30f, new Color(0.85f, 0.85f, 0.8f));
        string[] names = { "Tung Tung Tung Sahur", "Ballerina Cappuccina", "Lirili Larila", "Tralalero Tralala", "Brr Brr Patapim", "Bombardiro Crocodilo" };
        menu.levelTexts = new TMP_Text[6];
        menu.minusButtons = new Button[6];
        menu.plusButtons = new Button[6];
        for (int i = 0; i < 6; i++)
        {
            float y = 245f - i * 82f;
            Label("Name", custom, names[i], new Vector2(-330f, y), new Vector2(620f, 70f), 38f, Color.white, TextAlignmentOptions.Left);
            menu.minusButtons[i] = MakeButton("Minus", custom, "-", new Vector2(130f, y), new Vector2(90f, 68f), 54f, ButtonColor, out _);
            menu.levelTexts[i] = Label("Level", custom, "10", new Vector2(245f, y), new Vector2(110f, 68f), 46f, new Color(1f, 0.85f, 0.4f));
            menu.plusButtons[i] = MakeButton("Plus", custom, "+", new Vector2(360f, y), new Vector2(90f, 68f), 54f, ButtonColor, out _);
        }
        menu.allZeroButton = MakeButton("All 0", custom, "ALL 0", new Vector2(-480f, -275f), new Vector2(220f, 85f), 36f, new Color(0.2f, 0.2f, 0.22f, 0.95f), out _);
        menu.allTenButton = MakeButton("All 10", custom, "ALL 10", new Vector2(-235f, -275f), new Vector2(220f, 85f), 36f, new Color(0.2f, 0.2f, 0.22f, 0.95f), out _);
        menu.allTwentyButton = MakeButton("All 20", custom, "BRAINROT 20", new Vector2(45f, -275f), new Vector2(290f, 85f), 36f, new Color(0.45f, 0.05f, 0.05f, 0.95f), out _);
        menu.startCustomButton = MakeButton("Start", custom, "START", new Vector2(420f, -275f), new Vector2(330f, 95f), 46f, new Color(0.08f, 0.35f, 0.12f, 0.95f), out _);
        menu.backButton = MakeButton("Back", custom, "BACK", new Vector2(0f, -385f), new Vector2(240f, 75f), 36f, new Color(0.2f, 0.2f, 0.22f, 0.95f), out _);

        // The night card (tips) and the end screen.
        RectTransform card = Rect("Night Card", board.transform, Vector2.zero, new Vector2(1500f, 950f));
        menu.cardPanel = card.gameObject;
        menu.cardText = Label("Card", card, "", Vector2.zero, new Vector2(1400f, 880f), 38f, new Color(0.93f, 0.9f, 0.85f));
        RectTransform end = Rect("End Screen", board.transform, Vector2.zero, new Vector2(1500f, 950f));
        menu.endPanel = end.gameObject;
        menu.endText = Label("Result", end, "", new Vector2(0f, 130f), new Vector2(1400f, 520f), 58f, Color.white);
        menu.retryButton = MakeButton("Retry", end, "TRY AGAIN", new Vector2(-420f, -300f), new Vector2(340f, 125f), 44f, ButtonColor, out _);
        menu.nextButton = MakeButton("Next", end, "NEXT NIGHT", new Vector2(0f, -300f), new Vector2(340f, 125f), 44f, new Color(0.08f, 0.35f, 0.12f, 0.95f), out _);
        menu.menuButton = MakeButton("Menu", end, "MENU", new Vector2(420f, -300f), new Vector2(340f, 125f), 44f, new Color(0.2f, 0.2f, 0.22f, 0.95f), out _);
        custom.gameObject.SetActive(false);
        card.gameObject.SetActive(false);
        end.gameObject.SetActive(false);

        // Camera buttons on the front (window) rail, the door button on the back rail.
        B.Parent = ui;
        Material plastic = B.Mats["Dark Plastic"];
        B.Box("Camera Panel", new Vector3(-0.02f, 0.99f, 1.36f), new Vector3(0.74f, 1.03f, 1.48f), plastic, 1f, false);
        UnityEngine.Canvas front = WorldCanvas("Camera Buttons", ui, new Vector3(0.36f, 1.07f, 1.415f), Quaternion.Euler(50f, 0f, 0f), new Vector2(720f, 140f));
        Panel("Background", front.transform, Vector2.zero, new Vector2(720f, 140f), PanelColor);
        menu.cameraButtons = new Button[4];
        string[] feeds = { "YARD", "ROOF", "HALL", "SIDE" };
        for (int i = 0; i < 4; i++)
            menu.cameraButtons[i] = MakeButton($"CAM {i + 1}", front.transform, $"CAM {i + 1}\n<size=60%>{feeds[i]}</size>", new Vector2(-290f + i * 118f, 0f), new Vector2(108f, 115f), 30f, new Color(0.08f, 0.25f, 0.12f, 0.95f), out _);
        menu.monitorButton = MakeButton("Monitor", front.transform, "MON\n<size=60%>ON/OFF</size>", new Vector2(185f, 0f), new Vector2(120f, 115f), 30f, new Color(0.15f, 0.15f, 0.18f, 0.95f), out _);
        menu.panicLabel = Label("6-7", front.transform, "6-7 READY", new Vector2(305f, 0f), new Vector2(100f, 115f), 24f, new Color(1f, 0.85f, 0.4f));

        B.Box("Door Panel", new Vector3(-0.32f, 0.99f, -1.48f), new Vector3(0.32f, 1.03f, -1.36f), plastic, 1f, false);
        UnityEngine.Canvas back = WorldCanvas("Door Button", ui, new Vector3(0f, 1.07f, -1.415f), Quaternion.Euler(50f, 180f, 0f), new Vector2(620f, 140f));
        Panel("Background", back.transform, Vector2.zero, new Vector2(620f, 140f), PanelColor);
        menu.doorButton = MakeButton("Door", back.transform, "DOOR", new Vector2(-145f, 0f), new Vector2(300f, 120f), 40f, new Color(0.55f, 0.06f, 0.05f, 0.95f), out TextMeshProUGUI doorLabel);
        menu.doorLabel = doorLabel;
        doorPower = Label("Power", back.transform, "POWER 100%", new Vector2(160f, 0f), new Vector2(280f, 130f), 30f, new Color(0.9f, 0.9f, 0.85f));
        return menu;
    }

    static UnityEngine.Canvas WorldCanvas(string name, Transform under, Vector3 position, Quaternion rotation, Vector2 sizeMillimeters)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(under, false);
        go.transform.SetPositionAndRotation(position, rotation);
        go.transform.localScale = Vector3.one * 0.001f; // 1 unit = 1 mm
        var canvas = go.AddComponent<UnityEngine.Canvas>();
        canvas.renderMode = RenderMode.WorldSpace;
        ((RectTransform)go.transform).sizeDelta = sizeMillimeters;
        go.AddComponent<GraphicRaycaster>();              // mouse (desktop)
        go.AddComponent<TrackedDeviceGraphicRaycaster>(); // VR rays and pokes
        return canvas;
    }

    static RectTransform Rect(string name, Transform under, Vector2 position, Vector2 size)
    {
        var go = new GameObject(name, typeof(RectTransform));
        var rt = (RectTransform)go.transform;
        rt.SetParent(under, false);
        rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = position;
        rt.sizeDelta = size;
        return rt;
    }

    static Image Panel(string name, Transform under, Vector2 position, Vector2 size, Color color)
    {
        var image = Rect(name, under, position, size).gameObject.AddComponent<Image>();
        image.color = color;
        return image;
    }

    static TextMeshProUGUI Label(string name, Transform under, string text, Vector2 position, Vector2 size, float fontSize, Color color,
                                 TextAlignmentOptions alignment = TextAlignmentOptions.Center)
    {
        var label = Rect(name, under, position, size).gameObject.AddComponent<TextMeshProUGUI>();
        label.text = text;
        label.fontSize = fontSize;
        label.color = color;
        label.alignment = alignment;
        label.textWrappingMode = TextWrappingModes.Normal;
        label.raycastTarget = false;
        return label;
    }

    static Button MakeButton(string name, Transform under, string text, Vector2 position, Vector2 size, float fontSize, Color color, out TextMeshProUGUI label)
    {
        Image image = Panel(name, under, position, size, color);
        var button = image.gameObject.AddComponent<Button>();
        ColorBlock colors = button.colors;
        colors.normalColor = Color.white;
        colors.highlightedColor = new Color(1.6f, 1.4f, 1.2f);
        colors.pressedColor = new Color(2.2f, 1.6f, 1.2f);
        colors.selectedColor = Color.white;
        colors.colorMultiplier = 1.5f;
        button.colors = colors;
        Navigation navigation = button.navigation;
        navigation.mode = Navigation.Mode.None;
        button.navigation = navigation;
        label = Label("Label", image.transform, text, Vector2.zero, size - new Vector2(10f, 6f), fontSize, Color.white);
        return button;
    }
}
