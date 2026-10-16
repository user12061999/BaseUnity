# Optional Ads and Firebase integrations

The core library compiles without any advertising or Firebase SDK. SDK-specific source stays in
`Samples~/Integrations` and is copied into the game project only through the installer menus.
No SDK binaries, account configuration or advertising IDs are distributed in this repository.

## Feature configuration

Open **Tools > BaseUnity > Feature Manager** (also available on the FeatureConfig inspector).
The utility window has **Ads**, **Firebase** and **Project Setup** tabs. Edits support Undo; **Save**
writes the selected config asset. The window is read-only during Play Mode.

The Ads tab stores Android/iOS App IDs, per-format ad-unit IDs and a MAX SDK key. **Apply app IDs /
SDK key to installed SDK** writes non-empty values to the installed SDK settings asset; blank values
preserve existing vendor settings. SDK versions with unrecognized settings fields report an error
without changing IDs. The selected network is a setup preference; the assigned provider asset is
the runtime implementation. Switching networks clears the old provider reference so you can assign
the matching implementation and check unit IDs. No SDK is required merely to enter and save IDs.

The Firebase tab imports the official JSON/plist into the Assets root, previews Project ID/App ID,
and validates the current Android package or iOS bundle identifier. Duplicate config locations are
rejected. Replacing an existing file backs it up under `ProjectSettings/BaseUnityConfigBackups`;
an identical file is left untouched. Firebase identifiers are read from these files rather than
stored as disconnected manual fields. Project Setup links to Player Settings and can assign the
selected FeatureConfig to a selected GameFlowConfig or scene Bootstrap.

Use **Tools > BaseUnity > Features > Create Feature Config**, then assign the asset to
`GameBootstrap.FeatureConfig` or `GameFlowConfig.features`. New GameFlow installations create one
automatically. Ads and Firebase are disabled by default. Bootstrap clones the config at startup:
editing a source asset during Play does not modify the running services.

| Group | Switches/settings |
| --- | --- |
| Ads | Enabled, Banner, Interstitial, Rewarded, Test Mode, request timeout, interstitial cooldown, Android/iOS unit IDs |
| Firebase | Enabled, Analytics, Crashlytics, Remote Config, request timeout, fetch interval, local key/value defaults |
| Providers | One AdsProvider (AdMob **or** MAX) and one FirebaseProvider |

Both services are always registered. Disabled or missing providers return safe results; they do not
delay Home or gameplay. `Status` and `LastError` expose initialization problems. Initialization is
asynchronous; await `InitializeAsync()` before logging events. Failed initialization can be retried
by toggling `SetEnabled(false)`, `SetEnabled(true)` and calling `InitializeAsync()` again.

Local flags decide whether a module is available. Remote Config does **not** silently enable modules
or override feature flags. Its fetched values are applied by your game explicitly, at a suitable point.
Runtime `SetEnabled(false)` cancels in-flight work, disposes the backend and suppresses late rewards.
It cannot force a native fullscreen ad that is already onscreen to close. Service APIs use Unity's main thread.

## AdMob

1. Install the official [Google Mobile Ads Unity plugin](https://developers.google.com/admob/unity/quick-start).
2. Configure Android/iOS **App IDs** in Google Mobile Ads Settings, separately from ad-unit IDs.
3. Run **Tools > BaseUnity > Features > Install AdMob Adapter**.
4. Create **Assets > Create > BaseUnity > Providers > AdMob** and assign it to FeatureConfig.adsProvider.
5. Enable Ads and desired formats. Keep Test Mode on during development; it uses the official
   [demo ad units](https://developers.google.com/admob/unity/test-ads). Production units come from your account.

The adapter uses the `InterstitialAd.Load` / `RewardedAd.Load` API and destroys consumed objects.
Only the SDK reward callback qualifies a rewarded result. Closing an ad alone never grants a reward.
AdMob callbacks are marshalled back to the captured Unity synchronization context.

## AppLovin MAX

1. Install the official [MAX Unity plugin](https://support.applovin.com/en/max/unity/overview/integration)
   with the current `AdViewConfiguration` API, and set the SDK key in **AppLovin > Integration Manager**.
2. Run **Tools > BaseUnity > Features > Install AppLovin MAX Adapter**.
3. Create **Assets > Create > BaseUnity > Providers > AppLovin MAX**, then assign it as adsProvider.
4. Supply your Android/iOS MAX ad-unit IDs. For Test Mode, supply the test device advertising IDs on
   the provider asset. MAX has no shared Google-style demo ad units; missing test-device configuration
   fails initialization instead of silently requesting production ads.

Choose one top-level AdsProvider per game. If using MAX mediation with AdMob, configure AdMob as a
MAX network adapter; do not also initialize BaseUnity's standalone AdMob provider. Configure the
project's consent flow before enabling/initializing the selected SDK. Other scripts must not
independently initialize the same SDK or show the same ad units.

## Ads calls

```csharp
var ads = BaseUnity.Core.GameBootstrap.Instance.Services.Get<BaseUnity.Features.AdsService>();
if (await ads.LoadAsync(BaseUnity.Features.AdFormat.Rewarded))
{
    var result = await ads.ShowAsync(BaseUnity.Features.AdFormat.Rewarded);
    if (result == BaseUnity.Features.AdResult.Rewarded)
    {
        // Grant this placement's reward once, while the requesting gameplay session is still valid.
    }
}
```

Preload at a natural point; Show never starts a hidden network request. The first load initializes
the service if needed. Load again after consuming a fullscreen ad. Show returns Disabled, Unavailable,
Busy, Cooldown, Closed, Rewarded or Failed. Banner Show returns Closed immediately to acknowledge
display; call HideBanner to hide it. A banner placement needs reserved space in the game's layout;
the base does not automatically overlay advertising on the gameplay HUD. Ads do not automatically
pause/resume gameplay or grant currency: the placement owns that policy.

## Firebase

1. Create/register your Android/iOS apps using the exact application identifiers in your game.
2. Follow [Firebase Unity setup](https://firebase.google.com/docs/unity/setup). Import **Analytics,
   Crashlytics and Remote Config** packages (and their Firebase App/dependency resolver dependencies).
   This adapter references all three SDKs; the runtime switches independently gate their use.
3. Put your `google-services.json` / `GoogleService-Info.plist` in the **game project's** Assets.
4. Run **Tools > BaseUnity > Features > Install Firebase Adapter**.
5. Create **Assets > Create > BaseUnity > Providers > Firebase**, assign it in FeatureConfig, then
   enable Firebase and the required products.

The installer includes Android manifest defaults and an iOS build postprocessor to disable automatic
Analytics/Crashlytics collection by default. Runtime initialization applies the selected flags;
disabling an initialized FirebaseService turns both collection flags off. These are application
feature switches, not an SDK removal mechanism: native SDKs may initialize before Unity, and Firebase's
persisted collection preferences can override platform defaults on an existing installation. For a
build that must contain no Firebase behavior, omit the SDK and adapter. Validate collection settings
on both a fresh install and an upgrade when connecting a real project.

```csharp
var firebase = BaseUnity.Core.GameBootstrap.Instance.Services.Get<BaseUnity.Features.FirebaseService>();
await firebase.InitializeAsync();
firebase.LogEvent("level_start", new System.Collections.Generic.Dictionary<string, string> { ["level"] = "1" });
firebase.RecordException(new System.InvalidOperationException("Example handled failure"));
await firebase.FetchRemoteConfigAsync();
long coins = firebase.GetLong("starting_coins", 100);
bool offerEnabled = firebase.GetBool("offer_enabled", false);
```

Remote fetch is explicit and bounded. Disabled, unavailable or failed fetches leave local defaults
usable; a failed refresh keeps the last successful values for the current run. Disable clears those
values. Fetch settings and SDK cache follow [Firebase Remote Config](https://firebase.google.com/docs/remote-config/unity/get-started).
Firebase Auth, Firestore, Messaging and Storage are not part of this adapter.

## Installation and validation limits

Installer menus verify SDK types before copying source and never overwrite an installed adapter.
Adapters intentionally compile in the game project's default assembly so both Unity-package and
UPM SDK distributions work without hardcoded assembly references. If placing them inside a custom
asmdef, add references to BaseUnity.Runtime and the chosen SDK assemblies yourself.

Core tests use deterministic fake backends to verify flags, missing providers, cooldowns, rewards,
disposal and late callbacks. Ads adapters reject Editor simulation; they cannot manufacture rewards.
This version has not been validated against installed vendor SDK binaries or on Android/iOS.
After installing SDKs and account files, build to a test device and verify test ads, Firebase DebugView,
Remote Config and Crashlytics delivery before switching to production ad units.
