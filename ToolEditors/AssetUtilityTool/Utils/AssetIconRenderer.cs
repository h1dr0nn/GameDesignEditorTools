#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using System;
using Object = UnityEngine.Object;

namespace h1dr0n.EditorTools
{
    public static class AssetIconRenderer
    {
        public static float framingTweak = 0.98f;

        public static Texture2D Render(GameObject prefab, int s, Color bg, Vector2 orbit, float pad, float fov, bool autoLit, float exposureMul)
        {
            var pru = new PreviewRenderUtility();
            GameObject go = null;
            Texture2D texBlack = null, texWhite = null, result = null;

            Texture2D SRPPreview(bool whiteBg, PreviewRenderUtility p, GameObject target)
            {
                var rect = new Rect(0, 0, s, s);
                p.cameraFieldOfView = fov;
                p.camera.nearClipPlane = 0.01f;
                p.camera.farClipPlane = 10000f;
                p.camera.clearFlags = CameraClearFlags.SolidColor;
                p.camera.backgroundColor = whiteBg ? Color.white : Color.black;

                try
                {
                    var urpType = Type.GetType("UnityEngine.Rendering.Universal.UniversalAdditionalCameraData, Unity.RenderPipelines.Universal.Runtime");
                    if (urpType != null)
                    {
                        var urp = p.camera.gameObject.GetComponent(urpType) ?? p.camera.gameObject.AddComponent(urpType);
                        urpType.GetProperty("renderType")?.SetValue(urp, 0, null);
                        urpType.GetProperty("clearFlags")?.SetValue(urp, 2, null);
                    }
                }
                catch { }

                var bounds = AssetIconGeneratorUtils.CalcBounds(target);
                var center = bounds.center;
                var radius = Mathf.Max(bounds.extents.x, Mathf.Max(bounds.extents.y, bounds.extents.z));
                if (radius <= 0f) radius = 1f;
                var rot = Quaternion.Euler(orbit.x, orbit.y, 0f);
                var dir = rot * Vector3.forward;
                var dist = radius * pad / Mathf.Tan(fov * Mathf.Deg2Rad * 0.5f) * framingTweak;
                var camPos = center - dir * dist;

                p.camera.transform.position = camPos;
                p.camera.transform.rotation = Quaternion.LookRotation(center - camPos, Vector3.up);
                p.camera.cullingMask = ~0;

                p.BeginPreview(rect, GUIStyle.none);
                p.camera.Render();
                var rt = p.EndPreview() as RenderTexture;
                if (rt == null) return null;

                var prev = RenderTexture.active;
                RenderTexture.active = rt;
                var outTex = new Texture2D(s, s, TextureFormat.RGBA32, false, false);
                outTex.ReadPixels(rect, 0, 0, false);
                outTex.Apply(false);
                RenderTexture.active = prev;
                return outTex;
            }

            Texture2D ManualUnlit(bool whiteBg, PreviewRenderUtility p, GameObject target)
            {
                var bounds = AssetIconGeneratorUtils.CalcBounds(target);
                var center = bounds.center;
                var radius = Mathf.Max(bounds.extents.x, Mathf.Max(bounds.extents.y, bounds.extents.z));
                if (radius <= 0f) radius = 1f;

                p.cameraFieldOfView = fov;
                var rot = Quaternion.Euler(orbit.x, orbit.y, 0f);
                var dir = rot * Vector3.forward;
                var dist = radius * pad / Mathf.Tan(fov * Mathf.Deg2Rad * 0.5f) * framingTweak;
                var camPos = center - dir * dist;

                var cam = p.camera;
                cam.transform.position = camPos;
                cam.transform.rotation = Quaternion.LookRotation(center - camPos, Vector3.up);
                cam.aspect = 1f;
                cam.nearClipPlane = 0.01f;
                cam.farClipPlane = 10000f;

                var proj = Matrix4x4.Perspective(fov, 1f, cam.nearClipPlane, cam.farClipPlane);
                var view = cam.worldToCameraMatrix;

                var rt = new RenderTexture(s, s, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB)
                { antiAliasing = 8, useMipMap = false, autoGenerateMips = false };
                rt.Create();

                var prevRT = RenderTexture.active;
                RenderTexture.active = rt;
                GL.Viewport(new Rect(0, 0, s, s));
                GL.Clear(true, true, whiteBg ? Color.white : Color.black);

                GL.PushMatrix();
                GL.LoadProjectionMatrix(proj);
                GL.modelview = view;

                var unlit = Shader.Find("Universal Render Pipeline/Unlit") ??
                            Shader.Find("Unlit/Texture") ??
                            Shader.Find("UI/Default");
                if (unlit == null)
                {
                    RenderTexture.active = prevRT;
                    rt.Release();
                    Object.DestroyImmediate(rt);
                    return null;
                }
                var mat = new Material(unlit) { hideFlags = HideFlags.HideAndDontSave };
                var bakeMesh = new Mesh();

                Texture TryMap(Material m)
                {
                    if (m == null) return null;
                    if (m.HasProperty("_BaseMap")) return m.GetTexture("_BaseMap");
                    if (m.HasProperty("_MainTex")) return m.GetTexture("_MainTex");
                    return null;
                }
                Color TryCol(Material m)
                {
                    if (m == null) return Color.white;
                    if (m.HasProperty("_BaseColor")) return m.GetColor("_BaseColor");
                    if (m.HasProperty("_Color")) return m.GetColor("_Color");
                    return Color.white;
                }

                foreach (var mr in target.GetComponentsInChildren<MeshRenderer>(true))
                {
                    var mf = mr.GetComponent<MeshFilter>();
                    if (mf == null || mf.sharedMesh == null) continue;
                    var mesh = mf.sharedMesh;
                    var mats = mr.sharedMaterials;
                    int sub = Mathf.Min(mesh.subMeshCount, mats.Length);
                    for (int i = 0; i < sub; i++)
                    {
                        var src = mats[i];
                        var col = TryCol(src);
                        var tex = TryMap(src);

                        if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", col);
                        if (mat.HasProperty("_Color")) mat.SetColor("_Color", col);
                        if (mat.HasProperty("_BaseMap")) mat.SetTexture("_BaseMap", tex);
                        if (mat.HasProperty("_MainTex")) mat.SetTexture("_MainTex", tex);

                        mat.SetPass(0);
                        Graphics.DrawMeshNow(mesh, mr.localToWorldMatrix, i);
                    }
                }

                foreach (var smr in target.GetComponentsInChildren<SkinnedMeshRenderer>(true))
                {
                    if (smr.sharedMesh == null) continue;
                    smr.BakeMesh(bakeMesh);
                    var mats = smr.sharedMaterials;
                    int sub = Mathf.Min(bakeMesh.subMeshCount, mats.Length);
                    for (int i = 0; i < sub; i++)
                    {
                        var src = mats[i];
                        var col = TryCol(src);
                        var tex = TryMap(src);

                        if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", col);
                        if (mat.HasProperty("_Color")) mat.SetColor("_Color", col);
                        if (mat.HasProperty("_BaseMap")) mat.SetTexture("_BaseMap", tex);
                        if (mat.HasProperty("_MainTex")) mat.SetTexture("_MainTex", tex);

                        mat.SetPass(0);
                        Graphics.DrawMeshNow(bakeMesh, smr.localToWorldMatrix, i);
                    }
                }

                Object.DestroyImmediate(bakeMesh);
                Object.DestroyImmediate(mat);
                GL.PopMatrix();

                var outTex = new Texture2D(s, s, TextureFormat.RGBA32, false, false);
                outTex.ReadPixels(new Rect(0, 0, s, s), 0, 0, false);
                outTex.Apply(false);

                RenderTexture.active = prevRT;
                rt.Release();
                Object.DestroyImmediate(rt);
                return outTex;
            }

            void Reconstruct(Texture2D tBlack, Texture2D tWhite, Texture2D dest)
            {
                var cB = tBlack.GetPixels32();
                var cW = tWhite.GetPixels32();
                var cO = new Color32[cB.Length];

                for (int i = 0; i < cB.Length; i++)
                {
                    float rb = cB[i].r / 255f, gb = cB[i].g / 255f, bb = cB[i].b / 255f;
                    float rw = cW[i].r / 255f, gw = cW[i].g / 255f, bw = cW[i].b / 255f;

                    float aR = 1f - Mathf.Clamp01(rw - rb);
                    float aG = 1f - Mathf.Clamp01(gw - gb);
                    float aB = 1f - Mathf.Clamp01(bw - bb);
                    float A = Mathf.Clamp01((aR + aG + aB) / 3f);

                    float R = (A > 1e-6f) ? Mathf.Clamp01(rb / A) : 0f;
                    float G = (A > 1e-6f) ? Mathf.Clamp01(gb / A) : 0f;
                    float B = (A > 1e-6f) ? Mathf.Clamp01(bb / A) : 0f;

                    if (exposureMul != 1f)
                    {
                        R = Mathf.LinearToGammaSpace(Mathf.Clamp01(Mathf.GammaToLinearSpace(R) * exposureMul));
                        G = Mathf.LinearToGammaSpace(Mathf.Clamp01(Mathf.GammaToLinearSpace(G) * exposureMul));
                        B = Mathf.LinearToGammaSpace(Mathf.Clamp01(Mathf.GammaToLinearSpace(B) * exposureMul));
                    }

                    cO[i] = new Color(R, G, B, A);
                }
                dest.SetPixels32(cO);
                dest.Apply(false);
            }

            try
            {
                go = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
                if (go == null) { pru.Cleanup(); return null; }
                go.transform.position = Vector3.zero;
                go.transform.rotation = Quaternion.identity;
                go.transform.localScale = Vector3.one;
                pru.AddSingleGO(go);

                if (autoLit)
                {
                    texBlack = SRPPreview(false, pru, go);
                    texWhite = SRPPreview(true, pru, go);

                    bool AllBlack(Texture2D t)
                    {
                        if (t == null) return true;
                        var c = t.GetPixels32();
                        for (int i = 0; i < c.Length; i++)
                            if (c[i].r != 0 || c[i].g != 0 || c[i].b != 0) return false;
                        return true;
                    }
                    if (AllBlack(texBlack) && AllBlack(texWhite))
                    {
                        if (texBlack != null) Object.DestroyImmediate(texBlack);
                        if (texWhite != null) Object.DestroyImmediate(texWhite);
                        texBlack = ManualUnlit(false, pru, go);
                        texWhite = ManualUnlit(true, pru, go);
                    }
                }
                else
                {
                    texBlack = ManualUnlit(false, pru, go);
                    texWhite = ManualUnlit(true, pru, go);
                }

                if (texBlack == null || texWhite == null) return null;

                result = new Texture2D(s, s, TextureFormat.RGBA32, false, false);
                Reconstruct(texBlack, texWhite, result);
            }
            finally
            {
                if (go != null) Object.DestroyImmediate(go);
                if (texBlack != null) Object.DestroyImmediate(texBlack);
                if (texWhite != null) Object.DestroyImmediate(texWhite);
                pru.Cleanup();
            }

            return result;
        }

        public static Texture2D FallbackAssetPreview(GameObject prefab, int desired)
        {
            var t = AssetPreview.GetAssetPreview(prefab) ?? AssetPreview.GetMiniThumbnail(prefab);
            if (t == null) return null;
            var src = t as Texture2D;
            if (src == null) return null;
            if (src.width == desired && src.height == desired) return AssetIconGeneratorUtils.DuplicateTexture(src);

            var rt = RenderTexture.GetTemporary(desired, desired, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
            var prev = RenderTexture.active;
            Graphics.Blit(src, rt);
            RenderTexture.active = rt;
            var tex = new Texture2D(desired, desired, TextureFormat.RGBA32, false, false);
            tex.ReadPixels(new Rect(0, 0, desired, desired), 0, 0);
            tex.Apply();
            RenderTexture.active = prev;
            RenderTexture.ReleaseTemporary(rt);
            return tex;
        }
    }
}
#endif
