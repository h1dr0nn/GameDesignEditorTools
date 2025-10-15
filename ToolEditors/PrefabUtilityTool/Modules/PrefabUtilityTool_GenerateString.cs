#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using System.Text;

public partial class PrefabUtilityTool
{
    [SerializeField, TextArea(5, 20)] private string generatedString = "";

    private void DrawGenerateStringGUI()
    {
        using (new EditorGUILayout.VerticalScope("box"))
        {
            EditorGUILayout.LabelField("Generate String From Names", EditorStyles.boldLabel);
            if (GUILayout.Button("Generate From Prefabs"))
            {
                var sb = new StringBuilder();
                foreach (var prefab in prefabs)
                    if (prefab != null) sb.AppendLine(prefab.name);
                generatedString = sb.ToString();
            }

            using (new EditorGUILayout.HorizontalScope())
            {
                generatedString = EditorGUILayout.TextArea(generatedString, GUILayout.Height(120));
                using (new EditorGUILayout.VerticalScope(GUILayout.Width(60)))
                {
                    if (GUILayout.Button("Copy"))
                        EditorGUIUtility.systemCopyBuffer = generatedString;
                    if (GUILayout.Button("Clear"))
                        generatedString = "";
                }
            }
        }
    }
}
#endif
