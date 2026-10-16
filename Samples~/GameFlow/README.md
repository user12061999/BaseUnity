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
Levels/Level001.asset    per-level configuration (also Level002/003)
Levels/TapTargetGameplay.prefab   playable example
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
underlying pointer and keyboard interaction. The 900×1600 portrait layout fits inside the safe
area, with a full-screen background at other aspect ratios. Set the game's default orientation
to Portrait in Player Settings when deploying to mobile.

Edit the generated prefab in Prefab Mode. Every generator run creates a new variant instead of
overwriting prefab edits. Clear `uiPrefab` to use the lightweight default UI.
The view automatically hides its demo backdrop and Win/Lose/Collect controls when a level has a
gameplay prefab, exposing the scene and keeping the score, timer and pause HUD.

Only builder/binding code is distributed. Layer Lab textures, fonts and generated prefabs stay
in the game project; other projects using this theme need their own imported pack.

## Flow behavior

Bootstrap reads progress/settings → Loading → Home → Play → Loading → InGame.
Home offers Play and Choose Level. Selection shows locked, playable and completed levels, six per page.
The prefab example awards points for tapping a moving target and loses when the red trap is tapped.
Levels without a prefab retain the legacy Collect +10 and Win/Lose demo controls.
Win offers Next Level, Retry and Home. Lose offers Retry/Home. The final win shows all levels complete.
Settings persists only on Save. Pause and Result stop scaled game time, including the level timer;
scene transitions restore time, and UI remains usable while paused.

## Level configuration and gameplay API

`GameFlowConfig.levels` is an ordered list, numbered from 1. The three defaults have target scores
30/50/80 and a 60-second timer. A zero timer is unlimited. `sceneOverride` optionally selects a
different Build Settings scene for a level; otherwise all levels use the common InGame scene.
An empty/invalid level list produces a configuration error instead of starting a session.

Each list entry can reference a `LevelData` asset. When assigned, that asset supplies the title,
target score, time limit, scene override and gameplay prefab; inline values are ignored.
Create assets with **Assets > Create > BaseUnity > Level Data** and add references in config order.
To upgrade an existing installation, use **Tools > BaseUnity > Game Flow > Generate Level Assets**.
It converts inline entries into assets and supplies the tap example where no prefab was assigned.
Existing LevelData references and existing demo prefabs are never overwritten. An already customized
scene-only game should create its LevelData manually and leave gameplayPrefab empty.

`flow.Levels` exposes the `LevelManager`, including `Instance`, `GameplayRoot`, `Context`,
`StartLevel(number)`, `RetryLevel()` and `NextLevel()`. It creates a fresh root in the loaded scene,
initializes `ILevelGameplay` components while the root is inactive, then activates it. Use Initialize
to capture the session; gameplay starts when `context.IsPlaying` becomes true or SessionStarted fires.
Retry/Next destroy the old root and instantiate fresh gameplay; Home clears it. Never put another
bootstrap or EventSystem in a gameplay prefab, or move managed gameplay to DontDestroyOnLoad.

```csharp
public sealed class MyGameplay : UnityEngine.MonoBehaviour, BaseUnity.GameFlow.ILevelGameplay
{
    private BaseUnity.GameFlow.LevelContext context;
    public void Initialize(BaseUnity.GameFlow.LevelContext session) => context = session;
    public void Collect() => context.AddScore(10);
    public void PuzzleSolved() => context.Win();
    public void OutOfMoves() => context.Lose("Out of moves!");
}
```

These callbacks are ignored outside Playing and after the session is replaced. Use the context in
delayed callbacks rather than finding the global bootstrap, so old callbacks cannot finish a new level.
The sample tap game explicitly wins at its target even if autoWinAtTarget is disabled; replace it
with your own prefab for a different win rule.

```csharp
var flow = (BaseUnity.GameFlow.GameFlowBootstrap)BaseUnity.Core.GameBootstrap.Instance;
flow.AddScore(10);    // Wins automatically at target when autoWinAtTarget is enabled.
flow.WinLevel();      // Or call when your own gameplay win condition is met.
flow.LoseLevel("Out of moves!");
flow.RetryLevel();    // Only from Result, stays on the same level.
flow.NextLevel();     // Only after Win, only when another level exists.
```

Call the relevant method at the relevant gameplay event; the lines above demonstrate separate actions.
Disable `autoWinAtTarget` for games whose win condition is unrelated to score.
`CurrentLevel`, `CurrentDefinition`, `RemainingTime`, `Outcome` and `CanNextLevel` expose live state.
`StartLevel(number)` accepts only unlocked levels from Home/LevelSelect/Result. Play at Home resumes the highest
unlocked level. Retry resets score, timer and outcome. Concurrent load requests and repeated end
calls are ignored, so a level cannot be counted twice.

Win updates the highest completed/unlocked level; Lose never unlocks a level. Scores, sessions,
wins and losses persist in the configured save slot. Old saves with only score/session fields are
normalized to level 1 without losing their existing data. The last level is capped; it does not loop.
Keep level ordering stable in shipped games: progress currently uses level numbers, not stable IDs.
If saving fails, the result UI reports it and progress remains available for the current run.

## Add your game

1. Change title/scene paths/save slot in GameFlowConfig.
2. Create LevelData assets, assign your gameplay prefabs and add them to the ordered config list. Implement ILevelGameplay to receive the session context. Scene-only gameplay remains supported. Legacy `CompleteSession()` remains an alias for `WinLevel()`.
3. Subscribe to `SessionStarted` / `SessionEnded` via the core EventBus, disposing subscriptions with their owner. Both carry the level number; ended events also carry score/outcome. `SessionStarted` fires after scene loading completes; objects should subscribe in Awake/OnEnable.
4. Customize the generated Layer Lab prefab or use the plain UI. Both support level selection and keep managed prefab gameplay visible. The sample uses uGUI, requiring no TMP font setup.
5. Put project modifications in the installed copy. Updating the submodule leaves that copy untouched; port desired sample updates manually. There is deliberately no overwrite/update button.

Only generated scenes/config belong to the game project. The library repository distributes source plus the generator, not the host project's Assets/ProjectSettings/Library.

## Verify

After generating scenes, run `BaseUnity.GameFlow.Tests` in PlayMode. Its integration test drives the actual scenes and button callbacks through Settings cancel, Play, scoring, Pause/Resume, Result, saved progress, Retry and Home. It uses a temporary save slot and cleans it up. It does not replace visual/device input testing.

LevelFlowTests also checks prefab cleanup/recreation, stale callbacks, selection pagination,
win/lose guards, locked levels, next-level loading, timer pause/timeout,
final-level behavior, progress after restarting the bootstrap, and old-save compatibility.
Tests clone config and use disposable save slots to preserve project settings and player progress.
