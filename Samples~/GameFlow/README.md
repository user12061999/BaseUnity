# Game Flow sample

Requires Unity 6 and Unity UI (`com.unity.ugui`). Supports the old input backend or the new Input System.

## Install and run

- For a Git submodule: **Tools > BaseUnity > Install Game Flow Sample**.
- For UPM: import **Game Flow** in Package Manager, then choose **Tools > BaseUnity > Game Flow > Generate Missing Scenes**.
- Open the generated **Scenes/Bootstrap.unity** and press Play.

The installer copies editable source into `Assets/BaseUnityGameFlow` and generates:

```text
GameFlowConfig.asset
Scenes/Bootstrap.unity   persistent services, flow controller and UI
Scenes/Home.unity        home scene content
Scenes/InGame.unity      gameplay scene content
```

Scene generation does not overwrite existing scenes or config. It puts the three scenes first in the shared Build Settings list and preserves other entries. If an active Unity 6 Build Profile overrides this list, add the three generated scenes to that profile, with Bootstrap first.

Start from Bootstrap; opening Home/InGame alone does not initialize the flow. Use only the sample bootstrap, not an additional GameBootstrap or QuickStartBootstrap. The sample owns one persistent Canvas/EventSystem; avoid duplicate EventSystems in Home/InGame.

## Optional Layer Lab UI

Import **Layer Lab GUI Pro-CasualGame** at `Assets/Layer Lab/GUI Pro-CasualGame`, then choose
**Tools > BaseUnity > Game Flow > Apply Layer Lab UI**. Select the destination GameFlowConfig
first if the project contains multiple configs.

The tool creates an editable `UI/LayerLabGameFlow.prefab` beside the config and assigns `uiPrefab`.
It uses the pack's sprites and Lilita One font for Home, HUD, Loading, Settings, Pause and Result.
Settings includes Master/Music/Effects sliders, mute, Save and Cancel. Modal screens block
underlying pointer and keyboard interaction. The 1600×900 landscape layout fits inside the safe
area, with a full-screen background at other aspect ratios; a dedicated portrait layout is not included.

Edit the generated prefab in Prefab Mode. Every generator run creates a new variant instead of
overwriting prefab edits. Clear `uiPrefab` to use the lightweight default UI.
Remove or disable `Demo Backdrop - remove for real gameplay` from InGame to expose your own scene,
then replace the demo collect/finish controls with game-specific HUD.

Only builder/binding code is distributed. Layer Lab textures, fonts and generated prefabs stay
in the game project; other projects using this theme need their own imported pack.

## Flow behavior

Bootstrap reads progress/settings → Loading → Home → Play → Loading → InGame.
InGame supports Collect +10 (demo), Pause/Resume and Finish session.
Result supports Retry or Home. Settings edits master volume/mute and persists on Save & Back.
Progress saves best score/session count. Pause and Result stop scaled game time. Scene transitions and exit restore time; UI remains usable while paused.

## Add your game

1. Change title/scene paths/save slot in GameFlowConfig.
2. Add gameplay objects to InGame. `GameFlowBootstrap` exposes `AddScore(int)`, `CompleteSession()`, `Pause()`, `Resume()`, `GoHome()` and `Play()`.
3. Subscribe to `SessionStarted` / `SessionEnded` via the core EventBus, disposing subscriptions with their owner. `SessionStarted` fires after scene loading completes; objects should subscribe in Awake/OnEnable.
4. Replace `GameFlowUI` with your own prefab-based screens. The sample uses generated uGUI screens so it needs no TMP font setup. Its full-screen demo view is intended to be replaced when adding visible gameplay.
5. Put project modifications in the installed copy. Updating the submodule leaves that copy untouched; port desired sample updates manually. There is deliberately no overwrite/update button.

Only generated scenes/config belong to the game project. The library repository distributes source plus the generator, not the host project's Assets/ProjectSettings/Library.

## Verify

After generating scenes, run `BaseUnity.GameFlow.Tests` in PlayMode. Its integration test drives the actual scenes and button callbacks through Settings cancel, Play, scoring, Pause/Resume, Result, saved progress, Retry and Home. It uses a temporary save slot and cleans it up. It does not replace visual/device input testing.
