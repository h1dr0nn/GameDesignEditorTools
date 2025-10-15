#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

public partial class PrefabUtilityTool
{
    [SerializeField] private GameObject childPrefabToAdd;

    private void DrawAddChildPrefabGUI()
    {
        using (new EditorGUILayout.VerticalScope("box"))
        {
            EditorGUILayout.LabelField("Add Child Prefab If Missing", EditorStyles.boldLabel);
            childPrefabToAdd = (GameObject)EditorGUILayout.ObjectField("Child Prefab", childPrefabToAdd, typeof(GameObject), false);
            if (GUILayout.Button("Process All", GUILayout.Height(28)))
                AddChildPrefabIfMissing();
        }
    }

    private void AddChildPrefabIfMissing()
    {
        if (childPrefabToAdd == null)
        {
            EditorUtility.DisplayDialog("Error", "Please assign a Child Prefab.", "OK");
            return;
        }

        foreach (var prefab in prefabs)
        {
            if (prefab == null) continue;
            bool hasChild = false;
            foreach (Transform child in prefab.transform)
            {
                if (child.name == childPrefabToAdd.name)
                {
                    hasChild = true;
                    break;
                }
            }

            if (!hasChild)
            {
                var newChild = (GameObject)PrefabUtility.InstantiatePrefab(childPrefabToAdd, prefab.transform);
                newChild.name = childPrefabToAdd.name;
                Undo.RegisterCreatedObjectUndo(newChild, "Add Child Prefab");
            }
        }

        EditorUtility.DisplayDialog("Done", "Child Prefabs processed successfully.", "OK");
    }
}
#endif
