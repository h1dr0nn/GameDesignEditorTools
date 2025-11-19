#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using System.Collections.Generic;

namespace h1dr0n.EditorTools
{
    public static class AudioPeakAnalyzerModule
    {
        private static Color waveformColor = new(0.3f, 0.8f, 1f);
        private static Color peakColor = Color.red;
        private static float displayHeight = 100f;
        private static Vector2 scroll;
        private static List<PeakResult> lastResults;

        public static void DrawGUI(List<AudioClip> clips)
        {
            using (new EditorGUILayout.VerticalScope("box"))
            {
                EditorGUILayout.LabelField("Peak Analyzer", EditorStyles.boldLabel);
                EditorGUILayout.HelpBox("Visualize the waveform and identify peak values (in dB) for each selected audio clip.", MessageType.Info);

                waveformColor = EditorGUILayout.ColorField("Waveform Color", waveformColor);
                peakColor = EditorGUILayout.ColorField("Peak Marker Color", peakColor);
                displayHeight = EditorGUILayout.Slider("Display Height", displayHeight, 50, 300);

                EditorGUILayout.Space(8);
                EditorGUI.BeginDisabledGroup(clips == null || clips.Count == 0);
                if (GUILayout.Button("Analyze Peaks", GUILayout.Height(30)))
                    Analyze(clips);
                EditorGUI.EndDisabledGroup();

                if (lastResults != null && lastResults.Count > 0)
                    DrawWaveforms();
            }
        }

        private static void Analyze(List<AudioClip> clips)
        {
            lastResults = new List<PeakResult>();
            foreach (var clip in clips)
            {
                if (clip == null) continue;

                float[] data = new float[clip.samples * clip.channels];
                clip.GetData(data, 0);

                float peak = 0f;
                int peakIndex = 0;
                for (int i = 0; i < data.Length; i++)
                {
                    float abs = Mathf.Abs(data[i]);
                    if (abs > peak)
                    {
                        peak = abs;
                        peakIndex = i;
                    }
                }

                float peakDb = 20f * Mathf.Log10(Mathf.Max(1e-6f, peak));
                lastResults.Add(new PeakResult
                {
                    clip = clip,
                    data = data,
                    peakValue = peak,
                    peakDb = peakDb,
                    peakIndex = peakIndex
                });

                Debug.Log($"[AudioPeakAnalyzer] {clip.name} peak {peakDb:F2} dB at sample {peakIndex}");
            }
        }

        private static void DrawWaveforms()
        {
            using (var sv = new EditorGUILayout.ScrollViewScope(scroll, GUILayout.Height(displayHeight * lastResults.Count)))
            {
                scroll = sv.scrollPosition;
                foreach (var r in lastResults)
                {
                    EditorGUILayout.LabelField($"{r.clip.name} — Peak: {r.peakDb:F1} dB", EditorStyles.boldLabel);
                    Rect rect = GUILayoutUtility.GetRect(100, displayHeight, GUILayout.ExpandWidth(true));
                    DrawWaveform(rect, r);
                }
            }
        }

        private static void DrawWaveform(Rect rect, PeakResult r)
        {
            Handles.DrawSolidRectangleWithOutline(rect, new Color(0, 0, 0, 0.1f), Color.gray);
            if (r.data == null || r.data.Length == 0) return;

            int step = Mathf.Max(1, r.data.Length / (int)rect.width);
            Vector3 prev = Vector3.zero;
            Handles.color = waveformColor;

            for (int i = 0; i < rect.width; i++)
            {
                int idx = i * step;
                float value = r.data[idx];
                float y = rect.center.y - value * (rect.height / 2f);
                Vector3 p = new(rect.x + i, y, 0);
                if (i > 0)
                    Handles.DrawLine(prev, p);
                prev = p;
            }

            Handles.color = peakColor;
            float peakX = (r.peakIndex / (float)r.data.Length) * rect.width;
            Handles.DrawLine(new Vector3(rect.x + peakX, rect.y), new Vector3(rect.x + peakX, rect.yMax));
        }

        private class PeakResult
        {
            public AudioClip clip;
            public float[] data;
            public float peakValue;
            public float peakDb;
            public int peakIndex;
        }
    }
}
#endif
