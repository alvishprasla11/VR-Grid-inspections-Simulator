using TMPro;
using UnityEngine;

/// Turns the real grid stress level into mission stakes.
/// Your mission / scoring code reads TimeLimitSeconds, SpeedWeight and DispatchMessage.
public class MissionUrgency : MonoBehaviour {
    public TMP_Text dispatchText;          // the dispatch call shown before launch
    public float baseTimeLimit = 300f;     // seconds for a normal-day mission

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
                DispatchMessage = "Normal grid conditions. Inspect the section carefully and report the fault.";
                break;
        }
        if (dispatchText) dispatchText.text = DispatchMessage;
    }
}
