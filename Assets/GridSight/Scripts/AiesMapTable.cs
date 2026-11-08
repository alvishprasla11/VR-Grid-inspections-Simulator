using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using System.IO;
using UnityEngine.InputSystem;

[Serializable] public class AiesLine { public int kv; public float[] pts; }
[Serializable] public class AiesOutline { public string kind; public float[] pts; }
[Serializable] public class AiesStation { public string name; public float x, y; }
[Serializable] public class AiesGen { public string type; public float x, y; }
[Serializable] public class AiesPlace { public string name; public float x, y; }
[Serializable] public class AiesMap {
    public string source; public float aspect;
    public AiesLine[] lines; public AiesOutline[] outlines; public AiesStation[] stations; public AiesGen[] generators; public AiesPlace[] places;
}

/// Holographic map table drawn from AESO's published 2026 AIES map (Calgary Area Detail):
/// real transmission lines by voltage, real substations, the city boundary and rivers.
/// Call ShowOutage() / CrewEnRoute() / Restored() from buttons or your mission code.
public class AiesMapTable : MonoBehaviour {
    public TextAsset mapJson;                       // aies_calgary_inset.json
    [Tooltip("Width of the map on the table in metres; height follows the map's aspect ratio")]
    public float width = 2.2f;
    public float lift = 0.08f;                      // height above the table surface
    [Tooltip("Substation whose distribution area has the outage in this scenario")]
    public string outageStation = "SS-41";
    public bool showStationLabels = true;
    [Tooltip("Index of the simulated training fault in the real map line data.")]
    public int faultLineIndex = 41;
    public GridMissionLauncher missionLauncher;
    public float Width => width;
    public float Depth => depth;

    static readonly Dictionary<int, float> KvWidth = new Dictionary<int, float> { { 69, 0.003f }, { 138, 0.004f }, { 240, 0.006f }, { 500, 0.009f } };
    static readonly Color NormalLineColor = new Color(0.12f, 0.95f, 0.3f, 0.95f);
    static readonly Color FaultLineColor = new Color(1f, 0.02f, 0.02f, 1f);
    static readonly Color DispatchedLineColor = new Color(1f, 0.48f, 0.04f, 1f);

    AiesMap map; float depth; Material lineMat;
    readonly Dictionary<string, Transform> stations = new Dictionary<string, Transform>();
    LineRenderer ring, faultLine; TextMeshPro ringLabel; Color ringColor; bool pulsing;
    InputAction dismissMissionAction;
    GameObject missionCompleteOverlay;

    void Start() {
        Build();
        if (HasCapturedPhoto()) {
            dismissMissionAction = new InputAction("DismissMissionComplete", InputActionType.Button,
                "<XRController>{RightHand}/primaryButton");
            dismissMissionAction.AddBinding("<OculusTouchController>{RightHand}/primaryButton");
            dismissMissionAction.Enable();
            StartCoroutine(ShowMissionCompleteAfterReturn());
        }
    }

    void Update() {
        if (missionCompleteOverlay != null && dismissMissionAction != null &&
            dismissMissionAction.WasPressedThisFrame()) {
            Destroy(missionCompleteOverlay);
            missionCompleteOverlay = null;
        }
        if (faultLine != null) {
            float brightness = 0.72f + 0.28f * (0.5f + 0.5f * Mathf.Sin(Time.time * 6f));
            faultLine.startColor = faultLine.endColor = new Color(1f, brightness * 0.08f, brightness * 0.08f, 1f);
            faultLine.widthMultiplier = 0.010f + brightness * 0.005f;
        }
        if (ring == null || !ring.enabled) return;
        float s = pulsing ? 1f + 0.35f * Mathf.Sin(Time.time * 5f) : 1f;
        ring.transform.localScale = Vector3.one;
        ring.widthMultiplier = 0.008f * s;
        var c = ringColor; c.a = pulsing ? 0.6f + 0.4f * Mathf.Sin(Time.time * 5f) : 1f; ring.startColor = ring.endColor = c;
    }

    void OnDestroy() {
        dismissMissionAction?.Disable();
        dismissMissionAction?.Dispose();
    }

    public void Build() {
        if (mapJson == null) { Debug.LogWarning("AiesMapTable: assign aies_calgary_inset.json"); return; }
        map = JsonUtility.FromJson<AiesMap>(mapJson.text);
        depth = width / Mathf.Max(0.1f, map.aspect);
        Shader lineShader = Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Unlit/Color") ?? Shader.Find("Sprites/Default");
        lineMat = new Material(lineShader) { color = Color.white };
        lineMat.enableInstancing = true;
        foreach (Transform c in transform) Destroy(c.gameObject);
        stations.Clear();

        if (map.outlines != null)
            foreach (var o in map.outlines)
                Line("outline_" + o.kind, o.pts, o.kind == "city" ? new Color(0.95f, 0.9f, 0.45f, 0.35f) : new Color(0.3f, 0.65f, 1f, 0.6f), 0.0015f, -0.001f);
        for (int i = 0; i < map.lines.Length; i++) {
            var l = map.lines[i];
            bool isFault = i == faultLineIndex;
            bool dispatched = isFault && HasCapturedPhoto();
            Line(isFault ? "FAULT · PRESS TRIGGER TO LAUNCH DRONE" : l.kv + "kV", l.pts,
                isFault ? dispatched ? DispatchedLineColor : FaultLineColor : NormalLineColor,
                isFault ? 0.012f : KvWidth.ContainsKey(l.kv) ? KvWidth[l.kv] : 0.004f, 0,
                isFault);
        }

        foreach (var s in map.stations) {
            var cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
            Destroy(cube.GetComponent<Collider>());
            cube.name = string.IsNullOrEmpty(s.name) ? "Substation" : s.name;
            cube.transform.SetParent(transform, false);
            cube.transform.localPosition = P(s.x, s.y, 0.008f);
            cube.transform.localScale = new Vector3(0.018f, 0.012f, 0.018f);
            cube.GetComponent<Renderer>().material = new Material(lineMat) { color = new Color(0.85f, 0.9f, 0.95f) };
            if (!string.IsNullOrEmpty(s.name)) {
                stations[s.name] = cube.transform;
                if (showStationLabels) Label(s.name, P(s.x, s.y, 0.015f) + new Vector3(0.04f, 0, 0.014f), 0.18f, Color.white);
            }
        }
        if (map.places != null)
            foreach (var p in map.places) Label(p.name.ToUpper(), P(p.x, p.y, 0.02f), 0.35f, new Color(1f, 0.85f, 0.4f, 0.8f));
        Label("Source: AESO Alberta Interconnected Electric System Map 2026 (Calgary Area Detail)",
              new Vector3(0, lift, -depth / 2 - 0.05f), 0.14f, new Color(0.7f, 0.85f, 1f, 0.8f));
        if (!HasCapturedPhoto())
            AddOutageInstruction();
        else
            AddEvidencePanel();

        // outage ring (hidden until ShowOutage)
        var rg = new GameObject("OutageRing"); rg.transform.SetParent(transform, false);
        ring = rg.AddComponent<LineRenderer>(); ring.useWorldSpace = false; ring.loop = true; ring.material = lineMat;
        ring.positionCount = 48; ring.widthMultiplier = 0.008f; ring.enabled = false;
        ringLabel = Label("", Vector3.zero, 0.26f, Color.white); ringLabel.fontStyle = FontStyles.Bold;
    }

    Vector3 P(float x, float y, float up) => new Vector3((x - 0.5f) * width, lift + up, (0.5f - y) * depth);

    static bool HasCapturedPhoto() {
        string path = PlayerPrefs.GetString("GridSightFaultPhotoPath", "");
        return !string.IsNullOrEmpty(path) && File.Exists(path);
    }

    System.Collections.IEnumerator ShowMissionCompleteAfterReturn() {
        yield return new WaitForSecondsRealtime(2f);
        if (missionCompleteOverlay != null || Camera.main == null) yield break;

        missionCompleteOverlay = new GameObject("MissionCompleteOverlay",
            typeof(RectTransform), typeof(Canvas), typeof(CanvasGroup));
        missionCompleteOverlay.transform.SetParent(Camera.main.transform, false);
        missionCompleteOverlay.transform.localPosition = new Vector3(0f, 0f, 1.35f);
        missionCompleteOverlay.transform.localRotation = Quaternion.identity;
        missionCompleteOverlay.transform.localScale = Vector3.one * 0.0015f;

        var canvas = missionCompleteOverlay.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.WorldSpace;
        canvas.worldCamera = Camera.main;
        canvas.sortingOrder = 200;
        var rect = missionCompleteOverlay.GetComponent<RectTransform>();
        rect.sizeDelta = new Vector2(1000f, 700f);

        var background = new GameObject("MissionCompleteBackground", typeof(RectTransform), typeof(Image));
        background.transform.SetParent(missionCompleteOverlay.transform, false);
        var backgroundRect = background.GetComponent<RectTransform>();
        backgroundRect.anchorMin = Vector2.zero;
        backgroundRect.anchorMax = Vector2.one;
        backgroundRect.offsetMin = Vector2.zero;
        backgroundRect.offsetMax = Vector2.zero;
        background.GetComponent<Image>().color = new Color(0.01f, 0.04f, 0.08f, 0.88f);

        var textObject = new GameObject("MissionCompleteText", typeof(RectTransform), typeof(TextMeshProUGUI));
        textObject.transform.SetParent(missionCompleteOverlay.transform, false);
        var textRect = textObject.GetComponent<RectTransform>();
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.offsetMin = new Vector2(40f, 40f);
        textRect.offsetMax = new Vector2(-40f, -40f);
        var text = textObject.GetComponent<TextMeshProUGUI>();
        string score = PlayerPrefs.GetString("GridSightMissionScore", "8");
        string reason = PlayerPrefs.GetString("GridSightMissionScoreReason",
            "Fault found and documented. Improve route efficiency and report speed.");
        string time = PlayerPrefs.GetString("GridSightMissionTime", "—");
        int safeViolations = PlayerPrefs.GetInt("GridSightSafeDistanceViolations", 0);
        text.text = $"<size=145%>MISSION COMPLETED</size>\n\n" +
            $"<size=120%>{score}/10</size>\n\n" +
            $"<size=70%>FAULTS FOUND: 1\nSAFE-DISTANCE VIOLATIONS: {safeViolations}\n" +
            $"TIME: {time}s · REPORT ACCURACY: 100%\n\n{reason}\n\n" +
            "<color=#8ED8FF>PRESS A TO CONTINUE</color></size>";
        text.alignment = TextAlignmentOptions.Center;
        text.fontSize = 58f;
        text.fontStyle = FontStyles.Bold;
        text.color = Color.white;
    }

    void Line(string name, float[] pts, Color c, float w, float up, bool clickable = false) {
        if (pts == null || pts.Length < 4) return;
        var go = new GameObject(name); go.transform.SetParent(transform, false);
        var lr = go.AddComponent<LineRenderer>();
        var material = ColoredMaterial(c);
        lr.useWorldSpace = false; lr.material = material; lr.startColor = lr.endColor = c; lr.widthMultiplier = w;
        lr.numCapVertices = 6; lr.numCornerVertices = 6;
        lr.alignment = LineAlignment.View;
        lr.textureMode = LineTextureMode.Stretch;
        lr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        lr.receiveShadows = false;
        lr.positionCount = pts.Length / 2;
        for (int i = 0; i < pts.Length / 2; i++) {
            var p = P(pts[2 * i], pts[2 * i + 1], up);
            lr.SetPosition(i, p);
        }

        if (clickable) {
            faultLine = lr;
            var target = go.AddComponent<GridFaultClickTarget>();
            target.launcher = missionLauncher;
            target.points = pts;
            target.map = this;
        }
    }

    void AddOutageInstruction() {
        var label = Label("CLICK ON OUTAGES TO CHECK THEM OUT WITH DRONE",
            new Vector3(0f, lift - 0.02f, -depth / 2 - 0.13f), 0.16f, Color.white);
        label.transform.localRotation = Quaternion.Euler(78f, 0f, 0f);
    }

    void AddEvidencePanel() {
        string path = PlayerPrefs.GetString("GridSightFaultPhotoPath", "");
        if (string.IsNullOrEmpty(path) || !File.Exists(path)) return;
        var panel = GameObject.CreatePrimitive(PrimitiveType.Cube);
        panel.name = "FaultEvidencePhotoPanel";
        panel.transform.SetParent(transform, false);
        panel.transform.localPosition = new Vector3(-0.98f, lift + 0.18f, -depth / 2 - 0.25f);
        panel.transform.localScale = new Vector3(0.85f, 0.06f, 0.62f);
        panel.GetComponent<Renderer>().material = ColoredMaterial(new Color(0.04f, 0.08f, 0.12f, 1f));
        var canvasObject = new GameObject("FaultEvidencePhoto", typeof(RectTransform), typeof(Canvas));
        canvasObject.transform.SetParent(transform, false);
        canvasObject.transform.localPosition = panel.transform.localPosition + new Vector3(0f, 0.06f, 0f);
        canvasObject.transform.localRotation = Quaternion.Euler(78f, 0f, 0f);
        canvasObject.transform.localScale = Vector3.one * 0.001f;
        canvasObject.GetComponent<Canvas>().renderMode = RenderMode.WorldSpace;
        var rect = canvasObject.GetComponent<RectTransform>();
        rect.sizeDelta = new Vector2(850f, 500f);
        var imageObject = new GameObject("CapturedFaultImage", typeof(RectTransform), typeof(RawImage));
        imageObject.transform.SetParent(canvasObject.transform, false);
        var imageRect = imageObject.GetComponent<RectTransform>();
        imageRect.anchorMin = Vector2.zero; imageRect.anchorMax = Vector2.one;
        imageRect.offsetMin = Vector2.zero; imageRect.offsetMax = Vector2.zero;
        byte[] bytes;
        try {
            bytes = File.ReadAllBytes(path);
        }
        catch (System.IO.IOException exception) {
            Debug.LogError($"GridSight could not read the captured fault photo at {path}: {exception.Message}", this);
            Destroy(panel);
            Destroy(canvasObject);
            return;
        }
        var texture = new Texture2D(2, 2, TextureFormat.RGB24, false);
        if (!texture.LoadImage(bytes, false)) {
            Destroy(texture);
            Debug.LogError($"GridSight could not decode the captured fault photo at {path}.", this);
            Destroy(panel);
            Destroy(canvasObject);
            return;
        }
        imageObject.GetComponent<RawImage>().texture = texture;
        var status = Label("DISPATCHED", panel.transform.localPosition + new Vector3(0f, 0.08f, 0.37f), 0.2f, DispatchedLineColor);
        status.fontStyle = FontStyles.Bold;
    }

    Material ColoredMaterial(Color color) {
        var material = new Material(lineMat) { color = color };
        if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", color);
        if (material.HasProperty("_Color")) material.SetColor("_Color", color);
        if ((color == FaultLineColor || color == DispatchedLineColor) && material.HasProperty("_EmissionColor")) {
            material.EnableKeyword("_EMISSION");
            material.SetColor("_EmissionColor", color * 2.5f);
        }
        return material;
    }

    TextMeshPro Label(string text, Vector3 localPos, float size, Color c) {
        var go = new GameObject("Label_" + text); go.transform.SetParent(transform, false);
        go.transform.localPosition = localPos;
        var t = go.AddComponent<TextMeshPro>();
        t.text = text; t.fontSize = size; t.color = c; t.alignment = TextAlignmentOptions.Center;
        t.rectTransform.sizeDelta = new Vector2(1.2f, 0.1f);
        return t;
    }

    public Vector3 StationWorldPosition(string name) => stations.TryGetValue(name, out var t) ? t.position : transform.position;

    public void ShowOutage() => SetRing(new Color(1f, 0.2f, 0.2f), $"OUTAGE · {outageStation} AREA", true);
    public void CrewEnRoute() => SetRing(new Color(1f, 0.77f, 0.19f), $"CREW EN ROUTE · {outageStation}", false);
    public void Restored() => SetRing(new Color(0.3f, 1f, 0.5f), $"POWER RESTORED · {outageStation}", false);

    void SetRing(Color c, string text, bool pulse) {
        if (ring == null || !stations.TryGetValue(outageStation, out var st)) { Debug.LogWarning($"Station {outageStation} not found"); return; }
        Vector3 center = transform.InverseTransformPoint(st.position);
        for (int i = 0; i < ring.positionCount; i++) {
            float a = i / (float)ring.positionCount * Mathf.PI * 2f;
            ring.SetPosition(i, center + new Vector3(Mathf.Cos(a) * 0.08f, 0.01f, Mathf.Sin(a) * 0.08f));
        }
        ringColor = c; ring.startColor = ring.endColor = c; ring.enabled = true; pulsing = pulse;
        ringLabel.text = text; ringLabel.color = c; ringLabel.transform.localPosition = center + new Vector3(0, 0.03f, 0.13f);
    }

}
