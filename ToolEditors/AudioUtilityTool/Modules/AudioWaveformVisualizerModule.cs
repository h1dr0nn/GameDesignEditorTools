#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using System.Collections.Generic;

public static class AudioWaveformVisualizerModule
{
    private static Color waveformColor = new(0.3f, 0.8f, 1f);
    private static Color backgroundColor = new(0.08f, 0.08f, 0.08f);
    private static Color axisColor = new(0.3f, 0.3f, 0.3f);
    private static float displayHeight = 120f;
    private static float zoom = 1f;
    private static Vector2 scroll;

    private static readonly Dictionary<AudioClip, float[]> waveformCache = new();

    public static void DrawGUI(List<AudioClip> clips)
    {
        using (new EditorGUILayout.VerticalScope("box"))
        {
            EditorGUILayout.LabelField("Waveform Visualizer", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox("Preview waveforms of selected AudioClips directly in the editor. Use zoom to inspect finer details.", MessageType.Info);

            waveformColor = EditorGUILayout.ColorField("Waveform Color", waveformColor);
            backgroundColor = EditorGUILayout.ColorField("Background Color", backgroundColor);
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

            try
            {
                int total = clip.samples * clip.channels;
                float[] data = new float[total];
                clip.GetData(data, 0);
                waveformCache[clip] = data;
                Debug.Log($"[AudioWaveform] ✅ Cached {clip.name} ({clip.samples} samples, {clip.frequency} Hz, {clip.channels} ch)");
            }
            catch (System.Exception ex)
            {
                Debug.LogWarning($"[AudioWaveform] ⚠️ Failed to read {clip.name}: {ex.Message}");
            }
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

                EditorGUILayout.Space(6);
                EditorGUILayout.LabelField($"{clip.name}  ({clip.frequency} Hz, {clip.channels} ch)", EditorStyles.boldLabel);

                Rect rect = GUILayoutUtility.GetRect(100, displayHeight, GUILayout.ExpandWidth(true));
                DrawWaveform(rect, data);
            }
        }
    }

    private static void DrawWaveform(Rect rect, float[] data)
    {
        EditorGUI.DrawRect(rect, backgroundColor);

        if (data == null || data.Length == 0)
        {
            EditorGUI.LabelField(rect, "No waveform data", EditorStyles.centeredGreyMiniLabel);
            return;
        }

        Handles.color = axisColor;
        Handles.DrawLine(new Vector3(rect.x, rect.center.y), new Vector3(rect.xMax, rect.center.y));

        int step = Mathf.Max(1, Mathf.RoundToInt(data.Length / (rect.width * zoom)));
        Handles.color = waveformColor;

        Vector3 prev = Vector3.zero;
        bool hasPrev = false;

        for (int i = 0; i < rect.width; i++)
        {
            int idx = i * step;
            if (idx >= data.Length) break;

            float y = rect.center.y - data[idx] * (rect.height / 2f);
            Vector3 p = new(rect.x + i, y, 0);

            if (hasPrev)
                Handles.DrawLine(prev, p);

            prev = p;
            hasPrev = true;
        }
    }
}
#endif
