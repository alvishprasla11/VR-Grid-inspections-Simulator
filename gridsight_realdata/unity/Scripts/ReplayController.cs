using System.Collections;
using UnityEngine;

/// Plays back a real Alberta grid-stress day on the control room wall while the trainee flies.
/// Create replay files on the laptop:  python aeso_fetch.py replay 2024-01-13 "Jan 13 2024 grid alert, extreme cold"
public class ReplayController : MonoBehaviour {
    public ControlRoomDashboard dashboard;
    public MissionUrgency urgency;
    public string replayFile = "replay_2024-01-13.json";
    [Tooltip("Real seconds per historical hour")] public float secondsPerHour = 10f;
    [Tooltip("Start at this hour of the day (e.g. 16 = 4 pm)")] public int startHour = 16;

    bool running;

    // Hook this to a VR button ("Replay real day")
    public void StartReplay() { if (!running) StartCoroutine(Run()); }
    public void StopReplay() { running = false; }

    IEnumerator Run() {
        running = true;
        var client = GridDataClient.I;
        // Pause the live feed on the wall while replaying
        client.OnLive -= dashboard.Show; client.OnLive -= urgency.Apply;

        GridReplay r = null;
        yield return client.Load<GridReplay>(replayFile, v => r = v);
        if (r == null || r.hours == null) { running = false; yield break; }

        urgency.Set("HIGH", r.hours[Mathf.Min(startHour, r.hours.Length - 1)].load_mw);
        for (int i = startHour; i < r.hours.Length && running; i++) {
            dashboard.ShowReplayHour(r, r.hours[i]);
            yield return new WaitForSeconds(secondsPerHour);
        }
        running = false;
        client.OnLive += dashboard.Show; client.OnLive += urgency.Apply;
        if (client.Live != null) { dashboard.Show(client.Live); urgency.Apply(client.Live); }
    }
}
