using System;
using System.Collections;
using System.IO;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using UnityEngine;
using UnityEngine.Networking;

// Data shapes match the JSON written by laptop/aeso_fetch.py
[Serializable] public class GridLive {
    public string mode, source, fetched_utc, effective_time, stress_level, stress_reason;
    public float load_mw, dispatchable_generation_mw, dispatchable_capability_mw, headroom_mw, wind_mw, solar_mw, gas_mw;
    public float reserve_required_mw, reserve_dispatched_mw, pool_price;
    public float[] load_today;
    public AesoFuelSummary[] fuel_mix;
    public bool outage_forecast_received;
}
[Serializable] public class AesoFuelSummary {
    public string fuel_type;
    public float generation_mw, capability_mw;
}
[Serializable] public class ReplayHour { public string t; public float load_mw, price; }
[Serializable] public class GridReplay { public string mode, date, label, source; public ReplayHour[] hours; }
[Serializable] public class ImpactBaseline { public string utility; public float saifi, saidi_hours, customers; public string[] sources; }

/// Polls AESO directly on Quest when an API key is configured. Falls back to local snapshots.
public class GridDataClient : MonoBehaviour {
    public static GridDataClient I;
    [Header("AESO direct connection")]
    public bool useAesoApi = true;
    [Tooltip("Register at https://developer-apim.aeso.ca and subscribe to AESO Public API.")]
    public string aesoApiKey = "";
    public string aesoApiBaseUrl = "https://apimgw.aeso.ca/public/";
    public string actualForecastPath = "actualforecast-api/v1/load/albertaInternalLoad";
    public string poolPricePath = "poolprice-api/v1.1/price/poolPrice";
    public string outageForecastPath = "loadoutageforecast-api/v1/loadOutageForecast";
    public string assetListPath = "assetlist-api/v1/assetList";
    public string generationCapacityPath = "aiesgencapacity-api/v1/generationCapacity";
    public string poolParticipantPath = "poolparticipant-api/v1/poolParticipant";
    public bool createOperationsWindows = true;
    [Header("Polling")]
    public float pollSeconds = 30f;
    [Tooltip("Optional legacy laptop/snapshot URL.")]
    public string laptopUrl = "";

    public GridLive Live { get; private set; }
    public ImpactBaseline Impact { get; private set; }
    public bool UsingSnapshot { get; private set; }
    public bool AesoOnline { get; private set; }
    public string LastAesoError { get; private set; }
    public string LastSuccessfulFetchUtc { get; private set; }
    public string LatestOutageForecastJson { get; private set; }
    public string LatestAssetListJson { get; private set; }
    public string LatestGenerationCapacityJson { get; private set; }
    public string LatestPoolParticipantJson { get; private set; }
    public event Action<string> OnOutageForecast;
    public event Action<GridLive> OnLive;

    void Awake() {
        I = this;
        if (createOperationsWindows)
            gameObject.AddComponent<GridOperationsWindows>();
    }

    IEnumerator Start() {
        yield return Load<ImpactBaseline>("impact_baseline.json", v => Impact = v);
        while (true) {
            if (useAesoApi && !string.IsNullOrWhiteSpace(aesoApiKey)) {
                yield return PollAeso();
            } else {
                yield return Load<GridLive>("grid_live.json", v => { Live = v; OnLive?.Invoke(v); });
            }
            yield return new WaitForSeconds(pollSeconds);
        }
    }

    public IEnumerator Load<T>(string file, Action<T> done) {
        if (!string.IsNullOrWhiteSpace(laptopUrl)) {
            using (var req = UnityWebRequest.Get(laptopUrl + file)) {
                req.timeout = 5;
                yield return req.SendWebRequest();
                if (req.result == UnityWebRequest.Result.Success) {
                    UsingSnapshot = false;
                    done(JsonUtility.FromJson<T>(req.downloadHandler.text));
                    yield break;
                }
            }
        }
        string path = Path.Combine(Application.streamingAssetsPath, file);
        if (!path.Contains("://")) path = "file://" + path;
        using (var req = UnityWebRequest.Get(path)) {
            req.timeout = 5;
            yield return req.SendWebRequest();
            if (req.result == UnityWebRequest.Result.Success) {
                UsingSnapshot = true;
                done(JsonUtility.FromJson<T>(req.downloadHandler.text));
            } else {
                Debug.LogWarning($"GridSight: could not load {file}: {req.error}", this);
            }
        }
    }

    IEnumerator PollAeso() {
        string current = null;
        yield return RequestAeso("currentsupplydemand-api/v2/csd/summary/current", body => current = body);
        string today = DateTime.UtcNow.AddHours(-7).ToString("yyyy-MM-dd");
        string loadReport = null;
        yield return RequestAeso($"{actualForecastPath}?startDate={today}", body => loadReport = body);
        string priceReport = null;
        yield return RequestAeso($"{poolPricePath}?startDate={today}", body => priceReport = body);

        if (!string.IsNullOrEmpty(current)) {
            var response = JsonUtility.FromJson<AesoCsdResponse>(current);
            if (response?.@return != null) {
                Live = AesoLiveMapper.Map(response.@return);
                var loads = AesoReportParser.Numbers(loadReport, "alberta_internal_load");
                var prices = AesoReportParser.Numbers(priceReport, "pool_price");
                if (loads.Count > 0) Live.load_today = loads.ToArray();
                if (prices.Count > 0) Live.pool_price = prices[prices.Count - 1];
                Live.source = "AESO Current Supply Demand + Actual/Forecast Load + Pool Price APIs (direct Quest connection)";
                UsingSnapshot = false;
                AesoOnline = true;
                LastAesoError = "";
                LastSuccessfulFetchUtc = DateTime.UtcNow.ToString("o");
                OnLive?.Invoke(Live);
            }
        }
        else {
            yield return Load<GridLive>("grid_live.json", v => {
                Live = v;
                OnLive?.Invoke(v);
            });
        }

        string outage = null;
        yield return RequestAeso($"{outageForecastPath}?startDate={today}&endDate={today}", body => outage = body);
        if (!string.IsNullOrEmpty(outage)) {
            LatestOutageForecastJson = outage;
            if (Live != null) Live.outage_forecast_received = true;
            OnOutageForecast?.Invoke(outage);
        }

        yield return RequestAeso(assetListPath, body => LatestAssetListJson = body);
        yield return RequestAeso(generationCapacityPath, body => LatestGenerationCapacityJson = body);
        yield return RequestAeso(poolParticipantPath, body => LatestPoolParticipantJson = body);
    }

    public static class AesoReportParser {
        public static List<float> Numbers(string json, string field) {
            var values = new List<float>();
            if (string.IsNullOrEmpty(json)) return values;
            string pattern = $"\"{Regex.Escape(field)}\"\\s*:\\s*(-?(?:\\d+\\.?\\d*|\\.\\d+))";
            foreach (Match match in Regex.Matches(json, pattern, RegexOptions.IgnoreCase)) {
                if (float.TryParse(match.Groups[1].Value, System.Globalization.NumberStyles.Float,
                        System.Globalization.CultureInfo.InvariantCulture, out float value)) {
                    values.Add(value);
                }
            }
            return values;
        }
    }

    IEnumerator RequestAeso(string path, Action<string> completed) {
        string url = aesoApiBaseUrl.TrimEnd('/') + "/" + path.TrimStart('/');
        using (var req = UnityWebRequest.Get(url)) {
            req.timeout = 15;
            req.SetRequestHeader("API-KEY", aesoApiKey);
            yield return req.SendWebRequest();
            if (req.result == UnityWebRequest.Result.Success) {
                completed(req.downloadHandler.text);
            } else {
                AesoOnline = false;
                LastAesoError = $"HTTP {req.responseCode}: {req.error}";
                Debug.LogWarning($"GridSight AESO request failed ({req.responseCode}): {req.error}", this);
            }
        }
    }
}

[Serializable] public sealed class AesoCsdResponse { public AesoCsdData @return; }
[Serializable] public sealed class AesoCsdData {
    public string effective_datetime_mpt, effective_datetime_utc;
    public float alberta_internal_load, alberta_total_load;
    public float contingency_reserve_required;
    public float dispatched_contigency_reserve_total, dispatched_contingency_reserve_total;
    public AesoGeneration[] generation_data_list;
}
[Serializable] public sealed class AesoGeneration {
    public string fuel_type;
    public float aggregated_maximum_capability, aggregated_net_generation;
}

public static class AesoLiveMapper {
    public static GridLive Map(AesoCsdData data) {
        var generations = data.generation_data_list ?? Array.Empty<AesoGeneration>();
        float dispatchableGeneration = 0f, dispatchableCapability = 0f, wind = 0f, solar = 0f, gas = 0f;
        var fuelMix = new List<AesoFuelSummary>();
        foreach (var generation in generations) {
            string fuel = (generation.fuel_type ?? "").ToUpperInvariant();
            fuelMix.Add(new AesoFuelSummary {
                fuel_type = generation.fuel_type,
                generation_mw = generation.aggregated_net_generation,
                capability_mw = generation.aggregated_maximum_capability
            });
            if (fuel == "WIND") wind += generation.aggregated_net_generation;
            else if (fuel == "SOLAR") solar += generation.aggregated_net_generation;
            else {
                dispatchableGeneration += generation.aggregated_net_generation;
                dispatchableCapability += generation.aggregated_maximum_capability;
                if (fuel.Contains("GAS")) gas += generation.aggregated_net_generation;
            }
        }
        float headroom = dispatchableCapability - dispatchableGeneration;
        string level = headroom < 300f ? "HIGH" : headroom < 800f ? "ELEVATED" : "NORMAL";
        return new GridLive {
            mode = "live",
            source = "AESO Current Supply Demand API (direct Quest connection)",
            fetched_utc = DateTime.UtcNow.ToString("o"),
            effective_time = data.effective_datetime_mpt ?? data.effective_datetime_utc,
            stress_level = level,
            stress_reason = $"{headroom:0} MW of unused dispatchable capability",
            load_mw = data.alberta_internal_load > 0 ? data.alberta_internal_load : data.alberta_total_load,
            dispatchable_generation_mw = dispatchableGeneration,
            dispatchable_capability_mw = dispatchableCapability,
            headroom_mw = headroom,
            wind_mw = wind,
            solar_mw = solar,
            gas_mw = gas,
            pool_price = -1f,
            reserve_required_mw = data.contingency_reserve_required,
            reserve_dispatched_mw = data.dispatched_contingency_reserve_total != 0
                ? data.dispatched_contingency_reserve_total
                : data.dispatched_contigency_reserve_total,
            load_today = Array.Empty<float>(),
            fuel_mix = fuelMix.ToArray(),
            outage_forecast_received = false
        };
    }
}
