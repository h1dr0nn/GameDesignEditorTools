#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

public partial class PrefabUtilityTool
{
    [SerializeField] private Vector3 positionOffset = Vector3.zero;
    [SerializeField] private Vector3 rotationOffset = Vector3.zero;

    private void DrawApplyOffsetGUI()
    {
        using (new EditorGUILayout.VerticalScope("box"))
        {
            EditorGUILayout.LabelField("Apply Offset To Scene Prefabs", EditorStyles.boldLabel);
            positionOffset = EditorGUILayout.Vector3Field("Position Offset", positionOffset);
            rotationOffset = EditorGUILayout.Vector3Field("Rotation Offset", rotationOffset);
            if (GUILayout.Button("Apply Offset", GUILayout.Height(28)))
            {
                foreach (var prefab in prefabs)
                {
                    if (prefab == null) continue;
                    Undo.RecordObject(prefab.transform, "Apply Offset");
                    prefab.transform.position += positionOffset;
                    prefab.transform.rotation *= Quaternion.Euler(rotationOffset);
                    EditorUtility.SetDirty(prefab.transform);
                }
            }
        }
    }
}
#endif
