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
            navObstacleCarve = EditorGUILayout.Toggle("Carve", navObstacleCarve);
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Clear All")) foreach (var p in prefabs) ClearNavMeshObstacle(p);
                if (GUILayout.Button("Add All")) foreach (var p in prefabs) AddNavMeshObstacle(p);
            }
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
