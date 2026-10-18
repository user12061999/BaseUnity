using System;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace BaseUnity.Editor
{
    public sealed class SampleDiffWindow : EditorWindow
    {
        private string current, incoming, baseline, diff;
        private int mode;
        private Vector2 scroll;
        public static void Open(SampleUpdateEntry entry)
        {
            var window = CreateInstance<SampleDiffWindow>(); window.titleContent = new GUIContent("Diff • " + entry.path);
            window.current = Decode(entry.currentContent); window.incoming = Decode(entry.sourceContent); window.baseline = Decode(entry.baselineContent);
            window.diff = Difference(window.current, window.incoming); window.minSize = new Vector2(700, 460); window.Show();
        }
        private static string Decode(string value)
        {
            if (value == null) return "";
            byte[] bytes;
            try { bytes = Convert.FromBase64String(value); } catch { return "[Invalid snapshot data]"; }
            try
            {
                string text = new UTF8Encoding(false, true).GetString(bytes);
                if (text.IndexOf('\0') >= 0) throw new DecoderFallbackException();
                return text.Replace("\r\n", "\n");
            }
            catch (DecoderFallbackException)
            {
                using (var sha = System.Security.Cryptography.SHA256.Create())
                    return "[Binary: " + bytes.Length + " bytes; SHA-256 " + BitConverter.ToString(sha.ComputeHash(bytes)).Replace("-", "") + "]";
            }
        }
        public static string Difference(string before, string after)
        {
            var a = before.Split('\n'); var b = after.Split('\n');
            if ((long)a.Length * b.Length > 1000000) return "Diff is too large; use Current/Source tabs to inspect the full content.";
            var lengths = new int[a.Length + 1, b.Length + 1];
            for (int i = a.Length - 1; i >= 0; i--) for (int j = b.Length - 1; j >= 0; j--)
                lengths[i, j] = a[i] == b[j] ? lengths[i + 1, j + 1] + 1 : Math.Max(lengths[i + 1, j], lengths[i, j + 1]);
            var output = new StringBuilder("--- Current project file\n+++ Incoming sample source\n"); int x = 0, y = 0;
            while (x < a.Length || y < b.Length)
            {
                if (x < a.Length && y < b.Length && a[x] == b[y]) { output.Append("  " + a[x] + "\n"); x++; y++; }
                else if (y < b.Length && (x == a.Length || lengths[x, y + 1] >= lengths[x + 1, y])) output.Append("+ " + b[y++] + "\n");
                else output.Append("- " + a[x++] + "\n");
            }
            return output.ToString();
        }
        private void OnGUI()
        {
            mode = GUILayout.Toolbar(mode, new[] { "Diff", "Current", "Source", "Tracked baseline" });
            string text = mode == 0 ? diff : mode == 1 ? current : mode == 2 ? incoming : baseline;
            scroll = EditorGUILayout.BeginScrollView(scroll);
            var style = new GUIStyle(EditorStyles.textArea) { wordWrap = false, richText = false };
            EditorGUILayout.SelectableLabel(text, style, GUILayout.MinHeight(Math.Max(400, style.CalcHeight(new GUIContent(text), position.width - 30))), GUILayout.MinWidth(1000));
            EditorGUILayout.EndScrollView();
        }
    }
}
