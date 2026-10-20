using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
namespace BaseUnity.GameFlow.Editor
{
    public static class UIBarBuilder
    {
        private const string Pack = "Assets/Layer Lab/GUI Pro-CasualGame/ResourcesData/Sprite/Components/Slider/";
        [MenuItem("Tools/BaseUnity/UI/Repair Slider and Progress Fills")]
        public static void RepairSelected() { UIThemeBuilder.Generate(UIThemeBuilder.SelectedConfig()); }
        public static Sprite WhiteSprite(string folder)
        {
            string path = folder + "/BarWhite.png";
            var existing = AssetDatabase.LoadAssetAtPath<Sprite>(path); if (existing) return existing;
            Directory.CreateDirectory(folder); var texture = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            try { texture.SetPixels(new[] { Color.white, Color.white, Color.white, Color.white }); texture.Apply(); File.WriteAllBytes(path, texture.EncodeToPNG()); }
            finally { UnityEngine.Object.DestroyImmediate(texture); }
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
            var importer = (TextureImporter)AssetImporter.GetAtPath(path); importer.textureType = TextureImporterType.Sprite; importer.spriteImportMode = SpriteImportMode.Single; importer.spritePixelsPerUnit = 100; importer.mipmapEnabled = false;
            var settings = new TextureImporterSettings(); importer.ReadTextureSettings(settings); settings.spriteMeshType = SpriteMeshType.FullRect; importer.SetTextureSettings(settings); importer.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<Sprite>(path);
        }
        public static void EnsureStyles(UIThemeConfig theme, string folder)
        {
            bool maskMissing = !theme.barMaskSprite;
            if (maskMissing) theme.barMaskSprite = WhiteSprite(folder + "/Shared");
            var styles = (theme.styles ?? Array.Empty<UIThemeStyle>()).ToList();
            Sprite white = null;
            foreach (var role in new[] { UIThemeRole.BarTrack, UIThemeRole.BarFill })
            {
                if (theme.Find(role) != null) continue;
                var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(Pack + (role == UIThemeRole.BarTrack ? "Slider_Basic01_Bg.png" : "Slider_Basic01_Fill_White.png"));
                if (!sprite) { if (!white) white = WhiteSprite(folder + "/Shared"); sprite = white; }
                var success = theme.Find(UIThemeRole.Success);
                styles.Add(new UIThemeStyle { role = role, replaceSprite = true, sprite = sprite, graphic = role == UIThemeRole.BarTrack ? new Color(.25f, .28f, .35f) : success != null ? success.graphic : new Color(.3f, .86f, .64f) });
            }
            if (!maskMissing && styles.Count == (theme.styles?.Length ?? 0)) return;
            theme.styles = styles.ToArray(); EditorUtility.SetDirty(theme); AssetDatabase.SaveAssetIfDirty(theme);
        }
        private static bool Own(Component component) => !PrefabUtility.GetNearestPrefabInstanceRoot(component.gameObject);
        private static void Stretch(RectTransform rect, float inset)
        { rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one; rect.offsetMin = Vector2.one * inset; rect.offsetMax = Vector2.one * -inset; rect.localScale = Vector3.one; rect.localRotation = Quaternion.identity; }
        private static void Style(Image image, UIThemeRole role, UIThemeConfig theme, Sprite white)
        {
            var binding = image.GetComponent<UIThemeBinding>() ?? image.gameObject.AddComponent<UIThemeBinding>(); binding.role = role; binding.theme = theme; binding.graphic = image; binding.useSprite = true;
            var style = theme ? theme.Find(role) : null;
            var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(Pack + (role == UIThemeRole.BarTrack ? "Slider_Basic01_Bg.png" : "Slider_Basic01_Fill_White.png"));
            // Replace the old button/background art. A configured bar style takes precedence.
            if (style != null && !style.replaceSprite && image.sprite) sprite = image.sprite;
            else if (style != null && style.sprite) sprite = style.sprite;
            image.sprite = sprite ? sprite : white; image.preserveAspect = false; image.useSpriteMesh = false; image.raycastTarget = role == UIThemeRole.BarTrack && image.raycastTarget;
            image.color = style != null ? style.graphic : role == UIThemeRole.BarTrack ? new Color(.25f, .28f, .35f) : new Color(.3f, .86f, .64f);
            if (role == UIThemeRole.BarTrack) image.type = Image.Type.Sliced;
            else image.type = Image.Type.Sliced;
        }
        private static void Fill(Image driver, UIThemeConfig theme, Sprite white)
        {
            if (!white) throw new InvalidOperationException("Generate a solid bar mask sprite before repairing bars.");
            var artTransform = driver.transform.Find("Fill artwork");
            Image art;
            if (artTransform) art = artTransform.GetComponent<Image>();
            else
            {
                var child = new GameObject("Fill artwork", typeof(RectTransform), typeof(Image)); child.transform.SetParent(driver.transform, false); art = child.GetComponent<Image>();
            }
            var oldBinding = driver.GetComponent<UIThemeBinding>();
            bool copyFlags = oldBinding && oldBinding.graphic == driver;
            Stretch(art.rectTransform, 0); Style(art, UIThemeRole.BarFill, theme, white); art.raycastTarget = false;
            if (copyFlags) { var binding = art.GetComponent<UIThemeBinding>(); binding.useGraphicColor = oldBinding.useGraphicColor; }
            // The original Image remains the progress/value API. Its filled rectangle is a stencil,
            // while the child renders the full-size nine-sliced art under that stencil.
            driver.sprite = white; driver.type = Image.Type.Filled; driver.color = Color.white; driver.preserveAspect = false; driver.useSpriteMesh = false; driver.raycastTarget = false;
            driver.fillMethod = Image.FillMethod.Horizontal; driver.fillOrigin = 0;
            var mask = driver.GetComponent<Mask>() ?? driver.gameObject.AddComponent<Mask>(); mask.showMaskGraphic = false;
            if (!oldBinding) oldBinding = driver.gameObject.AddComponent<UIThemeBinding>();
            oldBinding.role = UIThemeRole.BarFill; oldBinding.theme = theme; oldBinding.graphic = null; oldBinding.useSprite = false; oldBinding.useGraphicColor = false;
        }
        public static void ConfigureRoot(GameObject root, UIThemeConfig theme, Sprite white)
        {
            foreach (var slider in root.GetComponentsInChildren<Slider>(true).Where(Own))
            {
                if (!slider.fillRect) continue; var fill = slider.fillRect.GetComponent<Image>(); var track = slider.transform.Find("Track")?.GetComponent<Image>();
                if (!track) track = slider.transform.Find("Background")?.GetComponent<Image>();
                if (!fill || !track) continue;
                var area = fill.rectTransform.parent as RectTransform;
                if (!area || area == slider.transform || area == track.transform)
                { area = new GameObject("Fill area", typeof(RectTransform)).GetComponent<RectTransform>(); area.SetParent(track.transform, false); }
                else area.SetParent(track.transform, false);
                Stretch(area, 2); fill.rectTransform.SetParent(area, false); Stretch(fill.rectTransform, 0);
                Style(track, UIThemeRole.BarTrack, theme, white); Fill(fill, theme, white);
                bool horizontal = slider.direction == Slider.Direction.LeftToRight || slider.direction == Slider.Direction.RightToLeft;
                fill.fillMethod = horizontal ? Image.FillMethod.Horizontal : Image.FillMethod.Vertical;
                fill.fillOrigin = slider.direction == Slider.Direction.RightToLeft || slider.direction == Slider.Direction.TopToBottom ? 1 : 0;
                // Refresh Slider's cached image and visuals without changing value or invoking callbacks.
                slider.fillRect = null; slider.fillRect = fill.rectTransform;
            }
            foreach (var fill in root.GetComponentsInChildren<Image>(true).Where(Own).ToArray())
            {
                if (fill.type != Image.Type.Filled || fill.fillMethod != Image.FillMethod.Horizontal || fill.GetComponentInParent<Slider>()) continue;
                var track = fill.transform.parent ? fill.transform.parent.GetComponent<Image>() : null;
                if (!track && fill.transform.parent) track = fill.transform.parent.Find("Progress track")?.GetComponent<Image>();
                if (!track || track == fill || (fill.name != "Fill" && fill.name != "Progress fill")) continue;
                fill.transform.SetParent(track.transform, false); Stretch(fill.rectTransform, 2);
                Style(track, UIThemeRole.BarTrack, theme, white); Fill(fill, theme, white);
            }
        }
    }
}
