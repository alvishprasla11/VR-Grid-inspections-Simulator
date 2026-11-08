using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// End-of-mission impact screen. Baseline = real ENMAX reliability data reported to the AUC (Rule 002).
/// The only assumption is the slider: minutes saved per outage by faster drone fault-finding.
public class ImpactScreen : MonoBehaviour {
    public TMP_Text baselineText, resultText, sourceText;
    public Slider minutesSavedSlider;   // e.g. range 0-30, set from the trainee's own mission time if you like

    void Start() {
        if (minutesSavedSlider) minutesSavedSlider.onValueChanged.AddListener(_ => Refresh());
        Invoke(nameof(Refresh), 1f);
    }

    /// Optional: call with the trainee's real result, e.g. how much faster than the patrol benchmark they were
    public void SetMinutesSaved(float m) { if (minutesSavedSlider) minutesSavedSlider.value = m; Refresh(); }

    public void Refresh() {
        var b = GridDataClient.I ? GridDataClient.I.Impact : null;
        if (b == null) return;
        float interruptionsPerYear = b.saifi * b.customers;          // customer interruptions / year
        float baselineHours = b.saidi_hours * b.customers;           // customer-hours without power / year
        float m = minutesSavedSlider ? minutesSavedSlider.value : 10f;
        float savedHours = interruptionsPerYear * m / 60f;

        baselineText.text = $"{b.utility}: {baselineHours:N0} customer-hours without power per year\n" +
                            $"<size=60%>{b.saifi} outages × {b.saidi_hours} h per customer, ~{b.customers:N0} customers</size>";
        resultText.text = $"If drone inspection saves {m:0} min per outage:\n" +
                          $"<b>{savedHours:N0} customer-hours</b> of power back each year";
        sourceText.text = "Real data: " + string.Join(", ", b.sources) + ". Minutes saved is a what-if.";
    }
}
