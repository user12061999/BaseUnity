# Mobile modules (0.13)

Core services have no advertising, Addressables, IAP, notification or native-haptics dependency.
Configure them in **Feature Manager > Modules** or **Setup and Update Wizard**. Disabled modules hide
their settings and start no new SDK work, except cleanup of previously owned notifications.
Popup/Feedback default to enabled; other modules are opt-in. Installed packages/plugins remain in the
host build until explicitly removed; runtime switches do not strip SDK binaries.

## Install

1. Use **Tools > BaseUnity > Integrations > Install Mobile Module Packages**, or install each package
   separately. Verified SDK compilation: Addressables **4.1.1**, Mobile Notifications **2.5.0**, Unity IAP
   **5.4.4**, with Unity **6000.3.26f1**.
2. Enable the module in Feature Manager, **Install adapter**, then **Create and assign provider asset**.
   SDK adapters are copied once into Assets/BaseUnityIntegrations. Existing project-owned copies are
   never overwritten; merge future adapter changes explicitly.
3. Haptics requires only its adapter, including Android VIBRATE permission and an iOS UIKit bridge.
4. Notifications: click **Use explicit notification permission (iOS)**. Configure Android icons in Project
   Settings > Mobile Notifications. Reserve unique notification IDs for this module.
5. IAP requires real products/store IDs, the Offline wallet and a trusted verifier; see below.
6. Update the installed Game Flow scripts through the Wizard. Existing prefab/art assets remain intact.

## Assets and Addressables

```csharp
var assets = bootstrap.Services.Get<BaseUnity.Features.AssetService>();
using (var lease = await assets.LoadAsync<UnityEngine.TextAsset>("my_text", cancellation))
{
    if (lease != null) Use(lease.Asset.text);
}
```

Each successful load owns a disposable lease. Keep a prefab lease alive while instantiated objects use
its assets. Dispose leases after their users are gone. Service disposal releases remaining leases.
Cancellation returns null and still releases late native handles. Missing providers and wrong types
fail without leaking handles; inspect LastError. This Task adapter targets desktop/mobile, not WebGL.

Build Addressables content for players and configure local/remote catalogs yourself. Package installation
does not publish content. Keep startup/home assets local for offline play. LevelData/GameLevel have an
optional `addressablePrefab` taking precedence over `gameplayPrefab`: loading waits before Playing,
and leaving the level releases the asset lease.

## Tutorial

Definitions use stable tutorial IDs, step IDs, messages and optional target keys. Progress saves before
advancing or completing; failed writes keep the current step. Completed tutorials do not repeat.
Start(id)/StartNext() starts manually; Auto start at Home is optional. Skip respects allowSkip.
Reset(id) explicitly removes progress; Stop() preserves durable progress. A resumed step index beyond
an edited definition is rejected: migrate/reset explicitly when changing step layouts.

The sample highlights rectangular UI targets and optionally blocks input outside the target. Bind a
button with TutorialTarget.Initialize(key, tutorialService), or call Activate() for a custom action.
Built-in keys: `home.play`, `gameplay.collect`. Action steps require the matching key; informational
steps show NEXT. Missing/inactive targets never block the entire screen, but action steps still wait
for their target or allowed Skip. Supply game-specific targets in the relevant scene.

## Popup queue and feedback

PopupService.Enqueue(id, content, priority, scope, cancellation) returns Accepted/Dismissed/Cancelled/
Unavailable. One entry is active. Pending entries use descending priority then FIFO; an active dialog
is never interrupted. IDs deduplicate until completion; capacity bounds outstanding requests.
AttachPresenter() must remain alive while UI can show dialogs. Losing the last presenter cancels
requests so awaiting callers do not hang. Suspend() holds presentation/resolution. CancelScope() and
CancelAll() cancel relevant requests. All service APIs use Unity's main thread.

FeedbackService provides Toast(text), nested Loading(message) scopes and ConfirmAsync(). Loading stays
visible until every owner releases its scope. Feedback itself does not freeze game time or block input.
The sample pauses gameplay for shared confirmation dialogs and resumes only a pause it owns. It defers
dialogs/tutorials behind native consent, ad transitions and the existing Offline menu. Requests scoped
to `scene` are cancelled on scene transition; `game` requests survive the transition.

## Haptics and local notifications

HapticService supports Light/Medium/Heavy/Success/Failure, a minimum interval and a saved player toggle.
The sample wires buttons and win/lose feedback. Unsupported platforms/no hardware return false.
Android uses vibration effects; iOS uses UIKit generators. Verify feel on hardware.

Notifications never ask permission automatically from the core. EnableAsync follows an explicit player
action. Denial or a failed preference save leaves them off. Schedule requires a positive reserved ID and
future UTC time. An owned ID is saved before native scheduling so a failed write cannot orphan a new
notification. Disable cancels pending permission work and only owned scheduled/delivered IDs; it never
uses the SDK cancel-all APIs. Disabled-module cleanup also cancels saved schedules if its provider is
still installed. Keep the adapter available until cleanup succeeds when removing the feature.
The ownership ledger is bounded to 64 IDs; delivered/missing schedules remain harmless until cancelled.

The sample MORE menu offers haptics/notification toggles, tutorial and store controls. Reminders target
the next UTC daily reset; suspending after claiming a daily reward reschedules the reminder. For energy
refill, call Schedule with the actual refill UTC time supplied by your game's energy system. Permissions,
battery policies, notification icons and delivery need device tests. These are local reminders, not
Firebase push messaging; exact OS delivery time is not guaranteed.

## IAP and receipt verification

Supported: quantity-one, single-product consumables/non-consumables, store metadata/prices, purchase,
restore, pending retries, entitlements and Remove Ads. Subscriptions and multi-item/multi-quantity
orders are rejected without confirmation. A purchase button only opens the store flow; it grants nothing.
The bundled adapter rejects Editor fake-store purchases. Automated tests inject explicit mock backends.

Flow: PendingOrder -> trusted verification -> atomic wallet + transaction save -> ConfirmPurchase.
Verification/save/confirmation errors keep delivery pending. Redelivery after restart skips a matching
already committed transaction. A non-consumable restored under another transaction does not repeat its
currency bonus. RestoreAsync waits for verification and durable delivery before reporting success.
Confirmed consumables cannot be restored. Keep a Restore Purchases button available to players.

Configure logical IDs, Android/iOS store IDs, Coin/Gem grants and optional non-consumable entitlements.
The configured Remove Ads entitlement suppresses banner/interstitial immediately and after restart.
Optional rewarded ads remain available. An unavailable store never blocks gameplay.

Install **Server Receipt Verifier**, assign its asset and configure its HTTPS endpoint. This repository
provides the client adapter/protocol, not a hosted verification backend or store account configuration.
A runtime authorizationToken delegate can supply a short-lived Bearer token; do not embed server secrets.
POST JSON contains transactionId, originalTransactionId, logical productId, storeProductId, applicationId,
store, receiptFormat and receipt. Apple StoreKit 2 uses `apple-jws` when available, otherwise the adapter
forwards the Unity unified receipt. The server response must contain:

```json
{ "valid": true, "transactionId": "exact-request-transaction", "productId": "exact-logical-product" }
```

Your trusted server must validate with Apple/Google, verify signature/status/app/product/quantity, bind
ownership to the intended player, enforce replay/refund policy and map logical IDs using its own catalog.
Echoing fields or checking JSON shape is not verification. HTTP errors, redirects, mismatched IDs, empty
receipts or rejection never grant or confirm. Local saves are not tamper-proof; account-wide balances
and refund revocation require a server-authoritative economy. Automatic refund revocation is not included.

Offline payload schemas 0/1/2 upgrade additively to **3** on the next successful save; the envelope stays
at version 1. New fields hold store receipts, owned products and entitlements. Existing balances/progress
remain. Older BaseUnity versions reject schema 3 rather than silently downgrading it.

## Validation and platform checks

Validate Build Configuration checks providers/packages, save slots, catalog/store IDs, notification
permission settings and receipt endpoint. It cannot prove a store/dashboard/CDN setup works. Native
notifications/haptics/purchases/restored receipts need Android/iOS hardware and store sandbox accounts.
Optional SDKs, generated provider assets and account settings stay outside the library Git repository.

References: [Addressables memory](https://docs.unity3d.com/Packages/com.unity.addressables@2.7/manual/MemoryManagement.html),
[Mobile Notifications](https://docs.unity3d.com/Packages/com.unity.mobile.notifications@2.5/manual/index.html),
[Unity IAP purchases](https://docs.unity.com/en-us/iap/purchases).

Verified for this release: **127 BaseUnity EditMode tests + 35 PlayMode tests passed**. Coverage includes
cancellation/late asset release, level lease lifetime, real sample tutorial/button interactions, dialog
pause/resume, permission opt-out, transaction/save/confirmation failure and restored delivery waiting
for verification. SDK adapters compiled against the versions above. Native device/store sandbox flows
remain to be verified with real project/store/account configuration.
