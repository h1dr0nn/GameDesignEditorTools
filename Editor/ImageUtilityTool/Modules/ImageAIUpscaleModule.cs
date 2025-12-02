#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using System.Collections.Generic;
using System.IO;

namespace h1dr0n.EditorTools
{
    public static class ImageAIUpscaleModule
    {
        private static int scale = 4;
        private static bool overwriteSource = false;
        private static bool showLog = true;
        private static string outputSuffix = "_Upscaled";
        private static bool resizeToOriginal = true;
        private static bool preserveAlpha = true;

        public static void DrawGUI(List<Texture2D> textures)
        {
            using (new EditorGUILayout.VerticalScope("box"))
            {
                EditorGUILayout.LabelField("AI Upscale (Real-ESRGAN)", EditorStyles.boldLabel);
                EditorGUILayout.HelpBox("High-quality AI Upscale using Real-ESRGAN.", MessageType.Info);

                if (!RealESRGANPath.Exists())
                {
                    EditorGUILayout.HelpBox("RealESRGAN executable not found in the Bundle folder.", MessageType.Error);
                    return;
                }

                scale = EditorGUILayout.IntPopup("Scale", scale,
                    new[] { "x2", "x3", "x4" },
                    new[] { 2, 3, 4 });

                preserveAlpha = EditorGUILayout.ToggleLeft("Preserve Alpha Channel", preserveAlpha);
                showLog = EditorGUILayout.ToggleLeft("Show Log in Console", showLog);
                overwriteSource = EditorGUILayout.ToggleLeft("Overwrite Source Files", overwriteSource);

                if (!overwriteSource)
                    outputSuffix = EditorGUILayout.TextField("Output Suffix", outputSuffix);

                resizeToOriginal = EditorGUILayout.ToggleLeft("Resize to Original Size", resizeToOriginal);

                EditorGUILayout.Space(10);

                EditorGUI.BeginDisabledGroup(textures == null || textures.Count == 0);
                if (GUILayout.Button("Upscale Images", GUILayout.Height(30)))
                    ProcessUpscale(textures);
                EditorGUI.EndDisabledGroup();
            }
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
                $"Processed: {done}\nSkipped: {skipped}",
                "OK");
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

            string exe = RealESRGANPath.GetBinaryPath();
            var psi = new System.Diagnostics.ProcessStartInfo
            {
                FileName = exe,
                Arguments = $"-i \"{tempInput}\" -o \"{tempOutput}\" -s {scale}",
                CreateNoWindow = true,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            };

            var p = System.Diagnostics.Process.Start(psi);
            string output = p.StandardOutput.ReadToEnd();
            string error = p.StandardError.ReadToEnd();
            p.WaitForExit();

            if (!File.Exists(tempOutput))
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

            if (showLog)
                Debug.Log($"[AI Upscale] Done {tex.name}");

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
