#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

namespace h1dr0n.EditorTools
{
    public partial class PrefabUtilityTool
    {
        [SerializeField] private GameObject childPrefabToAdd;

        private void DrawAddChildPrefabGUI()
        {
            using (new EditorGUILayout.VerticalScope("box"))
            {
                EditorGUILayout.LabelField("Add Child Prefab If Missing", EditorStyles.boldLabel);
                EditorGUILayout.HelpBox("Add a specific child prefab under each selected prefab if it does not already exist.", MessageType.Info);

                childPrefabToAdd = (GameObject)EditorGUILayout.ObjectField("Child Prefab", childPrefabToAdd, typeof(GameObject), false);

                EditorGUILayout.Space(6);
                EditorGUI.BeginDisabledGroup(childPrefabToAdd == null);
                if (GUILayout.Button("Process All Prefabs", GUILayout.Height(28)))
                    AddChildPrefabIfMissing();
                EditorGUI.EndDisabledGroup();
            }
        }

        private void AddChildPrefabIfMissing()
        {
            if (childPrefabToAdd == null)
            {
                EditorUtility.DisplayDialog("Error", "Please assign a Child Prefab before processing.", "OK");
                return;
            }

            int addedCount = 0;

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
                    addedCount++;
                }
            }

            EditorUtility.DisplayDialog("Process Complete",
                $"Child prefab added to {addedCount} prefabs (if missing).", "OK");
        }
    }
}
#endif
