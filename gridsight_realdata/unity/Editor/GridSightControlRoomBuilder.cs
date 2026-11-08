// Put this file in Assets/Editor/. Menu: GridSight > Build Control Room Scene
// Builds the full VR control room and wires every GridSight script. Re-run any time to rebuild.
using System;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Events;
using UnityEngine.UI;

public static class GridSightControlRoomBuilder {
    static readonly Color Navy = new Color(0.03f, 0.06f, 0.10f), Panel = new Color(0.07f, 0.15f, 0.24f, 0.95f),
        Cyan = new Color(0.22f, 0.78f, 1f), Ink = new Color(0.91f, 0.95f, 0.97f), Muted = new Color(0.56f, 0.66f, 0.74f);

    [MenuItem("GridSight/Build Control Room Scene")]
    public static void Build() {
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        var scene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);
        RenderSettings.ambientLight = new Color(0.25f, 0.3f, 0.38f);
        var cam = Camera.main;
        cam.transform.position = new Vector3(0, 1.6f, -0.9f);
        cam.backgroundColor = Navy; cam.clearFlags = CameraClearFlags.SolidColor;
        cam.gameObject.AddComponent<GridSightXRInteraction>();

        // ---------- room ----------
        var room = new GameObject("ControlRoom").transform;
        var dark = Lit(new Color(0.06f, 0.09f, 0.13f)); var wall = Lit(new Color(0.09f, 0.13f, 0.19f));
        var glow = Unlit(Cyan);
        Box("Floor", room, new Vector3(0, -0.05f, 1), new Vector3(9, 0.1f, 10), dark);
        Box("Ceiling", room, new Vector3(0, 3.6f, 1), new Vector3(9, 0.1f, 10), dark);
        Box("WallFront", room, new Vector3(0, 1.8f, 4.5f), new Vector3(9, 3.6f, 0.1f), wall);
        Box("WallBack", room, new Vector3(0, 1.8f, -3.2f), new Vector3(9, 3.6f, 0.1f), wall);
        Box("WallLeft", room, new Vector3(-4.5f, 1.8f, 1), new Vector3(0.1f, 3.6f, 10), wall);
        Box("WallRight", room, new Vector3(4.5f, 1.8f, 1), new Vector3(0.1f, 3.6f, 10), wall);
        for (int i = -3; i <= 3; i++) Box("LightStrip", room, new Vector3(i * 1.2f, 3.52f, 1), new Vector3(0.06f, 0.04f, 8), glow);
        Box("FloorTrim", room, new Vector3(0, 0.02f, 4.42f), new Vector3(9, 0.04f, 0.04f), glow);
        foreach (var x in new[] { -2.5f, 2.5f }) {
            var l = new GameObject("RoomLight").AddComponent<Light>(); l.type = LightType.Point; l.range = 8; l.intensity = 1.2f;
            l.color = new Color(0.75f, 0.85f, 1f); l.transform.SetParent(room); l.transform.position = new Vector3(x, 3.1f, 1.2f);
        }

        // ---------- map table with the real AESO AIES map ----------
        Cyl("MapTable", room, new Vector3(0, 0.45f, 1.5f), new Vector3(2.9f, 0.45f, 2.9f), Lit(new Color(0.05f, 0.08f, 0.12f)));
        Cyl("TableGlass", room, new Vector3(0, 0.905f, 1.5f), new Vector3(2.8f, 0.005f, 2.8f), Unlit(new Color(0.04f, 0.12f, 0.2f)));
        Cyl("TableRim", room, new Vector3(0, 0.9f, 1.5f), new Vector3(2.95f, 0.01f, 2.95f), glow);
        var mapGo = new GameObject("AIES_Map_Calgary"); mapGo.transform.SetParent(room);
        mapGo.transform.position = new Vector3(0, 0.912f, 1.5f);
        var map = mapGo.AddComponent<AiesMapTable>(); map.width = 2.1f;
        map.mapJson = FindAsset<TextAsset>("aies_calgary_inset");
        if (map.mapJson == null) Debug.LogWarning("GridSight: copy Data/aies_calgary_inset.json into Assets/GridSight/Data/ then rebuild.");

        // ---------- data + logic ----------
        var data = new GameObject("GridData");
        var client = data.AddComponent<GridDataClient>();
        var urgency = data.AddComponent<MissionUrgency>();
        var replay = data.AddComponent<ReplayController>();

        // ---------- wall dashboard (front wall) ----------
        var wallC = WorldCanvas("LiveGridWall", new Vector3(0, 2.0f, 4.4f), Quaternion.identity, new Vector2(3600, 1700), cam);
        Img(wallC, "Bg", Vector2.zero, new Vector2(3600, 1700), Panel);
        Txt(wallC, "Title", "ALBERTA GRID · LIVE AESO DATA", new Vector2(0, 720), new Vector2(3400, 140), 90, Cyan, FontStyles.Bold);
        var dash = wallC.gameObject.AddComponent<ControlRoomDashboard>();
        dash.loadText = Tile(wallC, "Load", new Vector2(-1150, 380));
        dash.priceText = Tile(wallC, "Price", new Vector2(0, 380));
        dash.headroomText = Tile(wallC, "Headroom", new Vector2(1150, 380));
        dash.windSolarText = Txt(wallC, "WindSolar", "Wind — · Solar —", new Vector2(0, 60), new Vector2(3400, 110), 70, Ink);
        var light = Img(wallC, "StressLight", new Vector2(-1500, -200), new Vector2(180, 180), Color.green);
        light.sprite = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/Knob.psd");
        dash.stressLight = light;
        dash.stressText = Txt(wallC, "Stress", "GRID STRESS: —", new Vector2(150, -200), new Vector2(3000, 220), 90, Ink, FontStyles.Bold);
        dash.stressText.alignment = TextAlignmentOptions.Left;
        Txt(wallC, "SparkLabel", "Alberta demand today (MW)", new Vector2(0, -330), new Vector2(3400, 80), 55, Muted);
        dash.sourceText = Txt(wallC, "Source", "Waiting for data…", new Vector2(0, -760), new Vector2(3400, 90), 48, Muted);
        var spark = new GameObject("LoadSparkline").AddComponent<LineRenderer>();
        spark.transform.position = new Vector3(-1.1f, 1.30f, 4.38f);
        spark.useWorldSpace = false; spark.widthMultiplier = 0.012f; spark.material = Unlit(Cyan); spark.positionCount = 0;
        dash.sparkline = spark; dash.sparkSize = new Vector2(2.2f, 0.28f);

        // ---------- dispatch screen (behind the table) ----------
        var disp = WorldCanvas("DispatchScreen", new Vector3(0, 1.1f, 3.05f), Quaternion.Euler(25, 0, 0), new Vector2(2000, 420), cam);
        Img(disp, "Bg", Vector2.zero, new Vector2(2000, 420), new Color(0.12f, 0.05f, 0.06f, 0.95f));
        Txt(disp, "Header", "DISPATCH", new Vector2(-780, 150), new Vector2(400, 90), 60, new Color(1f, 0.45f, 0.45f), FontStyles.Bold);
        urgency.dispatchText = Txt(disp, "Message", "Waiting for live grid conditions…", new Vector2(0, -30), new Vector2(1880, 300), 52, Ink);

        // ---------- operator console (right of the player) ----------
        var con = WorldCanvas("OperatorConsole", new Vector3(1.2f, 1.15f, 0.2f), Quaternion.Euler(35, 40, 0), new Vector2(900, 1000), cam);
        Img(con, "Bg", Vector2.zero, new Vector2(900, 1000), Panel);
        Txt(con, "Header", "OPERATOR CONSOLE", new Vector2(0, 430), new Vector2(860, 90), 56, Cyan, FontStyles.Bold);
        Btn(con, "1 · Outage alert", new Vector2(0, 290), new Color(0.75f, 0.15f, 0.15f), map.ShowOutage);
        Btn(con, "2 · Crew en route", new Vector2(0, 150), new Color(0.8f, 0.55f, 0.1f), map.CrewEnRoute);
        Btn(con, "3 · Power restored", new Vector2(0, 10), new Color(0.15f, 0.6f, 0.3f), map.Restored);
        Btn(con, "Replay real grid-alert day", new Vector2(0, -170), new Color(0.2f, 0.35f, 0.7f), replay.StartReplay);
        Btn(con, "Back to live data", new Vector2(0, -310), new Color(0.25f, 0.3f, 0.38f), replay.StopReplay);

        // ---------- impact screen (right wall) ----------
        var imp = WorldCanvas("ImpactScreen", new Vector3(4.4f, 1.9f, 1.5f), Quaternion.Euler(0, 90, 0), new Vector2(2600, 1300), cam);
        Img(imp, "Bg", Vector2.zero, new Vector2(2600, 1300), Panel);
        Txt(imp, "Header", "COMMUNITY IMPACT · REAL RELIABILITY DATA", new Vector2(0, 540), new Vector2(2500, 110), 70, Cyan, FontStyles.Bold);
        var impact = imp.gameObject.AddComponent<ImpactScreen>();
        impact.baselineText = Txt(imp, "Baseline", "", new Vector2(0, 300), new Vector2(2450, 260), 66, Ink);
        impact.resultText = Txt(imp, "Result", "", new Vector2(0, -20), new Vector2(2450, 260), 72, new Color(0.4f, 1f, 0.6f));
        impact.minutesSavedSlider = MakeSlider(imp, new Vector2(0, -260), new Vector2(1800, 70));
        Txt(imp, "SliderLabel", "What-if: minutes saved per outage (0–30)", new Vector2(0, -350), new Vector2(2400, 80), 50, Muted);
        impact.sourceText = Txt(imp, "Source", "", new Vector2(0, -540), new Vector2(2450, 90), 44, Muted);

        // ---------- replay wiring ----------
        replay.dashboard = dash; replay.urgency = urgency;

        // ---------- input: XR if installed, otherwise mouse/Input System ----------
        var es = new GameObject("EventSystem", typeof(EventSystem));
        var xrModule = T("UnityEngine.XR.Interaction.Toolkit.UI.XRUIInputModule");
        var isModule = T("UnityEngine.InputSystem.UI.InputSystemUIInputModule");
        if (xrModule != null) es.AddComponent(xrModule);
        else if (isModule != null) es.AddComponent(isModule);
        else es.AddComponent<StandaloneInputModule>();

        // ---------- save ----------
        if (!AssetDatabase.IsValidFolder("Assets/Scenes")) AssetDatabase.CreateFolder("Assets", "Scenes");
        EditorSceneManager.SaveScene(scene, "Assets/Scenes/GridSight_ControlRoom.unity");
        Selection.activeGameObject = data;
        EditorUtility.DisplayDialog("GridSight", "Control room built: Assets/Scenes/GridSight_ControlRoom.unity\n\n" +
            "Next:\n1. Select GridData and set Laptop Url to http://<laptop-ip>:8000/\n" +
            (xrModule == null ? "2. XR Interaction Toolkit not found: install it, then rebuild.\n" : "2. Drag in 'XR Origin (XR Rig)' from XRI Starter Assets and delete Main Camera.\n") +
            "3. Press Play.", "OK");
    }

    // ---------- helpers ----------
    static Type T(string fullName) => AppDomain.CurrentDomain.GetAssemblies().Select(a => a.GetType(fullName)).FirstOrDefault(t => t != null);
    static TA FindAsset<TA>(string name) where TA : UnityEngine.Object {
        var g = AssetDatabase.FindAssets(name + " t:" + typeof(TA).Name).FirstOrDefault();
        return g == null ? null : AssetDatabase.LoadAssetAtPath<TA>(AssetDatabase.GUIDToAssetPath(g));
    }
    static Material Lit(Color c) {
        var sh = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("HDRP/Lit") ?? Shader.Find("Standard");
        var m = new Material(sh);
        if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", c);
        if (m.HasProperty("_Color")) m.SetColor("_Color", c);
        return m;
    }
    static Material Unlit(Color c) {
        var sh = Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Unlit/Color") ?? Shader.Find("Sprites/Default");
        var m = new Material(sh);
        if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", c);
        if (m.HasProperty("_Color")) m.SetColor("_Color", c);
        return m;
    }
    static GameObject Box(string n, Transform p, Vector3 pos, Vector3 s, Material m) => Prim(PrimitiveType.Cube, n, p, pos, s, m);
    static GameObject Cyl(string n, Transform p, Vector3 pos, Vector3 s, Material m) => Prim(PrimitiveType.Cylinder, n, p, pos, s, m);
    static GameObject Prim(PrimitiveType t, string n, Transform p, Vector3 pos, Vector3 s, Material m) {
        var g = GameObject.CreatePrimitive(t); g.name = n; g.transform.SetParent(p); g.transform.position = pos; g.transform.localScale = s;
        g.GetComponent<Renderer>().sharedMaterial = m; return g;
    }
    static RectTransform WorldCanvas(string n, Vector3 pos, Quaternion rot, Vector2 size, Camera cam) {
        var g = new GameObject(n, typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        var c = g.GetComponent<Canvas>(); c.renderMode = RenderMode.WorldSpace; c.worldCamera = cam;
        var rt = g.GetComponent<RectTransform>(); rt.sizeDelta = size; rt.position = pos; rt.rotation = rot; rt.localScale = Vector3.one * 0.001f;
        var tracked = T("UnityEngine.XR.Interaction.Toolkit.UI.TrackedDeviceGraphicRaycaster");
        if (tracked != null) g.AddComponent(tracked);
        return rt;
    }
    static RectTransform Place(GameObject g, RectTransform parent, Vector2 pos, Vector2 size) {
        var rt = g.GetComponent<RectTransform>() ?? g.AddComponent<RectTransform>();
        rt.SetParent(parent, false); rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = pos; rt.sizeDelta = size; return rt;
    }
    static Image Img(RectTransform parent, string n, Vector2 pos, Vector2 size, Color c) {
        var g = new GameObject(n, typeof(RectTransform)); Place(g, parent, pos, size);
        var i = g.AddComponent<Image>(); i.color = c; return i;
    }
    static TMP_Text Txt(RectTransform parent, string n, string text, Vector2 pos, Vector2 size, float fs, Color c, FontStyles style = FontStyles.Normal) {
        var g = new GameObject(n, typeof(RectTransform)); Place(g, parent, pos, size);
        var t = g.AddComponent<TextMeshProUGUI>(); t.text = text; t.fontSize = fs; t.color = c; t.fontStyle = style;
        t.alignment = TextAlignmentOptions.Center; t.richText = true; t.raycastTarget = false; return t;
    }
    static TMP_Text Tile(RectTransform parent, string n, Vector2 pos) {
        Img(parent, n + "Tile", pos, new Vector2(1050, 480), new Color(0.05f, 0.1f, 0.17f, 1f));
        return Txt(parent, n, "—", pos, new Vector2(1000, 440), 120, Ink, FontStyles.Bold);
    }
    static void Btn(RectTransform parent, string label, Vector2 pos, Color c, UnityAction action) {
        var g = new GameObject("Btn_" + label, typeof(RectTransform)); Place(g, parent, pos, new Vector2(800, 110));
        var img = g.AddComponent<Image>(); img.color = c;
        var b = g.AddComponent<Button>(); b.targetGraphic = img;
        UnityEventTools.AddPersistentListener(b.onClick, action);
        Txt(g.GetComponent<RectTransform>(), "Label", label, Vector2.zero, new Vector2(780, 100), 48, Color.white, FontStyles.Bold);
    }
    static Slider MakeSlider(RectTransform parent, Vector2 pos, Vector2 size) {
        var res = new DefaultControls.Resources {
            standard = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd"),
            background = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/Background.psd"),
            knob = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/Knob.psd") };
        var g = DefaultControls.CreateSlider(res); Place(g, parent, pos, size);
        var s = g.GetComponent<Slider>(); s.minValue = 0; s.maxValue = 30; s.wholeNumbers = true; s.value = 10; return s;
    }
}
