# Changelog

## 0.5.0

- LevelData assets and a non-destructive generator for ordered level definitions and gameplay prefabs.
- LevelManager instantiates scene-local gameplay, resets it on Retry/Next and cleans up on Home.
- Session-scoped LevelContext connects prefab score/Win/Lose callbacks and rejects stale or paused input.
- Portrait level selection with locked/completed states, six levels per page and replay of unlocked levels.
- Playable tap-target/trap sample; the HUD automatically hides legacy demo controls when a gameplay prefab is present.
- PlayMode coverage for prefab lifecycle, stale callbacks and paginated selection.

## 0.4.0

- Portrait 900×1600 UI, level/target/timer HUD and distinct Win/Lose result states.
- Configurable ordered levels with score targets, optional timers and scene overrides.
- Win, Lose, Retry and Next Level APIs; only wins unlock levels, final level cannot advance past the list.
- Persistent unlocked/completed level and win/loss counts, compatible with older score/session saves.
- PlayMode coverage for duplicate finish calls, locked levels, timer pause/timeout, restart persistence and legacy saves.

## 0.3.0

- Optional Layer Lab UI builder creates a project-owned editable prefab using installed CasualGame sprites/fonts.
- Home, gameplay HUD, Settings sliders, Pause, Result and Loading with modal input blocking.
- Safe-area layout and full-screen background; original code-built UI remains available without the art pack.

## 0.2.0

- Game Flow sample: Bootstrap → Home → InGame → Result, Settings, Pause/Resume, Retry, Loading.
- Editor installer copies editable source and generates scenes/config without overwriting game changes.
- Best score/session persistence, scene validation, transition input guard and gameplay event hooks.
- PlayMode integration test for scene and button flows.

## 0.1.0

- Repository thư viện cho Git submodule và UPM, tách khỏi Unity sandbox.
- Bootstrap, service registry, event bus, state machine, timer và logging.
- Scene loading, JSON save/backup/version, audio, UI stack, prefab pooling.
- Manual input và adapter Unity Input System tùy chọn.
- Editor menu, quick-start sample và EditMode/PlayMode tests.
