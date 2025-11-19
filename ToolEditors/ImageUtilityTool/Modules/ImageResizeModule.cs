#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using System.Collections.Generic;
using System.IO;

namespace h1dr0n.EditorTools
{
    public static class ImageResizeModule
    {
        private static int targetWidth = 512;
        private static int targetHeight = 512;
        private static bool keepAspect = true;
        private static bool usePercentScale = false;
        private static float percentScale = 50f;
        private static bool overwriteSource = false;
        private static bool showLog = true;
        private static string outputSuffix = "_resized";

        public static void DrawGUI(List<Texture2D> textures)
        {
            using (new EditorGUILayout.VerticalScope("box"))
            {
                EditorGUILayout.LabelField("Image Resize Settings", EditorStyles.boldLabel);
                EditorGUILayout.HelpBox("Resize selected images by a percentage scale or to fixed dimensions.", MessageType.Info);

                usePercentScale = EditorGUILayout.ToggleLeft("Use Percent Scale", usePercentScale);
                if (usePercentScale)
                {
                    percentScale = EditorGUILayout.Slider("Scale (%)", percentScale, 5f, 200f);
                }
                else
                {
                    targetWidth = EditorGUILayout.IntField("Target Width", targetWidth);
                    targetHeight = EditorGUILayout.IntField("Target Height", targetHeight);
                    keepAspect = EditorGUILayout.ToggleLeft("Keep Aspect Ratio", keepAspect);
                }

                overwriteSource = EditorGUILayout.ToggleLeft("Overwrite Source Files", overwriteSource);
                if (!overwriteSource)
                    outputSuffix = EditorGUILayout.TextField("Output Suffix", outputSuffix);

                showLog = EditorGUILayout.ToggleLeft("Show Log in Console", showLog);

                EditorGUILayout.Space(10);

                EditorGUI.BeginDisabledGroup(textures == null || textures.Count == 0);
                if (GUILayout.Button("Resize Selected Images", GUILayout.Height(30)))
                    ResizeImages(textures);
                EditorGUI.EndDisabledGroup();
            }
        }

        private static void ResizeImages(List<Texture2D> textures)
        {
            if (textures == null || textures.Count == 0)
                return;

            int processed = 0, skipped = 0;

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
                    byte[] data = File.ReadAllBytes(path);
                    Texture2D source = new Texture2D(2, 2, TextureFormat.RGBA32, false);
                    source.LoadImage(data);

                    int newW, newH;

                    if (usePercentScale)
                    {
                        float scale = percentScale / 100f;
                        newW = Mathf.RoundToInt(source.width * scale);
                        newH = Mathf.RoundToInt(source.height * scale);
                    }
                    else
                    {
                        if (keepAspect)
                        {
                            Vector2Int newSize = ImageMathUtility.GetScaledSize(
                                new Vector2Int(source.width, source.height),
                                targetWidth, targetHeight, true);
                            newW = newSize.x;
                            newH = newSize.y;
                        }
                        else
                        {
                            newW = targetWidth;
                            newH = targetHeight;
                        }
                    }

                    Texture2D resized = ImageMathUtility.Resize(source, newW, newH);
                    string dir = Path.GetDirectoryName(path);
                    string name = Path.GetFileNameWithoutExtension(path);
                    string ext = Path.GetExtension(path).ToLowerInvariant();

                    string savePath = overwriteSource
                        ? path
                        : Path.Combine(dir, $"{name}{outputSuffix}{ext}");

                    ImageIOUtility.SaveTexture(resized, savePath, ext.Contains("png") ? "png" : "jpg");
                    Object.DestroyImmediate(resized);
                    Object.DestroyImmediate(source);

                    if (showLog)
                        Debug.Log($"[ImageResize] ✅ {Path.GetFileName(path)} resized to {newW}×{newH}");

                    processed++;
                }
                catch (System.Exception ex)
                {
                    Debug.LogWarning($"[ImageResize] ⚠️ Error resizing {tex.name}: {ex.Message}");
                    skipped++;
                }
            }

            ImageIOUtility.RefreshAssetDatabase();
            EditorUtility.DisplayDialog("Resize Complete",
                $"Processed: {processed}\nSkipped: {skipped}", "OK");
        }
    }
}
#endif
