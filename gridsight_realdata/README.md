# GridSight VR: real-data control room

A one-click Unity tool builds the whole VR control room. Real, published Alberta energy data drives it:

- **Map table:** Calgary's real transmission grid, traced from AESO's *Alberta Interconnected Electric System Map 2026* (Calgary Area Detail). It shows 232 line sections in the AESO legend colours (69 / 138 / 240 / 500 kV), 51 real substations, the city boundary and the rivers. See `docs/aies_calgary_map_preview.png`.
- **Front wall:** live AESO supply and demand, pool price, wind and solar, spare capacity, and today's demand curve.
- **Dispatch screen:** the mission's urgency changes with the live grid stress.
- **Operator console:** Outage alert → Crew en route → Power restored (on the real substation SS-41), plus "Replay real grid-alert day."
- **Right wall:** community impact from ENMAX's real 2024 reliability record (AUC Rule 002).

```
AESO public API ──HTTPS/API-KEY──> Quest: GridDataClient polls every 30 s
                                  └─ local StreamingAssets snapshot fallback
```

The Quest client now supports direct HTTPS polling. Register at the [AESO API portal](https://developer-apim.aeso.ca/),
subscribe to the AESO Public API product, and enter the subscription key on the `GridData` object in the control-room
scene. The app requests Current Supply Demand and Load Outage Forecast directly; no laptop or local server is required.

The control room also creates two live operator windows: **Supplier Dispatch** shows AESO fuel-category output,
capability, dispatchable headroom, wind, solar, gas-classified generation, and pool-participant response records;
**Market / Reserve / Outages** shows
load, pool price, contingency reserve, stress status, outage-feed state, asset/generation-capacity response counts,
load samples, and API health. Asset List and AIES Gen Capacity paths are configurable on `GridData` because AESO
may revise operation names in the developer portal.

Important: an API key embedded in a standalone APK can be extracted. For a public release, use a small authenticated
proxy instead of shipping a production key in the headset. The direct mode is intended for a controlled training build.

## 1. Unity: build the control room in one click (about 10 minutes)

1. In your Unity project (Unity 2022 LTS or Unity 6, URP recommended), copy:
   - `unity/Scripts/*.cs` → `Assets/GridSight/Scripts/`
   - `unity/Editor/GridSightControlRoomBuilder.cs` → `Assets/GridSight/Editor/` (it **must** be in a folder named `Editor`)
   - `unity/Data/aies_calgary_inset.json` → `Assets/GridSight/Data/`
2. Import TextMeshPro essentials: **Window > TextMeshPro > Import TMP Essential Resources**.
3. Install **XR Interaction Toolkit** (Package Manager) and import its *Starter Assets* sample. This is optional for desktop testing but needed for Quest controllers.
4. Menu **GridSight > Build Control Room Scene**. It creates and saves `Assets/Scenes/GridSight_ControlRoom.unity` with everything wired.
5. In the new scene:
   - Select **GridData** and set **Laptop Url** to `http://<laptop-ip>:8000/` (keep the final slash).
   - Drag **XR Origin (XR Rig)** from the XRI Starter Assets into the scene at position (0, 0, -0.9) and delete *Main Camera*.
6. On `GridData`, set `Use Aeso Api` to enabled and enter the AESO subscription key. The default HTTPS base URL is
   `https://apimgw.aeso.ca/public/`.
7. Build for Android/Quest and press Play. No laptop server is required. If the key is blank or the API is unavailable,
   the client uses files in `Assets/StreamingAssets` when supplied.

To change the outage substation, select **AIES_Map_Calgary** and edit **Outage Station** (for example SS-9, SS-31 or SS-54). The drone mission then takes place on a distribution feeder in that substation's area.

## 2. Laptop: the live data feed (about 15 minutes)

1. Get a free AESO API key from the AESO API portal (self-serve, issued instantly). Set it:
   - Windows: `setx AESO_API_KEY "your-key"` (then open a new terminal)
   - Mac/Linux: `export AESO_API_KEY="your-key"`
2. `pip install requests pandas openpyxl`
3. In `laptop/`, run once:
   - `python aeso_fetch.py impact` writes the ENMAX reliability baseline.
   - `python aeso_fetch.py eea` lists real AESO grid-alert days (2006 to Nov 2024). Pick one.
   - `python aeso_fetch.py replay 2024-01-13 "Jan 13 2024 grid alert, extreme cold"`. Set the ReplayController's *Replay File* to match.
4. `python aeso_fetch.py live` prints the real field names once, then updates every 60 s.
5. Second terminal: `cd laptop/data` then `python -m http.server 8000`. Find the laptop IP with `ipconfig` (Windows) or `ifconfig` (Mac).

**Demo backup:** before building for Quest, copy the JSON files from `laptop/data/` into `Assets/StreamingAssets/`. If the venue Wi-Fi drops, the wall shows this real (older) snapshot, labelled "SAVED SNAPSHOT". Never put made-up files there.

## 3. What is real, and what isn't

| On screen | Source | Real? |
| --- | --- | --- |
| Map table: transmission lines, voltages, substations, city boundary, rivers | AESO AIES Map 2026, Calgary Area Detail (traced from the PDF; not for engineering use) | Yes, published |
| Demand, pool price, wind, solar, reserves, spare capacity | AESO Current Supply Demand, Pool Price, Actual/Forecast APIs | Yes, live |
| Replay-day demand and prices | AESO historical data for a real grid-alert date from AESO's EEA file | Yes, historical |
| Customer-hours without power in Calgary | AUC Rule 002 2024 (ENMAX SAIFI 0.64, SAIDI 0.81 h) × about 580,000 sites | Yes (check against the AUC chart) |
| Stress thresholds (300 / 800 MW) | GridSight design rule | Our rule, so say so |
| Minutes saved per outage | Slider or the trainee's result | What-if, labelled |
| Fault location on the feeder | Training scenario | Simulated, so say so openly |

Note: substation names on the table were matched to the map labels automatically and then checked by eye. A few labels on the AESO map use leader lines, so check any name you plan to mention in the pitch against the PDF.

Pitch line: "The map is AESO's real Calgary grid, the wall is live AESO data, and the impact is ENMAX's real outage record. Only the fault itself is a training scenario."
