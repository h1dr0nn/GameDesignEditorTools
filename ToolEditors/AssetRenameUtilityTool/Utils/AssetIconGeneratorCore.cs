#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using System.Collections.Generic;
using System.IO;
using System.Linq;

public static class AssetIconGeneratorCore

{
    public static void Generate(List<GameObject> prefabs, string outputFolder, string prefix, string suffix,
        int size, Color bg, Vector2 orbit, float padding, float fov,
        bool autoLit, float exposure, bool overwrite, bool importAsSprite)
    {
        if (prefabs == null || prefabs.Count == 0) return;

        var absOut = MakeAbsolute(outputFolder);
        Directory.CreateDirectory(absOut);

        int saved = 0;
        try
        {
            for (int i = 0; i < prefabs.Count; i++)
            {
                var prefab = prefabs[i];
                float p = (i + 1f) / prefabs.Count;
                if (EditorUtility.DisplayCancelableProgressBar("Generating Prefab Icons", prefab.name, p)) break;

                var tex = Render(prefab, size, bg, orbit, padding, fov, autoLit, exposure);
                if (tex == null) tex = AssetPreview.GetAssetPreview(prefab);
                if (tex == null) continue;

                var fileName = $"{prefix}{prefab.name}{suffix}.png";
                var relPath = $"{outputFolder.TrimEnd('/')}/{fileName}";
                var absPath = Path.Combine(absOut, fileName);

                if (!overwrite && File.Exists(absPath))
                {
                    var uniq = AssetDatabase.GenerateUniqueAssetPath(relPath);
                    absPath = MakeAbsolute(uniq);
                    relPath = uniq;
                }

                File.WriteAllBytes(absPath, tex.EncodeToPNG());
                Object.DestroyImmediate(tex);

                AssetDatabase.ImportAsset(relPath);
                var ti = (TextureImporter)AssetImporter.GetAtPath(relPath);
                if (ti != null)
                {
                    ti.alphaIsTransparency = true;
                    ti.mipmapEnabled = false;
                    ti.textureCompression = TextureImporterCompression.Uncompressed;
                    if (importAsSprite) ti.textureType = TextureImporterType.Sprite;
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
            EditorUtility.DisplayDialog("Prefab Icon Generator", $"Saved {saved}/{prefabs.Count} icons to:\n{outputFolder}", "OK");
        }
    }

    private static Texture2D Render(GameObject prefab, int size, Color bg, Vector2 orbit, float padding, float fov, bool autoLit, float exposure)
    {
        var pru = new PreviewRenderUtility();
        Texture2D tex = null;
        GameObject go = null;

        try
        {
            go = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            if (go == null) return null;
            pru.AddSingleGO(go);

            pru.cameraFieldOfView = fov;
            pru.camera.backgroundColor = bg;
            pru.camera.clearFlags = CameraClearFlags.SolidColor;

            var bounds = new Bounds(Vector3.zero, Vector3.one);
            foreach (var r in go.GetComponentsInChildren<Renderer>())
                bounds.Encapsulate(r.bounds);

            var center = bounds.center;
            var radius = bounds.extents.magnitude;
            var dir = Quaternion.Euler(orbit.x, orbit.y, 0f) * Vector3.forward;
            var dist = radius * padding / Mathf.Tan(fov * Mathf.Deg2Rad * 0.5f);
            var camPos = center - dir * dist;

            pru.camera.transform.position = camPos;
            pru.camera.transform.rotation = Quaternion.LookRotation(center - camPos, Vector3.up);

            var rect = new Rect(0, 0, size, size);
            pru.BeginPreview(rect, GUIStyle.none);
            pru.camera.Render();
            tex = pru.EndPreview() as Texture2D;

            if (tex == null)
            {
                RenderTexture.active = pru.camera.targetTexture;
                tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
                tex.ReadPixels(rect, 0, 0);
                tex.Apply();
            }

            return tex;
        }
        catch
        {
            return null;
        }
        finally
        {
            if (go != null) Object.DestroyImmediate(go);
            pru.Cleanup();
        }
    }

    public static string MakeAbsolute(string relative)
    {
        var root = Application.dataPath[..^"Assets".Length];
        if (string.IsNullOrEmpty(relative)) return root;
        if (relative.StartsWith("Assets")) return Path.GetFullPath(Path.Combine(root, relative));
        return relative;
    }

    public static string MakeRelativeToAssets(string absolute)
    {
        if (string.IsNullOrEmpty(absolute)) return null;
        absolute = Path.GetFullPath(absolute).Replace('\\', '/');
        var root = Application.dataPath[..^"Assets".Length].Replace('\\', '/');
        if (absolute.StartsWith(root))
        {
            var rel = absolute.Substring(root.Length);
            if (!rel.StartsWith("Assets")) rel = "Assets/" + rel.TrimStart('/');
            return rel.Replace('\\', '/');
        }
        return null;
    }
}
#endif
