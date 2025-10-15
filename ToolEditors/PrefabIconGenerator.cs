#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

public class PrefabIconGenerator : EditorWindow
{
    [SerializeField] private List<GameObject> prefabs = new List<GameObject>();
    [SerializeField] private List<DefaultAsset> folders = new List<DefaultAsset>();
    [SerializeField] private bool includeSubfolders = true;

    [SerializeField] private int size = 256;
    [SerializeField] private Color background = new Color(0, 0, 0, 0);
    [SerializeField] private Vector2 orbitEuler = new Vector2(20f, -30f);
    [SerializeField] private float padding = 1.1f;
    [SerializeField] private float camFov = 30f;

    [SerializeField] private string outputFolder = "Assets/_h1dr0n/Assets/Textures/Icons";
    [SerializeField] private string namePrefix = "";
    [SerializeField] private string nameSuffix = "_Icon";
    [SerializeField] private bool overwrite = true;
    [SerializeField] private bool importAsSprite = true;

    private enum RenderMode { AutoLit, ForceUnlit }
    [SerializeField] private RenderMode renderMode = RenderMode.AutoLit;
    [SerializeField, Range(0.5f, 3f)] private float exposure = 2f;

    private Vector2 _scroll;

    [MenuItem("Tools/Game Design/Prefab Icon Generator")]
    public static void Open() => GetWindow<PrefabIconGenerator>("Prefab Icon Generator");

    private void OnGUI()
    {
        using (var sv = new EditorGUILayout.ScrollViewScope(_scroll))
        {
            _scroll = sv.scrollPosition;

            EditorGUILayout.LabelField("Input", EditorStyles.boldLabel);
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                var so = new SerializedObject(this);
                EditorGUILayout.PropertyField(so.FindProperty("prefabs"), true);
                EditorGUILayout.Space(4);
                EditorGUILayout.PropertyField(so.FindProperty("folders"), true);
                EditorGUILayout.PropertyField(so.FindProperty("includeSubfolders"));
                so.ApplyModifiedProperties();

                using (new EditorGUILayout.HorizontalScope())
                {
                    if (GUILayout.Button("Add Prefabs From Selection", GUILayout.Height(22))) AddFromSelection();
                    if (GUILayout.Button("Scan Folders → Add Prefabs", GUILayout.Height(22))) ScanFolders();
                    if (GUILayout.Button("Remove Nulls", GUILayout.Height(22))) prefabs.RemoveAll(p => p == null);
                    if (GUILayout.Button("Clear", GUILayout.Height(22))) prefabs.Clear();
                }
            }

            EditorGUILayout.Space(6);
            EditorGUILayout.LabelField("Render", EditorStyles.boldLabel);
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                size = EditorGUILayout.IntPopup("Size", Mathf.Clamp(size, 32, 4096), new[] { "128", "256", "512", "1024", "2048" }, new[] { 128, 256, 512, 1024, 2048 });
                background = EditorGUILayout.ColorField("Background", background);
                orbitEuler = EditorGUILayout.Vector2Field("Orbit (X,Y)", orbitEuler);
                padding = EditorGUILayout.Slider("Padding", Mathf.Max(1.0f, padding), 1.0f, 2.0f);
                camFov = EditorGUILayout.Slider("Camera FOV", camFov, 10f, 60f);
            }

            EditorGUILayout.Space(6);
            EditorGUILayout.LabelField("Tone", EditorStyles.boldLabel);
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                renderMode = (RenderMode)EditorGUILayout.EnumPopup("Render Mode", renderMode);
                exposure = EditorGUILayout.Slider("Exposure", exposure, 0.5f, 3f);
            }

            EditorGUILayout.Space(6);
            EditorGUILayout.LabelField("Output", EditorStyles.boldLabel);
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    outputFolder = EditorGUILayout.TextField("Folder", outputFolder);
                    if (GUILayout.Button("Pick...", GUILayout.Width(64)))
                    {
                        var abs = EditorUtility.OpenFolderPanel("Select Output Folder", MakeAbsolute(outputFolder), "");
                        if (!string.IsNullOrEmpty(abs))
                        {
                            var rel = MakeRelativeToAssets(abs);
                            if (!string.IsNullOrEmpty(rel)) outputFolder = rel;
                        }
                    }
                }
                namePrefix = EditorGUILayout.TextField("Name Prefix", namePrefix);
                nameSuffix = EditorGUILayout.TextField("Name Suffix", nameSuffix);
                overwrite = EditorGUILayout.ToggleLeft("Overwrite If Exists", overwrite);
                importAsSprite = EditorGUILayout.ToggleLeft("Import As Sprite", importAsSprite);
            }

            EditorGUILayout.Space(8);
            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUI.BeginDisabledGroup(prefabs.Count == 0);
                if (GUILayout.Button($"Generate ({prefabs.Count})", GUILayout.Height(32))) Generate();
                EditorGUI.EndDisabledGroup();

                if (GUILayout.Button("Open Output Folder", GUILayout.Height(32)))
                {
                    var abs = MakeAbsolute(outputFolder);
                    Directory.CreateDirectory(abs);
                    EditorUtility.RevealInFinder(abs);
                }
            }
        }
    }

    private void AddFromSelection()
    {
        var set = new HashSet<string>(prefabs.Where(p => p != null).Select(AssetDatabase.GetAssetPath), System.StringComparer.OrdinalIgnoreCase);
        foreach (var o in Selection.objects)
        {
            var path = AssetDatabase.GetAssetPath(o);
            if (string.IsNullOrEmpty(path) || !path.EndsWith(".prefab")) continue;
            if (!set.Add(path)) continue;
            var go = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (go != null) prefabs.Add(go);
        }
    }

    private void ScanFolders()
    {
        var seen = new HashSet<string>(prefabs.Where(p => p != null).Select(AssetDatabase.GetAssetPath), System.StringComparer.OrdinalIgnoreCase);
        int before = seen.Count;
        foreach (var f in folders.Where(x => x != null))
        {
            var folderPath = AssetDatabase.GetAssetPath(f);
            if (string.IsNullOrEmpty(folderPath) || !AssetDatabase.IsValidFolder(folderPath)) continue;
            var guids = AssetDatabase.FindAssets("t:Prefab", new[] { folderPath });
            foreach (var g in guids)
            {
                var path = AssetDatabase.GUIDToAssetPath(g);
                if (!path.EndsWith(".prefab", System.StringComparison.OrdinalIgnoreCase)) continue;
                if (!includeSubfolders)
                {
                    var parent = Path.GetDirectoryName(path)?.Replace("\\", "/") ?? "";
                    if (!string.Equals(parent, folderPath.Replace("\\", "/"), System.StringComparison.OrdinalIgnoreCase)) continue;
                }
                if (!seen.Add(path)) continue;
                var go = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (go != null) prefabs.Add(go);
            }
        }
        ShowNotification(new GUIContent($"Added {seen.Count - before} prefabs"));
    }

    private void Generate()
    {
        var list = prefabs.Where(p => p != null).Distinct().ToList();
        if (list.Count == 0) return;

        var absOut = MakeAbsolute(outputFolder);
        Directory.CreateDirectory(absOut);

        int saved = 0;
        try
        {
            for (int i = 0; i < list.Count; i++)
            {
                var prefab = list[i];
                float p = (i + 1f) / list.Count;
                if (EditorUtility.DisplayCancelableProgressBar("Generating Icons", prefab.name, p)) break;

                var tex = RenderWithPreviewUtility(prefab, size, background, orbitEuler, padding, camFov, renderMode, exposure);
                if (tex == null) tex = FallbackAssetPreview(prefab, size);
                if (tex == null) continue;

                var fileName = $"{namePrefix}{prefab.name}{nameSuffix}.png";
                var relPath = $"{outputFolder.TrimEnd('/')}/{fileName}";
                var absPath = Path.Combine(absOut, fileName);

                if (!overwrite && File.Exists(absPath))
                {
                    var uniq = AssetDatabase.GenerateUniqueAssetPath(relPath);
                    absPath = MakeAbsolute(uniq);
                    relPath = uniq;
                }

                File.WriteAllBytes(absPath, tex.EncodeToPNG());
                DestroyImmediate(tex);

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

                Debug.Log($"Saved icon: {relPath}");
                saved++;
            }
        }
        finally
        {
            EditorUtility.ClearProgressBar();
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            EditorUtility.DisplayDialog("Prefab Icon Generator", $"Saved {saved}/{list.Count} icons to:\n{outputFolder}", "OK");
        }
    }

    private static Texture2D RenderWithPreviewUtility(GameObject prefab, int s, Color bg, Vector2 orbit, float pad, float fov, RenderMode mode, float exposureMul)
    {
        Texture2D SRPPreview(bool whiteBg, PreviewRenderUtility pru, GameObject go)
        {
            var rect = new Rect(0, 0, s, s);
            pru.cameraFieldOfView = fov;
            pru.camera.nearClipPlane = 0.01f;
            pru.camera.farClipPlane = 10000f;
            pru.camera.clearFlags = CameraClearFlags.SolidColor;
            pru.camera.backgroundColor = whiteBg ? Color.white : Color.black;

            try
            {
                var urpType = System.Type.GetType("UnityEngine.Rendering.Universal.UniversalAdditionalCameraData, Unity.RenderPipelines.Universal.Runtime");
                if (urpType != null)
                {
                    var urp = pru.camera.gameObject.GetComponent(urpType) ?? pru.camera.gameObject.AddComponent(urpType);
                    var renderTypeProp = urpType.GetProperty("renderType");
                    if (renderTypeProp != null) renderTypeProp.SetValue(urp, 0, null);
                    var clearFlagsProp = urpType.GetProperty("clearFlags");
                    if (clearFlagsProp != null) clearFlagsProp.SetValue(urp, 2, null);
                }
            }
            catch { }

            var bounds = CalcBounds(go);
            var center = bounds.center;
            var radius = Mathf.Max(bounds.extents.x, Mathf.Max(bounds.extents.y, bounds.extents.z));
            if (radius <= 0f) radius = 1f;

            var rot = Quaternion.Euler(orbit.x, orbit.y, 0f);
            var dir = rot * Vector3.forward;
            var dist = radius * pad / Mathf.Tan(fov * Mathf.Deg2Rad * 0.5f);
            var camPos = center - dir * dist;

            pru.camera.transform.position = camPos;
            pru.camera.transform.rotation = Quaternion.LookRotation(center - camPos, Vector3.up);
            pru.camera.cullingMask = ~0;

            pru.BeginPreview(rect, GUIStyle.none);
            pru.camera.Render();
            var rt = pru.EndPreview() as RenderTexture;
            if (rt == null) return null;

            var prev = RenderTexture.active;
            RenderTexture.active = rt;
            var outTex = new Texture2D(s, s, TextureFormat.RGBA32, false, false);
            outTex.ReadPixels(rect, 0, 0, false);
            outTex.Apply(false);
            RenderTexture.active = prev;
            return outTex;
        }

        Texture2D ManualUnlit(bool whiteBg, PreviewRenderUtility pru, GameObject go)
        {
            var bounds = CalcBounds(go);
            var center = bounds.center;
            var radius = Mathf.Max(bounds.extents.x, Mathf.Max(bounds.extents.y, bounds.extents.z));
            if (radius <= 0f) radius = 1f;

            pru.cameraFieldOfView = fov;
            var rot = Quaternion.Euler(orbit.x, orbit.y, 0f);
            var dir = rot * Vector3.forward;
            var dist = radius * pad / Mathf.Tan(fov * Mathf.Deg2Rad * 0.5f);
            var camPos = center - dir * dist;

            var cam = pru.camera;
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
                GL.PopMatrix();
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

            foreach (var mr in go.GetComponentsInChildren<MeshRenderer>(true))
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

            foreach (var smr in go.GetComponentsInChildren<SkinnedMeshRenderer>(true))
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

        var pru = new PreviewRenderUtility();
        GameObject go = null;
        Texture2D texBlack = null, texWhite = null, result = null;

        try
        {
            go = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            if (go == null) { pru.Cleanup(); return null; }
            go.transform.position = Vector3.zero;
            go.transform.rotation = Quaternion.identity;
            go.transform.localScale = Vector3.one;
            pru.AddSingleGO(go);

            if (mode == RenderMode.ForceUnlit)
            {
                texBlack = ManualUnlit(false, pru, go);
                texWhite = ManualUnlit(true, pru, go);
            }
            else
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

    private static Texture2D FallbackAssetPreview(GameObject prefab, int desired)
    {
        var t = AssetPreview.GetAssetPreview(prefab) ?? AssetPreview.GetMiniThumbnail(prefab);
        if (t == null) return null;

        var src = t as Texture2D;
        if (src == null) return null;

        if (src.width == desired && src.height == desired) return DuplicateTexture(src);

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

    private static Bounds CalcBounds(GameObject go)
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

    private static Bounds TransformBounds(Matrix4x4 m, Bounds b)
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

    private static Texture2D DuplicateTexture(Texture2D src)
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

    private static string MakeAbsolute(string assetsRelative)
    {
        var root = Application.dataPath.Substring(0, Application.dataPath.Length - "Assets".Length);
        if (string.IsNullOrEmpty(assetsRelative)) return root;
        if (assetsRelative.StartsWith("Assets")) return Path.GetFullPath(Path.Combine(root, assetsRelative));
        return assetsRelative;
    }

    private static string MakeRelativeToAssets(string absolute)
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
