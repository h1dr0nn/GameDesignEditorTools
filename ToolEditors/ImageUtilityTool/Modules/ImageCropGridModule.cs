#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using System.Collections.Generic;
using System.IO;

public static class ImageCropGridModule
{
    private static int gridSize = 5;
    private static string outputPrefix = "cell";
    private static bool overwriteSource = false;
    private static bool showLog = true;

    public static void DrawGUI(List<Texture2D> textures)
    {
        using (new EditorGUILayout.VerticalScope("box"))
        {
            EditorGUILayout.LabelField("Crop Image to Grid", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "Split each selected image into an N×N grid of equal cells and export each cell as a separate image file.",
                MessageType.Info);

            gridSize = EditorGUILayout.IntSlider("Grid Size (N×N)", gridSize, 2, 20);
            outputPrefix = EditorGUILayout.TextField("Output Prefix", outputPrefix);
            overwriteSource = EditorGUILayout.ToggleLeft("Overwrite Source Files", overwriteSource);
            showLog = EditorGUILayout.ToggleLeft("Show Log in Console", showLog);

            EditorGUILayout.Space(10);

            EditorGUI.BeginDisabledGroup(textures == null || textures.Count == 0);
            if (GUILayout.Button("Crop Selected Images", GUILayout.Height(30)))
                CropImages(textures);
            EditorGUI.EndDisabledGroup();
        }
    }

    private static void CropImages(List<Texture2D> textures)
    {
        if (textures == null || textures.Count == 0)
            return;

        int totalCrops = 0;
        int totalImages = 0;

        foreach (var tex in textures)
        {
            if (tex == null) continue;

            string path = AssetDatabase.GetAssetPath(tex);
            if (string.IsNullOrEmpty(path) || !File.Exists(path)) continue;

            string dir = Path.GetDirectoryName(path);
            string name = Path.GetFileNameWithoutExtension(path);
            string ext = Path.GetExtension(path).ToLowerInvariant();

            try
            {
                byte[] data = File.ReadAllBytes(path);
                Texture2D source = new Texture2D(2, 2, TextureFormat.RGBA32, false);
                source.LoadImage(data);

                int cellW = source.width / gridSize;
                int cellH = source.height / gridSize;

                int count = 0;
                for (int row = 0; row < gridSize; row++)
                {
                    for (int col = 0; col < gridSize; col++)
                    {
                        Texture2D cell = new Texture2D(cellW, cellH, TextureFormat.RGBA32, false);
                        cell.SetPixels(source.GetPixels(col * cellW, row * cellH, cellW, cellH));
                        cell.Apply();

                        string outputName = $"{outputPrefix}_{name}_{row}_{col}.png";
                        string outputPath = Path.Combine(dir, outputName);

                        ImageIOUtility.SaveTexture(cell, outputPath, "png");
                        Object.DestroyImmediate(cell);
                        count++;

                        if (showLog)
                            Debug.Log($"[ImageCrop] ✅ {outputName} ({cellW}×{cellH}) saved.");
                    }
                }

                if (!overwriteSource)
                    Debug.Log($"[ImageCrop] {name} split into {count} parts ({cellW}×{cellH} each).");

                Object.DestroyImmediate(source);
                totalCrops += count;
                totalImages++;
            }
            catch (System.Exception ex)
            {
                Debug.LogWarning($"[ImageCrop] ⚠️ Error cropping {tex.name}: {ex.Message}");
            }
        }

        ImageIOUtility.RefreshAssetDatabase();
        EditorUtility.DisplayDialog("Crop Complete",
            $"Processed: {totalImages} images\nGenerated: {totalCrops} cropped files", "OK");
    }
}
#endif
