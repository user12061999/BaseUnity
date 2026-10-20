using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;
namespace BaseUnity.GameFlow.Editor
{
    [Serializable] public sealed class UILayoutIssue
    {
        public string screen, device, language, hierarchy, message;
    }
    [Serializable] public sealed class UILayoutReport
    {
        public string generatedUtc;
        public int cases;
        public List<UILayoutIssue> issues = new List<UILayoutIssue>();
        public void Save(string path)
        { Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path))); File.WriteAllText(path, JsonUtility.ToJson(this, true)); }
    }
    public static class UILayoutValidator
    {
        private static string PathOf(Transform item, Transform root)
        { string path = item.name; while (item.parent && item.parent != root) { item = item.parent; path = item.name + "/" + path; } return path; }
        public static List<UILayoutIssue> Inspect(UIPreviewSession session, string screen, string device, string language)
        {
            var issues = new List<UILayoutIssue>();
            Action<Transform, string> add = (item, message) => issues.Add(new UILayoutIssue { screen = screen, device = device, language = language, hierarchy = PathOf(item, session.Root.transform), message = message });
            var corners = new Vector3[4];
            foreach (var control in session.Root.GetComponentsInChildren<Selectable>())
            {
                var rect = (RectTransform)control.transform; rect.GetWorldCorners(corners);
                var bounds = Rect.MinMaxRect(corners.Min(x => x.x) + session.Width / 2f, corners.Min(x => x.y) + session.Height / 2f, corners.Max(x => x.x) + session.Width / 2f, corners.Max(x => x.y) + session.Height / 2f);
                if (bounds.xMin < session.SafeArea.xMin - 1 || bounds.yMin < session.SafeArea.yMin - 1 || bounds.xMax > session.SafeArea.xMax + 1 || bounds.yMax > session.SafeArea.yMax + 1) add(control.transform, "Control extends outside the simulated safe area.");
                if (bounds.width < 48 || bounds.height < 48) add(control.transform, "Touch target is smaller than 48 simulated pixels; review on the target device.");
            }
            foreach (var text in session.Root.GetComponentsInChildren<Text>())
            {
                if (string.IsNullOrEmpty(text.text)) continue;
                if (!text.font) { add(text.transform, "Font is not assigned."); continue; }
                string missing = new string(text.text.Where(c => !char.IsWhiteSpace(c) && !char.IsControl(c) && !text.font.HasCharacter(c)).Distinct().ToArray());
                if (missing.Length > 0) add(text.transform, "Font does not contain sample glyphs: " + missing);
                var settings = text.GetGenerationSettings(text.rectTransform.rect.size);
                text.cachedTextGenerator.Populate(text.text, settings);
                int bestSize = text.cachedTextGenerator.fontSizeUsedForBestFit;
                settings.fontSize = text.resizeTextForBestFit ? (bestSize > 0 ? bestSize : text.resizeTextMaxSize) : text.fontSize;
                settings.resizeTextForBestFit = false; settings.verticalOverflow = VerticalWrapMode.Overflow;
                float height = text.cachedTextGeneratorForLayout.GetPreferredHeight(text.text, settings) / Mathf.Max(.01f, text.pixelsPerUnit);
                if (height > text.rectTransform.rect.height + 2 && text.verticalOverflow == VerticalWrapMode.Truncate) add(text.transform, "Sample text can be truncated vertically; enlarge the label, enable best fit, or use a shorter translation.");
            }
            return issues;
        }
        public static UILayoutReport ValidateAll(GameFlowConfig config, UIThemeConfig theme)
        {
            var report = new UILayoutReport { generatedUtc = DateTime.UtcNow.ToString("O") };
            foreach (var device in UIPreviewDevice.Defaults)
            foreach (var language in new[] { UIPreviewLanguage.English, UIPreviewLanguage.LongVietnamese })
            foreach (UIPreviewScreen screen in Enum.GetValues(typeof(UIPreviewScreen)))
            {
                using (var preview = new UIPreviewSession(config, theme, screen, device, true, language))
                    report.issues.AddRange(Inspect(preview, screen.ToString(), device.name, language.ToString()));
                report.cases++;
            }
            return report;
        }
    }
}
