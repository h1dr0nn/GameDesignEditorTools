#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using System.Collections.Generic;
using System.IO;

public static class ImagePaddingModule
{
    private static int paddingPixels = 16;
    private static float paddingPercent = 0f;
    private static bool usePercent = false;
    private static bool transparentBackground = true;
    private static Color backgroundColor = Color.black;
    private static bool overwriteSource = false;
    private static bool showLog = true;
    private static string outputSuffix = "_padded";

    public static void DrawGUI(List<Texture2D> textures)
    {
        using (new EditorGUILayout.VerticalScope("box"))
        {
            EditorGUILayout.LabelField("Image Padding Settings", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "Add empty margins (padding) around selected images. ",
                MessageType.Info);

            usePercent = EditorGUILayout.ToggleLeft("Use Percent Padding", usePercent);
            if (usePercent)
                paddingPercent = EditorGUILayout.Slider("Padding (%)", paddingPercent, 0f, 50f);
            else
                paddingPixels = EditorGUILayout.IntSlider("Padding (px)", paddingPixels, 0, 512);

            transparentBackground = EditorGUILayout.ToggleLeft("Transparent Background", transparentBackground);
            if (!transparentBackground)
                backgroundColor = EditorGUILayout.ColorField("Background Color", backgroundColor);

            overwriteSource = EditorGUILayout.ToggleLeft("Overwrite Source Files", overwriteSource);
            if (!overwriteSource)
                outputSuffix = EditorGUILayout.TextField("Output Suffix", outputSuffix);

            showLog = EditorGUILayout.ToggleLeft("Show Log in Console", showLog);

            EditorGUILayout.Space(10);

            EditorGUI.BeginDisabledGroup(textures == null || textures.Count == 0);
            if (GUILayout.Button("Apply Padding", GUILayout.Height(30)))
                ApplyPadding(textures);
            EditorGUI.EndDisabledGroup();
        }
    }

    private static void ApplyPadding(List<Texture2D> textures)
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
                byte[] data = File.ReadAllBytes(path);
                Texture2D source = new Texture2D(2, 2, TextureFormat.RGBA32, false);
                source.LoadImage(data);

                int padX, padY;
                if (usePercent)
                {
                    padX = Mathf.RoundToInt(source.width * (paddingPercent / 100f));
                    padY = Mathf.RoundToInt(source.height * (paddingPercent / 100f));
                }
                else
                {
                    padX = padY = paddingPixels;
                }

                int newWidth = source.width + padX * 2;
                int newHeight = source.height + padY * 2;

                Texture2D padded = new Texture2D(newWidth, newHeight, TextureFormat.RGBA32, false);
                Color[] bg = new Color[newWidth * newHeight];
                Color fillColor = transparentBackground ? new Color(0, 0, 0, 0) : backgroundColor;
                for (int i = 0; i < bg.Length; i++) bg[i] = fillColor;
                padded.SetPixels(bg);

                padded.SetPixels(padX, padY, source.width, source.height, source.GetPixels());
                padded.Apply();

                string dir = Path.GetDirectoryName(path);
                string name = Path.GetFileNameWithoutExtension(path);
                string ext = Path.GetExtension(path).ToLowerInvariant();

                string savePath = overwriteSource
                    ? path
                    : Path.Combine(dir, $"{name}{outputSuffix}{ext}");

                ImageIOUtility.SaveTexture(padded, savePath, ext.Contains("png") ? "png" : "jpg");
                Object.DestroyImmediate(source);
                Object.DestroyImmediate(padded);

                if (showLog)
                    Debug.Log($"[ImagePadding] ✅ {Path.GetFileName(path)} padded to {newWidth}×{newHeight}");

                processed++;
            }
            catch (System.Exception ex)
            {
                Debug.LogWarning($"[ImagePadding] ⚠️ Error adding padding to {tex.name}: {ex.Message}");
                skipped++;
            }
        }

        ImageIOUtility.RefreshAssetDatabase();
        EditorUtility.DisplayDialog("Padding Complete",
            $"Processed: {processed}\nSkipped: {skipped}", "OK");
    }
}
#endif
