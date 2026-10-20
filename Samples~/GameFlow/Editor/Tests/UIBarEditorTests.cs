using System;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
namespace BaseUnity.GameFlow.Editor.Tests
{
    public sealed class UIBarEditorTests
    {
        private GameFlowConfig Config => AssetDatabase.LoadAssetAtPath<GameFlowConfig>("Assets/BaseUnityGameFlow/GameFlowConfig.asset");
        [Test] public void SlidersKeepValueCallbacksAndUseFullSizeClippedArtwork()
        {
            using (var preview = new UIPreviewSession(Config, Config.uiTheme, UIPreviewScreen.Settings, UIPreviewDevice.Defaults[0], true, UIPreviewLanguage.English))
            {
                var sliders = preview.Root.GetComponentsInChildren<Slider>(); Assert.AreEqual(3, sliders.Length);
                int callbacks = 0; var values = sliders.Select(x => x.value).ToArray();
                foreach (var slider in sliders) slider.onValueChanged.AddListener(_ => callbacks++);
                UIBarBuilder.ConfigureRoot(preview.Root, Config.uiTheme, Config.uiTheme.barMaskSprite);
                Assert.AreEqual(0, callbacks);
                for (int i = 0; i < sliders.Length; i++)
                {
                    var slider = sliders[i]; Assert.AreEqual(values[i], slider.value);
                    var driver = slider.fillRect.GetComponent<Image>(); var art = driver.transform.Find("Fill artwork").GetComponent<Image>();
                    Assert.AreEqual(Image.Type.Filled, driver.type); Assert.AreEqual(Image.Type.Sliced, art.type); Assert.IsFalse(driver.GetComponent<Mask>().showMaskGraphic);
                    Assert.AreEqual(Vector2.zero, slider.fillRect.offsetMin); Assert.AreEqual(Vector2.zero, slider.fillRect.offsetMax);
                    var area = (RectTransform)driver.transform.parent; var track = area.parent.GetComponent<Image>(); Assert.IsNotNull(track);
                    Assert.AreEqual(track.rectTransform.rect.width - 4, area.rect.width, .01f);
                    foreach (float value in new[] { 0f, .5f, 1f }) { slider.SetValueWithoutNotify(value); Assert.AreEqual(value, driver.fillAmount, .0001f); Assert.AreEqual(area.rect.width, art.rectTransform.rect.width, .01f); }
                }
                Assert.AreEqual(0, callbacks);
            }
        }
        [Test] public void LoadingAndQuestProgressCoverTheirTrackInterior()
        {
            foreach (var screen in new[] { UIPreviewScreen.Loading, UIPreviewScreen.Quests })
            using (var preview = new UIPreviewSession(Config, Config.uiTheme, screen, UIPreviewDevice.Defaults[0], true, UIPreviewLanguage.English))
            {
                var drivers = preview.Root.GetComponentsInChildren<Image>().Where(x => x.type == Image.Type.Filled && x.GetComponent<Mask>()).ToArray(); Assert.Greater(drivers.Length, 0);
                foreach (var driver in drivers)
                {
                    var track = driver.transform.parent.GetComponent<Image>(); Assert.IsNotNull(track);
                    Assert.AreEqual(track.rectTransform.rect.width - 4, driver.rectTransform.rect.width, .01f);
                    Assert.AreEqual(track.rectTransform.rect.height - 4, driver.rectTransform.rect.height, .01f);
                    var art = driver.transform.Find("Fill artwork").GetComponent<Image>(); Assert.AreEqual(UIThemeRole.BarFill, art.GetComponent<UIThemeBinding>().role);
                    art.GetComponent<UIThemeBinding>().Apply(Config.uiTheme); Assert.AreSame(Config.uiTheme.barMaskSprite, driver.sprite);
                    driver.fillAmount = 0; Assert.AreEqual(0, driver.fillAmount); driver.fillAmount = 1; Assert.AreEqual(1, driver.fillAmount);
                }
            }
        }
        [Test] public void RenderedEndpointsAreEmptyAtZeroAndFilledAtOne()
        {
            var theme = UnityEngine.Object.Instantiate(Config.uiTheme); theme.Find(UIThemeRole.BarTrack).graphic = Color.black; theme.Find(UIThemeRole.BarFill).graphic = Color.green;
            try
            {
                using (var preview = new UIPreviewSession(Config, theme, UIPreviewScreen.Settings, UIPreviewDevice.Defaults[0], true, UIPreviewLanguage.English))
                {
                    var slider = preview.Root.GetComponentsInChildren<Slider>()[0]; slider.handleRect.gameObject.SetActive(false);
                    var corners = new Vector3[4]; slider.fillRect.GetWorldCorners(corners);
                    foreach (float value in new[] { 0f, .5f, 1f })
                    {
                        slider.SetValueWithoutNotify(value); var rendered = preview.Render(1280); var previous = RenderTexture.active; Texture2D image = null;
                        try
                        {
                            RenderTexture.active = rendered; image = new Texture2D(rendered.width, rendered.height); image.ReadPixels(new Rect(0, 0, rendered.width, rendered.height), 0, 0); image.Apply();
                            int y = Mathf.RoundToInt((corners[0].y + corners[1].y) / 2 + preview.Height / 2f);
                            var left = image.GetPixel(Mathf.RoundToInt(corners[0].x + preview.Width / 2f + 4), y); var right = image.GetPixel(Mathf.RoundToInt(corners[2].x + preview.Width / 2f - 4), y);
                            Assert.AreEqual(value > 0, left.g > .4f && left.r < .2f, "Left endpoint at " + value);
                            Assert.AreEqual(value >= 1, right.g > .4f && right.r < .2f, "Right endpoint at " + value);
                        }
                        finally { RenderTexture.active = previous; if (image) UnityEngine.Object.DestroyImmediate(image); }
                    }
                }
            }
            finally { UnityEngine.Object.DestroyImmediate(theme); }
        }
    }
}
