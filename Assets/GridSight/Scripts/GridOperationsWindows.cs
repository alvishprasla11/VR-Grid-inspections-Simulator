using System;
using System.Text.RegularExpressions;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public sealed class GridOperationsWindows : MonoBehaviour {
    TMP_Text supplierText, marketText;
    readonly System.Collections.Generic.Dictionary<string, TMP_Text> marketTiles = new System.Collections.Generic.Dictionary<string, TMP_Text>();
    readonly System.Collections.Generic.Dictionary<string, TMP_Text> supplierTiles = new System.Collections.Generic.Dictionary<string, TMP_Text>();
    readonly System.Collections.Generic.Dictionary<string, TMP_Text> capacityTiles = new System.Collections.Generic.Dictionary<string, TMP_Text>();
    readonly System.Collections.Generic.Dictionary<string, TMP_Text> participantTiles = new System.Collections.Generic.Dictionary<string, TMP_Text>();
    Canvas supplierCanvas, marketCanvas;
    static readonly Color Panel = new Color(0.035f, 0.08f, 0.13f, 0.97f);
    static readonly Color Cyan = new Color(0.22f, 0.78f, 1f);
    static readonly Color Ink = new Color(0.91f, 0.95f, 0.97f);
    static readonly Color Muted = new Color(0.58f, 0.7f, 0.78f);

    void Start() {
        supplierCanvas = MakeWindow("SupplierDispatchWindow", new Vector3(-3.15f, 2.35f, 3.75f), Quaternion.Euler(0, -12f, 0), "SUPPLIER DISPATCH · AESO", out supplierText);
        marketCanvas = MakeWindow("MarketReliabilityWindow", new Vector3(3.15f, 2.35f, 3.75f), Quaternion.Euler(0, 12f, 0), "MARKET · RESERVE · OUTAGES", out marketText);
        var capacity = MakeWindow("SupplierCapacityWindow", new Vector3(-3.9f, 1.05f, 2.25f), Quaternion.Euler(0, -24f, 0), "CAPABILITY · HEADROOM", out _);
        var participants = MakeWindow("SupplierParticipantsWindow", new Vector3(3.9f, 1.05f, 2.25f), Quaternion.Euler(0, 24f, 0), "SUPPLIERS · ASSETS", out _);
        CreateMetricGrid(supplierCanvas.GetComponent<RectTransform>(), supplierTiles,
            new[] { "WIND", "SOLAR", "GAS", "DISPATCHABLE", "CAPABILITY", "HEADROOM", "FUEL TYPES", "EFFECTIVE" });
        CreateMetricGrid(capacity.GetComponent<RectTransform>(), capacityTiles,
            new[] { "MAX CAPACITY", "OUTPUT", "HEADROOM", "RESERVE", "RESERVE NEED", "LOAD SAMPLES", "ASSET RECORDS", "CAPACITY RECORDS" });
        CreateMetricGrid(participants.GetComponent<RectTransform>(), participantTiles,
            new[] { "POOL PARTICIPANTS", "GENERATION UNITS", "ASSET FEED", "CAPACITY FEED", "OUTAGE FEED", "AESO STATUS", "LAST UPDATE", "MODE" });
        supplierText.gameObject.SetActive(false);
        CreateMarketGrid(marketCanvas.GetComponent<RectTransform>());
        Refresh();
        GridDataClient.I.OnLive += OnLive;
        GridDataClient.I.OnOutageForecast += OnOutageForecast;
    }

    void OnDestroy() {
        if (GridDataClient.I != null) {
            GridDataClient.I.OnLive -= OnLive;
            GridDataClient.I.OnOutageForecast -= OnOutageForecast;
        }
    }

    void OnLive(GridLive _) => Refresh();
    void OnOutageForecast(string _) => Refresh();

    void Refresh() {
        var client = GridDataClient.I;
        if (client == null) return;
        var d = client.Live;
        if (d == null) {
            supplierText.text = "WAITING FOR AESO DATA...";
            marketText.text = "WAITING FOR AESO DATA...";
            return;
        }

        SetTile(supplierTiles, "WIND", $"{F(d.wind_mw)} MW");
        SetTile(supplierTiles, "SOLAR", $"{F(d.solar_mw)} MW");
        SetTile(supplierTiles, "GAS", $"{F(d.gas_mw)} MW");
        SetTile(supplierTiles, "DISPATCHABLE", $"{F(d.dispatchable_generation_mw)} MW");
        SetTile(supplierTiles, "CAPABILITY", $"{F(d.dispatchable_capability_mw)} MW");
        SetTile(supplierTiles, "HEADROOM", $"{F(d.headroom_mw)} MW");
        SetTile(supplierTiles, "FUEL TYPES", (d.fuel_mix?.Length ?? 0).ToString());
        SetTile(supplierTiles, "EFFECTIVE", d.effective_time ?? "—");
        SetTile(capacityTiles, "MAX CAPACITY", $"{F(d.dispatchable_capability_mw)} MW");
        SetTile(capacityTiles, "OUTPUT", $"{F(d.dispatchable_generation_mw)} MW");
        SetTile(capacityTiles, "HEADROOM", $"{F(d.headroom_mw)} MW");
        SetTile(capacityTiles, "RESERVE", $"{F(d.reserve_dispatched_mw)} MW");
        SetTile(capacityTiles, "RESERVE NEED", $"{F(d.reserve_required_mw)} MW");
        SetTile(capacityTiles, "LOAD SAMPLES", (d.load_today?.Length ?? 0).ToString());
        SetTile(capacityTiles, "ASSET RECORDS", CountObjects(client.LatestAssetListJson).ToString("N0"));
        SetTile(capacityTiles, "CAPACITY RECORDS", CountObjects(client.LatestGenerationCapacityJson).ToString("N0"));
        SetTile(participantTiles, "POOL PARTICIPANTS", CountObjects(client.LatestPoolParticipantJson).ToString("N0"));
        SetTile(participantTiles, "GENERATION UNITS", (d.fuel_mix?.Length ?? 0).ToString());
        SetTile(participantTiles, "ASSET FEED", string.IsNullOrEmpty(client.LatestAssetListJson) ? "NO DATA" : "LIVE");
        SetTile(participantTiles, "CAPACITY FEED", string.IsNullOrEmpty(client.LatestGenerationCapacityJson) ? "NO DATA" : "LIVE");
        SetTile(participantTiles, "OUTAGE FEED", string.IsNullOrEmpty(client.LatestOutageForecastJson) ? "NO DATA" : "LIVE");
        SetTile(participantTiles, "AESO STATUS", client.AesoOnline ? "ONLINE" : "ERROR");
        SetTile(participantTiles, "LAST UPDATE", client.LastSuccessfulFetchUtc ?? "—");
        SetTile(participantTiles, "MODE", d.mode ?? "—");

        string outage = string.IsNullOrEmpty(client.LatestOutageForecastJson) ? "NO DATA" : "RECEIVED";
        SetTile(marketTiles, "LOAD", $"{F(d.load_mw)} MW");
        SetTile(marketTiles, "POOL PRICE", Price(d.pool_price));
        SetTile(marketTiles, "RESERVE", $"{F(d.reserve_dispatched_mw)} / {F(d.reserve_required_mw)} MW");
        SetTile(marketTiles, "GRID STRESS", d.stress_level);
        SetTile(marketTiles, "OUTAGE FEED", outage);
        SetTile(marketTiles, "ASSET RECORDS", CountObjects(client.LatestAssetListJson).ToString("N0"));
        SetTile(marketTiles, "CAPACITY RECORDS", CountObjects(client.LatestGenerationCapacityJson).ToString("N0"));
        SetTile(marketTiles, "AESO STATUS", client.AesoOnline ? "ONLINE" : "ERROR");
    }

    static string FuelRows(AesoFuelSummary[] rows) {
        if (rows == null || rows.Length == 0) return "No fuel-mix records received";
        string result = "";
        foreach (var row in rows)
            result += $"{row.fuel_type}: {F(row.generation_mw)} MW / {F(row.capability_mw)} MW cap\n";
        return result.TrimEnd();
    }

    static string F(float value) => value < 0 ? "—" : value.ToString("N0");
    static string Price(float value) => value < 0 ? "—" : "$" + value.ToString("N2") + "/MWh";
    void SetTile(System.Collections.Generic.Dictionary<string, TMP_Text> tiles, string key, string value) {
        if (tiles.TryGetValue(key, out var tile))
            tile.text = value;
    }

    void CreateMarketGrid(RectTransform parent) {
        string[] labels = { "LOAD", "POOL PRICE", "RESERVE", "GRID STRESS", "OUTAGE FEED", "ASSET RECORDS", "CAPACITY RECORDS", "AESO STATUS" };
        CreateMetricGrid(parent, marketTiles, labels);
        marketText.gameObject.SetActive(false);
    }

    void CreateMetricGrid(RectTransform parent, System.Collections.Generic.Dictionary<string, TMP_Text> tiles, string[] labels) {
        for (int i = 0; i < labels.Length; i++) {
            int column = i % 2;
            int row = i / 2;
            var tile = new GameObject("MetricTile_" + labels[i], typeof(RectTransform), typeof(Image));
            var rect = tile.GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = new Vector2(column == 0 ? -510f : 510f, 330f - row * 260f);
            rect.sizeDelta = new Vector2(900f, 215f);
            tile.GetComponent<Image>().color = new Color(0.06f, 0.14f, 0.22f, 1f);
            var label = Text(rect, labels[i], new Vector2(0f, 65f), new Vector2(820f, 50f), 32f, Muted);
            label.fontStyle = FontStyles.Bold;
            var value = Text(rect, "—", new Vector2(0f, -25f), new Vector2(820f, 125f), 68f, Ink);
            value.fontStyle = FontStyles.Bold;
            value.alignment = TextAlignmentOptions.Center;
            tiles[labels[i]] = value;
        }
    }
    static int CountObjects(string json) {
        if (string.IsNullOrEmpty(json)) return 0;
        return Regex.Matches(json, "\"[^\"]+\"\\s*:").Count;
    }

    Canvas MakeWindow(string name, Vector3 position, Quaternion rotation, string title, out TMP_Text body) {
        var go = new GameObject(name, typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler));
        var canvas = go.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.WorldSpace;
        canvas.worldCamera = Camera.main;
        var rect = go.GetComponent<RectTransform>();
        rect.position = position;
        rect.rotation = rotation;
        rect.sizeDelta = new Vector2(2100, 1450);
        rect.localScale = Vector3.one * 0.001f;

        var background = new GameObject("Background", typeof(RectTransform), typeof(Image));
        var bgRect = background.GetComponent<RectTransform>();
        bgRect.SetParent(rect, false);
        bgRect.anchorMin = Vector2.zero; bgRect.anchorMax = Vector2.one;
        bgRect.offsetMin = Vector2.zero; bgRect.offsetMax = Vector2.zero;
        background.GetComponent<Image>().color = Panel;

        var header = Text(rect, title, new Vector2(0, 555), new Vector2(1700, 110), 62, Cyan);
        header.fontStyle = FontStyles.Bold;
        body = Text(rect, "", new Vector2(0, -30), new Vector2(1900, 1120), 48, Ink);
        body.alignment = TextAlignmentOptions.TopLeft;
        return canvas;
    }

    static TMP_Text Text(RectTransform parent, string value, Vector2 position, Vector2 size, float fontSize, Color color) {
        var go = new GameObject("Text", typeof(RectTransform));
        var rect = go.GetComponent<RectTransform>();
        rect.SetParent(parent, false);
        rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = position; rect.sizeDelta = size;
        var text = go.AddComponent<TextMeshProUGUI>();
        text.text = value; text.fontSize = fontSize; text.color = color;
        text.richText = true; text.raycastTarget = false;
        return text;
    }
}
