#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using System.Collections.Generic;
using System.IO;

namespace h1dr0n.EditorTools
{
    public static class AIUpscaleModule
    {
        // Engine selection
        private enum UpscaleEngine { RealESRGAN, Waifu2x }
        private static UpscaleEngine currentEngine = UpscaleEngine.RealESRGAN;

        // Real-ESRGAN settings
        private static int esrganModelIndex = 0;
        private static readonly string[] esrganModels = new[] { "realesrgan-x4plus", "realesrgan-x4plus-anime", "realesrnet-x4plus" };
        private static readonly string[] esrganModelNames = new[] { "x4plus (General)", "x4plus-anime (Anime)", "RealESRNet (Natural)" };
        private static int esrganScale = 4;
        private static int esrganTileSize = 0;
        private static bool esrganFP32 = false;

        // Waifu2x settings
        private static int waifu2xModelIndex = 0;
        private static readonly string[] waifu2xModels = new[] { "cunet", "upconv_7_anime", "upconv_7_photo" };
        private static readonly string[] waifu2xModelNames = new[] { "CUNet (Best Quality)", "UpConv Anime", "UpConv Photo" };
        private static int waifu2xScale = 2;
        private static int waifu2xTileSize = 0;
        private static bool waifu2xTTA = false;

        // Common settings
        private static bool preserveAlpha = true;
        private static bool resizeToOriginal = true;
        private static bool overwriteSource = false;
        private static string outputSuffix = "_Upscaled";
        private static bool showLog = true;

        public static void DrawGUI(List<Texture2D> textures)
        {
            using (new EditorGUILayout.VerticalScope("box"))
            {
                EditorGUILayout.LabelField("AI Upscale", EditorStyles.boldLabel);
                EditorGUILayout.HelpBox("High-quality AI upscaling using Real-ESRGAN or Waifu2x.", MessageType.Info);

                // Engine Selection
                currentEngine = (UpscaleEngine)EditorGUILayout.EnumPopup("Engine", currentEngine);

                EditorGUILayout.Space(5);

                // Engine-specific settings
                if (currentEngine == UpscaleEngine.RealESRGAN)
                {
                    DrawRealESRGANSettings();
                }
                else
                {
                    DrawWaifu2xSettings();
                }

                EditorGUILayout.Space(5);

                // Common settings
                DrawCommonSettings();

                EditorGUILayout.Space(10);

                // Action button
                EditorGUI.BeginDisabledGroup(textures == null || textures.Count == 0);
                if (GUILayout.Button("Upscale Images", GUILayout.Height(30)))
                    ProcessUpscale(textures);
                EditorGUI.EndDisabledGroup();
            }
        }

        private static void DrawRealESRGANSettings()
        {
            if (!PathResolver.RealESRGANExists())
            {
                EditorGUILayout.HelpBox("Real-ESRGAN executable not found!", MessageType.Error);
                return;
            }

            EditorGUILayout.LabelField("Real-ESRGAN Settings", EditorStyles.miniBoldLabel);
            esrganModelIndex = EditorGUILayout.Popup("Model", esrganModelIndex, esrganModelNames);
            esrganScale = EditorGUILayout.IntPopup("Scale", esrganScale, 
                new[] { "x2", "x3", "x4" }, new[] { 2, 3, 4 });
            esrganTileSize = EditorGUILayout.IntPopup("Tile Size", esrganTileSize,
                new[] { "No Tile", "256", "512", "1024" }, new[] { 0, 256, 512, 1024 });
            esrganFP32 = EditorGUILayout.ToggleLeft("FP32 Precision (slower, more stable)", esrganFP32);
        }

        private static void DrawWaifu2xSettings()
        {
            if (!PathResolver.Waifu2xExists())
            {
                EditorGUILayout.HelpBox("Waifu2x executable not found!", MessageType.Error);
                return;
            }

            EditorGUILayout.LabelField("Waifu2x Settings", EditorStyles.miniBoldLabel);
            waifu2xModelIndex = EditorGUILayout.Popup("Model", waifu2xModelIndex, waifu2xModelNames);
            waifu2xScale = EditorGUILayout.IntPopup("Scale", waifu2xScale, 
                new[] { "x1 (denoise only)", "x2" }, new[] { 1, 2 });
            waifu2xTileSize = EditorGUILayout.IntPopup("Tile Size", waifu2xTileSize,
                new[] { "No Tile", "200", "400" }, new[] { 0, 200, 400 });
            waifu2xTTA = EditorGUILayout.ToggleLeft("TTA Mode (slower, better quality)", waifu2xTTA);
        }

        private static void DrawCommonSettings()
        {
            EditorGUILayout.LabelField("Common Settings", EditorStyles.miniBoldLabel);
            preserveAlpha = EditorGUILayout.ToggleLeft("Preserve Alpha Channel", preserveAlpha);
            resizeToOriginal = EditorGUILayout.ToggleLeft("Resize to Original Size", resizeToOriginal);
            overwriteSource = EditorGUILayout.ToggleLeft("Overwrite Source Files", overwriteSource);
            
            if (!overwriteSource)
                outputSuffix = EditorGUILayout.TextField("Output Suffix", outputSuffix);
            
            showLog = EditorGUILayout.ToggleLeft("Show Logs in Console", showLog);
        }

        private static void ProcessUpscale(List<Texture2D> textures)
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

                    EditorUtility.DisplayProgressBar("AI Upscale", tex.name, (float)i / count);

                    bool ok = UpscaleSingle(tex);
                    if (ok) done++;
                    else skipped++;
                }
            }
            finally
            {
                EditorUtility.ClearProgressBar();
                AssetDatabase.Refresh();
            }

            EditorUtility.DisplayDialog("AI Upscale Complete",
                $"Processed: {done}\nSkipped: {skipped}", "OK");
        }

        private static bool UpscaleSingle(Texture2D tex)
        {
            string srcPath = AssetDatabase.GetAssetPath(tex);
            if (!File.Exists(srcPath))
                return false;

            string ext = Path.GetExtension(srcPath).ToLowerInvariant();
            string dir = Path.GetDirectoryName(srcPath);
            string name = Path.GetFileNameWithoutExtension(srcPath);

            string tempInput = Path.Combine(Application.temporaryCachePath, name + "_in.png");
            WriteTexture(srcPath, tempInput, preserveAlpha);

            string tempOutput = Path.Combine(Application.temporaryCachePath, name + "_up.png");

            // Process with selected engine
            ProcessResult result;
            string output, error;

            if (currentEngine == UpscaleEngine.RealESRGAN)
            {
                result = RealESRGANEngine.Process(
                    tempInput, tempOutput,
                    out output, out error,
                    model: esrganModels[esrganModelIndex],
                    scale: esrganScale,
                    tileSize: esrganTileSize,
                    fp32: esrganFP32
                );
            }
            else
            {
                result = Waifu2xEngine.Process(
                    tempInput, tempOutput,
                    out output, out error,
                    model: waifu2xModels[waifu2xModelIndex],
                    scale: waifu2xScale,
                    noiseLevel: -1, // No denoise in upscale module
                    tileSize: waifu2xTileSize,
                    tta: waifu2xTTA
                );
            }

            if (result != ProcessResult.Success || !File.Exists(tempOutput))
            {
                if (showLog)
                {
                    Debug.LogError($"[AI Upscale] Failed: {tex.name}");
                    if (!string.IsNullOrEmpty(error))
                        Debug.LogError($"[AI Upscale] Error: {error}");
                    if (!string.IsNullOrEmpty(output))
                        Debug.Log($"[AI Upscale] Output: {output}");
                }
                return false;
            }

            byte[] upscaledData = File.ReadAllBytes(tempOutput);
            Texture2D upTex = new Texture2D(2, 2);
            upTex.LoadImage(upscaledData);

            Texture2D finalTex = upTex;

            if (resizeToOriginal)
            {
                finalTex = ResizeTexture(upTex, tex.width, tex.height);
                Object.DestroyImmediate(upTex);
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

            // Clean up temp files
            if (File.Exists(tempInput)) File.Delete(tempInput);
            if (File.Exists(tempOutput)) File.Delete(tempOutput);

            if (showLog)
                Debug.Log($"[AI Upscale] Done: {tex.name}");

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
