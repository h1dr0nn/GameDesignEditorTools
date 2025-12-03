#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using System.Collections.Generic;
using System.IO;

namespace h1dr0n.EditorTools
{
    public static class AIFaceEnhanceModule
    {
        // Settings
        private static int esrganModelIndex = 0;
        private static readonly string[] esrganModels = new[] { "realesrgan-x4plus", "realesrnet-x4plus" };
        private static readonly string[] esrganModelNames = new[] { "x4plus (Sharper)", "RealESRNet (Softer)" };
        private static int esrganScale = 4;
        private static int esrganTileSize = 0;

        // Common settings
        private static bool preserveAlpha = true;
        private static bool resizeToOriginal = true;
        private static bool overwriteSource = false;
        private static string outputSuffix = "_FaceEnhanced";
        private static bool showLog = true;

        public static void DrawGUI(List<Texture2D> textures)
        {
            using (new EditorGUILayout.VerticalScope("box"))
            {
                EditorGUILayout.LabelField("AI Face Enhancement", EditorStyles.boldLabel);
                EditorGUILayout.HelpBox(
                    "Uses Real-ESRGAN to upscale and enhance portrait photos. Best for real photos with faces. " +
                    "NOT recommended for anime or cartoon images.",
                    MessageType.Info);

                if (!PathResolver.RealESRGANExists())
                {
                    EditorGUILayout.HelpBox("Real-ESRGAN executable not found!", MessageType.Error);
                    return;
                }

                EditorGUILayout.Space(5);

                // Settings
                EditorGUILayout.LabelField("Settings", EditorStyles.miniBoldLabel);
                esrganModelIndex = EditorGUILayout.Popup("Base Model", esrganModelIndex, esrganModelNames);
                esrganScale = EditorGUILayout.IntPopup("Upscale", esrganScale, 
                    new[] { "x2", "x4" }, new[] { 2, 4 });
                esrganTileSize = EditorGUILayout.IntPopup("Tile Size", esrganTileSize,
                    new[] { "No Tile", "256", "512", "1024" }, new[] { 0, 256, 512, 1024 });

                EditorGUILayout.Space(5);

                // Common settings
                EditorGUILayout.LabelField("Common Settings", EditorStyles.miniBoldLabel);
                preserveAlpha = EditorGUILayout.ToggleLeft("Preserve Alpha Channel", preserveAlpha);
                resizeToOriginal = EditorGUILayout.ToggleLeft("Resize to Original Size", resizeToOriginal);
                overwriteSource = EditorGUILayout.ToggleLeft("Overwrite Source Files", overwriteSource);
                
                if (!overwriteSource)
                    outputSuffix = EditorGUILayout.TextField("Output Suffix", outputSuffix);
                
                showLog = EditorGUILayout.ToggleLeft("Show Logs in Console", showLog);

                EditorGUILayout.Space(5);
                EditorGUILayout.HelpBox("Note: This module uses Real-ESRGAN for upscaling. Advanced face restoration (GFPGAN) is not available in the ncnn-vulkan version.", MessageType.Info);

                EditorGUILayout.Space(10);

                // Action button
                EditorGUI.BeginDisabledGroup(textures == null || textures.Count == 0);
                if (GUILayout.Button("Enhance Faces", GUILayout.Height(30)))
                    ProcessFaceEnhance(textures);
                EditorGUI.EndDisabledGroup();
            }
        }

        private static void ProcessFaceEnhance(List<Texture2D> textures)
        {
            int count = textures.Count;
            int done = 0, skipped = 0;

            try
            {
                for (int i = 0; i < count; i++)
                {
                    Texture2D tex = textures[i];
                    if (tex == null)
                    {
                        skipped++;
                        continue;
                    }

                    EditorUtility.DisplayProgressBar("AI Face Enhance", tex.name, (float)i / count);

                    bool ok = EnhanceSingle(tex);
                    if (ok) done++;
                    else skipped++;
                }
            }
            finally
            {
                EditorUtility.ClearProgressBar();
                AssetDatabase.Refresh();
            }

            EditorUtility.DisplayDialog("AI Face Enhancement Complete",
                $"Processed: {done}\nSkipped: {skipped}", "OK");
        }

        private static bool EnhanceSingle(Texture2D tex)
        {
            string srcPath = AssetDatabase.GetAssetPath(tex);
            if (!File.Exists(srcPath))
                return false;

            string ext = Path.GetExtension(srcPath).ToLowerInvariant();
            string dir = Path.GetDirectoryName(srcPath);
            string name = Path.GetFileNameWithoutExtension(srcPath);

            string tempInput = Path.Combine(Application.temporaryCachePath, name + "_in.png");
            WriteTexture(srcPath, tempInput, preserveAlpha);

            string tempOutput = Path.Combine(Application.temporaryCachePath, name + "_enhanced.png");

            // Process with Real-ESRGAN (Note: ncnn-vulkan doesn't support GFPGAN face enhancement)
            ProcessResult result = RealESRGANEngine.Process(
                tempInput, tempOutput,
                out string output, out string error,
                model: esrganModels[esrganModelIndex],
                scale: esrganScale,
                tileSize: esrganTileSize
            );

            if (result != ProcessResult.Success || !File.Exists(tempOutput))
            {
                if (showLog)
                {
                    Debug.LogError($"[AI Face Enhance] Failed: {tex.name}");
                    if (!string.IsNullOrEmpty(error))
                        Debug.LogError($"[AI Face Enhance] Error: {error}");
                    if (!string.IsNullOrEmpty(output))
                        Debug.Log($"[AI Face Enhance] Output: {output}");
                }
                return false;
            }

            byte[] enhancedData = File.ReadAllBytes(tempOutput);
            Texture2D enhancedTex = new Texture2D(2, 2);
            enhancedTex.LoadImage(enhancedData);

            Texture2D finalTex = enhancedTex;

            if (resizeToOriginal)
            {
                finalTex = ResizeTexture(enhancedTex, tex.width, tex.height);
                Object.DestroyImmediate(enhancedTex);
            }

            string savePath = overwriteSource
                ? srcPath
                : Path.Combine(dir, $"{name}{outputSuffix}{ext}");

            byte[] outputBytes;
            if (ext == ".jpg" || ext == ".jpeg")
                outputBytes = finalTex.EncodeToJPG(95);
            else
                outputBytes = finalTex.EncodeToPNG();

            File.WriteAllBytes(savePath, outputBytes);
            Object.DestroyImmediate(finalTex);

            // Clean up
            if (File.Exists(tempInput)) File.Delete(tempInput);
            if (File.Exists(tempOutput)) File.Delete(tempOutput);

            if (showLog)
                Debug.Log($"[AI Face Enhance] Done: {tex.name}");

            return true;
        }

        private static void WriteTexture(string srcPath, string dstPath, bool keepAlpha)
        {
            byte[] bytes = File.ReadAllBytes(srcPath);
            Texture2D tex = new Texture2D(2, 2);
            tex.LoadImage(bytes);

            if (!keepAlpha)
            {
                Texture2D rgb = new Texture2D(tex.width, tex.height, TextureFormat.RGB24, false);
                Color32[] px = tex.GetPixels32();
                for (int i = 0; i < px.Length; i++)
                    px[i].a = 255;
                rgb.SetPixels32(px);
                rgb.Apply();
                File.WriteAllBytes(dstPath, rgb.EncodeToPNG());
                Object.DestroyImmediate(rgb);
            }
            else
            {
                File.WriteAllBytes(dstPath, tex.EncodeToPNG());
            }

            Object.DestroyImmediate(tex);
        }

        private static Texture2D ResizeTexture(Texture2D source, int targetWidth, int targetHeight)
        {
            RenderTexture rt = RenderTexture.GetTemporary(targetWidth, targetHeight);
            RenderTexture.active = rt;
            Graphics.Blit(source, rt);

            Texture2D result = new Texture2D(targetWidth, targetHeight, TextureFormat.RGBA32, false);
            result.ReadPixels(new Rect(0, 0, targetWidth, targetHeight), 0, 0);
            result.Apply();

            RenderTexture.active = null;
            RenderTexture.ReleaseTemporary(rt);

            return result;
        }
    }
}
#endif
