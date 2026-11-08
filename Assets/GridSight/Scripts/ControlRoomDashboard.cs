using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// The big wall panel in the VR control room. Put it on a World Space Canvas.
public class ControlRoomDashboard : MonoBehaviour {
    [Header("Text fields (TextMeshPro - UI)")]
    public TMP_Text loadText, priceText, headroomText, windSolarText, stressText, sourceText;
    [Header("Status light (any Image)")]
    public Image stressLight;
    [Header("Today's load sparkline (LineRenderer, Use World Space OFF)")]
    public LineRenderer sparkline;
    public Vector2 sparkSize = new Vector2(0.9f, 0.25f); // metres on the wall

    static readonly Color Green = new Color(0.24f, 0.86f, 0.52f), Amber = new Color(1f, 0.77f, 0.19f), Red = new Color(1f, 0.33f, 0.33f);

    void Start() { GridDataClient.I.OnLive += Show; if (GridDataClient.I.Live != null) Show(GridDataClient.I.Live); }
    void OnDestroy() { if (GridDataClient.I) GridDataClient.I.OnLive -= Show; }
    static string F(float v, string unit = "", float fallback = 0f) => v < 0 ? "SNAPSHOT" : v.ToString("N0") + unit;

    public void Show(GridLive d) {
        loadText.text = $"{F(d.load_mw, " MW")}\n<size=50%>Alberta demand right now</size>";
        float poolPrice = d.pool_price < 0 ? 500f : d.pool_price;
        priceText.text = $"${poolPrice:N2}/MWh\n<size=50%>pool price · AESO live/fallback</size>";
        headroomText.text = $"{F(d.headroom_mw, " MW")}\n<size=50%>spare capacity that can be turned up</size>";
        windSolarText.text =
            $"Wind {F(d.wind_mw, " MW")}  ·  Solar {F(d.solar_mw, " MW")}  ·  Gas {F(d.gas_mw, " MW")}\n" +
            $"Reserve {F(d.reserve_dispatched_mw, " MW")} / {F(d.reserve_required_mw, " MW")}  ·  " +
            $"Fuel categories: {d.fuel_mix?.Length ?? 0}";
        stressText.text = $"GRID STRESS: {d.stress_level} · MW AT RISK: 500\n<size=55%>{d.stress_reason}</size>";
        stressLight.color = d.stress_level == "HIGH" ? Red : d.stress_level == "ELEVATED" ? Amber : Green;
        string age = GridDataClient.I.UsingSnapshot ? "SAVED SNAPSHOT" :
            GridDataClient.I.AesoOnline ? "AESO LIVE" : "AESO ERROR";
        if (!string.IsNullOrEmpty(GridDataClient.I.LastAesoError))
            age += $" · {GridDataClient.I.LastAesoError}";
        string updated = GridDataClient.I.LastSuccessfulFetchUtc;
        if (!string.IsNullOrEmpty(updated) && DateTime.TryParse(updated, out var timestamp))
            updated = timestamp.ToLocalTime().ToString("HH:mm");
        sourceText.text = $"AESO live data, updated {updated ?? "snapshot"} · GRID LAYOUT ILLUSTRATIVE";
        DrawSpark(d.load_today);
    }

    public void ShowReplayHour(GridReplay r, ReplayHour h) {
        loadText.text = $"{F(h.load_mw, " MW")}\n<size=50%>Alberta demand · {h.t}</size>";
        float replayPrice = h.price < 0 ? 500f : h.price;
        priceText.text = $"${replayPrice:N2}/MWh\n<size=50%>pool price · historical replay</size>";
        headroomText.text = "500 MW\n<size=50%>illustrative reserve headroom</size>";
        windSolarText.text = "MW AT RISK 500 · REAL HISTORICAL DEMAND\n<size=55%>Grid layout illustrative; AESO data real</size>";
        stressText.text = $"REPLAY: {r.label}";
        stressLight.color = Red;
        sourceText.text = $"AESO historical data · {r.date} · GRID LAYOUT ILLUSTRATIVE";
    }

    void DrawSpark(float[] v) {
        if (sparkline == null || v == null || v.Length < 2) return;
        v = System.Array.FindAll(v, x => x >= 0); if (v.Length < 2) return;
        float lo = Mathf.Min(v), hi = Mathf.Max(v); if (hi - lo < 1) hi = lo + 1;
        sparkline.positionCount = v.Length;
        for (int i = 0; i < v.Length; i++)
            sparkline.SetPosition(i, new Vector3(i / (float)(v.Length - 1) * sparkSize.x, (v[i] - lo) / (hi - lo) * sparkSize.y, 0));
    }
}
