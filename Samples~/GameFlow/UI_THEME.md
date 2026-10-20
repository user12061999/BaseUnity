# Shared theme, components and UI preview

Open **Tools > BaseUnity > UI > Theme and Preview Workbench** and select the project's GameFlowConfig.
Use **Prepare theme/components** once after updating sample source. Setup Wizard also provides
**Prepare UI theme / shared components**. This creates missing assets, assigns semantic bindings,
links shared currency/cards and applies the initial theme. Existing theme/library assets are reused.

## Change a theme

1. Click **Duplicate theme** to keep the previous palette, or **Edit theme** to select the current asset.
2. In the Inspector, set the common font and each role's graphic tint, text color, optional sprite,
   selectable colors and text scale. PrimaryButton, SecondaryButton, Tab, Panel, PanelArt, Card,
   Title, Body, Muted and Currency cover the main sample UI. Success/Error supply feedback colors.
3. Select the theme in **Preview theme**. Preview updates with Inspector edits without saving over
   the game prefabs. Click **Refresh preview** after changing a prefab in another Inspector.
4. Click **Apply theme to prefabs** to save the theme selection and update all owned, referenced
   prefab dependencies. This creates a backup in `ProjectSettings/BaseUnityUIBackups` first.

The default generated theme lives at `UI/Theme/Theme.asset`. A null font keeps the currently assigned fonts.
`Replace Sprite` explicitly permits replacing or clearing an Image's sprite; otherwise its existing
sprite is preserved. Icon/decorative Artwork bindings disable sprite replacement by default.
Each UIThemeBinding has an explicit role and per-property opt-outs. The first preparation infers
roles from the existing controls; adjust these tags in Prefab Mode for your own art direction.

Typography scales from a captured baseline, so repeated Apply does not grow/shrink text cumulatively.
After intentionally changing a bound label's authored size, use the binding's **Capture Authored
Typography** context menu before reapplying the theme. Positions, anchors, prices, content, navigation,
visibility and interactability remain owned by the prefab/presenter. Tutorial masks, dimmers and badge
colors keep their intentional meaning. Settings touch areas are at least 70 reference units tall;
the transparent slider touch area is intentionally excluded from theme tinting.

## Shared components

`UI/Theme/Components.asset` references six editable prefabs in `UI/Theme/Shared`:

- PrimaryButton, SecondaryButton and TabButton for adding consistent controls to new screens.
- CurrencyBar, linked as the HomeHub wallet bar.
- ItemCard and QuestCard, linked to the live Store and Quest presenter respectively.

Currency/card prefab structure edits propagate to their nested/generated instances. Existing buttons
retain their authored sizes and hierarchy, and share role styles through UIThemeBinding. Change
Theme.asset to update colors, fonts and sprites across all of them; structural edits to a shared
button prefab affect its own instances, not unrelated existing controls. Dynamic level/menu controls
also carry the same role bindings and inherit a runtime theme when instantiated under the game root.

The controller supports `GameFlowThemeController.SetTheme(theme)` for runtime switching. It changes
presentation only and does not save a player's theme choice. A null theme stops further overrides;
it does not restore an older prefab appearance. Offline menu success/error colors follow the theme.

## Preview and layout checks

The workbench previews Home, Store, Quest, Daily Reward, Inventory, InGame, Settings, Pause, Win, Lose,
Loading, Error, level selection, purchase confirmation, queued popup, tutorial, toast and More menu.
Sample data is local and illustrative; it never initializes Bootstrap, SDKs, purchases or player saves.
Vietnamese preview strings exercise the font/layout; they are not a replacement for localization data.

Choose English, Vietnamese or LongVietnamese. Four portrait presets cover 16:9, 19.5:9, 20:9 and
3:4 tablet, with simulated notch/home-indicator insets. The green frame marks the simulated safe area.
**Export preview PNG** saves the current rendering without the editor frame.

**Validate all devices/screens** runs 144 combinations (18 screens x 4 devices x English/long Vietnamese)
and saves the complete JSON report to `ProjectSettings/BaseUnityUIReports`. It checks interactive
bounds against the safe area, a 48 simulated-pixel touch-size heuristic, missing sample glyphs and
vertical text truncation. The window displays the first 100 findings; JSON contains all findings.
These are geometry/font checks, not device certification: they do not measure physical dp, validate
all localized strings, detect every overlap, or test native SDK dialogs. Review exported screenshots
and test on real target devices before shipping.

All rendering uses temporary preview scenes/cameras/textures which are disposed on refresh/close.
The active scene setup, Screen/Game View resolution and player data are not changed by preview.
Applying/preparing writes only project-owned prefabs referenced by config within its folder; it
never edits vendor art or the original Layer Lab package. Save prefab edits before applying.
Backups mirror asset paths (with metas); restore intentionally with Unity stopped and a clean checkout.
