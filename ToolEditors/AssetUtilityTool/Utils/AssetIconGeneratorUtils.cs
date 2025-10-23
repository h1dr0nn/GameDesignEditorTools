#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using System.IO;

public static class AssetIconGeneratorUtils
{
    public static Bounds CalcBounds(GameObject go)
    {
        var rends = go.GetComponentsInChildren<Renderer>(true);
        var has = false;
        var b = new Bounds(Vector3.zero, Vector3.zero);
        foreach (var r in rends)
        {
            if (!has) { b = r.bounds; has = true; }
            else b.Encapsulate(r.bounds);
        }
        if (!has)
        {
            var mfs = go.GetComponentsInChildren<MeshFilter>(true);
            foreach (var mf in mfs)
            {
                if (mf.sharedMesh == null) continue;
                var rb = mf.sharedMesh.bounds;
                var wb = TransformBounds(mf.transform.localToWorldMatrix, rb);
                if (!has) { b = wb; has = true; }
                else b.Encapsulate(wb);
            }
        }
        if (!has) b = new Bounds(go.transform.position, Vector3.one);
        return b;
    }

    public static Bounds TransformBounds(Matrix4x4 m, Bounds b)
    {
        var c = m.MultiplyPoint3x4(b.center);
        var ext = b.extents;
        var ax = m.MultiplyVector(new Vector3(ext.x, 0, 0));
        var ay = m.MultiplyVector(new Vector3(0, ext.y, 0));
        var az = m.MultiplyVector(new Vector3(0, 0, ext.z));
        ext.x = Mathf.Abs(ax.x) + Mathf.Abs(ay.x) + Mathf.Abs(az.x);
        ext.y = Mathf.Abs(ax.y) + Mathf.Abs(ay.y) + Mathf.Abs(az.y);
        ext.z = Mathf.Abs(ax.z) + Mathf.Abs(ay.z) + Mathf.Abs(az.z);
        return new Bounds(c, ext * 2f);
    }

    public static Texture2D DuplicateTexture(Texture2D src)
    {
        if (src == null) return null;
        var rt = RenderTexture.GetTemporary(src.width, src.height, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
        Graphics.Blit(src, rt);
        var prev = RenderTexture.active;
        RenderTexture.active = rt;
        var tex = new Texture2D(src.width, src.height, TextureFormat.RGBA32, false, false);
        tex.ReadPixels(new Rect(0, 0, src.width, src.height), 0, 0);
        tex.Apply();
        RenderTexture.active = prev;
        RenderTexture.ReleaseTemporary(rt);
        return tex;
    }

    public static string MakeAbsolute(string assetsRelative)
    {
        var root = Application.dataPath.Substring(0, Application.dataPath.Length - "Assets".Length);
        if (string.IsNullOrEmpty(assetsRelative)) return root;
        if (assetsRelative.StartsWith("Assets")) return Path.GetFullPath(Path.Combine(root, assetsRelative));
        return assetsRelative;
    }

    public static string MakeRelativeToAssets(string absolute)
    {
        if (string.IsNullOrEmpty(absolute)) return null;
        absolute = Path.GetFullPath(absolute).Replace('\\', '/');
        var root = Application.dataPath.Substring(0, Application.dataPath.Length - "Assets".Length).Replace('\\', '/');
        if (absolute.StartsWith(root))
        {
            var rel = absolute.Substring(root.Length);
            if (!rel.StartsWith("Assets")) rel = "Assets" + (rel.StartsWith("/") ? "" : "/") + rel;
            return rel.Replace('\\', '/');
        }
        return null;
    }
}
#endif
