#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using System.Collections.Generic;
using System.IO;

namespace h1dr0n.EditorTools
{
    public static class AIDenoiseModule
    {
        // Engine selection
        private enum DenoiseEngine { Waifu2x, RealESRGAN }
        private static DenoiseEngine currentEngine = DenoiseEngine.Waifu2x;

        // Waifu2x settings (primary for denoise)
        private static int waifu2xModelIndex = 0;
        private static readonly string[] waifu2xModels = new[] { "cunet", "upconv_7_photo" };
        private static readonly string[] waifu2xModelNames = new[] { "CUNet", "UpConv Photo" };
        private static int waifu2xNoiseLevel = 1;
        private static int waifu2xScale = 1; // 1x = denoise only
        private static int waifu2xTileSize = 0;

        // Real-ESRGAN settings
        private static int esrganScale = 2;
        private static int esrganTileSize = 0;

        // Common settings
        private static bool preserveAlpha = true;
        private static bool overwriteSource = false;
        private static string outputSuffix = "_Denoised";
        private static bool showLog = true;

        public static void DrawGUI(List<Texture2D> textures)
        {
            using (new EditorGUILayout.VerticalScope("box"))
            {
                EditorGUILayout.LabelField("AI Denoise", EditorStyles.boldLabel);
                EditorGUILayout.HelpBox("AI-powered noise reduction. Waifu2x is recommended for best denoise results.", MessageType.Info);

                // Engine Selection
                currentEngine = (DenoiseEngine)EditorGUILayout.EnumPopup("Engine", currentEngine);

                EditorGUILayout.Space(5);

                // Engine-specific settings
                if (currentEngine == DenoiseEngine.Waifu2x)
                {
                    DrawWaifu2xSettings();
                }
                else
                {
                    DrawRealESRGANSettings();
                }

                EditorGUILayout.Space(5);

                // Common settings
                DrawCommonSettings();

                EditorGUILayout.Space(10);

                // Action button
                EditorGUI.BeginDisabledGroup(textures == null || textures.Count == 0);
                if (GUILayout.Button("Denoise Images", GUILayout.Height(30)))
                    ProcessDenoise(textures);
                EditorGUI.EndDisabledGroup();
            }
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
            waifu2xNoiseLevel = EditorGUILayout.IntSlider("Noise Level", waifu2xNoiseLevel, 0, 3);
            EditorGUILayout.HelpBox($"Level {waifu2xNoiseLevel}: " + GetNoiseLevelDescription(waifu2xNoiseLevel), MessageType.None);
            
            waifu2xScale = EditorGUILayout.IntPopup("Scale", waifu2xScale, 
                new[] { "x1 (denoise only)", "x2 (denoise + upscale)" }, new[] { 1, 2 });
            waifu2xTileSize = EditorGUILayout.IntPopup("Tile Size", waifu2xTileSize,
                new[] { "No Tile", "200", "400" }, new[] { 0, 200, 400 });
        }

        private static void DrawRealESRGANSettings()
        {
            if (!PathResolver.RealESRGANExists())
            {
                EditorGUILayout.HelpBox("Real-ESRGAN executable not found!", MessageType.Error);
                return;
            }

            EditorGUILayout.LabelField("Real-ESRGAN Settings", EditorStyles.miniBoldLabel);
            EditorGUILayout.HelpBox("Uses realesr-general-x4v3 denoise variant", MessageType.Info);
            esrganScale = EditorGUILayout.IntPopup("Scale", esrganScale, 
                new[] { "x2", "x4" }, new[] { 2, 4 });
            esrganTileSize = EditorGUILayout.IntPopup("Tile Size", esrganTileSize,
                new[] { "No Tile", "256", "512" }, new[] { 0, 256, 512 });
        }

        private static void DrawCommonSettings()
        {
            EditorGUILayout.LabelField("Common Settings", EditorStyles.miniBoldLabel);
            preserveAlpha = EditorGUILayout.ToggleLeft("Preserve Alpha Channel", preserveAlpha);
            overwriteSource = EditorGUILayout.ToggleLeft("Overwrite Source Files", overwriteSource);
            
            if (!overwriteSource)
                outputSuffix = EditorGUILayout.TextField("Output Suffix", outputSuffix);
            
            showLog = EditorGUILayout.ToggleLeft("Show Logs in Console", showLog);
        }

        private static string GetNoiseLevelDescription(int level)
        {
            return level switch
            {
                0 => "Light noise reduction",
                1 => "Medium noise reduction (recommended)",
                2 => "Strong noise reduction",
                3 => "Very strong noise reduction",
                _=> ""
            };
        }

        private static void ProcessDenoise(List<Texture2D> textures)
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

                    EditorUtility.DisplayProgressBar("AI Denoise", tex.name, (float)i / count);

                    bool ok = DenoiseSingle(tex);
                    if (ok) done++;
                    else skipped++;
                }
            }
            finally
            {
                EditorUtility.ClearProgressBar();
                AssetDatabase.Refresh();
            }

            EditorUtility.DisplayDialog("AI Denoise Complete",
                $"Processed: {done}\nSkipped: {skipped}", "OK");
        }

        private static bool DenoiseSingle(Texture2D tex)
        {
            string srcPath = AssetDatabase.GetAssetPath(tex);
            if (!File.Exists(srcPath))
                return false;

            string ext = Path.GetExtension(srcPath).ToLowerInvariant();
            string dir = Path.GetDirectoryName(srcPath);
            string name = Path.GetFileNameWithoutExtension(srcPath);

            string tempInput = Path.Combine(Application.temporaryCachePath, name + "_in.png");
            AIUpscaleModule_WriteTexture(srcPath, tempInput, preserveAlpha);

            string tempOutput = Path.Combine(Application.temporaryCachePath, name + "_denoised.png");

            // Process with selected engine
            ProcessResult result;
            string output, error;

            if (currentEngine == DenoiseEngine.Waifu2x)
            {
                result = Waifu2xEngine.Process(
                    tempInput, tempOutput,
                    out output, out error,
                    model: waifu2xModels[waifu2xModelIndex],
                    scale: waifu2xScale,
                    noiseLevel: waifu2xNoiseLevel,
                    tileSize: waifu2xTileSize
                );
            }
            else
            {
                // Real-ESRGAN denoise variant
                result = RealESRGANEngine.Process(
                    tempInput, tempOutput,
                    out output, out error,
                    model: "realesr-general-x4v3:denoise",
                    scale: esrganScale,
                    tileSize: esrganTileSize
                );
            }

            if (result != ProcessResult.Success || !File.Exists(tempOutput))
            {
                if (showLog)
                {
                    Debug.LogError($"[AI Denoise] Failed: {tex.name}");
                    if (!string.IsNullOrEmpty(error))
                        Debug.LogError($"[AI Denoise] Error: {error}");
                }
                return false;
            }

            byte[] denoisedData = File.ReadAllBytes(tempOutput);
            Texture2D denoisedTex = new Texture2D(2, 2);
            denoisedTex.LoadImage(denoisedData);

            string savePath = overwriteSource
                ? srcPath
                : Path.Combine(dir, $"{name}{outputSuffix}{ext}");

            byte[] outputBytes;
            if (ext == ".jpg" || ext == ".jpeg")
                outputBytes = denoisedTex.EncodeToJPG(95);
            else
                outputBytes = denoisedTex.EncodeToPNG();

            File.WriteAllBytes(savePath, outputBytes);
            Object.DestroyImmediate(denoisedTex);

            // Clean up
            if (File.Exists(tempInput)) File.Delete(tempInput);
            if (File.Exists(tempOutput)) File.Delete(tempOutput);

            if (showLog)
                Debug.Log($"[AI Denoise] Done: {tex.name}");

            return true;
        }

        // Shared texture writing utility
        private static void AIUpscaleModule_WriteTexture(string srcPath, string dstPath, bool keepAlpha)
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
    }
}
#endif
