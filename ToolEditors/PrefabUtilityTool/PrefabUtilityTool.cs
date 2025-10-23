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
        AddChildPrefabIfMissing,
        AddColliderFromMesh,
        AddNavMeshObstacleFromMesh,
        ApplyOffsetToScenePrefabs,
        BatchReplacePrefabs,
        CreateSplineFromPrefabs,
        GenerateStringFromNames
    }

    [SerializeField] private FunctionMode currentMode = FunctionMode.GenerateStringFromNames;

    [MenuItem("Tools/Game Design/Prefab Utility Tool")]
    private static void Open()
    {
        var window = GetWindow<PrefabUtilityTool>();
        window.titleContent = new GUIContent("Prefab Utility Tool");
        window.minSize = new Vector2(540, 480);
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
            EditorGUILayout.HelpBox("Drag and drop GameObjects from the Hierarchy or select them directly in the Scene to add to the list.", MessageType.Info);

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
                default:
                    EditorGUILayout.HelpBox("Module not implemented yet.", MessageType.Info);
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

            using (var sv = new EditorGUILayout.ScrollViewScope(_scrollPrefabs, GUILayout.MinHeight(160)))
            {
                _scrollPrefabs = sv.scrollPosition;
                EditorGUILayout.PropertyField(_prefabsProp, includeChildren: true);
            }

            float buttonWidth = (EditorGUIUtility.currentViewWidth - 80f) / 2f;
            float buttonHeight = 24f;
            GUILayout.Space(4);

            using (new EditorGUILayout.HorizontalScope())
            {
                GUILayout.FlexibleSpace();
                if (GUILayout.Button("Add From Selection", GUILayout.Width(buttonWidth), GUILayout.Height(buttonHeight)))
                    AddFromSelection();

                if (GUILayout.Button("Add From Hierarchy Selection", GUILayout.Width(buttonWidth), GUILayout.Height(buttonHeight)))
                    AddFromHierarchySelection();
                GUILayout.FlexibleSpace();
            }

            using (new EditorGUILayout.HorizontalScope())
            {
                GUILayout.FlexibleSpace();
                if (GUILayout.Button("Remove Nulls", GUILayout.Width(buttonWidth), GUILayout.Height(buttonHeight)))
                    prefabs.RemoveAll(s => s == null);

                if (GUILayout.Button("Clear All", GUILayout.Width(buttonWidth), GUILayout.Height(buttonHeight)))
                    prefabs.Clear();
                GUILayout.FlexibleSpace();
            }
        }
    }

    private void AddFromSelection()
    {
        foreach (var obj in Selection.objects)
        {
            if (obj is GameObject go && go.scene.IsValid() && !prefabs.Contains(go))
                prefabs.Add(go);
        }
    }

    private void AddFromHierarchySelection()
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
