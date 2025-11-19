#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace h1dr0n.EditorTools
{
    public static class AssetIconGeneratorCore
    {
        public static void Generate(
            List<GameObject> prefabs,
            string outputFolder,
            string prefix,
            string suffix,
            int size,
            Color background,
            Vector2 orbit,
            float padding,
            float fov,
            bool autoLit,
            float exposure,
            bool overwrite,
            bool importAsSprite)
        {
            if (prefabs == null || prefabs.Count == 0) return;

            var absOut = AssetIconGeneratorUtils.MakeAbsolute(outputFolder);
            Directory.CreateDirectory(absOut);

            int saved = 0;

            try
            {
                for (int i = 0; i < prefabs.Count; i++)
                {
                    var prefab = prefabs[i];
                    float progress = (i + 1f) / prefabs.Count;
                    if (EditorUtility.DisplayCancelableProgressBar("Generating Prefab Icons", prefab.name, progress))
                        break;

                    var tex = AssetIconRenderer.Render(prefab, size, background, orbit, padding, fov, autoLit, exposure);
                    if (tex == null)
                        tex = AssetIconRenderer.FallbackAssetPreview(prefab, size);
                    if (tex == null)
                        continue;

                    var fileName = $"{prefix}{prefab.name}{suffix}.png";
                    var relPath = $"{outputFolder.TrimEnd('/')}/{fileName}";
                    var absPath = Path.Combine(absOut, fileName);

                    if (!overwrite && File.Exists(absPath))
                    {
                        var uniq = AssetDatabase.GenerateUniqueAssetPath(relPath);
                        absPath = AssetIconGeneratorUtils.MakeAbsolute(uniq);
                        relPath = uniq;
                    }

                    File.WriteAllBytes(absPath, tex.EncodeToPNG());
                    Object.DestroyImmediate(tex);

                    AssetDatabase.ImportAsset(relPath);
                    if (AssetImporter.GetAtPath(relPath) is TextureImporter ti)
                    {
                        ti.alphaIsTransparency = true;
                        ti.mipmapEnabled = false;
                        ti.textureCompression = TextureImporterCompression.Uncompressed;
                        if (importAsSprite)
                            ti.textureType = TextureImporterType.Sprite;
                        ti.SaveAndReimport();
                    }

                    Debug.Log($"[IconGen] Saved icon: {relPath}");
                    saved++;
                }
            }
            finally
            {
                EditorUtility.ClearProgressBar();
                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();
                EditorUtility.DisplayDialog(
                    "Prefab Icon Generator",
                    $"Saved {saved}/{prefabs.Count} icons to:\n{outputFolder}",
                    "OK");
            }
        }
    }
}
#endif
