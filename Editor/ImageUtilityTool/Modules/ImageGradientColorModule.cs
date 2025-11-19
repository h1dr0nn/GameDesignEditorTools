#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace h1dr0n.EditorTools
{
    public static class ImageGradientColorModule
    {
        private enum ColorToneMode
        {
            Normal,     // Keep original colors
            Pastel,     // Lighten and desaturate for soft tones
            Vibrant,    // Increase saturation and brightness for vivid colors
            Muted,      // Reduce saturation and brightness for subdued tones
            Deep        // Increase contrast for darker, richer tones
        }

        private static ColorToneMode colorToneMode = ColorToneMode.Pastel;

        private static int maxCandidates = 10;
        private static int simplifyStep = 8;
        private static int darkThreshold = 30;
        private static int lightThreshold = 225;

        // Tone parameters
        private static float pastelBrighten = 0.15f;
        private static float pastelSaturation = 0.5f;
        private static float vibrantSaturation = 1.4f;
        private static float vibrantValue = 1.2f;
        private static float mutedSaturation = 0.4f;
        private static float mutedValue = 0.8f;
        private static float deepSaturation = 1.2f;
        private static float deepValue = 0.7f;

        private static Vector2 _scroll;
        private static readonly List<(string name, string color1, string color2)> _results = new();

        public static void DrawGUI(List<Texture2D> textures)
        {
            using (new EditorGUILayout.VerticalScope("box"))
            {
                EditorGUILayout.LabelField("Gradient Color Finder", EditorStyles.boldLabel);
                EditorGUILayout.HelpBox(
                    "Analyze images to detect two primary HEX colors forming a gradient pair. ",
                    MessageType.Info);

                maxCandidates = EditorGUILayout.IntSlider("Top Colors", maxCandidates, 3, 30);
                simplifyStep = EditorGUILayout.IntSlider("Simplify Step", simplifyStep, 1, 32);
                darkThreshold = EditorGUILayout.IntSlider("Dark Threshold", darkThreshold, 0, 100);
                lightThreshold = EditorGUILayout.IntSlider("Light Threshold", lightThreshold, 150, 255);

                EditorGUILayout.Space(4);
                EditorGUILayout.LabelField("Color Tone Mode", EditorStyles.boldLabel);
                colorToneMode = (ColorToneMode)EditorGUILayout.EnumPopup("Tone Mode", colorToneMode);

                DrawToneModeSettings();

                EditorGUILayout.Space(8);
                EditorGUI.BeginDisabledGroup(textures == null || textures.Count == 0);
                if (GUILayout.Button("Analyze Gradient Colors", GUILayout.Height(30)))
                    AnalyzeGradients(textures);
                EditorGUI.EndDisabledGroup();

                if (_results.Count > 0)
                {
                    EditorGUILayout.Space(10);
                    EditorGUILayout.LabelField("Results (Copy to Google Sheets / Excel)", EditorStyles.boldLabel);

                    if (GUILayout.Button("Copy Colors To Clipboard", GUILayout.Height(24)))
                    {
                        var sb = new StringBuilder();
                        foreach (var (_, c1, c2) in _results)
                            sb.AppendLine($"{c1}\t{c2}");
                        EditorGUIUtility.systemCopyBuffer = sb.ToString();
                        Debug.Log($"[ImageGradientColor] Copied {_results.Count} rows (colors only) to clipboard.");
                    }

                    using (var scroll = new EditorGUILayout.ScrollViewScope(_scroll, GUILayout.MinHeight(250)))
                    {
                        _scroll = scroll.scrollPosition;
                        EditorGUILayout.BeginHorizontal();
                        GUILayout.Label("File", EditorStyles.boldLabel, GUILayout.Width(150));
                        GUILayout.Label("Color 1", EditorStyles.boldLabel, GUILayout.Width(130));
                        GUILayout.Label("Color 2", EditorStyles.boldLabel, GUILayout.Width(130));
                        EditorGUILayout.EndHorizontal();

                        foreach (var (name, c1, c2) in _results)
                        {
                            EditorGUILayout.BeginHorizontal("box");
                            GUILayout.Label(name, GUILayout.Width(150), GUILayout.Height(18));
                            DrawColorCell(c1);
                            DrawColorCell(c2);
                            EditorGUILayout.EndHorizontal();
                        }
                    }
                }
            }
        }

        private static void DrawToneModeSettings()
        {
            using (new EditorGUILayout.VerticalScope("box"))
            {
                switch (colorToneMode)
                {
                    case ColorToneMode.Pastel:
                        pastelBrighten = EditorGUILayout.Slider("Brighten Factor", pastelBrighten, 0f, 0.4f);
                        pastelSaturation = EditorGUILayout.Slider("Saturation Factor", pastelSaturation, 0.1f, 1f);
                        break;

                    case ColorToneMode.Vibrant:
                        vibrantSaturation = EditorGUILayout.Slider("Saturation Boost", vibrantSaturation, 1f, 2f);
                        vibrantValue = EditorGUILayout.Slider("Brightness Boost", vibrantValue, 1f, 1.5f);
                        break;

                    case ColorToneMode.Muted:
                        mutedSaturation = EditorGUILayout.Slider("Saturation Reduction", mutedSaturation, 0.2f, 0.8f);
                        mutedValue = EditorGUILayout.Slider("Brightness Reduction", mutedValue, 0.5f, 1f);
                        break;

                    case ColorToneMode.Deep:
                        deepSaturation = EditorGUILayout.Slider("Saturation Boost", deepSaturation, 1f, 1.6f);
                        deepValue = EditorGUILayout.Slider("Brightness Factor", deepValue, 0.4f, 1f);
                        break;

                    default:
                        EditorGUILayout.HelpBox("Normal tone keeps the original image colors unchanged.", MessageType.None);
                        break;
                }
            }
        }

        private static void DrawColorCell(string hex)
        {
            if (!ColorUtility.TryParseHtmlString(hex, out var parsed))
                parsed = Color.magenta;

            Rect rect = GUILayoutUtility.GetRect(18, 18, GUILayout.Width(18));
            EditorGUI.DrawRect(rect, parsed);
            EditorGUILayout.SelectableLabel(hex, GUILayout.Width(100), GUILayout.Height(18));
        }

        private static void AnalyzeGradients(List<Texture2D> textures)
        {
            _results.Clear();

            foreach (var tex in textures)
            {
                if (tex == null) continue;
                string path = AssetDatabase.GetAssetPath(tex);
                if (string.IsNullOrEmpty(path) || !File.Exists(path)) continue;

                Texture2D readableTex = GetReadableCopy(tex);
                if (readableTex == null)
                {
                    _results.Add((tex.name, "⚠️", "Not Readable"));
                    continue;
                }

                var pixels = readableTex.GetPixels()
                    .Select(c => new Color32((byte)(c.r * 255), (byte)(c.g * 255), (byte)(c.b * 255), 255))
                    .Where(c => !IsTooDarkOrLight(c, darkThreshold, lightThreshold))
                    .Select(c => SimplifyColor(c, simplifyStep))
                    .ToList();

                if (pixels.Count == 0)
                {
                    _results.Add((tex.name, "⚠️", "No Valid Colors"));
                    continue;
                }

                var colorGroups = pixels
                    .GroupBy(c => c)
                    .OrderByDescending(g => g.Count())
                    .Take(maxCandidates)
                    .Select(g => (g.Key, g.Count()))
                    .ToList();

                if (colorGroups.Count < 2)
                {
                    _results.Add((tex.name, "⚠️", "Too Few Colors"));
                    continue;
                }

                float maxDist = -1f;
                Color32 c1 = default, c2 = default;

                for (int i = 0; i < colorGroups.Count; i++)
                {
                    for (int j = i + 1; j < colorGroups.Count; j++)
                    {
                        float dist = ColorDistance(colorGroups[i].Key, colorGroups[j].Key);
                        if (dist > maxDist)
                        {
                            maxDist = dist;
                            c1 = colorGroups[i].Key;
                            c2 = colorGroups[j].Key;
                        }
                    }
                }

                string hex1 = ApplyTone(ColorToHex(c1));
                string hex2 = ApplyTone(ColorToHex(c2));
                _results.Add((tex.name, hex1, hex2));
            }

            Debug.Log($"[ImageGradientColor] ✅ Processed {_results.Count} images");
        }

        #region Helpers

        private static Texture2D GetReadableCopy(Texture2D source)
        {
            if (source.isReadable) return source;
            try
            {
                string path = AssetDatabase.GetAssetPath(source);
                var importer = (TextureImporter)AssetImporter.GetAtPath(path);
                bool wasReadable = importer.isReadable;
                importer.isReadable = true;
                importer.SaveAndReimport();
                var copy = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
                importer.isReadable = wasReadable;
                importer.SaveAndReimport();
                return copy;
            }
            catch { return null; }
        }

        private static bool IsTooDarkOrLight(Color32 c, int darkThresh, int lightThresh)
        {
            if (c.r <= darkThresh && c.g <= darkThresh && c.b <= darkThresh) return true;
            if (c.r >= lightThresh && c.g >= lightThresh && c.b >= lightThresh) return true;
            return false;
        }

        private static Color32 SimplifyColor(Color32 c, int step)
        {
            byte r = (byte)((c.r / step) * step);
            byte g = (byte)((c.g / step) * step);
            byte b = (byte)((c.b / step) * step);
            return new Color32(r, g, b, 255);
        }

        private static float ColorDistance(Color32 a, Color32 b)
        {
            float dr = a.r - b.r;
            float dg = a.g - b.g;
            float db = a.b - b.b;
            return Mathf.Sqrt(dr * dr + dg * dg + db * db);
        }

        private static string ColorToHex(Color32 c)
        {
            return $"#{c.r:X2}{c.g:X2}{c.b:X2}";
        }

        #endregion

        #region Tone Logic

        private static string ApplyTone(string hex)
        {
            if (!ColorUtility.TryParseHtmlString(hex, out var color))
                return hex;

            Color.RGBToHSV(color, out float h, out float s, out float v);

            switch (colorToneMode)
            {
                case ColorToneMode.Pastel:
                    s *= pastelSaturation;
                    v = Mathf.Min(1f, v + pastelBrighten);
                    break;

                case ColorToneMode.Vibrant:
                    s = Mathf.Min(1f, s * vibrantSaturation);
                    v = Mathf.Min(1f, v * vibrantValue);
                    break;

                case ColorToneMode.Muted:
                    s *= mutedSaturation;
                    v *= mutedValue;
                    break;

                case ColorToneMode.Deep:
                    s = Mathf.Min(1f, s * deepSaturation);
                    v *= deepValue;
                    break;

                case ColorToneMode.Normal:
                default:
                    break;
            }

            Color newColor = Color.HSVToRGB(h, Mathf.Clamp01(s), Mathf.Clamp01(v));
            return $"#{(int)(newColor.r * 255):X2}{(int)(newColor.g * 255):X2}{(int)(newColor.b * 255):X2}";
        }

        #endregion
    }
}
#endif
