# Changelog

## 0.20.0

- Add per-store sequential save migrations, validation before atomic persistence, optional SHA-256 envelope checksums, slot inspection and explicit backup restore. Preserve legacy saves and reject overwriting future versions.
- Add interruptible two-source music crossfades driven by unscaled time, bounded SFX voices, per-clip limits/cooldown and independent per-voice volume.
- Add optional Preparing/Countdown/Finishing session phases, input flushing, background stage suspension and restart without rewards for an abandoned run. Existing configs retain immediate gameplay by default.
- Add project-owned SessionOverlay prefab, Pause Restart action and Home/InGame audio profile with replaceable generated demo music. Repeat generation preserves authored assets and creates backups before assigning references.
- Add prebuild checks, save/audio/lifecycle regression coverage and integration guide.

## 0.19.1

- Fix slider/loading/quest progress endpoints: align fill to the track interior and clip full-size nine-sliced artwork instead of shrinking or stretching button sprites.
- Keep the existing Image.fillAmount/Slider API, values and callbacks; use a solid full-rectangle stencil to render exact zero and full progress.
- Add independent BarTrack/BarFill theme roles and a backed-up repair menu for existing owned UI assets.
- Generate the fix in new sample prefabs too; add geometry, callback and rendered 0/50/100-percent endpoint checks.

## 0.19.0

- Add semantic UIThemeConfig/Binding for common fonts, colors, sprites, selectable states and baseline text scaling.
- Add six shared component prefabs and link currency/item/quest presentations to the sample; support runtime theme switching for dynamic clones.
- Add an Editor Theme/Preview Workbench with 18 screens, English/Vietnamese/long text, safe area, phone/tablet presets and PNG export.
- Add 144-case layout reports covering safe bounds, small touch targets, missing sample glyphs and truncated text.
- Add project-scoped dependency application, pre-apply backups, Wizard integration and prebuild theme/library checks.
- Expand Settings touch targets without changing slider track appearance; add isolated preview/role/serialization/rendering tests.

## 0.18.0

- Remove sample booster catalog, runtime effects, HUD slots, use confirmation, generator and tests.
- Generate project-owned nested prefabs for Home, InGame, Loading, Settings, Pause, Result and Error without replacing the original UI.
- Add editable level-selection/cell, More menu/templates, queued popup, tutorial, toast/loading, ad waiting and result-reward prefabs.
- Reuse Store/Quest/Daily/Inventory prefabs; preserve runtime content binding, services, UI sound and effects.
- Preserve authored Result positions/colors and bind the privacy button from the Settings prefab.
- Add Wizard generation, reference checks and PlayMode coverage for prefab-backed flows.

## 0.17.0

- Add editable InGame HUD, goal progress, timer and inventory-backed booster slots.
- Add Extra Time and Tap Target Hint examples with a reusable reversible booster interface.
- Confirm use, pause only the owned dialog, and offer a coin Store purchase when inventory is empty.
- Roll back transient effects when inventory debit fails; reject unsupported/expired sessions and track per-level limits/scaled cooldowns.
- Keep purchase and activation separate; reuse existing wallet, inventory, localization and UI feedback services.
- Add non-destructive HUD/catalog/slot generation, Wizard options and prebuild binding/catalog checks.

## 0.16.0

- Add configurable UI cue profiles, custom clips and quiet generated fallback tones; honor AudioService Master/Effects/Mute.
- Add automatic button press/release, click ripple, optional desktop hover and slider/toggle/dropdown ticks.
- Add unscaled panel entry/close feedback and bounded pooled reward/win/lose/success/error particles.
- Emit balance deltas only after committed saves; animate currency grants without replaying loaded state or granting rewards from presentation.
- Add independent sound/motion/hover/reduced-motion controls to Feature Manager and a project-owned profile generator.
- Restore authored button scales on disable and avoid duplicate pointer/onClick feedback; reuse scan buffers and particle instances.

## 0.15.0

- Add an editable portrait Home hub with wallet, player card, level, Play/Levels/Settings and feature shortcuts.
- Add nested Store/Quest/card prefabs, optional Layer Lab styling and a separate presentation ScriptableObject catalog.
- Add game-currency purchase confirmation, affordability/limit/owned states and verified IAP purchase/restore/pending delivery UI.
- Add daily quest and lifetime achievement tabs with progress, transactional claims and live reward badges.
- Upgrade offline payload schema to 4 for durable achievement claims and cumulative score; preserve older progress and purchase receipts.
- Add optional hub generation to Setup Wizard and preserve existing generated prefabs/presentation assets.
- Coordinate hub overlays with settings, legacy daily/inventory UI, tutorials, popups and mobile settings.

## 0.14.0

- Add portable mobile Development/Release build presets with independent scene lists, APK/AAB selection, forced validation and build summaries.
- Keep Release free of development/debugger/profiler-connect flags; restore Android bundle/export settings after each build.
- Separate Editor and Development saves from the existing production directory, without migrating or overwriting production data.
- Add a portrait, safe-area Game Flow debug panel compiled only for Editor/Development players.
- Show SDK status/errors, FPS/memory/GC, asset leases, popup state and weakly tracked pool counters.
- Test win/lose/next/retry/home through normal Game Flow APIs; preserve pause ownership.
- Add confirmed test-save reset that cancels owned notifications, disposes services, archives test saves and reloads Bootstrap.
- Add regression coverage for build flags, save isolation/archival, pool diagnostics and debug flow controls.

## 0.13.0

- Add scoped asset leases, an optional Addressables provider and addressable level prefabs.
- Add durable tutorial steps, matching UI action targets, highlighting, skip/reset and optional Home startup.
- Add bounded priority/FIFO popup scheduling, deduplication, scope cancellation and presenter lifetimes.
- Add toast, nested loading scopes and shared confirmation UI.
- Add native Android/iOS haptics, throttling and saved player preferences.
- Add explicit notification permission, UTC/daily scheduling and owned-ID-only cancellation.
- Add Unity IAP 5 purchase/restore, metadata, pending retries and trusted HTTPS verification adapter.
- Save verified purchases atomically with wallet rewards/entitlements before store confirmation; upgrade Offline schema to 3.
- Honor Remove Ads for banners/interstitials while preserving optional rewarded ads.
- Add module controls to Feature Manager/Wizard, package installers and pre-build checks.
- Add portable Game Flow UI, usage sample and lifecycle/transaction regression coverage.

## 0.12.0

- Add Google UMP consent gathering, privacy options and retry/reset APIs with an optional SDK adapter.
- Gate AdMob/MAX initialization, loads and shows on UMP permission without delaying gameplay.
- Invalidate cached ads and suppress late rewards when privacy choices change; prevent overlapping native forms.
- Add hidden-when-disabled consent configuration, development-only testing and build validation.
- Add privacy options/retry buttons to fallback and Layer Lab sample Settings without rebuilding prefabs.
- Forward TFUA to AdMob requests and conservatively block MAX for the configured under-age audience.
- Verify AdMob adapter compilation against Google Mobile Ads Unity 11.6.0, including its new AdFormat type.
- Add consent lifecycle and validation regression tests.

## 0.11.0

- Setup Wizard for module switches, missing scenes/LevelData/UI, optional UniTask installation and post-setup validation.
- Persist pending setup work across script compilation and reload; redirect the original sample installer to the Wizard.
- Track installed Game Flow source baselines, show file states and line diffs, and preserve local/untracked changes by default.
- Apply reviewed updates with stale-preview/path checks, backups and rollback on write failure.
- Restore backups only when newer changes are preserved; keep existing GUIDs and protect generated game assets from code updates.
- Use portable project-relative manifests, capture setup/build-scene changes and expose backup review in the Editor.
- Add scoped scene generation and save only owned config assets.
- Add updater regression coverage including conflicts, deletions, failures, restoration, traversal and project relocation.

## 0.10.0

- Add an optional UniTask assembly with scene, Ads/Firebase and feature initialization adapters.
- Install pinned UniTask 2.5.11 from an Editor menu without vendoring SDK files into the library.
- Add UniTask timed pool returns, per-rental IDs to reject stale timers, pool counters and a disposal cancellation token.
- Preserve existing PrefabPool and Task-based service APIs.
- Add a cancelable pooling sample and PlayMode regression coverage for reuse, pause, cancellation and verified ad results.
- Include actual phase/focus details when sample flow tests time out.

## 0.9.0

- Run automatic validation before player builds using the actual BuildPlayerOptions scene list.
- Inspect saved scenes in temporary preview scenes and validate only referenced Bootstrap/GameFlow configs.
- Block errors, optionally treat warnings as errors, and reject test ads in Release by default.
- Project settings UI, Feature Manager shortcut and JSON report under Library/BaseUnity.
- Check Bootstrap order/root placement, missing scene scripts, invalid GameFlow levels/save slots and existing feature/UI rules.
- Add nine EditMode regression tests for build scope, policy, reports and preserving open scenes.

## 0.8.0

- Migrate legacy progress once into the offline profile; save level progress, rewards and missions in one transaction.
- Persist unacknowledged results and verified rewarded receipts; restore results and finish recorded bonuses once after restart.
- Validate valuable saves, recover from backup without overwriting the good backup, preserve corrupt files and reject future schemas.
- Keep unified progression when economy is disabled; block gameplay on unreadable startup data.
- Generate editable menu, Shop, Daily Reward, Missions, Inventory and item-row prefabs without overwriting existing project assets.
- Selected tabs, saving blocker, success/error feedback and unscaled popup animations; retain the code-built UI fallback.
- Validate prefab bindings and expand save fault/restart/UI regression coverage.

## 0.7.0

- Optional offline wallet, inventory, transactional shop, first-win rewards, daily rewards and daily missions.
- English/Vietnamese localization, custom text overrides and saved language selection.
- Portrait Home menu and result reward controls, reusing project-owned Layer Lab button art when available.
- Verified rewarded-ad multiplier, preloaded interstitial cadence, Home banner, flow analytics and optional Remote Config fetch.
- Retry failed progress/reward saves before leaving results; prevent duplicate completion/reward callbacks.
- Feature Manager tabs and validation for offline, localization and flow integration settings.
- Firebase Editor missing-config guard keeps offline gameplay usable without native configuration errors.
- Economy/localization tests and PlayMode coverage for UI transactions, rewards and save retries.

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
