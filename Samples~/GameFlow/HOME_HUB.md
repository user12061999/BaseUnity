# Home / Store / Quest sample

Update Game Flow source through the Setup and Update Wizard. Generate the hub through the Wizard
or **Tools > BaseUnity > Game Flow > Generate Missing Home Store Quest Prefabs** with the desired
GameFlowConfig selected. Run from Bootstrap. Existing projects can leave `homeHubPrefab` empty to
keep their previous Home/menu. Existing prefabs and presentation assets are not overwritten by regeneration.

Generated host assets:

- `HomeHub/HomeHub.prefab`: portrait 900×1600 safe-area canvas, Home/wallet/profile, navigation and confirmation.
- `HomeHub/Store.prefab` / `Quests.prefab`: separate nested page prefabs with tab, cards, paging and status bindings.
- `HomeHub/HubCard.prefab`: reusable icon/title/description/reward/progress/action card.
- `HomeHub/Presentation.asset`: avatar/player display name and optional icon/English/Vietnamese copy keyed by item/quest/product ID.

Edit layout, icons, colors and text in Prefab Mode. Layer Lab assets are referenced only if installed;
otherwise the generator uses plain UI and the built-in font. Art and generated assets stay in the host
project. The repository distributes source/builders, not licensed artwork or a complete Unity project.
To create a fresh layout after customizing an existing one, use another config/output directory or move
and archive the old generated folder and clear the config references deliberately; generation is not an updater.

## Data and transactions

Feature Manager > Offline owns shop IDs, currencies, prices, quantities/limits, daily quests, achievement
goals and coin rewards. `Presentation.asset` supplies only visual copy; it cannot override the economy.
The sample does not overwrite your existing catalogs or grant starting currency when generating UI.

Store > Coins uses the real OfflineService wallet. Purchase opens a local confirmation, then rechecks
funds/limits in `Buy`, saves before updating the balance and inventory, and suppresses repeated confirms.
Insufficient funds shows an error without spending. A finite purchase limit becomes Sold Out when reached.

Store > IAP appears only when purchasing is enabled. Product IDs map to the configured platform catalog;
localized prices/title/availability come from PurchaseService metadata. Missing metadata disables buying.
Nonconsumables show Owned after verified delivery. Buy starts the native flow; it never grants from the UI.
Restore and Retry Delivery use the existing verifier/atomic wallet pipeline. Valid platform IDs, sandbox
accounts and a trusted receipt verification endpoint are still required. No fake store purchase is provided.

Quest > Daily uses existing UTC device-time missions. Quest > Achievements uses lifetime goals and never
resets at midnight. Claims credit coins and record the claimed ID in the same save transaction, preventing
repeat grants and preserving retry after a failed save. Achievement IDs must be stable; changing rewards
under an already claimed ID does not make it claimable again. Offline goals are not server anti-cheat.

Offline payload schema 4 adds `claimedAchievements` and `lifetimeScore`. Existing schema 0–3 data,
balances, inventory, store receipts and entitlements remain. Session/win achievements can use existing
PlayerProgress counts; cumulative score starts from the update because older daily counters cannot
reconstruct past lifetime score. Schema 4 saves require this version or newer; older clients must not downgrade them.

## Home and badges

Home buttons open normal Play/level selection/settings, the new Store/Quest pages, or the existing
Daily Reward/Inventory/mobile settings presenters. Feature flags hide unavailable buttons/tabs and badges.
Quest badge counts currently unclaimed, completed daily goals plus achievements. Daily badge reflects
an available daily claim. Successful save/claim events update the wallet and badge; a one-second UTC
tick handles midnight refresh while Home stays open. Visiting a page does not mark rewards as claimed.
Clear Data / debug reset remains an explicit development-only operation, not a Home player action.

The hub covers the old Home graphics and blocks old Home controls while visible; original gameplay,
level selection, result and settings stay usable. Modal state is exposed through the existing offline
overlay coordinator so tutorials/popups/mobile menus do not draw through Store/Quest. The new Play
button supplies `home.play` for tutorials. Header/card text supports Vietnamese/English through the
existing localization service and presentation catalog.
