#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using System.Collections.Generic;

public static class AudioWaveformVisualizerModule
{
    private static Color waveformColor = new(0.3f, 0.8f, 1f);
    private static Color backgroundColor = new(0.1f, 0.1f, 0.1f);
    private static float displayHeight = 100f;
    private static float zoom = 1f;
    private static Vector2 scroll;

    private static Dictionary<AudioClip, float[]> waveformCache = new();

    public static void DrawGUI(List<AudioClip> clips)
    {
        using (new EditorGUILayout.VerticalScope("box"))
        {
            EditorGUILayout.LabelField("Waveform Visualizer", EditorStyles.boldLabel);
            waveformColor = EditorGUILayout.ColorField("Waveform Color", waveformColor);
            backgroundColor = EditorGUILayout.ColorField("Background", backgroundColor);
            displayHeight = EditorGUILayout.Slider("Display Height", displayHeight, 50f, 300f);
            zoom = EditorGUILayout.Slider("Zoom", zoom, 0.1f, 10f);

            EditorGUILayout.Space(8);
            EditorGUI.BeginDisabledGroup(clips == null || clips.Count == 0);
            if (GUILayout.Button("Render Waveforms", GUILayout.Height(30)))
                CacheWaveforms(clips);
            EditorGUI.EndDisabledGroup();

            if (waveformCache.Count > 0)
                DrawCachedWaveforms();
        }
    }

    private static void CacheWaveforms(List<AudioClip> clips)
    {
        waveformCache.Clear();
        foreach (var clip in clips)
        {
            if (clip == null) continue;
            int total = clip.samples * clip.channels;
            float[] data = new float[total];
            clip.GetData(data, 0);
            waveformCache[clip] = data;
            Debug.Log($"[WaveformVisualizer] Cached {clip.name} ({clip.samples} samples)");
        }
    }

    private static void DrawCachedWaveforms()
    {
        using (var sv = new EditorGUILayout.ScrollViewScope(scroll, GUILayout.Height(displayHeight * waveformCache.Count + 60)))
        {
            scroll = sv.scrollPosition;
            foreach (var kv in waveformCache)
            {
                AudioClip clip = kv.Key;
                float[] data = kv.Value;
                EditorGUILayout.LabelField($"{clip.name} ({clip.frequency} Hz, {clip.channels} ch)", EditorStyles.boldLabel);
                Rect rect = GUILayoutUtility.GetRect(100, displayHeight, GUILayout.ExpandWidth(true));
                DrawWaveform(rect, data);
            }
        }
    }

    private static void DrawWaveform(Rect rect, float[] data)
    {
        if (data == null || data.Length == 0)
        {
            EditorGUI.DrawRect(rect, backgroundColor);
            EditorGUI.LabelField(rect, "No data", EditorStyles.centeredGreyMiniLabel);
            return;
        }

        EditorGUI.DrawRect(rect, backgroundColor);
        int step = Mathf.Max(1, Mathf.RoundToInt(data.Length / (rect.width * zoom)));
        Vector3 prev = Vector3.zero;
        Handles.color = waveformColor;

        for (int i = 0; i < rect.width; i++)
        {
            int idx = i * step;
            if (idx >= data.Length) break;
            float y = rect.center.y - data[idx] * (rect.height / 2f);
            Vector3 p = new(rect.x + i, y, 0);
            if (i > 0) Handles.DrawLine(prev, p);
            prev = p;
        }

        Handles.color = Color.gray;
        Handles.DrawLine(new Vector3(rect.x, rect.center.y), new Vector3(rect.xMax, rect.center.y));
    }
}
#endif
