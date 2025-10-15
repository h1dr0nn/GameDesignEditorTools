#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using System.Collections.Generic;

public partial class PrefabUtilityTool : EditorWindow
{
    [SerializeField] private List<GameObject> prefabs = new();
    private SerializedObject _so;
    private SerializedProperty _prefabsProp;
    private Vector2 _scrollAll, _scrollPrefabs;

    private enum FunctionMode
    {
        GenerateStringFromNames,
        ApplyOffsetToScenePrefabs,
        BatchReplacePrefabs,
        AddColliderFromMesh,
        AddNavMeshObstacleFromMesh,
        AddChildPrefabIfMissing,
        CreateSplineFromPrefabs
    }

    [SerializeField] private FunctionMode currentMode = FunctionMode.GenerateStringFromNames;

    [MenuItem("Tools/Game Design/Prefab Utility Tool")]
    private static void Open()
    {
        var window = GetWindow<PrefabUtilityTool>();
        window.titleContent = new GUIContent("Prefab Utility Tool");
        window.minSize = new Vector2(520, 460);
        window.Show();
    }

    private void OnEnable()
    {
        _so = new SerializedObject(this);
        _prefabsProp = _so.FindProperty("prefabs");
    }

    private void OnGUI()
    {
        using (var outer = new EditorGUILayout.ScrollViewScope(_scrollAll))
        {
            _scrollAll = outer.scrollPosition;
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Prefab Utility Tool", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox("Drag GameObjects from Hierarchy into the list.", MessageType.Info);
            _so.Update();
            DrawPrefabList();
            EditorGUILayout.Space(8);
            using (new EditorGUILayout.VerticalScope("box"))
            {
                EditorGUILayout.LabelField("Function Mode", EditorStyles.boldLabel);
                currentMode = (FunctionMode)EditorGUILayout.EnumPopup("Mode", currentMode);
            }
            EditorGUILayout.Space(8);

            switch (currentMode)
            {
                case FunctionMode.GenerateStringFromNames:
                    DrawGenerateStringGUI();
                    break;
                case FunctionMode.ApplyOffsetToScenePrefabs:
                    DrawApplyOffsetGUI();
                    break;
                case FunctionMode.BatchReplacePrefabs:
                    DrawBatchReplaceGUI();
                    break;
                case FunctionMode.AddColliderFromMesh:
                    DrawAddColliderGUI();
                    break;
                case FunctionMode.AddChildPrefabIfMissing:
                    DrawAddChildPrefabGUI();
                    break;
                case FunctionMode.AddNavMeshObstacleFromMesh:
                    DrawAddNavMeshObstacleGUI();
                    break;
                case FunctionMode.CreateSplineFromPrefabs:
                    DrawCreateSplineGUI();
                    break;
            }

            _so.ApplyModifiedProperties();
        }
    }

    private void DrawPrefabList()
    {
        using (new EditorGUILayout.VerticalScope("box"))
        {
            EditorGUILayout.LabelField("Scene Prefabs", EditorStyles.boldLabel);
            using (var sv = new EditorGUILayout.ScrollViewScope(_scrollPrefabs, GUILayout.MinHeight(140)))
            {
                _scrollPrefabs = sv.scrollPosition;
                EditorGUILayout.PropertyField(_prefabsProp, includeChildren: true);
            }

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Add From Selection")) AddFromSelection();
                if (GUILayout.Button("Remove Nulls")) prefabs.RemoveAll(s => s == null);
                if (GUILayout.Button("Clear All")) prefabs.Clear();
            }
        }
    }

    private void AddFromSelection()
    {
        foreach (var tr in Selection.transforms)
        {
            var go = tr.gameObject;
            if (go != null && go.scene.IsValid() && !prefabs.Contains(go))
                prefabs.Add(go);
        }
    }
}
#endif
