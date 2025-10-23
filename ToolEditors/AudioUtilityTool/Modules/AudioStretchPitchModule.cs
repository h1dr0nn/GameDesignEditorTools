#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using System.IO;
using System.Collections.Generic;

public static class AudioStretchPitchModule
{
    private enum Mode { Independent, LinkedResample }

    private static Mode mode = Mode.Independent;
    private static float stretchPercent = 100f;
    private static float pitchSemitones = 0f;
    private static int windowSize = 2048;
    private static float hopRatio = 0.25f;

    private static string suffix = "_sp";
    private static bool writeNextToSource = true;
    private static DefaultAsset outputFolder;
    private static bool overwriteSource = false;

    public static void DrawGUI(List<AudioClip> clips)
    {
        using (new EditorGUILayout.VerticalScope("box"))
        {
            EditorGUILayout.LabelField("Stretch & Pitch Settings", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox("Stretch audio duration or adjust pitch. 'Independent' mode keeps pitch and timing separate, while 'Linked Resample' changes both together.", MessageType.Info);

            mode = (Mode)EditorGUILayout.EnumPopup("Mode", mode);

            if (mode == Mode.Independent)
                stretchPercent = EditorGUILayout.Slider("Stretch Duration (%)", stretchPercent, 10f, 400f);
            else
                stretchPercent = EditorGUILayout.Slider("Speed (%)", stretchPercent, 10f, 400f);

            using (new EditorGUI.DisabledScope(mode == Mode.LinkedResample))
                pitchSemitones = EditorGUILayout.Slider("Pitch (Semitones)", pitchSemitones, -24f, 24f);

            EditorGUILayout.Space(4);
            windowSize = Mathf.ClosestPowerOfTwo(EditorGUILayout.IntSlider("Window Size", windowSize, 512, 8192));
            hopRatio = Mathf.Clamp01(EditorGUILayout.Slider("Hop Ratio", hopRatio, 0.1f, 0.9f));

            EditorGUILayout.Space(6);
            overwriteSource = EditorGUILayout.ToggleLeft("⚠️ Overwrite Source File (Dangerous)", overwriteSource);

            if (!overwriteSource)
            {
                suffix = EditorGUILayout.TextField("Filename Suffix", suffix);
                writeNextToSource = EditorGUILayout.Toggle("Write Next To Source", writeNextToSource);
                using (new EditorGUI.DisabledScope(writeNextToSource))
                    outputFolder = (DefaultAsset)EditorGUILayout.ObjectField("Output Folder", outputFolder, typeof(DefaultAsset), false);
            }
            else
            {
                EditorGUILayout.HelpBox("Overwrite is enabled — suffix and folder will be ignored.", MessageType.Warning);
            }

            EditorGUILayout.Space(8);
            EditorGUI.BeginDisabledGroup(clips == null || clips.Count == 0);
            if (GUILayout.Button("Apply Stretch & Pitch", GUILayout.Height(30)))
                ProcessClips(clips);
            EditorGUI.EndDisabledGroup();
        }
    }

    private static void ProcessClips(List<AudioClip> clips)
    {
        if (clips == null || clips.Count == 0) return;

        if (overwriteSource)
        {
            bool confirm = EditorUtility.DisplayDialog(
                "Confirm Overwrite",
                "You are about to OVERWRITE original audio files!\nThis action cannot be undone.\n\nContinue?",
                "Yes, overwrite", "Cancel");
            if (!confirm) return;
        }

        int done = 0, skipped = 0;
        string tempDir = Path.Combine(Application.dataPath, "../__AudioTempWrite");
        Directory.CreateDirectory(tempDir);

        foreach (var clip in clips)
        {
            if (clip == null) continue;
            string srcPath = AssetDatabase.GetAssetPath(clip);
            if (string.IsNullOrEmpty(srcPath)) continue;

            string ext = Path.GetExtension(srcPath).ToLower();
            string dir = overwriteSource
                ? Path.GetDirectoryName(srcPath)
                : (writeNextToSource ? Path.GetDirectoryName(srcPath)
                                     : AssetDatabase.GetAssetPath(outputFolder));
            if (string.IsNullOrEmpty(dir)) dir = "Assets";

            string baseName = Path.GetFileNameWithoutExtension(srcPath);
            string outName = overwriteSource ? baseName : baseName + suffix;
            string outPath = Path.Combine(dir, outName + ext).Replace("\\", "/");

            try
            {
                int channels = clip.channels;
                int freq = clip.frequency;
                int total = clip.samples * channels;
                float[] data = new float[total];
                clip.GetData(data, 0);

                float[] processed;

                if (mode == Mode.Independent)
                {
                    float stretchFactor = Mathf.Max(0.01f, stretchPercent / 100f);
                    processed = TimeStretchPhaseVocoder(data, channels, stretchFactor, windowSize, hopRatio);

                    if (Mathf.Abs(pitchSemitones) > 0.001f)
                    {
                        float pitchFactor = Mathf.Pow(2f, pitchSemitones / 12f);
                        processed = ResampleLinear(processed, channels, pitchFactor);
                        processed = TimeStretchPhaseVocoder(processed, channels, pitchFactor, windowSize, hopRatio);
                    }
                }
                else
                {
                    float speed = Mathf.Max(0.01f, stretchPercent / 100f);
                    processed = ResampleLinear(data, channels, speed);
                }

                string tempFile = Path.Combine(tempDir, baseName + ".wav");
                var bytes = AudioWavUtility.Encode(processed, channels, freq);
                File.WriteAllBytes(tempFile, bytes);

                string absoluteOutPath = Path.GetFullPath(outPath);
                File.Copy(tempFile, absoluteOutPath, true);

                done++;
                Debug.Log($"[AudioStretchPitch] ✅ Saved: {absoluteOutPath}");
            }
            catch (IOException ioEx)
            {
                Debug.LogWarning($"[AudioStretchPitch] ⚠️ Skipped {clip?.name} — file locked or in use: {ioEx.Message}");
                skipped++;
            }
            catch (System.Exception ex)
            {
                Debug.LogWarning($"[AudioStretchPitch] ⚠️ Error processing {clip?.name}: {ex.Message}");
                skipped++;
            }
        }

        AssetDatabase.Refresh();

        EditorUtility.DisplayDialog("Stretch & Pitch Complete",
            $"Processed: {clips.Count}\nCompleted: {done}\nSkipped: {skipped}\n\n" +
            $"{(overwriteSource ? "⚠️ Files were overwritten!" : string.Empty)}",
            "OK");
    }

    private static float[] TimeStretchPhaseVocoder(float[] interleaved, int channels, float factor, int winSize, float hopRatio)
    {
        if (Mathf.Approximately(factor, 1f)) return (float[])interleaved.Clone();

        int hopIn = Mathf.Max(1, Mathf.RoundToInt(winSize * hopRatio));
        int hopOut = Mathf.Max(1, Mathf.RoundToInt(hopIn * factor));

        float[] window = Hann(winSize);
        int frames = (interleaved.Length / channels - winSize) / hopIn;
        int outSamplesPerCh = winSize + frames * hopOut;
        float[] outInter = new float[outSamplesPerCh * channels];

        for (int ch = 0; ch < channels; ch++)
        {
            float[] phase = new float[winSize];
            float[] lastPhase = new float[winSize];
            float[] sum = new float[outSamplesPerCh];

            int outPos = 0;
            for (int frame = 0; frame < frames; frame++)
            {
                int inPos = frame * hopIn;
                for (int n = 0; n < winSize; n++)
                {
                    int idx = (inPos + n) * channels + ch;
                    phase[n] = interleaved[idx] * window[n];
                }

                if (frame == 0)
                {
                    OverlapAdd(sum, phase, outPos);
                    outPos += hopOut;
                    System.Array.Copy(phase, lastPhase, winSize);
                    continue;
                }

                float[] instPhase = new float[winSize];
                for (int n = 0; n < winSize; n++)
                {
                    float d = phase[n] - lastPhase[n];
                    instPhase[n] = lastPhase[n] + d;
                }

                OverlapAdd(sum, instPhase, outPos);
                outPos += hopOut;
                System.Array.Copy(phase, lastPhase, winSize);
            }

            for (int i = 0; i < sum.Length; i++)
            {
                int idx = i * channels + ch;
                if (idx < outInter.Length)
                    outInter[idx] = Mathf.Clamp(sum[i], -1f, 1f);
            }
        }

        return outInter;
    }

    private static void OverlapAdd(float[] dst, float[] frame, int pos)
    {
        int len = Mathf.Min(frame.Length, dst.Length - pos);
        for (int i = 0; i < len; i++)
            dst[pos + i] += frame[i];
    }

    private static float[] Hann(int N)
    {
        float[] w = new float[N];
        for (int n = 0; n < N; n++)
            w[n] = 0.5f * (1f - Mathf.Cos(2f * Mathf.PI * n / (N - 1)));
        return w;
    }

    private static float[] ResampleLinear(float[] interleaved, int channels, float speed)
    {
        if (Mathf.Approximately(speed, 1f)) return (float[])interleaved.Clone();

        int inFrames = interleaved.Length / channels;
        int outFrames = Mathf.Max(1, Mathf.FloorToInt(inFrames / speed));
        float[] outInter = new float[outFrames * channels];

        for (int ch = 0; ch < channels; ch++)
        {
            for (int i = 0; i < outFrames; i++)
            {
                float srcPos = i * speed;
                int i0 = Mathf.FloorToInt(srcPos);
                int i1 = Mathf.Min(i0 + 1, inFrames - 1);
                float t = srcPos - i0;

                float s0 = interleaved[i0 * channels + ch];
                float s1 = interleaved[i1 * channels + ch];
                float v = Mathf.Lerp(s0, s1, t);
                outInter[i * channels + ch] = v;
            }
        }

        return outInter;
    }
}
#endif
