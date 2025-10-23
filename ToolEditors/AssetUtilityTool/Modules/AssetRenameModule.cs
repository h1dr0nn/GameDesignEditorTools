#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using System.Collections.Generic;
using System.Linq;

public static class AssetRenameModule
{
    private static TextAsset csvMapping;
    private static bool autoDetectCsv = true;
    private static bool hasHeader = true;
    private static bool caseInsensitive = false;
    private static bool alsoRenamePrefabRoot = true;
    private static bool autoMakeUniqueIfConflict = true;
    private static bool autoSanitizeNewNames = true;
    private static bool autoCheckoutIfVCSActive = true;
    private static bool matchByGuid = true;
    private static bool matchByPath = true;
    private static bool matchByName = true;

    private static List<AssetRenameCore.RenameResult> _results = new();
    private static Vector2 _scrollResults;

    public static void DrawGUI(List<Object> assets, AssetUtilityTool.AssetTypeFilter assetFilter)
    {
        using (new EditorGUILayout.VerticalScope("box"))
        {
            EditorGUILayout.LabelField("Rename Assets (CSV Mapping)", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "Batch rename assets using a CSV mapping file.\nEach line should contain: oldName,newName,guid,path,type",
                MessageType.Info);
        }

        using (new EditorGUILayout.VerticalScope("box"))
        {
            EditorGUILayout.LabelField("CSV Mapping", EditorStyles.boldLabel);
            csvMapping = (TextAsset)EditorGUILayout.ObjectField("CSV File", csvMapping, typeof(TextAsset), false);
            autoDetectCsv = EditorGUILayout.Toggle("Auto Detect Format", autoDetectCsv);
            hasHeader = EditorGUILayout.Toggle("Has Header Row", hasHeader);
            caseInsensitive = EditorGUILayout.Toggle("Case Insensitive Match", caseInsensitive);
        }

        using (new EditorGUILayout.VerticalScope("box"))
        {
            EditorGUILayout.LabelField("Rename Options", EditorStyles.boldLabel);
            alsoRenamePrefabRoot = EditorGUILayout.Toggle("Also Rename Prefab Root", alsoRenamePrefabRoot);
            autoMakeUniqueIfConflict = EditorGUILayout.Toggle("Auto Make Unique If Conflict", autoMakeUniqueIfConflict);
            autoSanitizeNewNames = EditorGUILayout.Toggle("Auto Sanitize New Names", autoSanitizeNewNames);
            autoCheckoutIfVCSActive = EditorGUILayout.Toggle("Auto Checkout If VCS Active", autoCheckoutIfVCSActive);
        }

        using (new EditorGUILayout.VerticalScope("box"))
        {
            EditorGUILayout.LabelField("Match Priority", EditorStyles.boldLabel);
            matchByGuid = EditorGUILayout.Toggle("Match By GUID", matchByGuid);
            matchByPath = EditorGUILayout.Toggle("Match By Path", matchByPath);
            matchByName = EditorGUILayout.Toggle("Match By Name", matchByName);
        }

        EditorGUILayout.Space(6);
        DrawActionButtons(assets, assetFilter);
        EditorGUILayout.Space(8);
        DrawResults();
    }

    private static void DrawActionButtons(List<Object> assets, AssetUtilityTool.AssetTypeFilter filter)
    {
        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button("Scan & Preview", GUILayout.Height(28)))
                OnScanPreview(assets, filter);
            if (GUILayout.Button("Apply Renames", GUILayout.Height(28)))
                OnApplyRenames();
        }

        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button("Export CSV (From Assets)", GUILayout.Height(26)))
                AssetRenameCore.ExportCsvFromAssets(assets, filter, ref csvMapping);
            if (GUILayout.Button("Export Report", GUILayout.Height(26)))
                OnExportReport();
            if (GUILayout.Button("Clear Results", GUILayout.Height(26)))
                _results.Clear();
        }
    }

    private static void OnScanPreview(List<Object> assets, AssetUtilityTool.AssetTypeFilter filter)
    {
        _results = AssetRenameCore.ScanPreview(
            assets, csvMapping, ',', hasHeader, caseInsensitive,
            autoDetectCsv, autoSanitizeNewNames, filter,
            matchByGuid, matchByPath, matchByName);

        if (_results.Count == 0)
            EditorUtility.DisplayDialog("Scan Complete", "No matching assets found in the CSV mapping.", "OK");
    }

    private static void OnApplyRenames()
    {
        if (_results == null || _results.Count == 0)
        {
            EditorUtility.DisplayDialog("Apply Renames", "No pending rename results to apply.", "OK");
            return;
        }

        AssetRenameCore.ApplyRenames(_results, alsoRenamePrefabRoot, autoMakeUniqueIfConflict, autoCheckoutIfVCSActive);
        EditorUtility.DisplayDialog("Rename Complete", "All rename operations have finished.", "OK");
    }

    private static void OnExportReport()
    {
        if (_results == null || _results.Count == 0)
        {
            EditorUtility.DisplayDialog("Export Report", "No rename results to export.", "OK");
            return;
        }
        AssetRenameCore.ExportReportCsv(_results);
    }

    private static void DrawResults()
    {
        using (new EditorGUILayout.VerticalScope("box"))
        {
            EditorGUILayout.LabelField("Results", EditorStyles.boldLabel);

            if (_results.Count == 0)
            {
                EditorGUILayout.HelpBox("Click Scan & Preview to generate a rename list.", MessageType.Info);
                return;
            }

            using (var scroll = new EditorGUILayout.ScrollViewScope(_scrollResults, GUILayout.MinHeight(160)))
            {
                _scrollResults = scroll.scrollPosition;
                foreach (var r in _results)
                {
                    using (new EditorGUILayout.VerticalScope("box"))
                    {
                        EditorGUILayout.ObjectField("Asset", r.Asset, typeof(Object), false);
                        EditorGUILayout.LabelField("Old → New", $"{r.OldName}{r.Extension} → {r.NewName}{r.Extension}");
                        EditorGUILayout.LabelField("Status", $"{r.Status} {(string.IsNullOrEmpty(r.Note) ? "" : $"({r.Note})")}");
                    }
                }
            }
        }
    }
}
#endif
