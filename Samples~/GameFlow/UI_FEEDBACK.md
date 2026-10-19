# UI effects and sound

Update the Game Flow sample through Setup and Update Wizard. The sample automatically installs its
UI feedback presenter. Existing project-owned prefabs do not need to be regenerated: buttons/controls
under GameFlowBootstrap are scanned at startup and every 0.15 seconds for newly created UI.
Decoration never grants rewards, changes level logic, waits for a sound, or blocks transitions.

## Configuration

Feature Manager > Modules > Feedback exposes:

- Enable feedback: disables toast/loading/confirm and the UI feedback layer together.
- UI sounds / UI animations: independent sound/motion switches.
- Reduce motion: suppresses scale, ripple and particles while retaining configured sounds.
- Hover sounds: optional desktop pointer feedback; off by default and ignored on mobile.
- UI sound / effect profile: optional `UIFeedbackProfile` asset.

Run **Tools > BaseUnity > Game Flow > Generate Missing UI Feedback Profile** with a GameFlowConfig
selected. The generator assigns `UIFeedback/UIFeedback.asset` next to the config without overwriting
an existing assignment. Wizard generates it with Home/offline UI. You can also use Create > BaseUnity >
UI Feedback Profile and assign it to FeatureConfig for a custom game.

Profile sounds map Click, Hover, Open, Close, Success, Error, Purchase, Reward, Win, Lose and Change
to clips and per-cue gain. Empty clips use quiet synthesized tones if Fallback Tones is enabled.
Disable fallback for a project that wants only assigned audio, or set an individual cue volume to zero.
Generated tones are created lazily and released with the service. Custom AudioClips remain project-owned.
The sample contains no downloaded audio or licensed sound pack. Replace tones with your own SFX as desired.

Audio uses AudioService.PlayEffect and respects Master × Effects and Mute, including saved settings;
music volume remains independent. Short repeated cues are throttled, slider changes use a 75 ms interval,
and reward bursts coalesce within 120 ms. Coalescing only affects presentation, never economy grants.
Native OS audio behavior still needs testing on target devices.

## Visuals and lifecycle

Buttons compress to 94% by default and restore their authored scale on release/disable. Pointer-down
and onClick are deduplicated; keyboard/programmatic clicks still have a cue. Disabled buttons do not
respond to pointer feedback. The component owns scale only during its brief animation, so idle controls
are not continuously forced to a fixed size. Custom tween systems should choose one owner for button scale.

Store/Quest/confirmation/settings/pause entry uses a short scale easing. OfflineMenu retains its existing
fade/close animation. Popup queue, mobile menus, level selection and legacy fallback settings receive
open/close sound feedback. Closing logic is immediate; decoration never delays scene loads or transactions.
The effect canvas has no raycaster and all particle Images have raycastTarget=false.

The particle layer reuses a PrefabPool with at most 32 active images. Currency grants use a short curved
flight toward the Home wallet when visible, otherwise toward the upper screen. A generic disc works
without art; a configured rewardSprite, including the host project's optional Layer Lab coin, replaces it.
Reward/win/success/purchase/error/lose cues have distinct bursts. Unscaled time lets them finish at
timeScale=0. Dispose releases pool instances, runtime texture/sprite, generated clips and listeners.

BalanceChanged fires only after a successful OfflineService save. Positive coin/gem deltas trigger Reward;
spending, failed saves, duplicate claims and loading an existing save do not replay reward effects.
This covers Daily Reward, quests/achievements, level payouts, verified rewarded bonuses and verified IAP
currency delivery. Noncurrency shop purchases use Purchase; failed actions/toasts use Error. Win/Lose
presentation follows the real result transition. Zero-currency claims do not fabricate coin flights.

## Custom game usage

```csharp
using BaseUnity.Core;
using BaseUnity.Features;

var feedback = GameBootstrap.Instance.Services.Get<UIFeedbackService>();
feedback.Play(UICue.Success);
feedback.Play(UICue.Click, screenPosition);
```

Core UIFeedbackService has no Unity UI dependency; a custom game can subscribe to Played and provide
its own presenter. The Game Flow presenter/components require Unity UI. For buttons created and clicked
immediately in the same frame, add/bind UIButtonFeedback during construction instead of waiting for the
automatic scan. Do not add another pointer click sound system to the same button unless duplication is intended.
