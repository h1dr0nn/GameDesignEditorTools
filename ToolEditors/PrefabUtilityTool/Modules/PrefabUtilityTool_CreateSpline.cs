#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using UnityEngine.Splines;
using Unity.Mathematics;
using System.Collections.Generic;
using System.Linq;

public partial class PrefabUtilityTool
{
    [SerializeField] private string splineName = "SplineFromPrefabs";
    [SerializeField] private bool splineClosed = false;
    [SerializeField] private TangentMode splineTangentMode = TangentMode.AutoSmooth;
    [SerializeField] private OrderMode splineOrderMode = OrderMode.Nearest;

    public enum OrderMode { AsIs, ByName, Nearest }
    private void DrawCreateSplineGUI()
    {
        using (new EditorGUILayout.VerticalScope("box"))
        {
            EditorGUILayout.LabelField("Create Spline From Prefabs", EditorStyles.boldLabel);

            splineName = EditorGUILayout.TextField("Spline Name", splineName);
            splineClosed = EditorGUILayout.Toggle("Closed", splineClosed);
            splineTangentMode = (TangentMode)EditorGUILayout.EnumPopup("Tangent Mode", splineTangentMode);
            splineOrderMode = (OrderMode)EditorGUILayout.EnumPopup("Order", splineOrderMode);

            EditorGUILayout.Space(8);

            EditorGUI.BeginDisabledGroup(prefabs.Count < 2);
            if (GUILayout.Button("Create Spline", GUILayout.Height(30)))
                CreateSplineFromPrefabs(splineName, splineClosed, splineTangentMode, splineOrderMode);
            EditorGUI.EndDisabledGroup();
        }
    }

    private void CreateSplineFromPrefabs(
        string splineName = "SplineFromPrefabs",
        bool closed = false,
        TangentMode rotation = TangentMode.AutoSmooth,
        OrderMode orderMode = OrderMode.Nearest)
    {
        var points = prefabs
            .Where(p => p != null)
            .Select(p => p.transform)
            .Distinct()
            .ToList();

        if (points.Count < 2)
        {
            EditorUtility.DisplayDialog("Points To Spline", "Cần ít nhất 2 điểm hợp lệ để tạo spline.", "OK");
            return;
        }

        var orderedPoints = OrderPoints(points, orderMode);

        var rootName = string.IsNullOrWhiteSpace(splineName) ? "SplineFromPrefabs" : splineName.Trim();
        var rootGO = new GameObject(rootName);
        Undo.RegisterCreatedObjectUndo(rootGO, "Create Spline From Prefabs");

        var container = rootGO.AddComponent<SplineContainer>();
        var spline = container.Spline;
        spline.Clear();

        foreach (var t in orderedPoints)
        {
            var local = (float3)rootGO.transform.InverseTransformPoint(t.position);
            var knot = new BezierKnot(local) { Rotation = Quaternion.identity };
            spline.Add(knot, rotation);
        }

        spline.Closed = closed;

        Selection.activeGameObject = rootGO;
        EditorGUIUtility.PingObject(rootGO);

        Debug.Log($"[PrefabUtilityTool] Created spline '{rootName}' with {orderedPoints.Count} points.");
    }

    private static List<Transform> OrderPoints(List<Transform> pts, OrderMode mode)
    {
        if (pts == null || pts.Count <= 2)
            return pts?.ToList() ?? new List<Transform>();

        if (mode == OrderMode.AsIs)
            return pts.ToList();

        if (mode == OrderMode.ByName)
            return pts.OrderBy(t => t ? t.name : string.Empty, System.StringComparer.Ordinal).ToList();

        var list = pts.ToList();
        var ordered = new List<Transform>();
        var current = list[0];
        ordered.Add(current);
        list.RemoveAt(0);

        while (list.Count > 0)
        {
            Transform next = null;
            float best = float.MaxValue;
            foreach (var t in list)
            {
                var d = (t.position - current.position).sqrMagnitude;
                if (d < best) { best = d; next = t; }
            }
            ordered.Add(next);
            list.Remove(next);
            current = next;
        }

        return ordered;
    }
}
#endif
