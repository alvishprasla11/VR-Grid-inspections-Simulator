using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

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
    public float lift = 0.004f;                     // height above the table surface
    [Tooltip("Substation whose distribution area has the outage in this scenario")]
    public string outageStation = "SS-41";
    public bool showStationLabels = true;

    static readonly Dictionary<int, Color> KvColor = new Dictionary<int, Color> {
        { 69, new Color(0.55f, 0.35f, 1f) }, { 138, new Color(0.35f, 0.95f, 0.25f) },
        { 240, new Color(1f, 0.25f, 0.25f) }, { 500, new Color(0.25f, 0.4f, 1f) } };   // same colours as the AESO legend
    static readonly Dictionary<int, float> KvWidth = new Dictionary<int, float> { { 69, 0.003f }, { 138, 0.004f }, { 240, 0.006f }, { 500, 0.009f } };

    AiesMap map; float depth; Material lineMat;
    readonly Dictionary<string, Transform> stations = new Dictionary<string, Transform>();
    LineRenderer ring; TextMeshPro ringLabel; Color ringColor; bool pulsing;

    void Start() { Build(); }

    public void Build() {
        if (mapJson == null) { Debug.LogWarning("AiesMapTable: assign aies_calgary_inset.json"); return; }
        map = JsonUtility.FromJson<AiesMap>(mapJson.text);
        depth = width / Mathf.Max(0.1f, map.aspect);
        lineMat = new Material(Shader.Find("Sprites/Default"));
        foreach (Transform c in transform) Destroy(c.gameObject);
        stations.Clear();

        if (map.outlines != null)
            foreach (var o in map.outlines)
                Line("outline_" + o.kind, o.pts, o.kind == "city" ? new Color(0.95f, 0.9f, 0.45f, 0.35f) : new Color(0.3f, 0.65f, 1f, 0.6f), 0.0015f, -0.001f);
        foreach (var l in map.lines)
            Line(l.kv + "kV", l.pts, KvColor.ContainsKey(l.kv) ? KvColor[l.kv] : Color.white, KvWidth.ContainsKey(l.kv) ? KvWidth[l.kv] : 0.004f, 0);

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

        // outage ring (hidden until ShowOutage)
        var rg = new GameObject("OutageRing"); rg.transform.SetParent(transform, false);
        ring = rg.AddComponent<LineRenderer>(); ring.useWorldSpace = false; ring.loop = true; ring.material = lineMat;
        ring.positionCount = 48; ring.widthMultiplier = 0.008f; ring.enabled = false;
        ringLabel = Label("", Vector3.zero, 0.26f, Color.white); ringLabel.fontStyle = FontStyles.Bold;
    }

    Vector3 P(float x, float y, float up) => new Vector3((x - 0.5f) * width, lift + up, (0.5f - y) * depth);

    void Line(string name, float[] pts, Color c, float w, float up) {
        if (pts == null || pts.Length < 4) return;
        var go = new GameObject(name); go.transform.SetParent(transform, false);
        var lr = go.AddComponent<LineRenderer>();
        lr.useWorldSpace = false; lr.material = lineMat; lr.startColor = lr.endColor = c; lr.widthMultiplier = w;
        lr.numCapVertices = 2; lr.alignment = LineAlignment.TransformZ;
        go.transform.localRotation = Quaternion.Euler(90, 0, 0);       // lay the line ribbon flat on the table
        lr.positionCount = pts.Length / 2;
        for (int i = 0; i < pts.Length / 2; i++) {
            var p = P(pts[2 * i], pts[2 * i + 1], up);
            lr.SetPosition(i, Quaternion.Inverse(go.transform.localRotation) * p);
        }
    }

    TextMeshPro Label(string text, Vector3 localPos, float size, Color c) {
        var go = new GameObject("Label_" + text); go.transform.SetParent(transform, false);
        go.transform.localPosition = localPos;
        go.transform.localRotation = Quaternion.Euler(90, 0, 0);       // readable from above
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

    void Update() {
        if (ring == null || !ring.enabled) return;
        float s = pulsing ? 1f + 0.35f * Mathf.Sin(Time.time * 5f) : 1f;
        ring.transform.localScale = Vector3.one;
        ring.widthMultiplier = 0.008f * s;
        var c = ringColor; c.a = pulsing ? 0.6f + 0.4f * Mathf.Sin(Time.time * 5f) : 1f; ring.startColor = ring.endColor = c;
    }
}
