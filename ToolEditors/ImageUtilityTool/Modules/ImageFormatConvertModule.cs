#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using System.Collections.Generic;
using System.IO;

namespace h1dr0n.EditorTools
{
    public static class ImageFormatConvertModule
    {
        private static string targetFormat = "jpg";
        private static int jpgQuality = 85;
        private static bool deleteOriginal = false;
        private static bool overwriteIfExists = true;
        private static bool showLog = true;

        public static void DrawGUI(List<Texture2D> textures)
        {
            using (new EditorGUILayout.VerticalScope("box"))
            {
                EditorGUILayout.LabelField("Image Format Converter", EditorStyles.boldLabel);
                EditorGUILayout.HelpBox(
                    "Convert selected images between PNG and JPG formats in batch mode. ",
                    MessageType.Info);

                targetFormat = EditorGUILayout.Popup("Target Format",
                    GetFormatIndex(targetFormat), new[] { "JPG", "PNG" }) == 0 ? "jpg" : "png";

                if (targetFormat == "jpg")
                    jpgQuality = EditorGUILayout.IntSlider("JPG Quality", jpgQuality, 10, 100);

                deleteOriginal = EditorGUILayout.ToggleLeft("Delete Original After Conversion", deleteOriginal);
                overwriteIfExists = EditorGUILayout.ToggleLeft("Overwrite If File Exists", overwriteIfExists);
                showLog = EditorGUILayout.ToggleLeft("Show Log in Console", showLog);

                EditorGUILayout.Space(10);

                EditorGUI.BeginDisabledGroup(textures == null || textures.Count == 0);
                if (GUILayout.Button("Convert Selected Images", GUILayout.Height(30)))
                    ConvertImages(textures);
                EditorGUI.EndDisabledGroup();
            }
        }

        private static int GetFormatIndex(string fmt)
        {
            return fmt.ToLowerInvariant().Contains("png") ? 1 : 0;
        }

        private static void ConvertImages(List<Texture2D> textures)
        {
            if (textures == null || textures.Count == 0)
                return;

            int converted = 0, skipped = 0;

            foreach (var tex in textures)
            {
                if (tex == null) continue;

                string path = AssetDatabase.GetAssetPath(tex);
                if (string.IsNullOrEmpty(path) || !File.Exists(path))
                {
                    skipped++;
                    continue;
                }

                try
                {
                    byte[] srcData = File.ReadAllBytes(path);
                    Texture2D source = new Texture2D(2, 2, TextureFormat.RGBA32, false);
                    source.LoadImage(srcData);

                    string dir = Path.GetDirectoryName(path);
                    string name = Path.GetFileNameWithoutExtension(path);
                    string newPath = Path.Combine(dir, $"{name}.{targetFormat}");

                    if (File.Exists(newPath) && !overwriteIfExists)
                    {
                        if (showLog)
                            Debug.Log($"[ImageFormat] ⚠️ Skipped {name} (target file already exists)");
                        skipped++;
                        continue;
                    }

                    bool success = ImageIOUtility.SaveTexture(source, newPath, targetFormat, jpgQuality);
                    Object.DestroyImmediate(source);

                    if (success)
                    {
                        if (deleteOriginal && File.Exists(path))
                            File.Delete(path);

                        if (showLog)
                            Debug.Log($"[ImageFormat] ✅ {Path.GetFileName(path)} → {targetFormat.ToUpper()}");

                        converted++;
                    }
                    else skipped++;
                }
                catch (System.Exception ex)
                {
                    Debug.LogWarning($"[ImageFormat] ⚠️ Error converting {tex.name}: {ex.Message}");
                    skipped++;
                }
            }

            ImageIOUtility.RefreshAssetDatabase();
            EditorUtility.DisplayDialog("Format Conversion Complete",
                $"Converted: {converted}\nSkipped: {skipped}", "OK");
        }
    }
}
#endif
