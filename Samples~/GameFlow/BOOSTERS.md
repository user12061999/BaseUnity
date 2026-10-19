# InGame HUD and boosters

Update Game Flow source through the Wizard, then run **Generate Missing InGame HUD and Boosters**
with the intended GameFlowConfig selected. Wizard also offers **Generate InGame HUD / boosters**.
Existing files and config assignments are preserved. Host-owned assets are generated next to the config:

- `InGameHUD/InGameHUD.prefab`: portrait safe-area goal/timer/progress, booster strip and confirmation/Store overlay.
- `InGameHUD/BoosterSlot.prefab`: editable slot with button, title, icon, quantity and availability/cooldown state.
- `InGameHUD/Boosters.asset`: feature switch and booster definitions, matched to inventory/Shop by item ID.

Leaving `inGameHUDPrefab` empty preserves the previous UI. The optional HUD replaces the legacy
level/target/timer bindings while retaining score, pause, gameplay and result controls. The sample supports
up to four slots. Turning the catalog off hides boosters; turning Offline off hides the inventory-backed
strip while the HUD still shows gameplay status. Missing capability disables that booster rather than
selling an unusable effect. Custom games can replace this entire sample presenter.

## Defaults

| Inventory ID | Effect ID | Effect | Requirements |
| --- | --- | --- | --- |
| energy | extra_time | Add 15 seconds, capped at 24 hours | A currently playing timed level with time remaining |
| ticket | hint | Highlight the Tap Target target for 3 scaled seconds | A level prefab implementing the hint effect |

These use the existing Shop items and prices; generation does not change Shop settings or grant stock.
Edit prices/quantities/limits in Feature Manager > Offline. Edit effect amount, cooldown and maximum uses
per level in Boosters.asset. Maximum uses 0 means unlimited. Retry/new level resets usage/cooldown;
inventory remains durable. Cooldown and hint time freeze when the game is paused.

Tapping a ready slot opens a use confirmation. Cancel consumes nothing. Empty stock offers a small
coin Store overlay using the normal OfflineService.Buy transaction. A successful purchase leaves the
item in inventory; tap again to use it. Disabled Shop, insufficient funds, purchase limits and failed saves
cannot grant stock or apply an effect. No rewarded-ad shortcut or native IAP is implied by this coin Store.

Dialogs pause only a running session. They resume only the same context and their own pause. Scene
changes/results close stale dialogs; losing application focus closes the dialog and leaves the normal
pause screen for explicit resume. Confirmation buttons keep keyboard navigation inside the modal,
and the normal popup/tutorial coordinator defers other overlays. Existing UI sound/effect settings apply.

## Extension contract

Implement `IBoosterEffect` on a level-prefab component, or register an effect with BoosterController
for custom integrations. The HUD discovers prefab effects when a new LevelContext is loaded.
`CanUse` is a pure capability check. `Prepare` returns an `IBoosterAction` with `Apply` and `Revert`.

The controller runs synchronously on Unity's main thread:

1. Validate session, configuration, capability, stock, cooldown and usage limit.
2. Apply a reversible transient change.
3. Save the inventory debit using OfflineService.ConsumeItem.
4. On success, record usage/cooldown. On failure, revert the transient change.

Actions **must not** save data, grant currency, call Win/Lose, change scenes, launch async work or perform
irreversible destruction. Snapshot exactly the state needed for rollback, and reject stale LevelContexts.
For obstacle-removal boosters, a reversible deactivate/restore operation is suitable; immediate destruction
before a successful debit is not. Custom inventories must guarantee failed consumption changes nothing.
The built-in actions modify only remaining time or the demo hint expiry and emit no gameplay callbacks
before the debit. Callback/rollback exceptions return an explicit error; custom effects remain responsible
for a correct reversible implementation.

Booster activation and UI never bypass wallet, rewarded receipt or IAP verification rules. Current active
gameplay effects are transient, consistent with the sample's fresh-session startup; used inventory is saved.
This is an offline base, not server-authoritative inventory or crash-resumable gameplay.
