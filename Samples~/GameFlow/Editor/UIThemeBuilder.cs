using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
namespace BaseUnity.GameFlow.Editor
{
    public static class UIThemeBuilder
    {
        public static string Folder(GameFlowConfig config) => Path.GetDirectoryName(AssetDatabase.GetAssetPath(config)).Replace('\\', '/');
        public static GameFlowConfig SelectedConfig()
        {
            if (Selection.activeObject is GameFlowConfig config) return config;
            var ids = AssetDatabase.FindAssets("t:GameFlowConfig", new[] { "Assets" });
            if (ids.Length != 1) throw new InvalidOperationException("Select a GameFlowConfig first.");
            return AssetDatabase.LoadAssetAtPath<GameFlowConfig>(AssetDatabase.GUIDToAssetPath(ids[0]));
        }
        [MenuItem("Tools/BaseUnity/UI/Prepare Theme and Shared Components")]
        public static void GenerateSelected() { Generate(SelectedConfig()); }
        public static string[] PrefabPaths(GameFlowConfig config)
        {
            var roots = new UnityEngine.Object[] { config.uiPrefab, config.homeHubPrefab, config.offlineMenuPrefab, config.mobileModulesPrefab, config.offlineOverlayPrefab, config.levelSelectionPrefab, config.uiComponents };
            string prefix = Folder(config) + "/";
            return AssetDatabase.GetDependencies(roots.Where(x => x).Select(AssetDatabase.GetAssetPath).ToArray(), true)
                .Where(path => path.StartsWith(prefix, StringComparison.Ordinal) && path.EndsWith(".prefab", StringComparison.OrdinalIgnoreCase))
                .Distinct().OrderBy(path => AssetDatabase.GetDependencies(path, true).Count(x => x.EndsWith(".prefab"))).ThenBy(x => x, StringComparer.Ordinal).ToArray();
        }
        private static void Editable(GameFlowConfig config)
        {
            if (!config || !AssetDatabase.GetAssetPath(config).StartsWith("Assets/", StringComparison.Ordinal)) throw new InvalidOperationException("Choose a project-owned config in Assets.");
            if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling) throw new InvalidOperationException("Stop Play Mode and wait for compilation first.");
            string prefix = Folder(config) + "/";
            var owned = new UnityEngine.Object[] { config.uiPrefab, config.homeHubPrefab, config.offlineMenuPrefab, config.mobileModulesPrefab, config.offlineOverlayPrefab, config.levelSelectionPrefab, config.uiComponents };
            if (config.uiComponents) owned = owned.Concat(new UnityEngine.Object[] { config.uiComponents.primaryButton, config.uiComponents.secondaryButton, config.uiComponents.tabButton, config.uiComponents.currencyBar, config.uiComponents.itemCard, config.uiComponents.questCard }).ToArray();
            foreach (var asset in owned.Where(x => x)) if (!AssetDatabase.GetAssetPath(asset).StartsWith(prefix, StringComparison.Ordinal)) throw new InvalidOperationException("Copy editable UI assets into the config folder before preparing/applying: " + AssetDatabase.GetAssetPath(asset));
            foreach (var path in PrefabPaths(config).Concat(new[] { AssetDatabase.GetAssetPath(config), AssetDatabase.GetAssetPath(config.uiTheme) }))
                if (!string.IsNullOrEmpty(path) && EditorUtility.IsDirty(AssetDatabase.LoadMainAssetAtPath(path))) throw new InvalidOperationException("Save asset edits before applying the theme: " + path);
            var stage = UnityEditor.SceneManagement.PrefabStageUtility.GetCurrentPrefabStage();
            if (stage && stage.scene.isDirty) throw new InvalidOperationException("Save or close Prefab Mode edits first.");
        }
        public static string Backup(GameFlowConfig config)
        {
            string folder = "ProjectSettings/BaseUnityUIBackups/" + DateTime.UtcNow.ToString("yyyyMMdd-HHmmss") + "-" + Guid.NewGuid().ToString("N").Substring(0, 8);
            var paths = PrefabPaths(config).Concat(new[] { AssetDatabase.GetAssetPath(config), AssetDatabase.GetAssetPath(config.uiTheme), AssetDatabase.GetAssetPath(config.uiComponents) });
            foreach (string path in paths.Where(x => !string.IsNullOrEmpty(x)).Distinct())
            {
                string target = folder + "/" + path; Directory.CreateDirectory(Path.GetDirectoryName(target)); File.Copy(path, target, true);
                if (File.Exists(path + ".meta")) File.Copy(path + ".meta", target + ".meta", true);
            }
            return folder;
        }
        public static void Generate(GameFlowConfig config)
        {
            Editable(config); string backup = Backup(config);
            if (!config.uiPrefab || !config.homeHubPrefab || !config.offlineMenuPrefab || !config.mobileModulesPrefab) GameUIPrefabBuilder.Generate(config);
            string folder = Folder(config) + "/UI/Theme"; Directory.CreateDirectory(folder + "/Shared"); AssetDatabase.Refresh();
            Undo.RecordObject(config, "Configure UI theme");
            if (!config.uiTheme)
            {
                config.uiTheme = AssetDatabase.LoadAssetAtPath<UIThemeConfig>(folder + "/Theme.asset");
                if (!config.uiTheme) { config.uiTheme = CreateTheme(config); AssetDatabase.CreateAsset(config.uiTheme, folder + "/Theme.asset"); }
            }
            if (!config.uiComponents)
            {
                config.uiComponents = AssetDatabase.LoadAssetAtPath<UIComponentLibrary>(folder + "/Components.asset");
                if (!config.uiComponents) { config.uiComponents = ScriptableObject.CreateInstance<UIComponentLibrary>(); AssetDatabase.CreateAsset(config.uiComponents, folder + "/Components.asset"); }
            }
            Components(config, folder + "/Shared"); LinkHub(config);
            foreach (string path in PrefabPaths(config)) ProcessPrefab(path, config.uiTheme, true);
            EditorUtility.SetDirty(config); AssetDatabase.SaveAssetIfDirty(config);
            Debug.Log("UI theme/components ready. Backup: " + backup);
        }
        public static void ApplyTheme(GameFlowConfig config, UIThemeConfig theme)
        {
            Editable(config); if (!theme) throw new ArgumentNullException(nameof(theme)); string backup = Backup(config);
            Undo.RecordObject(config, "Choose UI theme"); config.uiTheme = theme;
            foreach (string path in PrefabPaths(config)) ProcessPrefab(path, theme, false);
            EditorUtility.SetDirty(config); AssetDatabase.SaveAssetIfDirty(config);
            Debug.Log("UI theme applied. Backup: " + backup);
        }
        private static UIThemeConfig CreateTheme(GameFlowConfig config)
        {
            var result = ScriptableObject.CreateInstance<UIThemeConfig>(); result.font = config.uiPrefab.title.font;
            var primary = config.uiPrefab.playButton; var secondary = config.homeHubPrefab.settings;
            result.styles = Enum.GetValues(typeof(UIThemeRole)).Cast<UIThemeRole>().Select(role => new UIThemeStyle { role = role }).ToArray();
            foreach (var style in result.styles)
            {
                style.graphic = new Color(.035f, .065f, .13f); style.text = Color.white;
                if (style.role == UIThemeRole.PrimaryButton || style.role == UIThemeRole.SecondaryButton || style.role == UIThemeRole.Tab)
                {
                    var source = style.role == UIThemeRole.PrimaryButton ? primary : secondary;
                    style.sprite = source.image.sprite; style.replaceSprite = style.sprite; style.graphic = source.image.color;
                    style.text = style.role == UIThemeRole.PrimaryButton ? new Color(.025f, .08f, .15f) : Color.white;
                    style.buttonColors = source.colors;
                }
                if (style.role == UIThemeRole.Artwork || style.role == UIThemeRole.PanelArt) style.graphic = Color.white;
                if (style.role == UIThemeRole.Card) style.graphic = new Color(.08f, .15f, .26f);
                if (style.role == UIThemeRole.Muted) style.text = new Color(.65f, .84f, 1);
                if (style.role == UIThemeRole.Currency) style.text = new Color(1, .83f, .32f);
                if (style.role == UIThemeRole.Success) { style.text = new Color(.4f, 1, .65f); style.graphic = style.text; }
                if (style.role == UIThemeRole.Error) { style.text = new Color(1, .48f, .4f); style.graphic = style.text; }
            }
            return result;
        }
        private static T CopyPrefab<T>(T source, string path) where T : Component
        {
            var existing = AssetDatabase.LoadAssetAtPath<T>(path); if (existing) return existing;
            var root = UnityEngine.Object.Instantiate(source.gameObject);
            try { foreach (var button in root.GetComponentsInChildren<Button>(true)) button.onClick = new Button.ButtonClickedEvent(); return PrefabUtility.SaveAsPrefabAsset(root, path).GetComponent<T>(); }
            finally { UnityEngine.Object.DestroyImmediate(root); }
        }
        private static void Components(GameFlowConfig config, string folder)
        {
            var library = config.uiComponents;
            if (!library.primaryButton) library.primaryButton = CopyPrefab(config.uiPrefab.playButton, folder + "/PrimaryButton.prefab");
            if (!library.secondaryButton) library.secondaryButton = CopyPrefab(config.homeHubPrefab.settings, folder + "/SecondaryButton.prefab");
            if (!library.tabButton) library.tabButton = CopyPrefab(config.homeHubPrefab.storePage.primary, folder + "/TabButton.prefab");
            if (!library.itemCard) library.itemCard = CopyPrefab(config.homeHubPrefab.cardPrefab, folder + "/ItemCard.prefab");
            if (!library.questCard) library.questCard = CopyPrefab(config.homeHubPrefab.cardPrefab, folder + "/QuestCard.prefab");
            if (!library.currencyBar)
            {
                string path = folder + "/CurrencyBar.prefab"; library.currencyBar = AssetDatabase.LoadAssetAtPath<CurrencyBarView>(path);
                if (!library.currencyBar)
                {
                    var root = UnityEngine.Object.Instantiate(config.homeHubPrefab.wallet.gameObject);
                    try { var view = root.AddComponent<CurrencyBarView>(); view.balance = root.GetComponent<Text>(); library.currencyBar = PrefabUtility.SaveAsPrefabAsset(root, path).GetComponent<CurrencyBarView>(); }
                    finally { UnityEngine.Object.DestroyImmediate(root); }
                }
            }
            EditorUtility.SetDirty(library); AssetDatabase.SaveAssetIfDirty(library);
            SetButtonRole(library.primaryButton, UIThemeRole.PrimaryButton, config.uiTheme);
            SetButtonRole(library.secondaryButton, UIThemeRole.SecondaryButton, config.uiTheme);
            SetButtonRole(library.tabButton, UIThemeRole.Tab, config.uiTheme);
        }
        private static void SetButtonRole(Button prefab, UIThemeRole role, UIThemeConfig theme)
        {
            string path = AssetDatabase.GetAssetPath(prefab); var root = PrefabUtility.LoadPrefabContents(path);
            try { var binding = root.GetComponent<UIThemeBinding>() ?? root.AddComponent<UIThemeBinding>(); binding.role = role; bool capture = !binding.theme; binding.theme = theme; binding.graphic = root.GetComponent<Image>(); binding.control = root.GetComponent<Button>(); binding.label = root.GetComponentInChildren<Text>(true); if (capture) binding.CaptureTypography(); PrefabUtility.SaveAsPrefabAsset(root, path); }
            finally { PrefabUtility.UnloadPrefabContents(root); }
        }
        private static void LinkHub(GameFlowConfig config)
        {
            string path = AssetDatabase.GetAssetPath(config.homeHubPrefab); var root = PrefabUtility.LoadPrefabContents(path);
            try
            {
                var view = root.GetComponent<HomeHubView>(); view.cardPrefab = config.uiComponents.itemCard; view.questCardPrefab = config.uiComponents.questCard;
                if (!view.wallet.GetComponent<CurrencyBarView>())
                {
                    var old = view.wallet; var rect = old.rectTransform;
                    var replacement = (GameObject)PrefabUtility.InstantiatePrefab(config.uiComponents.currencyBar.gameObject, rect.parent);
                    var target = (RectTransform)replacement.transform; target.anchorMin = rect.anchorMin; target.anchorMax = rect.anchorMax; target.pivot = rect.pivot;
                    target.sizeDelta = rect.sizeDelta; target.anchoredPosition3D = rect.anchoredPosition3D; target.localScale = rect.localScale; target.localRotation = rect.localRotation; target.SetSiblingIndex(rect.GetSiblingIndex());
                    view.wallet = replacement.GetComponent<CurrencyBarView>().balance; UnityEngine.Object.DestroyImmediate(old.gameObject);
                }
                PrefabUtility.SaveAsPrefabAsset(root, path);
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
        }
        private static bool Own(Component component) => !PrefabUtility.GetNearestPrefabInstanceRoot(component.gameObject);
        private static UIThemeRole ButtonRole(Button button)
        {
            var page = button.GetComponentInParent<HubPageView>(); if (page && (button == page.primary || button == page.secondary)) return UIThemeRole.Tab;
            var menu = button.GetComponentInParent<OfflineMenuView>(); if (menu && menu.tabs.Any(x => x.button == button)) return UIThemeRole.Tab;
            string name = button.name.ToLowerInvariant();
            return new[] { "play", "resume", "save", "collect", "accept", "confirm", "next level", "buy" }.Any(name.Contains) ? UIThemeRole.PrimaryButton : UIThemeRole.SecondaryButton;
        }
        private static UIThemeBinding Bind(Component node, UIThemeRole role)
        {
            var binding = node.GetComponent<UIThemeBinding>(); if (binding) return binding;
            binding = node.gameObject.AddComponent<UIThemeBinding>(); binding.role = role; return binding;
        }
        public static void ProcessPrefab(string path, UIThemeConfig theme, bool prepare)
        {
            var root = PrefabUtility.LoadPrefabContents(path);
            try
            {
                if (prepare)
                {
                    foreach (var slider in root.GetComponentsInChildren<Slider>(true).Where(Own))
                    {
                        var rect = (RectTransform)slider.transform; rect.sizeDelta = new Vector2(rect.sizeDelta.x, Mathf.Max(70, rect.sizeDelta.y));
                        if (!slider.transform.Find("Touch area"))
                        {
                            var area = new GameObject("Touch area", typeof(RectTransform), typeof(Image)); area.transform.SetParent(slider.transform, false); area.transform.SetAsFirstSibling();
                            var hit = (RectTransform)area.transform; hit.anchorMin = Vector2.zero; hit.anchorMax = Vector2.one; hit.offsetMin = hit.offsetMax = Vector2.zero;
                            area.GetComponent<Image>().color = Color.clear;
                        }
                    }

                    foreach (var button in root.GetComponentsInChildren<Button>(true).Where(Own))
                    {
                        if (button.name == "Volume -" || button.name == "Volume +")
                        { var rect = (RectTransform)button.transform; rect.sizeDelta = new Vector2(Mathf.Max(70, rect.sizeDelta.x), Mathf.Max(70, rect.sizeDelta.y)); }
                        var binding = Bind(button, ButtonRole(button)); binding.graphic = button.image; binding.control = button; binding.label = button.GetComponentInChildren<Text>(true);
                        // Typography capture is performed only when first tagging; repeated preparation cannot compound scaling.
                        if (!binding.theme) binding.CaptureTypography();
                    }
                    foreach (var text in root.GetComponentsInChildren<Text>(true).Where(Own))
                    {
                        if (text.GetComponentInParent<Button>()) continue;
                        string name = text.name.ToLowerInvariant();
                        var role = text.GetComponent<CurrencyBarView>() || name.Contains("balance") || name.Contains("wallet") ? UIThemeRole.Currency : name.Contains("description") || name.Contains("hint") ? UIThemeRole.Muted : text.fontSize >= 36 ? UIThemeRole.Title : UIThemeRole.Body;
                        var binding = Bind(text, role); binding.label = text; if (!binding.theme) binding.CaptureTypography();
                    }
                    foreach (var image in root.GetComponentsInChildren<Image>(true).Where(Own))
                    {
                        if (image.GetComponentInParent<Button>() || image.GetComponent<UIThemeBinding>()) continue;
                        string name = image.name.ToLowerInvariant();
                        var role = image.type == Image.Type.Filled ? UIThemeRole.Success : image.GetComponent<HubCardView>() ? UIThemeRole.Card : image.sprite ? (name.Contains("panel") ? UIThemeRole.PanelArt : UIThemeRole.Artwork) : name.Contains("background") || name.Contains("blocker") ? UIThemeRole.Background : UIThemeRole.Panel;
                        // Target masks/borders, dots, dimmers and gameplay-specific colors keep their intentional meaning.
                        if (name.Contains("touch area") || name.Contains("highlight") || name.Contains("outside target") || name.Contains("badge") || name.Contains("dim") || name.Contains("blocker")) continue;
                        var binding = Bind(image, role); binding.graphic = image; if (role == UIThemeRole.Artwork) binding.useSprite = false;
                    }
                }
                foreach (var binding in root.GetComponentsInChildren<UIThemeBinding>(true).Where(Own)) { binding.theme = theme; binding.Apply(theme); }
                var menu = root.GetComponent<OfflineMenuView>();
                if (menu) { var success = theme.Find(UIThemeRole.Success); var error = theme.Find(UIThemeRole.Error); if (success != null) menu.successColor = success.text; if (error != null) menu.errorColor = error.text; }
                PrefabUtility.SaveAsPrefabAsset(root, path);
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
        }
    }
}
