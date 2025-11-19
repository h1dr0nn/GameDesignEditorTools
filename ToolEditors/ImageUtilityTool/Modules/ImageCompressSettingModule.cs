#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using System.IO;
using System.Collections.Generic;

namespace h1dr0n.EditorTools
{
    public static class ImageCompressSettingModule
    {
        private static TextureImporterCompression compressionSetting = TextureImporterCompression.Compressed;
        private static TextureImporterFormat androidFormat = TextureImporterFormat.ASTC_6x6;
        private static TextureImporterFormat iosFormat = TextureImporterFormat.ASTC_6x6;
        private static TextureImporterFormat standaloneFormat = TextureImporterFormat.DXT5;
        private static int maxTextureSize = 1024;
        private static bool applyToAndroid = true;
        private static bool applyToiOS = true;
        private static bool applyToStandalone = true;

        public static void DrawGUI(List<Texture2D> textures)
        {
            using (new EditorGUILayout.VerticalScope("box"))
            {
                EditorGUILayout.LabelField("Unity Importer Compression", EditorStyles.boldLabel);
                EditorGUILayout.HelpBox(
                    "Modify the TextureImporter compression settings (ASTC/DXT/ETC) for selected textures across multiple platforms.",
                    MessageType.Info);

                compressionSetting = (TextureImporterCompression)EditorGUILayout.EnumPopup("Compression", compressionSetting);
                maxTextureSize = EditorGUILayout.IntPopup("Max Texture Size", maxTextureSize,
                    new[] { "256", "512", "1024", "2048", "4096" },
                    new[] { 256, 512, 1024, 2048, 4096 });

                EditorGUILayout.Space(6);
                EditorGUILayout.LabelField("Platform Overrides", EditorStyles.boldLabel);

                if (applyToAndroid = EditorGUILayout.ToggleLeft("Android", applyToAndroid))
                    androidFormat = (TextureImporterFormat)EditorGUILayout.EnumPopup("  Android Format", androidFormat);

                if (applyToiOS = EditorGUILayout.ToggleLeft("iOS", applyToiOS))
                    iosFormat = (TextureImporterFormat)EditorGUILayout.EnumPopup("  iOS Format", iosFormat);

                if (applyToStandalone = EditorGUILayout.ToggleLeft("Standalone", applyToStandalone))
                    standaloneFormat = (TextureImporterFormat)EditorGUILayout.EnumPopup("  Standalone Format", standaloneFormat);

                EditorGUILayout.Space(10);

                EditorGUI.BeginDisabledGroup(textures == null || textures.Count == 0);
                if (GUILayout.Button("Apply Import Compression", GUILayout.Height(30)))
                    ProcessTextures(textures);
                EditorGUI.EndDisabledGroup();
            }
        }

        private static void ProcessTextures(List<Texture2D> textures)
        {
            if (textures == null || textures.Count == 0) return;
            int done = 0, skipped = 0;

            foreach (var tex in textures)
            {
                if (tex == null) continue;
                string path = AssetDatabase.GetAssetPath(tex);
                if (string.IsNullOrEmpty(path)) continue;

                try
                {
                    var importer = AssetImporter.GetAtPath(path) as TextureImporter;
                    if (importer == null) { skipped++; continue; }

                    importer.textureCompression = compressionSetting;
                    importer.maxTextureSize = maxTextureSize;

                    if (applyToAndroid)
                        importer.SetPlatformTextureSettings(new TextureImporterPlatformSettings
                        {
                            name = "Android",
                            overridden = true,
                            format = androidFormat,
                            maxTextureSize = maxTextureSize
                        });

                    if (applyToiOS)
                        importer.SetPlatformTextureSettings(new TextureImporterPlatformSettings
                        {
                            name = "iPhone",
                            overridden = true,
                            format = iosFormat,
                            maxTextureSize = maxTextureSize
                        });

                    if (applyToStandalone)
                        importer.SetPlatformTextureSettings(new TextureImporterPlatformSettings
                        {
                            name = "Standalone",
                            overridden = true,
                            format = standaloneFormat,
                            maxTextureSize = maxTextureSize
                        });

                    importer.SaveAndReimport();
                    done++;
                }
                catch (System.Exception ex)
                {
                    Debug.LogWarning($"[ImageImporterCompress] ⚠️ {tex.name} error: {ex.Message}");
                    skipped++;
                }
            }

            EditorUtility.DisplayDialog("Unity Compression Applied",
                $"Processed: {textures.Count}\nUpdated: {done}\nSkipped: {skipped}", "OK");

            AssetDatabase.Refresh();
        }
    }
}
#endif
