using System;
using System.Collections;
using System.IO;
using UnityEngine;
using UnityEngine.Networking;

// Data shapes match the JSON written by laptop/aeso_fetch.py
[Serializable] public class GridLive {
    public string mode, source, fetched_utc, effective_time, stress_level, stress_reason;
    public float load_mw, dispatchable_generation_mw, dispatchable_capability_mw, headroom_mw, wind_mw, solar_mw, gas_mw;
    public float reserve_required_mw, reserve_dispatched_mw, pool_price;
    public float[] load_today;
}
[Serializable] public class ReplayHour { public string t; public float load_mw, price; }
[Serializable] public class GridReplay { public string mode, date, label, source; public ReplayHour[] hours; }
[Serializable] public class ImpactBaseline { public string utility; public float saifi, saidi_hours, customers; public string[] sources; }

/// Polls the laptop for real AESO data. Falls back to a real snapshot saved in StreamingAssets
/// (so the demo still works if the venue Wi-Fi drops - and the data is still real, just older).
public class GridDataClient : MonoBehaviour {
    public static GridDataClient I;
    [Tooltip("Laptop running aeso_fetch.py + 'python -m http.server 8000' in the data folder")]
    public string laptopUrl = "http://192.168.1.50:8000/";
    public float pollSeconds = 30f;

    public GridLive Live { get; private set; }
    public ImpactBaseline Impact { get; private set; }
    public bool UsingSnapshot { get; private set; }
    public event Action<GridLive> OnLive;

    void Awake() { I = this; }

    IEnumerator Start() {
        yield return Load<ImpactBaseline>("impact_baseline.json", v => Impact = v);
        while (true) {
            yield return Load<GridLive>("grid_live.json", v => { Live = v; OnLive?.Invoke(v); });
            yield return new WaitForSeconds(pollSeconds);
        }
    }

    public IEnumerator Load<T>(string file, Action<T> done) {
        using (var req = UnityWebRequest.Get(laptopUrl + file)) {
            req.timeout = 5;
            yield return req.SendWebRequest();
            if (req.result == UnityWebRequest.Result.Success) {
                UsingSnapshot = false;
                done(JsonUtility.FromJson<T>(req.downloadHandler.text));
                yield break;
            }
        }
        // Fallback: StreamingAssets (on Android/Quest this must also be read with UnityWebRequest)
        string path = Path.Combine(Application.streamingAssetsPath, file);
        if (!path.Contains("://")) path = "file://" + path;
        using (var req = UnityWebRequest.Get(path)) {
            yield return req.SendWebRequest();
            if (req.result == UnityWebRequest.Result.Success) {
                UsingSnapshot = true;
                done(JsonUtility.FromJson<T>(req.downloadHandler.text));
            } else Debug.LogWarning($"GridSight: could not load {file}");
        }
    }
}
