using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// Turns the real grid stress level into mission stakes.
/// Your mission / scoring code reads TimeLimitSeconds, SpeedWeight and DispatchMessage.
public class MissionUrgency : MonoBehaviour {
    public TMP_Text dispatchText;          // the dispatch call shown before launch
    public float baseTimeLimit = 300f;     // seconds for a normal-day mission
    static readonly Color DispatchedColor = new Color(1f, 0.48f, 0.04f, 1f);

    public float TimeLimitSeconds { get; private set; } = 300f;
    public float SpeedWeight { get; private set; } = 1f;
    public string DispatchMessage { get; private set; } = "";

    void Start() { GridDataClient.I.OnLive += Apply; if (GridDataClient.I.Live != null) Apply(GridDataClient.I.Live); }
    void OnDestroy() { if (GridDataClient.I) GridDataClient.I.OnLive -= Apply; }

    public void Apply(GridLive d) => Set(d.stress_level, d.load_mw);

    public void Set(string level, float loadMw) {
        switch (level) {
            case "HIGH":
                TimeLimitSeconds = baseTimeLimit * 0.6f; SpeedWeight = 2f;
                DispatchMessage = $"PRIORITY 1. Alberta grid is under stress ({loadMw:N0} MW demand). " +
                                  "Every minute counts: find the fault and report fast, critical customers first.";
                break;
            case "ELEVATED":
                TimeLimitSeconds = baseTimeLimit * 0.8f; SpeedWeight = 1.5f;
                DispatchMessage = $"Grid is tight ({loadMw:N0} MW demand). Locate the fault quickly and safely.";
                break;
            default:
                TimeLimitSeconds = baseTimeLimit; SpeedWeight = 1f;
                DispatchMessage = "DAMAGE DETECTED. FIND AND INSPECT WITH THE DRONE.";
                break;
        }
        bool dispatched = PlayerPrefs.GetString("GridSightRepairStatus", "") == "DISPATCHED REPAIRMEN";
        if (dispatched)
            DispatchMessage = "DISPATCHED — FAULT PHOTO CAPTURED. REPAIR CREW NOTIFIED.";
        if (dispatchText) {
            dispatchText.text = DispatchMessage;
            var header = dispatchText.transform.parent.Find("Header")?.GetComponent<TMP_Text>();
            if (header != null) {
                header.text = dispatched ? "DISPATCHED" : "DISPATCH";
                header.color = dispatched ? DispatchedColor : new Color(1f, 0.45f, 0.45f);
            }
            var background = dispatchText.transform.parent.Find("Bg")?.GetComponent<Image>();
            if (background != null)
                background.color = dispatched
                    ? new Color(0.20f, 0.10f, 0.02f, 0.95f)
                    : new Color(0.12f, 0.05f, 0.06f, 0.95f);
        }
    }
}
