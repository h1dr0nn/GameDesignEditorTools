#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using System.Collections.Generic;
using System.IO;

public static class ImageFileCompressModule
{
    private static int minKB = 80;
    private static int maxKB = 200;
    private static string outputFormat = "jpg";
    private static bool overwriteSource = true;
    private static bool showLog = true;

    public static void DrawGUI(List<Texture2D> textures)
    {
        using (new EditorGUILayout.VerticalScope("box"))
        {
            EditorGUILayout.LabelField("Image File Compression", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "Reduce the file size of selected images by compressing them into a target size range (min–max KB). ",
                MessageType.Info);

            minKB = EditorGUILayout.IntSlider("Min Size (KB)", minKB, 10, 1000);
            maxKB = EditorGUILayout.IntSlider("Max Size (KB)", maxKB, minKB, 2000);

            outputFormat = EditorGUILayout.Popup("Output Format",
                GetFormatIndex(outputFormat), new[] { "JPG", "PNG" }) == 0 ? "jpg" : "png";

            overwriteSource = EditorGUILayout.ToggleLeft("Overwrite Source Files", overwriteSource);
            showLog = EditorGUILayout.ToggleLeft("Show Log in Console", showLog);

            EditorGUILayout.Space(10);

            EditorGUI.BeginDisabledGroup(textures == null || textures.Count == 0);
            if (GUILayout.Button("Apply Compression", GUILayout.Height(30)))
                ProcessImages(textures);
            EditorGUI.EndDisabledGroup();
        }
    }

    private static int GetFormatIndex(string fmt)
    {
        return fmt.ToLowerInvariant().Contains("png") ? 1 : 0;
    }

    private static void ProcessImages(List<Texture2D> textures)
    {
        if (textures == null || textures.Count == 0) return;

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
                var bytes = File.ReadAllBytes(path);
                long originalKB = bytes.Length / 1024;
                Texture2D texCopy = new Texture2D(2, 2);
                texCopy.LoadImage(bytes);

                var (finalKB, q) = ImageCompressorCore.CompressToTargetSize(texCopy, path, minKB, maxKB, outputFormat);
                Object.DestroyImmediate(texCopy);

                if (showLog)
                    Debug.Log($"[ImageCompress] ✅ {Path.GetFileName(path)} → {finalKB:0.0} KB (Q={q}) [Before: {originalKB} KB]");

                processed++;
            }
            catch (System.Exception ex)
            {
                Debug.LogWarning($"[ImageCompress] ⚠️ {Path.GetFileName(path)} skipped — error: {ex.Message}");
                skipped++;
            }
        }

        ImageIOUtility.RefreshAssetDatabase();
        EditorUtility.DisplayDialog("Image Compression Complete",
            $"Total: {textures.Count}\nCompressed: {processed}\nSkipped: {skipped}", "OK");
    }
}
#endif
