#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using UnityEngine.AI;

public partial class PrefabUtilityTool
{
    [SerializeField] private bool navObstacleCarve = true;

    private void DrawAddNavMeshObstacleGUI()
    {
        using (new EditorGUILayout.VerticalScope("box"))
        {
            EditorGUILayout.LabelField("Add NavMeshObstacle From Mesh", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox("Add NavMeshObstacle components to selected prefabs based on their mesh structure.", MessageType.Info);

            navObstacleCarve = EditorGUILayout.Toggle("Carve", navObstacleCarve);

            EditorGUILayout.Space(6);

            EditorGUI.BeginDisabledGroup(prefabs == null || prefabs.Count == 0);
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Clear All Obstacles", GUILayout.Height(26)))
                {
                    foreach (var p in prefabs)
                        ClearNavMeshObstacle(p);
                    Debug.Log("[AddNavMeshObstacle] ✅ Cleared all NavMeshObstacles from selected prefabs.");
                }

                if (GUILayout.Button("Add Obstacles", GUILayout.Height(26)))
                {
                    foreach (var p in prefabs)
                        AddNavMeshObstacle(p);
                    Debug.Log("[AddNavMeshObstacle] ✅ Added NavMeshObstacles to all selected prefabs.");
                }
            }
            EditorGUI.EndDisabledGroup();
        }
    }

    private void ClearNavMeshObstacle(GameObject go)
    {
        if (!go) return;
        foreach (var obs in go.GetComponentsInChildren<NavMeshObstacle>(true))
            Undo.DestroyObjectImmediate(obs);
    }

    private void AddNavMeshObstacle(GameObject go)
    {
        if (!go) return;
        if (go.GetComponent<NavMeshObstacle>() != null) return;

        var obs = Undo.AddComponent<NavMeshObstacle>(go);
        obs.carving = navObstacleCarve;
        obs.shape = NavMeshObstacleShape.Box;
        obs.size = Vector3.one;
    }
}
#endif
