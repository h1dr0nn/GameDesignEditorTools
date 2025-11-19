#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using System.Collections.Generic;

namespace h1dr0n.EditorTools
{
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
                EditorGUILayout.HelpBox("Replace all selected prefabs in the scene with a target prefab while keeping optional properties.", MessageType.Info);

                targetPrefab = (GameObject)EditorGUILayout.ObjectField("Target Prefab", targetPrefab, typeof(GameObject), false);
                preserveLocalTransform = EditorGUILayout.ToggleLeft("Preserve Local Transform", preserveLocalTransform);
                keepName = EditorGUILayout.ToggleLeft("Keep Name", keepName);
                keepLayerAndTag = EditorGUILayout.ToggleLeft("Keep Layer & Tag", keepLayerAndTag);
                keepChildren = EditorGUILayout.ToggleLeft("Move Children To New Object", keepChildren);
                matchActiveState = EditorGUILayout.ToggleLeft("Match Active State", matchActiveState);

                EditorGUILayout.Space(8);

                EditorGUI.BeginDisabledGroup(targetPrefab == null || prefabs == null || prefabs.Count == 0);
                if (GUILayout.Button("Replace All Prefabs", GUILayout.Height(32)))
                {
                    ReplaceAllPrefabs();
                }
                EditorGUI.EndDisabledGroup();
            }
        }

        private void ReplaceAllPrefabs()
        {
            if (targetPrefab == null)
            {
                EditorUtility.DisplayDialog("Error", "Please assign a Target Prefab before replacing.", "OK");
                return;
            }

            int replacedCount = 0;
            foreach (var obj in prefabs)
            {
                if (obj == null) continue;
                ReplaceSingle(obj);
                replacedCount++;
            }

            Debug.Log($"[BatchReplacePrefabs] ✅ Replaced {replacedCount} prefabs with {targetPrefab.name}.");
            EditorUtility.DisplayDialog("Batch Replace Complete",
                $"Replaced: {replacedCount} prefabs\nTarget: {targetPrefab.name}", "OK");
        }

        private void ReplaceSingle(GameObject oldGO)
        {
            if (oldGO == null || targetPrefab == null) return;

            var parent = oldGO.transform.parent;
            var oldPos = preserveLocalTransform ? oldGO.transform.localPosition : oldGO.transform.position;
            var oldRot = preserveLocalTransform ? oldGO.transform.localRotation : oldGO.transform.rotation;
            var oldScale = preserveLocalTransform ? oldGO.transform.localScale : Vector3.one;

            var newGO = (GameObject)PrefabUtility.InstantiatePrefab(targetPrefab, parent);

            if (preserveLocalTransform)
            {
                newGO.transform.localPosition = oldPos;
                newGO.transform.localRotation = oldRot;
                newGO.transform.localScale = oldScale;
            }
            else
            {
                newGO.transform.position = oldGO.transform.position;
                newGO.transform.rotation = oldGO.transform.rotation;
            }

            if (keepName) newGO.name = oldGO.name;
            if (keepLayerAndTag)
            {
                newGO.layer = oldGO.layer;
                newGO.tag = oldGO.tag;
            }

            if (keepChildren)
            {
                var children = new List<Transform>();
                foreach (Transform child in oldGO.transform)
                    children.Add(child);

                foreach (var child in children)
                    child.SetParent(newGO.transform, true);
            }

            if (matchActiveState)
                newGO.SetActive(oldGO.activeSelf);

            Undo.RegisterCreatedObjectUndo(newGO, "Replace Prefab");
            Undo.DestroyObjectImmediate(oldGO);
            EditorUtility.SetDirty(newGO);
        }
    }
}
#endif
