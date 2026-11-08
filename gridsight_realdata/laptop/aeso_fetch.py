"""
GridSight VR - real AESO data feeder.

Runs on a laptop on the same Wi-Fi as the Quest. Pulls real, published AESO data
and writes small JSON files that the VR control room reads.

Modes
  python aeso_fetch.py live                 # every 60 s -> data/grid_live.json
  python aeso_fetch.py replay 2024-01-13    # one real day -> data/replay_2024-01-13.json
  python aeso_fetch.py eea                  # list real grid-alert days from AESO's EEA file
  python aeso_fetch.py impact               # writes data/impact_baseline.json (AUC Rule 002)

Then serve the folder so the headset can read it:
  cd data && python -m http.server 8000

Needs: pip install requests   (and pandas openpyxl for the 'eea' mode)
API key: free, self-serve from the AESO API portal. Set it as an environment variable:
  Windows:  setx AESO_API_KEY "your-key"     macOS/Linux: export AESO_API_KEY="your-key"
Never put the key inside the Unity / Quest build.
"""
import json, os, sys, time, datetime as dt
import requests

BASE = "https://apimgw.aeso.ca/public"
KEY = os.environ.get("AESO_API_KEY", "")
OUT = os.path.join(os.path.dirname(os.path.abspath(__file__)), "data")
os.makedirs(OUT, exist_ok=True)

# GridSight design thresholds for the "grid stress" level (our rule, not an AESO definition).
# Headroom = unused dispatchable capability = max capability - net generation, excluding wind/solar (all real AESO numbers).
STRESS_HIGH_MW = 300
STRESS_ELEVATED_MW = 800


def get(path):
    if not KEY:
        sys.exit("Set AESO_API_KEY first (free key from the AESO API portal).")
    r = requests.get(f"{BASE}/{path}", headers={"API-KEY": KEY}, timeout=30)
    r.raise_for_status()
    return r.json()


def num(x):
    try:
        return float(x)
    except (TypeError, ValueError):
        return None


def pick(d, *names):
    for n in names:
        if n in d and num(d[n]) is not None:
            return num(d[n])
    return None


def clean(o):
    # Unity's JsonUtility can't read null numbers: missing values become -1 and show as a dash in VR
    if isinstance(o, dict): return {k: clean(v) for k, v in o.items()}
    if isinstance(o, list): return [clean(v) for v in o]
    return -1 if o is None else o


def write(name, obj):
    obj = clean(obj)
    tmp = os.path.join(OUT, name + ".tmp")
    with open(tmp, "w") as f:
        json.dump(obj, f, indent=1)
    os.replace(tmp, os.path.join(OUT, name))  # atomic, so Unity never reads half a file


def today_mt():
    # Alberta date (Mountain Time); good enough without tz libraries
    return (dt.datetime.utcnow() - dt.timedelta(hours=7)).strftime("%Y-%m-%d")


def hourly_load(start, end=None):
    q = f"actualforecast-api/v1/load/albertaInternalLoad?startDate={start}" + (f"&endDate={end}" if end else "")
    rows = get(q)["return"]["Actual Forecast Report"]
    return [{"t": r.get("begin_datetime_mpt") or r.get("begin_datetime_utc"),
             "load_mw": num(r.get("alberta_internal_load")),
             "forecast_mw": num(r.get("forecast_alberta_internal_load"))} for r in rows]


def hourly_price(start, end=None):
    q = f"poolprice-api/v1.1/price/poolPrice?startDate={start}" + (f"&endDate={end}" if end else "")
    rows = get(q)["return"]["Pool Price Report"]
    return [{"t": r.get("begin_datetime_mpt") or r.get("begin_datetime_utc"),
             "price": num(r.get("pool_price"))} for r in rows]


def live_once(first=False):
    csd = get("currentsupplydemand-api/v2/csd/summary/current")["return"]
    if first:  # print the real field names once, so you can see what AESO sends
        print("CSD fields:", sorted(k for k in csd.keys() if not isinstance(csd[k], list)))
    gens = csd.get("generation_data_list", []) or []
    by_fuel = {g.get("fuel_type", "?").upper(): g for g in gens}
    # Headroom counts only generation that can be turned up on demand (wind and solar can't)
    disp = [g for g in gens if g.get("fuel_type", "").upper() not in ("WIND", "SOLAR")]
    max_cap = sum(num(g.get("aggregated_maximum_capability")) or 0 for g in disp)
    net_gen = sum(num(g.get("aggregated_net_generation")) or 0 for g in disp)
    load = pick(csd, "alberta_internal_load", "alberta_total_load")
    headroom = max_cap - net_gen if gens else None

    price_rows = [p for p in hourly_price(today_mt()) if p["price"] is not None]
    load_rows = [l for l in hourly_load(today_mt()) if l["load_mw"] is not None]

    level, reason = "NORMAL", "Grid has comfortable headroom"
    if headroom is not None and headroom < STRESS_HIGH_MW:
        level, reason = "HIGH", f"Only {headroom:.0f} MW of unused capability"
    elif headroom is not None and headroom < STRESS_ELEVATED_MW:
        level, reason = "ELEVATED", f"{headroom:.0f} MW of unused capability"

    data = {
        "mode": "live",
        "source": "AESO Current Supply Demand + Pool Price + Actual/Forecast load APIs",
        "fetched_utc": dt.datetime.utcnow().isoformat(timespec="seconds") + "Z",
        "effective_time": csd.get("effective_datetime_mpt") or csd.get("effective_datetime_utc"),
        "load_mw": load,
        "dispatchable_generation_mw": net_gen or None,
        "dispatchable_capability_mw": max_cap or None,
        "headroom_mw": headroom,
        "wind_mw": num(by_fuel.get("WIND", {}).get("aggregated_net_generation")),
        "solar_mw": num(by_fuel.get("SOLAR", {}).get("aggregated_net_generation")),
        "gas_mw": sum(num(by_fuel.get(k, {}).get("aggregated_net_generation")) or 0
                      for k in ("COGENERATION", "COMBINED CYCLE", "SIMPLE CYCLE", "GAS FIRED STEAM")),
        "reserve_required_mw": pick(csd, "contingency_reserve_required"),
        "reserve_dispatched_mw": pick(csd, "dispatched_contigency_reserve_total", "dispatched_contingency_reserve_total"),
        "pool_price": price_rows[-1]["price"] if price_rows else None,
        "load_today": [r["load_mw"] for r in load_rows],
        "stress_level": level,
        "stress_reason": reason,
    }
    write("grid_live.json", data)
    print(f"{data['fetched_utc']}  load={load}  headroom={headroom}  price={data['pool_price']}  -> {level}")


def replay(date, label=""):
    d = dt.date.fromisoformat(date)
    nxt = (d + dt.timedelta(days=1)).isoformat()
    loads = hourly_load(date, nxt)
    prices = {p["t"]: p["price"] for p in hourly_price(date, nxt)}
    hours = [{"t": l["t"], "load_mw": l["load_mw"], "price": prices.get(l["t"])} for l in loads]
    data = {"mode": "replay", "date": date, "label": label or f"Real AESO data for {date}",
            "source": "AESO Actual/Forecast load + Pool Price APIs (historical)", "hours": hours}
    write(f"replay_{date}.json", data)
    print(f"wrote replay_{date}.json with {len(hours)} hours")


def eea():
    import pandas as pd
    url = "https://www.aeso.ca/assets/Uploads/data-requests/EEA-Events-2006-Nov_21_2024.xlsx"
    df = pd.read_excel(url)
    print("Columns:", list(df.columns))
    print(df.tail(25).to_string())
    df.to_csv(os.path.join(OUT, "aeso_eea_events.csv"), index=False)
    print("Saved data/aeso_eea_events.csv. Pick a date and run: python aeso_fetch.py replay YYYY-MM-DD")


def impact():
    # AUC Rule 002 Compliance Summary Report 2024 (ENMAX Power, urban). Verify against the AUC chart before presenting.
    data = {
        "utility": "ENMAX Power (Calgary)",
        "saifi": 0.64,  # interruptions per customer per year, 2024
        "saidi_hours": 0.81,  # outage hours per customer per year, 2024
        "customers": 580000,  # metered sites, ENMAX Power fact sheet (approximate)
        "sources": ["AUC Rule 002 Compliance Summary Report 2024", "ENMAX Power fact sheet"],
    }
    write("impact_baseline.json", data)
    print("wrote impact_baseline.json")


if __name__ == "__main__":
    mode = sys.argv[1] if len(sys.argv) > 1 else "live"
    if mode == "live":
        first = True
        while True:
            try:
                live_once(first); first = False
            except Exception as e:  # keep the last good file if AESO hiccups
                print("fetch failed, keeping last file:", e)
            time.sleep(60)
    elif mode == "replay":
        replay(sys.argv[2], " ".join(sys.argv[3:]))
    elif mode == "eea":
        eea()
    elif mode == "impact":
        impact()
    else:
        print(__doc__)
