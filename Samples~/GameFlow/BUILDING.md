# Mobile builds and development diagnostics

Requires Unity 6, an imported Game Flow sample and the platform build module installed with Unity Hub.
The core build/save environment and pool diagnostics remain independent of Unity UI.

## Build presets

Open **Tools > BaseUnity > Mobile Build Profiles**. Each tab owns a separate ordered scene list;
copy the enabled scenes from the active Unity Build Profile or assign scene assets manually.
Bootstrap must be first. These portable presets call `BuildPipeline.BuildPlayer` rather than creating
Unity BuildProfile assets. Switch the active platform through Unity's Build Profiles first.

| Preset | Android | iOS | Build flags | Save directory |
| --- | --- | --- | --- | --- |
| Development | APK | Xcode export | Development, optional debugging/profiler connection | BaseUnity_Development |
| Release | AAB by default, optional APK | Xcode export | StrictMode, no development/debug/profiler connection | BaseUnity |
| Editor Play | — | — | Debug presenter available | BaseUnity_Editor |

Directories are under `Application.persistentDataPath`. The existing production `BaseUnity` directory
is preserved. Old Editor saves previously stored there are not automatically migrated. If needed,
copy a backup into `BaseUnity_Editor` while Play Mode is stopped. Never migrate test rewards into Release.

The presets do **not** mutate FeatureConfig, account IDs, signing keys, version, application identifier,
Firebase files, vendor settings or scripting backend. Use different Bootstrap/config scenes per preset
when a project needs different feature settings. For separate app installation, supply separate app IDs
and matching Firebase/Ads/store configuration in the host project. This tool does not claim staging
isolation for external SDKs just because save files are isolated.

Validate before building. The tool always enables the existing BaseUnity validator and blocks Release
test-Ads mode, even if automatic validation was disabled in Project Settings. It checks the exact saved
scene list, provider dependencies, IDs, catalogs and feature configuration. Configured warnings-as-errors
still apply. It never silently disables Ads/Firebase/IAP to make a build pass.
Save modified scenes first; keep artifacts outside Assets. Android bundle/export switches are temporarily
set for the selected preset and restored in a finally block. Development never exports a Gradle project
or AAB accidentally. Release signing still needs a valid project-owned keystore configuration.

- Configuration report: `Library/BaseUnity/LastBuildValidation.json` (including failed validation).
- Build summary: `Library/BaseUnity/LastMobileBuild.json` after a pipeline result: output, target,
  scenes, result, duration, bytes, warning/error count. No credentials or receipts are written.
- Presets: `ProjectSettings/BaseUnityMobileBuilds.asset` (host-project data, excluded from library Git).

Build pipeline reference: [Unity BuildPlayerOptions](https://docs.unity3d.com/6000.0/Documentation/ScriptReference/BuildPlayerOptions.html).

## DEBUG panel

Update the installed sample through Setup and Update Wizard. In Editor Play or Development Build,
tap **DEBUG** in the upper-right safe area. The entire presenter, reset runner and bootstrap hook are
inside `UNITY_EDITOR || DEVELOPMENT_BUILD`; a normal Release does not contain these classes/actions.
Custom games can reuse `BuildEnvironment` and `PoolDiagnostics.Capture()` without this sample UI.

The scrollable panel refreshes twice per second while open. It shows current scene/phase/level/score,
smoothed FPS, managed and reserved memory, cumulative GC collections, SDK states and latest errors,
Ads readiness/consent/Remove Ads, pending purchases, asset leases, popup queue, tutorial, haptics,
notification preference/permission, wallet and save path. Pool totals are library-owned tracked objects,
not a scan of every object in the game. Diagnostics hold weak pool references and remove registrations
on Dispose. No pool collection is active in Release.

The error history holds at most 12 Error/Exception/Assert messages, capped at 500 characters each;
it starts with this presenter, omits stack traces and clears only this local history. **Copy Status**
copies the visible snapshot locally; nothing is uploaded. Review custom SDK error strings before sharing.

Win/Lose/Next/Play-Retry/Home call the normal flow, so they really save test progression/rewards.
The panel pauses only a running game and releases only that pause. Existing player pauses remain paused.
Native Ads/Consent/purchase/notification interactions and scene loading gate test actions.
No fake rewarded callback, receipt verification bypass or real-money purchase cheat is provided.

## Test data reset

At Home, tap **Reset Test Data**, read the explanation, then **Confirm Reset**. Reset is unavailable
while native interactions or pending purchases are active, or if Bootstrap cannot be reloaded.
It cancels BaseUnity-owned notification reminders first, destroys the bootstrap so services dispose,
waits a frame, then moves the **current test directory** to a unique sibling `.backup-UTC-guid` directory.
It never recursively deletes player files, and the archive API rejects the Release environment.
Finally it reloads the original Bootstrap scene. If archival fails, files remain and bootstrap reloads
with the existing data; check the Console error.

This resets only saves managed by the default GameBootstrap store. Custom stores and SDK data,
PlayerPrefs, OS permission, UMP consent and store-owned purchases are not reset. To restore a backup,
stop the app/Play Mode and copy its contents back into the matching test directory.

## Device checklist

Install a Development APK, verify portrait/safe area, open DEBUG and play win/lose/next/retry.
Then check actual UMP/native ad loading using vendor test configuration; test offline/relaunch/save recovery,
notification permission/scheduling, haptics, and sandbox IAP verification/restore/pending redelivery.
Editor tests do not validate native dialogs, vendor account setup or signing. Release readiness requires
successful configuration validation, a real platform build and device/store testing.
