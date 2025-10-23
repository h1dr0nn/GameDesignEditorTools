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
            EditorGUILayout.HelpBox("Generate a plain text list of all selected prefab names.", MessageType.Info);

            EditorGUILayout.Space(4);

            EditorGUI.BeginDisabledGroup(prefabs == null || prefabs.Count == 0);
            if (GUILayout.Button("Generate From Prefabs", GUILayout.Height(28)))
            {
                var sb = new StringBuilder();
                foreach (var prefab in prefabs)
                {
                    if (prefab != null)
                        sb.AppendLine(prefab.name);
                }

                generatedString = sb.ToString();
                Debug.Log($"[GenerateString] ✅ Generated string with {prefabs.Count} prefab names.");
            }
            EditorGUI.EndDisabledGroup();

            EditorGUILayout.Space(6);

            using (new EditorGUILayout.HorizontalScope())
            {
                generatedString = EditorGUILayout.TextArea(generatedString, GUILayout.Height(140));

                using (new EditorGUILayout.VerticalScope(GUILayout.Width(64)))
                {
                    if (GUILayout.Button("Copy", GUILayout.Height(28)))
                    {
                        EditorGUIUtility.systemCopyBuffer = generatedString;
                        Debug.Log("[GenerateString] 📋 Copied generated text to clipboard.");
                    }

                    if (GUILayout.Button("Clear", GUILayout.Height(28)))
                        generatedString = "";
                }
            }
        }
    }
}
#endif
