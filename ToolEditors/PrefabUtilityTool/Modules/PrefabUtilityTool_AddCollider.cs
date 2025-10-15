#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

public partial class PrefabUtilityTool
{
    private enum ColliderType { Mesh, Box, Sphere, Capsule }

    [SerializeField] private ColliderType colliderType = ColliderType.Mesh;
    [SerializeField] private bool convexMeshCollider = true;

    private void DrawAddColliderGUI()
    {
        using (new EditorGUILayout.VerticalScope("box"))
        {
            EditorGUILayout.LabelField("Add Collider From Mesh", EditorStyles.boldLabel);
            colliderType = (ColliderType)EditorGUILayout.EnumPopup("Collider Type", colliderType);
            if (colliderType == ColliderType.Mesh)
                convexMeshCollider = EditorGUILayout.Toggle("Convex Mesh", convexMeshCollider);

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Clear All")) foreach (var p in prefabs) ClearColliders(p);
                if (GUILayout.Button("Add All")) foreach (var p in prefabs) AddColliderToGO(p);
            }
        }
    }

    private void ClearColliders(GameObject go)
    {
        if (!go) return;
        foreach (var col in go.GetComponentsInChildren<Collider>(true))
            Undo.DestroyObjectImmediate(col);
    }

    private void AddColliderToGO(GameObject go)
    {
        if (!go) return;

        MeshFilter mf = go.GetComponentInChildren<MeshFilter>();
        if (mf == null || mf.sharedMesh == null) return;

        var mesh = mf.sharedMesh;
        var bounds = mesh.bounds;

        switch (colliderType)
        {
            case ColliderType.Mesh:
                var mc = Undo.AddComponent<MeshCollider>(go);
                mc.sharedMesh = mesh;
                mc.convex = convexMeshCollider;
                break;
            case ColliderType.Box:
                var bc = Undo.AddComponent<BoxCollider>(go);
                bc.center = bounds.center;
                bc.size = bounds.size;
                break;
            case ColliderType.Sphere:
                var sc = Undo.AddComponent<SphereCollider>(go);
                sc.center = bounds.center;
                sc.radius = Mathf.Max(bounds.extents.x, bounds.extents.y, bounds.extents.z);
                break;
            case ColliderType.Capsule:
                var cc = Undo.AddComponent<CapsuleCollider>(go);
                cc.center = bounds.center;
                cc.height = bounds.size.y;
                cc.radius = Mathf.Max(bounds.extents.x, bounds.extents.z);
                break;
        }
    }
}
#endif
