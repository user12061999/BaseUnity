# Editable game UI prefabs

Select GameFlowConfig and run **Tools > BaseUnity > Game Flow > Generate Editable UI Prefabs**.
Setup Wizard also offers **Generate editable UI prefabs**. Stop Play Mode before generation.

Generated UI belongs to the host project. The library repository ships builders and bindings only;
no Layer Lab artwork or Unity project folders are distributed. Imported Layer Lab art is reused when
available; missing art falls back to plain colors and Unity's default font.

| Folder beside GameFlowConfig | Edit here |
| --- | --- |
| `UI/Theme/GameFlow.prefab` | Root canvas, safe area, bindings to nested screens |
| `UI/Theme/Screens/Home.prefab`, `InGame.prefab`, `Loading.prefab` | Legacy Home, HUD/demo controls, scene loading |
| `UI/Theme/Screens/Settings.prefab`, `Pause.prefab`, `Result.prefab`, `Error.prefab` | Audio/privacy, pause, win/lose and error presentation |
| `UI/Theme/HomeHub.prefab`, `Screens/HomeHub.prefab` | Current Home navigation, profile and notification dots |
| `HomeHub/Store.prefab`, `Quests.prefab`, `HubCard.prefab` | Store, Quest, shared item/quest card |
| `UI/Theme/Screens/PurchaseConfirmation.prefab` | Purchase confirmation |
| `OfflineUI/*.prefab` | Shop, Daily Reward, Missions, Inventory, menu and item rows |
| `UI/Theme/Screens/LevelSelection.prefab`, `Components/LevelCell.prefab` | Level selection grid, paging, locked/unlocked cells |
| `UI/Theme/MobileModules.prefab` and its nested `Screens` | More menu, popup, tutorial, toast, loading indicator |
| `UI/Theme/Components/MenuButton.prefab`, `MenuLabel.prefab` | Dynamic More-menu controls; font, sprite and style |
| `UI/Theme/OfflineOverlay.prefab` and its nested `Screens` | WaitingForAd, OfflineHomeBar and ResultReward |

Open a prefab in **Prefab Mode**. Edit sprites, colors, fonts, anchors, sizes and decorative children.
Nested prefab edits propagate to the root. Keep references on the View components assigned when
replacing objects. GameFlowConfig selects the root prefabs used at runtime; duplicate the complete
hierarchy for a separate project/theme and reassign the roots. HomeHub and GameFlow are separate:
HomeHub is the current default Home, while GameFlow Home is the fallback when HomeHub is unassigned.

Text, inventory/currency values, icons from presentation data, interactability, progress, visibility,
tutorial target masks and navigation are updated by code. Menu rows and level cells are instantiated
from templates, not drawn from hardcoded colors/fonts. Change their templates to style generated rows.
Pagination/grid layout and dynamic menu row positions still belong to the presenter/layout component.
Result no longer moves Retry/Home or forces a title color on win/lose; edit these in its prefab.
Use the same Result prefab for both outcomes; stars/failure badge and text switch with the result.

Generation preserves existing assets and assignments. The first run creates a new nested GameFlow
and HomeHub root and updates config references, leaving original roots intact. It never regenerates
existing theme roots. If a partially interrupted generation left a screen file, a new unique filename
is used to avoid overwriting it. Re-run for missing root groups; explicit edits/replacements are manual.
No destructive theme reset is performed. Save source/prefab changes before updating the installed sample.

The old booster feature is removed; energy/ticket remain ordinary inventory/Shop sample items.
No inventory, wallet, progress or player saves are deleted by this migration. Keep UI sound/effect
configuration in `UIFeedback/UIFeedback.asset` and Feedback settings as before.

Unassigned optional prefab fields retain the previous runtime fallback for projects upgrading code
before generating assets. The default Wizard generates all editable roots; custom games can replace
these presenters entirely. The IMGUI development/debug panel and decorative feedback particle pool
remain development/procedural systems, not game screen prefabs.
