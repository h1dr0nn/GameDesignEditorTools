#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using System.Collections.Generic;
using System.Linq;

public class AssetUtilityTool : EditorWindow
{
    public enum ModuleType
    {
        RenameAssets,
        IconGenerator,
    }

    public enum AssetTypeFilter
    {
        All, Prefab, Audio, Image, Material, ScriptableObject, Model, Scene, Shader, Animation, Text, Other
    }

    [SerializeField] private ModuleType currentModule = ModuleType.RenameAssets;
    [SerializeField] private AssetTypeFilter assetFilter = AssetTypeFilter.All;
    [SerializeField] private List<Object> selectedAssets = new();

    private Vector2 _scrollMain, _scrollAssets;

    [MenuItem("Tools/Game Design/Asset Utility Tool")]
    public static void Open()
    {
        var w = GetWindow<AssetUtilityTool>();
        w.titleContent = new GUIContent("Asset Utility Tool");
        w.minSize = new Vector2(540, 480);
        w.Show();
    }

    private void OnGUI()
    {
        using var scroll = new EditorGUILayout.ScrollViewScope(_scrollMain);
        _scrollMain = scroll.scrollPosition;

        EditorGUILayout.Space(4);
        EditorGUILayout.LabelField("Asset Utility Tool", EditorStyles.boldLabel);

        using (new EditorGUILayout.VerticalScope("box"))
        {
            EditorGUILayout.LabelField("Asset Type Filter", EditorStyles.boldLabel);
            assetFilter = (AssetTypeFilter)EditorGUILayout.EnumPopup("Filter", assetFilter);
        }

        EditorGUILayout.Space(6);
        EditorGUILayout.HelpBox("Drag & drop assets (excluding folders) or use Add From Selection / Folder. These assets will be used by the selected module below.", MessageType.Info);
        EditorGUILayout.Space(4);

        DrawAssetList();

        EditorGUILayout.Space(8);
        currentModule = (ModuleType)EditorGUILayout.EnumPopup("Active Module", currentModule);
        EditorGUILayout.Space(4);

        switch (currentModule)
        {
            case ModuleType.RenameAssets:
                AssetRenameModule.DrawGUI(selectedAssets, assetFilter);
                break;

            case ModuleType.IconGenerator:
                AssetIconGenerateModule.DrawGUI(selectedAssets);
                break;

            default:
                EditorGUILayout.HelpBox("Select a module to begin.", MessageType.Info);
                break;
        }
    }

    private void DrawAssetList()
    {
        using (new EditorGUILayout.VerticalScope("box"))
        {
            EditorGUILayout.LabelField("Assets", EditorStyles.boldLabel);

            var so = new SerializedObject(this);
            var prop = so.FindProperty(nameof(selectedAssets));

            using (var sv = new EditorGUILayout.ScrollViewScope(_scrollAssets, GUILayout.MinHeight(160)))
            {
                _scrollAssets = sv.scrollPosition;
                EditorGUILayout.PropertyField(prop, includeChildren: true);
            }

            so.ApplyModifiedProperties();

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
                    selectedAssets.RemoveAll(a => a == null);
                if (GUILayout.Button("Clear All", GUILayout.Width(buttonWidth), GUILayout.Height(buttonHeight)))
                    selectedAssets.Clear();
                GUILayout.FlexibleSpace();
            }
        }
    }

    private void AddFromSelection()
    {
        foreach (var obj in Selection.objects)
        {
            if (obj == null) continue;
            var path = AssetDatabase.GetAssetPath(obj);
            if (string.IsNullOrEmpty(path) || AssetDatabase.IsValidFolder(path)) continue;
            if (!AssetRenameCore.IsPathMatchesFilter(path, assetFilter)) continue;
            if (!selectedAssets.Contains(obj)) selectedAssets.Add(obj);
        }
    }

    private void AddFromFolderSelection()
    {
        var selected = Selection.objects;
        if (selected == null || selected.Length == 0)
        {
            EditorUtility.DisplayDialog("No Folder Selected", "Please select at least one folder in the Project window.", "OK");
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
                if (main != null && !selectedAssets.Contains(main))
                {
                    selectedAssets.Add(main);
                    added++;
                }
            }
        }

        if (added > 0)
            Debug.Log($"[AssetUtilityTool] Added {added} asset(s) from selected folder(s).");
        else
            EditorUtility.DisplayDialog("No Asset Found", "No valid assets found in the selected folder(s).", "OK");
    }
}
#endif
