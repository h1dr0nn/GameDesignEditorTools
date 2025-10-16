#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

public class AssetRenameUtilityTool : EditorWindow
{
    [SerializeField] private List<UnityEngine.Object> assets = new();
    [SerializeField] private TextAsset csvMapping;
    [SerializeField] private bool autoDetectCsv = true;
    [SerializeField] private bool hasHeader = true;
    [SerializeField] private bool caseInsensitive = false;
    [SerializeField] private bool alsoRenamePrefabRoot = true;
    [SerializeField] private bool autoMakeUniqueIfConflict = true;
    [SerializeField] private bool autoSanitizeNewNames = true;
    [SerializeField] private bool autoCheckoutIfVCSActive = true;
    [SerializeField] private bool matchByGuid = true;
    [SerializeField] private bool matchByPath = true;
    [SerializeField] private bool matchByName = true;
    [SerializeField] private AssetTypeFilter assetFilter = AssetTypeFilter.All;

    private Vector2 _scrollMain, _scrollAssets, _scrollResults;
    private List<AssetRenameCore.RenameResult> _results = new();

    public enum AssetTypeFilter
    {
        All, Prefab, Audio, Image, Material, ScriptableObject, Model, Scene, Shader, Animation, Text, Other
    }

    [MenuItem("Tools/Game Design/Asset Rename Utility Tool")]
    public static void Open()
    {
        var w = GetWindow<AssetRenameUtilityTool>();
        w.titleContent = new GUIContent("Asset Rename Utility Tool");
        w.minSize = new Vector2(540, 480);
        w.maxSize = new Vector2(640, 900);
        w.Show();
    }

    private void OnGUI()
    {
        using var scroll = new EditorGUILayout.ScrollViewScope(_scrollMain);
        _scrollMain = scroll.scrollPosition;
        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Asset Rename Utility Tool", EditorStyles.boldLabel);
        var so = new SerializedObject(this);
        so.Update();

        using (new EditorGUILayout.VerticalScope("box"))
        {
            EditorGUILayout.LabelField("Asset Type Filter", EditorStyles.boldLabel);
            assetFilter = (AssetTypeFilter)EditorGUILayout.EnumPopup("Filter", assetFilter);
        }

        EditorGUILayout.Space(6);
        EditorGUILayout.HelpBox("Kéo thả asset (trừ folder) hoặc Add From Selection / Folder. Nhập CSV mapping để rename hàng loạt.", MessageType.Info);
        EditorGUILayout.Space(4);
        DrawAssetList(so);
        EditorGUILayout.Space(8);

        using (new EditorGUILayout.VerticalScope("box"))
        {
            EditorGUILayout.LabelField("CSV Mapping", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(so.FindProperty(nameof(csvMapping)));
            DrawAlignedToggleGroup(
                ("Auto Detect Csv", so.FindProperty(nameof(autoDetectCsv)), false),
                ("Has Header", so.FindProperty(nameof(hasHeader)), autoDetectCsv),
                ("Case Insensitive", so.FindProperty(nameof(caseInsensitive)), false)
            );
        }

        EditorGUILayout.Space(8);
        using (new EditorGUILayout.VerticalScope("box"))
        {
            EditorGUILayout.LabelField("Options", EditorStyles.boldLabel);
            DrawAlignedToggleGroup(
                ("Also Rename Prefab Root", so.FindProperty(nameof(alsoRenamePrefabRoot)), false),
                ("Auto Make Unique If Conflict", so.FindProperty(nameof(autoMakeUniqueIfConflict)), false),
                ("Auto Sanitize New Names", so.FindProperty(nameof(autoSanitizeNewNames)), false),
                ("Auto Checkout If VCS Active", so.FindProperty(nameof(autoCheckoutIfVCSActive)), false)
            );
        }

        EditorGUILayout.Space(8);
        using (new EditorGUILayout.VerticalScope("box"))
        {
            EditorGUILayout.LabelField("Match Priority", EditorStyles.boldLabel);
            DrawAlignedToggleGroup(
                ("Match By Guid", so.FindProperty(nameof(matchByGuid)), false),
                ("Match By Path", so.FindProperty(nameof(matchByPath)), false),
                ("Match By Name", so.FindProperty(nameof(matchByName)), false)
            );
        }

        so.ApplyModifiedProperties();
        EditorGUILayout.Space(8);
        DrawActionButtons();
        EditorGUILayout.Space(8);
        DrawResults();
    }

    private void DrawAlignedToggleGroup(params (string label, SerializedProperty prop, bool disableOn)[] items)
    {
        GUILayout.Space(2);
        foreach (var (label, prop, disableOn) in items)
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                GUILayout.Label(label, GUILayout.Width(220));
                GUILayout.FlexibleSpace();
                using (new EditorGUI.DisabledScope(disableOn))
                    EditorGUILayout.PropertyField(prop, GUIContent.none, GUILayout.Width(50));
            }
        }
    }

    private void DrawAssetList(SerializedObject so)
    {
        using (new EditorGUILayout.VerticalScope("box"))
        {
            EditorGUILayout.LabelField("Assets", EditorStyles.boldLabel);
            using (var sv = new EditorGUILayout.ScrollViewScope(_scrollAssets, GUILayout.MinHeight(160)))
            {
                _scrollAssets = sv.scrollPosition;
                EditorGUILayout.PropertyField(so.FindProperty(nameof(assets)), includeChildren: true);
            }

            float buttonWidth = (EditorGUIUtility.currentViewWidth - 100f) / 2f;
            float buttonHeight = 24f;
            GUILayout.Space(4);
            using (new EditorGUILayout.HorizontalScope())
            {
                GUILayout.FlexibleSpace();
                if (GUILayout.Button("Add From Selection", GUILayout.Width(buttonWidth), GUILayout.Height(buttonHeight)))
                    AddFromSelection();
                if (GUILayout.Button("Add From Folder Selection", GUILayout.Width(buttonWidth), GUILayout.Height(buttonHeight)))
                    AddFromFolderSelection();
                GUILayout.FlexibleSpace();
            }

            using (new EditorGUILayout.HorizontalScope())
            {
                GUILayout.FlexibleSpace();
                if (GUILayout.Button("Remove Nulls", GUILayout.Width(buttonWidth), GUILayout.Height(buttonHeight)))
                    assets.RemoveAll(s => s == null);
                if (GUILayout.Button("Clear All", GUILayout.Width(buttonWidth), GUILayout.Height(buttonHeight)))
                    assets.Clear();
                GUILayout.FlexibleSpace();
            }
        }
    }

    private void DrawActionButtons()
    {
        using (new EditorGUILayout.HorizontalScope())
        {
            GUILayout.FlexibleSpace();
            float btnW = (EditorGUIUtility.currentViewWidth - 80f) / 2f;
            if (GUILayout.Button("Scan & Preview", GUILayout.Width(btnW), GUILayout.Height(28)))
                OnScanPreview();
            GUI.enabled = _results.Any(r => r.Status == "Pending");
            if (GUILayout.Button("Apply Renames", GUILayout.Width(btnW), GUILayout.Height(28)))
                OnApplyRenames();
            GUI.enabled = true;
            GUILayout.FlexibleSpace();
        }

        using (new EditorGUILayout.HorizontalScope())
        {
            GUILayout.FlexibleSpace();
            float btnW = (EditorGUIUtility.currentViewWidth - 106f) / 3f;
            if (GUILayout.Button("Export CSV (from Assets)", GUILayout.Width(btnW), GUILayout.Height(26)))
                OnExportCsv();
            if (GUILayout.Button("Export Report (.csv)", GUILayout.Width(btnW), GUILayout.Height(26)))
                OnExportReport();
            if (GUILayout.Button("Clear Results", GUILayout.Width(btnW), GUILayout.Height(26)))
                _results.Clear();
            GUILayout.FlexibleSpace();
        }
    }

    private void DrawResults()
    {
        int ok = _results.Count(r => r.Status == "OK");
        int pend = _results.Count(r => r.Status == "Pending");
        int skp = _results.Count(r => r.Status.StartsWith("Skipped", StringComparison.OrdinalIgnoreCase));
        int err = _results.Count(r => r.Status.StartsWith("Error", StringComparison.OrdinalIgnoreCase));

        EditorGUILayout.Space(4);
        EditorGUILayout.LabelField($"Results — OK: {ok}   Pending: {pend}   Skipped: {skp}   Error: {err}", EditorStyles.boldLabel);

        using (var scrollRes = new EditorGUILayout.ScrollViewScope(_scrollResults, GUILayout.MinHeight(180)))
        {
            _scrollResults = scrollRes.scrollPosition;
            if (_results.Count == 0)
            {
                EditorGUILayout.HelpBox("Click Scan & Preview hoặc Export CSV (from Assets).", MessageType.Info);
                return;
            }
            foreach (var r in _results)
            {
                using (new EditorGUILayout.VerticalScope("box"))
                {
                    EditorGUILayout.ObjectField("Asset", r.Asset, typeof(UnityEngine.Object), false);
                    EditorGUILayout.LabelField("Type: " + r.AssetType);
                    EditorGUILayout.LabelField("GUID: " + r.Guid);
                    EditorGUILayout.LabelField("Path: " + r.Path);
                    EditorGUILayout.LabelField("Old → New: " + r.OldName + r.Extension + " → " + r.NewName + r.Extension);
                    EditorGUILayout.LabelField("Match By: " + r.MatchKey);
                    EditorGUILayout.LabelField("Status: " + r.Status + (string.IsNullOrEmpty(r.Note) ? "" : $" ({r.Note})"));
                }
            }
        }
    }

    private void AddFromSelection()
    {
        foreach (var obj in Selection.objects)
        {
            if (obj == null) continue;
            var path = AssetDatabase.GetAssetPath(obj);
            if (string.IsNullOrEmpty(path)) continue;
            if (AssetDatabase.IsValidFolder(path)) continue;
            if (!AssetRenameCore.IsPathMatchesFilter(path, assetFilter)) continue;
            if (!assets.Contains(obj)) assets.Add(obj);
        }
    }

    private void AddFromFolderSelection()
    {
        var selected = Selection.objects;
        if (selected == null || selected.Length == 0)
        {
            EditorUtility.DisplayDialog("No Folder Selected", "Hãy chọn ít nhất 1 thư mục trong Project trước.", "OK");
            return;
        }

        int added = 0;
        foreach (var obj in selected)
        {
            string folderPath = AssetDatabase.GetAssetPath(obj);
            if (string.IsNullOrEmpty(folderPath) || !AssetDatabase.IsValidFolder(folderPath)) continue;
            var guids = AssetDatabase.FindAssets("", new[] { folderPath });
            foreach (var g in guids)
            {
                var path = AssetDatabase.GUIDToAssetPath(g);
                if (string.IsNullOrEmpty(path) || AssetDatabase.IsValidFolder(path)) continue;
                if (!AssetRenameCore.IsPathMatchesFilter(path, assetFilter)) continue;
                var main = AssetDatabase.LoadMainAssetAtPath(path);
                if (main != null && !assets.Contains(main))
                {
                    assets.Add(main);
                    added++;
                }
            }
        }

        if (added > 0)
            Debug.Log($"[AssetRenameUtilityTool] Added {added} asset(s) from selected folder(s).");
        else
            EditorUtility.DisplayDialog("No Asset Found", "Không tìm thấy asset phù hợp trong thư mục đã chọn.", "OK");
    }

    private void OnScanPreview()
    {
        _results = AssetRenameCore.ScanPreview(
            assets, csvMapping, ',', hasHeader, caseInsensitive,
            autoDetectCsv, autoSanitizeNewNames, assetFilter,
            matchByGuid, matchByPath, matchByName);
        Repaint();
    }

    private void OnApplyRenames()
    {
        AssetRenameCore.ApplyRenames(_results, alsoRenamePrefabRoot, autoMakeUniqueIfConflict, autoCheckoutIfVCSActive);
        Repaint();
    }

    private void OnExportCsv()
    {
        if (assets.Count == 0)
        {
            EditorUtility.DisplayDialog("Export CSV", "Asset list is empty.", "OK");
            return;
        }

        var uniq = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var names = new List<(string guid, string path, string name, string ext, string type)>();
        foreach (var a in assets.Where(a => a != null))
        {
            var path = AssetDatabase.GetAssetPath(a);
            if (string.IsNullOrEmpty(path) || AssetDatabase.IsValidFolder(path)) continue;
            if (!AssetRenameCore.IsPathMatchesFilter(path, assetFilter)) continue;
            if (!uniq.Add(path)) continue;
            names.Add((AssetDatabase.AssetPathToGUID(path),
                       path,
                       Path.GetFileNameWithoutExtension(path),
                       Path.GetExtension(path) ?? "",
                       AssetRenameCore.GetAssetTypeName(path)));
        }

        if (names.Count == 0)
        {
            EditorUtility.DisplayDialog("Export CSV", "No valid asset found.", "OK");
            return;
        }

        var savePath = EditorUtility.SaveFilePanelInProject("Save CSV Mapping", "AssetRenameMapping.csv", "csv", "Columns: oldName,newName,guid,path,type");
        if (string.IsNullOrEmpty(savePath)) return;

        var sb = new System.Text.StringBuilder();
        char d = ',';
        sb.Append("oldName").Append(d).Append("newName").Append(d).Append("guid").Append(d).Append("path").Append(d).Append("type").AppendLine();
        foreach (var n in names.OrderBy(x => x.name, StringComparer.OrdinalIgnoreCase))
        {
            sb.Append(AssetRenameCore.CsvEscape(n.name, d)).Append(d)
              .Append("").Append(d)
              .Append(AssetRenameCore.CsvEscape(n.guid, d)).Append(d)
              .Append(AssetRenameCore.CsvEscape(n.path, d)).Append(d)
              .Append(AssetRenameCore.CsvEscape(n.type, d)).AppendLine();
        }

        File.WriteAllText(savePath, sb.ToString(), new System.Text.UTF8Encoding(true));
        AssetDatabase.ImportAsset(savePath);
        var asset = AssetDatabase.LoadAssetAtPath<TextAsset>(savePath);
        csvMapping = asset;
        EditorUtility.SetDirty(this);
        AssetDatabase.SaveAssets();
        EditorGUIUtility.PingObject(asset);
        EditorUtility.DisplayDialog("Export CSV", $"Exported {names.Count} rows and auto-linked CSV Mapping.", "OK");
    }

    private void OnExportReport()
    {
        if (_results == null || _results.Count == 0)
        {
            EditorUtility.DisplayDialog("Export Report", "No data to export.", "OK");
            return;
        }
        AssetRenameCore.ExportReportCsv(_results);
    }
}
#endif
