using System;
using System.IO;
using System.Linq;
using BaseUnity.Core;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
namespace BaseUnity.GameFlow.Editor.Tests
{
    public sealed class UIThemeEditorTests
    {
        private GameFlowConfig Config => AssetDatabase.LoadAssetAtPath<GameFlowConfig>("Assets/BaseUnityGameFlow/GameFlowConfig.asset");
        [Test] public void BindingIsIdempotentAndPreservesGameplayLayoutAndContent()
        {
            var root = new GameObject("Button", typeof(RectTransform), typeof(Image), typeof(Button));
            var theme = ScriptableObject.CreateInstance<UIThemeConfig>();
            try
            {
                var label = new GameObject("Label", typeof(RectTransform), typeof(Text)).GetComponent<Text>(); label.transform.SetParent(root.transform); label.fontSize = 24; label.resizeTextMinSize = 12; label.resizeTextMaxSize = 24; label.text = "PRICE 50";
                var button = root.GetComponent<Button>(); button.interactable = false;
                var rect = (RectTransform)root.transform; rect.anchoredPosition = new Vector2(20, 70); rect.sizeDelta = new Vector2(350, 90);
                theme.styles = new[] { new UIThemeStyle { role = UIThemeRole.PrimaryButton, textScale = 1.25f, text = Color.cyan, graphic = Color.magenta } };
                var binding = root.AddComponent<UIThemeBinding>(); binding.role = UIThemeRole.PrimaryButton; binding.label = label; binding.graphic = root.GetComponent<Image>(); binding.control = button; binding.CaptureTypography();
                binding.Apply(theme); binding.Apply(theme);
                Assert.AreEqual(30, label.fontSize); Assert.AreEqual("PRICE 50", label.text); Assert.AreEqual(Color.cyan, label.color); Assert.AreEqual(Color.magenta, binding.graphic.color);
                Assert.IsFalse(button.interactable); Assert.AreEqual(new Vector2(20, 70), rect.anchoredPosition); Assert.AreEqual(new Vector2(350, 90), rect.sizeDelta);
                binding.useTextColor = false; label.color = Color.yellow; binding.Apply(theme); Assert.AreEqual(Color.yellow, label.color);
            }
            finally { UnityEngine.Object.DestroyImmediate(root); UnityEngine.Object.DestroyImmediate(theme); }
        }
        [Test] public void AllPreviewScreensAreIsolatedAndReleasePreviewScenes()
        {
            Assert.IsNotNull(Config.uiTheme); int count = EditorSceneManager.previewSceneCount;
            foreach (UIPreviewScreen screen in Enum.GetValues(typeof(UIPreviewScreen)))
            {
                using (var preview = new UIPreviewSession(Config, Config.uiTheme, screen, UIPreviewDevice.Defaults[1], true, UIPreviewLanguage.LongVietnamese))
                {
                    Assert.AreEqual(0, preview.Root.GetComponentsInChildren<GameBootstrap>(true).Length);
                    Assert.AreEqual(0, preview.Root.GetComponentsInChildren<AudioListener>(true).Length);
                    Assert.Greater(preview.Root.GetComponentsInChildren<Text>().Length, 0, screen.ToString());
                    Assert.Less(preview.SafeArea.height, preview.Height);
                }
                Assert.AreEqual(count, EditorSceneManager.previewSceneCount, screen.ToString());
            }
        }
        [Test] public void TemporaryPreviewThemeDoesNotChangeSerializedProjectAssets()
        {
            var paths = UIThemeBuilder.PrefabPaths(Config).Concat(new[] { AssetDatabase.GetAssetPath(Config), AssetDatabase.GetAssetPath(Config.uiTheme) }).ToArray();
            var before = paths.ToDictionary(x => x, File.ReadAllBytes); var copy = UnityEngine.Object.Instantiate(Config.uiTheme);
            try
            {
                copy.Find(UIThemeRole.Title).text = Color.magenta;
                using (var preview = new UIPreviewSession(Config, copy, UIPreviewScreen.Home, UIPreviewDevice.Defaults[0], false, UIPreviewLanguage.English))
                { Assert.IsTrue(preview.Root.GetComponentsInChildren<UIThemeBinding>().Any(x => x.role == UIThemeRole.Title && x.label && x.label.color == Color.magenta)); }
                foreach (var path in paths) CollectionAssert.AreEqual(before[path], File.ReadAllBytes(path), path);
            }
            finally { UnityEngine.Object.DestroyImmediate(copy); }
        }
        [Test] public void ValidatorFindsUnsafeSmallControlAndTruncatedText()
        {
            using (var preview = new UIPreviewSession(Config, Config.uiTheme, UIPreviewScreen.Home, UIPreviewDevice.Defaults[0], true, UIPreviewLanguage.English))
            {
                var node = new GameObject("Unsafe control", typeof(RectTransform), typeof(Image), typeof(Button)); node.transform.SetParent(preview.Root.transform, false);
                var rect = (RectTransform)node.transform; rect.sizeDelta = new Vector2(20, 20); rect.anchoredPosition = new Vector2(2000, 2000);
                var label = new GameObject("Truncated sample", typeof(RectTransform), typeof(Text)).GetComponent<Text>(); label.transform.SetParent(preview.Root.transform, false);
                label.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf"); label.fontSize = 30; label.rectTransform.sizeDelta = new Vector2(60, 10); label.text = "A very long sample that cannot fit"; label.verticalOverflow = VerticalWrapMode.Truncate;
                Canvas.ForceUpdateCanvases(); var issues = UILayoutValidator.Inspect(preview, "Test", "Phone", "en");
                Assert.IsTrue(issues.Any(x => x.hierarchy.Contains("Unsafe control") && x.message.Contains("outside")));
                Assert.IsTrue(issues.Any(x => x.hierarchy.Contains("Unsafe control") && x.message.Contains("48")));
                Assert.IsTrue(issues.Any(x => x.hierarchy.Contains("Truncated sample") && x.message.Contains("truncated")));
            }
        }
        [Test] public void PreviewRenderContainsVisibleUI()
        {
            using (var preview = new UIPreviewSession(Config, Config.uiTheme, UIPreviewScreen.Home, UIPreviewDevice.Defaults[0], true, UIPreviewLanguage.English))
            {
                var rendered = preview.Render(240); var previous = RenderTexture.active; Texture2D image = null;
                try { RenderTexture.active = rendered; image = new Texture2D(rendered.width, rendered.height); image.ReadPixels(new Rect(0, 0, rendered.width, rendered.height), 0, 0); image.Apply(); Assert.IsTrue(image.GetPixels().Any(x => x.r > .35f || x.g > .35f || x.b > .35f), "Preview must render UI, not just the camera background."); }
                finally { RenderTexture.active = previous; if (image) UnityEngine.Object.DestroyImmediate(image); }
            }
        }
        [Test] public void SharedComponentsAreLinkedAndRolesAreExplicit()
        {
            var library = Config.uiComponents; Assert.IsNotNull(library);
            Assert.AreEqual(UIThemeRole.PrimaryButton, library.primaryButton.GetComponent<UIThemeBinding>().role);
            Assert.AreEqual(UIThemeRole.SecondaryButton, library.secondaryButton.GetComponent<UIThemeBinding>().role);
            Assert.AreEqual(UIThemeRole.Tab, library.tabButton.GetComponent<UIThemeBinding>().role);
            Assert.IsNotNull(library.currencyBar.balance); Assert.AreNotSame(library.itemCard, library.questCard);
            Assert.AreEqual(library.itemCard, Config.homeHubPrefab.cardPrefab); Assert.AreEqual(library.questCard, Config.homeHubPrefab.questCardPrefab);
            Assert.IsNotNull(Config.homeHubPrefab.wallet.GetComponent<CurrencyBarView>());
        }
    }
}
