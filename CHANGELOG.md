# Changelog

## 0.6.4

- Supply Unity's configured JDK as process-local JAVA_HOME when it is missing, so Android Resolver's Gradle wrapper can locate Java.
- Preserve explicit JAVA_HOME values and leave system/user environment variables unchanged; include a manual configuration menu.

## 0.6.3

- Read-only pre-build validation window, accessible from Feature Manager > Project Setup or the Tools menu.
- Platform-specific checks for enabled Ads/Firebase modules, providers, SDK types, ad IDs, test settings and Firebase app configuration.
- Checks enabled build scenes, selected GameFlow Home/InGame/level overrides and FeatureConfig assignment.
- EditMode regression coverage for disabled modules/formats, platform selection, demo ads and missing build scenes.

## 0.6.2

- Feature Manager hides disabled Ads/Firebase settings while preserving saved values.
- Ad-unit IDs, interstitial cooldown, MAX test devices and Remote Config settings follow their feature switches.
- MAX tab includes guidance for adding AdMob through mediation; stale notices clear when editing or switching contexts.

## 0.6.1

- Feature Manager utility window with Ads, Firebase and Project Setup tabs; config save and Undo support.
- AdMob/MAX selection, app IDs, ad-unit IDs, SDK key and provider creation/assignment without editing source.
- Optional sync of non-empty app IDs/SDK key into installed vendor settings, with App ID validation.
- Validated Firebase JSON/plist import, matching application identifiers, ID preview and replacement backups.
- Five EditMode tests for Firebase parsing/client selection and App ID validation.

## 0.6.0

- Optional Ads/Firebase services with per-feature config, SDK-independent core and non-blocking initialization.
- Ads facade for banner/interstitial/rewarded, cooldown, request timeout, duplicate-show guards and cancellation-safe rewards.
- Optional AdMob and AppLovin MAX adapter source, installed only after the corresponding SDK is present.
- Firebase Analytics, Crashlytics and Remote Config adapter with local defaults and bounded fetch operations.
- Feature config editor/creation menu, GameFlow integration and SDK setup documentation.
- Tests cover disabled/missing providers, feature flags, reward gating, shutdown races and remote defaults.

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
