# RVQ11B - Slaves of AI - GridSight VR

# Team Members : Alaik Kadiwar: Alvish Prasla : Shahbaz Khawaja 
## GridSight VR Grid Drone Training Simulator

GridSight is a Unity virtual-reality training simulator for electric-grid drone
operators. The trainee works from a grid control room, launches an FPV drone
to inspect a simulated electrical fault, captures evidence, and receives a
mission score based on safe distance, evidence, report accuracy, and time.

The simulator targets Meta Quest standalone VR using OpenXR and Quest
controllers.

## Requirements

- Unity **6000.6.8f1**
- Unity Hub with Android Build Support, Android SDK & NDK Tools, and OpenJDK
- Meta Quest headset and Quest controllers
- USB connection and Quest Developer Mode for standalone deployment

The project includes XR Interaction Toolkit, OpenXR, the Input System, and
Universal Render Pipeline packages.

## Open the project

1. Clone or download this repository.
2. Open Unity Hub and choose **Add**.
3. Select the repository folder.
4. Open it with Unity **6000.6.8f1**.
5. Wait for Unity to finish importing and compiling.
6. Open `Assets/Scenes/GridSight_ControlRoom.unity`.

If the control-room layout needs to be rebuilt, use:
   
**GridSight > Build Control Room Scene**

## Run in the Unity Editor

1. Open **File > Build Profiles** or **File > Build Settings**.
2. Confirm these scenes are included, in this order:
   1. `Assets/Scenes/GridSight_ControlRoom.unity`
   2. `Assets/Scenes/SampleScene.unity`
3. Open `GridSight_ControlRoom.unity`.
4. Press the Unity **Play** button.
5. Use a connected XR headset and controllers.

The control room is the starting scene. Point the right-controller ray at the
fault on the map table and press the right trigger to launch the drone mission.

## Build and run on Meta Quest

1. Connect the Quest and enable Developer Mode.
2. Open **File > Build Profiles** or **File > Build Settings**.
3. Select **Android** and choose **Switch Platform** if needed.
4. Confirm both scenes are included, with the control room first.
5. Open **Edit > Project Settings > XR Plug-in Management**.
6. Enable **OpenXR** for Android.
7. Configure the Meta Quest/Android controller features shown in the OpenXR
   settings.
8. Select the connected Quest as the run device.
9. Choose **Build And Run**.

For initial testing, use a development build so Unity can provide runtime
logs. Accept the USB debugging prompt on the headset.

## Quest controls

### Control room

- Right controller ray: point at interactive objects
- Right trigger: select or activate an object
- Left thumbstick: move
- Right thumbstick: turn
- Right controller A/primary button: continue past the mission-complete screen

### Drone mission

- Right controller A/primary button: arm or disarm the drone
- Left thumbstick: horizontal movement
- Right thumbstick horizontal: yaw
- Right trigger: increase throttle
- Left trigger: decrease throttle
- Grip on either controller: capture the fault photo

The drone must be armed with the throttle low before flight controls are
accepted.

## Training flow

1. Review the grid stress, demand, market, reserve, and outage information.
2. Select the simulated fault on the map table.
3. Fly the FPV drone to inspect the electrical fault.
4. Maintain a safe distance from the fault.
5. Capture evidence with either controller grip.
6. Return to the control room after the capture overlay.
7. Confirm that the dispatch screen changes to **DISPATCHED** and orange.
8. Review the mission-complete card with score, time, violations, and feedback.
9. Press the right-controller A button to continue.

Captured photos and mission state are cleared when the application starts and
closes, so each session begins with a fresh mission.

## Grid data

The control room can display live AESO grid data when a valid API key is
configured. Without live access, local fallback values keep the training flow
usable. The grid layout is illustrative; displayed live or historical values
are labeled in the control room. A January 3, 2022 record-demand replay is also
included.

## Troubleshooting

### A scene does not load

Check that both scenes are in Build Settings and that
`GridSight_ControlRoom` is listed before `SampleScene`.

### Quest controllers do not respond

Confirm that OpenXR is enabled for Android, the headset is tracking, the Quest
controllers are paired, and the Unity Input System is active.

### Unity reports script compile errors

Wait for importing to finish and fix the first Console compiler error first.
Later errors may be consequences of that initial error.

### Live AESO data is unavailable

The simulator should use fallback training values. Check network and API
configuration only if live data is required; the drone mission itself does not
require live data.
