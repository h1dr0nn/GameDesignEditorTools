#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using System.Collections.Generic;

public partial class PrefabUtilityTool
{
    [SerializeField] private GameObject targetPrefab;
    [SerializeField] private bool preserveLocalTransform = false;
    [SerializeField] private bool keepName = false;
    [SerializeField] private bool keepLayerAndTag = true;
    [SerializeField] private bool keepChildren = true;
    [SerializeField] private bool matchActiveState = true;

    private void DrawBatchReplaceGUI()
    {
        using (new EditorGUILayout.VerticalScope("box"))
        {
            EditorGUILayout.LabelField("Batch Replace Prefabs", EditorStyles.boldLabel);
            targetPrefab = (GameObject)EditorGUILayout.ObjectField("Target Prefab", targetPrefab, typeof(GameObject), false);
            preserveLocalTransform = EditorGUILayout.ToggleLeft("Preserve Local Transform", preserveLocalTransform);
            keepName = EditorGUILayout.ToggleLeft("Keep Name", keepName);
            keepLayerAndTag = EditorGUILayout.ToggleLeft("Keep Layer & Tag", keepLayerAndTag);
            keepChildren = EditorGUILayout.ToggleLeft("Move Children To New Object", keepChildren);
            matchActiveState = EditorGUILayout.ToggleLeft("Match Active State", matchActiveState);

            if (GUILayout.Button("Replace All", GUILayout.Height(32)))
            {
                ReplaceAll();
            }
        }
    }

    private void ReplaceAll()
    {
        foreach (var obj in prefabs)
        {
            if (obj == null) continue;
            ReplaceSingle(obj);
        }
        Debug.Log("[PrefabUtilityTool] Replace complete.");
    }

    private void ReplaceSingle(GameObject oldGO)
    {
        var parent = oldGO.transform.parent;
        var newGO = (GameObject)PrefabUtility.InstantiatePrefab(targetPrefab, parent);
        newGO.transform.position = oldGO.transform.position;
        newGO.transform.rotation = oldGO.transform.rotation;
        Undo.RegisterCreatedObjectUndo(newGO, "Replace Prefab");
        Undo.DestroyObjectImmediate(oldGO);
    }
}
#endif
