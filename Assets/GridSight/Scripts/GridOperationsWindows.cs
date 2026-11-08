using System;
using System.Text.RegularExpressions;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public sealed class GridOperationsWindows : MonoBehaviour {
    TMP_Text supplierText, marketText;
    Canvas supplierCanvas, marketCanvas;
    static readonly Color Panel = new Color(0.035f, 0.08f, 0.13f, 0.97f);
    static readonly Color Cyan = new Color(0.22f, 0.78f, 1f);
    static readonly Color Ink = new Color(0.91f, 0.95f, 0.97f);
    static readonly Color Muted = new Color(0.58f, 0.7f, 0.78f);

    void Start() {
        supplierCanvas = MakeWindow("SupplierDispatchWindow", new Vector3(-3.25f, 2.0f, 4.25f), "SUPPLIER DISPATCH · AESO", out supplierText);
        marketCanvas = MakeWindow("MarketReliabilityWindow", new Vector3(3.25f, 2.0f, 4.2f), "MARKET · RESERVE · OUTAGES", out marketText);
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

        supplierText.text =
            $"<b>GENERATION SUPPLIERS / FUEL MIX</b>\n{FuelRows(d.fuel_mix)}\n" +
            $"<b>DISPATCH POSITION</b>\n" +
            $"Dispatchable output: {F(d.dispatchable_generation_mw)} MW\n" +
            $"Available capability: {F(d.dispatchable_capability_mw)} MW\n" +
            $"Headroom: {F(d.headroom_mw)} MW\n" +
            $"Wind: {F(d.wind_mw)} MW   Solar: {F(d.solar_mw)} MW\n" +
            $"Gas-classified: {F(d.gas_mw)} MW\n" +
            $"Pool participant response records: {CountObjects(client.LatestPoolParticipantJson)}";

        string outage = string.IsNullOrEmpty(client.LatestOutageForecastJson) ? "not received" : "received";
        marketText.text =
            $"<b>MARKET AND SYSTEM CONDITIONS</b>\n" +
            $"Alberta load: {F(d.load_mw)} MW\nPool price: {Price(d.pool_price)}\n" +
            $"Contingency reserve: {F(d.reserve_dispatched_mw)} / {F(d.reserve_required_mw)} MW\n" +
            $"Grid stress: {d.stress_level}\n\n" +
            $"<b>OUTAGE / RELIABILITY FEEDS</b>\n" +
            $"Load outage forecast: {outage}\n" +
            $"Asset list records: {CountObjects(client.LatestAssetListJson)}\n" +
            $"Generation-capacity records: {CountObjects(client.LatestGenerationCapacityJson)}\n" +
            $"Load samples today: {d.load_today?.Length ?? 0}\n\n" +
            $"<b>DATA HEALTH</b>\n{(client.AesoOnline ? "AESO ONLINE" : "AESO ERROR")}\n" +
            $"Last success: {client.LastSuccessfulFetchUtc ?? "—"}\n" +
            $"{client.LastAesoError ?? "No current error"}";
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
    static int CountObjects(string json) {
        if (string.IsNullOrEmpty(json)) return 0;
        return Regex.Matches(json, "\"[^\"]+\"\\s*:").Count;
    }

    Canvas MakeWindow(string name, Vector3 position, string title, out TMP_Text body) {
        var go = new GameObject(name, typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler));
        var canvas = go.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.WorldSpace;
        canvas.worldCamera = Camera.main;
        var rect = go.GetComponent<RectTransform>();
        rect.position = position;
        rect.rotation = Quaternion.identity;
        rect.sizeDelta = new Vector2(1800, 1300);
        rect.localScale = Vector3.one * 0.001f;

        var background = new GameObject("Background", typeof(RectTransform), typeof(Image));
        var bgRect = background.GetComponent<RectTransform>();
        bgRect.SetParent(rect, false);
        bgRect.anchorMin = Vector2.zero; bgRect.anchorMax = Vector2.one;
        bgRect.offsetMin = Vector2.zero; bgRect.offsetMax = Vector2.zero;
        background.GetComponent<Image>().color = Panel;

        var header = Text(rect, title, new Vector2(0, 555), new Vector2(1700, 110), 62, Cyan);
        header.fontStyle = FontStyles.Bold;
        body = Text(rect, "", new Vector2(0, -40), new Vector2(1600, 1000), 42, Ink);
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
