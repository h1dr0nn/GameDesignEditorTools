#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using System.IO;
using System.Collections.Generic;

public static class AudioTrimSilenceModule
{
    private static float thresholdDb = -40f;
    private static int fadeInMs = 5, fadeOutMs = 10;
    private static bool normalize = true;
    private static string suffix = "_trim";
    private static bool writeNextToSource = true;
    private static DefaultAsset outputFolder;
    private static bool overwriteSource = false;

    public static void DrawGUI(List<AudioClip> clips)
    {
        using (new EditorGUILayout.VerticalScope("box"))
        {
            EditorGUILayout.LabelField("Trim Silence Settings", EditorStyles.boldLabel);

            thresholdDb = EditorGUILayout.Slider("Threshold (dB)", thresholdDb, -80f, 0f);
            fadeInMs = EditorGUILayout.IntField("Fade In (ms)", fadeInMs);
            fadeOutMs = EditorGUILayout.IntField("Fade Out (ms)", fadeOutMs);
            normalize = EditorGUILayout.Toggle("Normalize Peak", normalize);

            EditorGUILayout.Space(4);
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
                EditorGUILayout.HelpBox("Overwrite đang bật → suffix và folder sẽ bị bỏ qua.", MessageType.Info);
            }

            EditorGUILayout.Space(8);
            EditorGUI.BeginDisabledGroup(clips == null || clips.Count == 0);
            if (GUILayout.Button("Trim & Export All", GUILayout.Height(30)))
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
                "Bạn sắp GHI ĐÈ lên file âm thanh gốc!\nHành động này không thể hoàn tác.\n\nTiếp tục?",
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
                if (TryTrimClip(clip, out float[] trimmedData))
                {
                    if (normalize) Normalize(trimmedData);
                    ApplyFade(trimmedData, clip.frequency, clip.channels, fadeInMs, fadeOutMs);

                    string tempFile = Path.Combine(tempDir, baseName + ".wav");
                    var bytes = AudioWavUtility.Encode(trimmedData, clip.channels, clip.frequency);
                    File.WriteAllBytes(tempFile, bytes);

                    string absoluteOutPath = Path.GetFullPath(outPath);
                    File.Copy(tempFile, absoluteOutPath, true);

                    done++;
                    Debug.Log($"[AudioTrim] ✅ Saved: {absoluteOutPath}");
                }
                else skipped++;
            }
            catch (IOException ioEx)
            {
                Debug.LogWarning($"[AudioTrim] ⚠️ Bỏ qua {clip.name} — file đang bị lock hoặc sử dụng: {ioEx.Message}");
                skipped++;
            }
        }

        AssetDatabase.Refresh();

        EditorUtility.DisplayDialog("Trim Complete",
            $"Processed: {clips.Count}\nTrimmed: {done}\nSkipped: {skipped}\n\n" +
            $"{(overwriteSource ? "⚠️ Files were overwritten!" : "")}",
            "OK");
    }

    private static bool TryTrimClip(AudioClip clip, out float[] result)
    {
        result = null;
        if (clip == null) return false;

        float threshold = Mathf.Pow(10f, thresholdDb / 20f);
        int total = clip.samples * clip.channels;
        float[] data = new float[total];
        clip.GetData(data, 0);

        int start = 0, end = total - 1;
        while (start < total && Mathf.Abs(data[start]) < threshold) start++;
        while (end > start && Mathf.Abs(data[end]) < threshold) end--;

        if (end <= start) return false;

        int length = end - start + 1;
        result = new float[length];
        System.Array.Copy(data, start, result, 0, length);
        return true;
    }

    private static void Normalize(float[] data)
    {
        float peak = 0f;
        foreach (var s in data) peak = Mathf.Max(peak, Mathf.Abs(s));
        if (peak < 1e-6f) return;
        float gain = 1f / peak;
        for (int i = 0; i < data.Length; i++) data[i] *= gain;
    }

    private static void ApplyFade(float[] data, int frequency, int channels, int fadeInMs, int fadeOutMs)
    {
        int fadeInSamples = Mathf.RoundToInt(fadeInMs / 1000f * frequency) * channels;
        int fadeOutSamples = Mathf.RoundToInt(fadeOutMs / 1000f * frequency) * channels;

        for (int i = 0; i < fadeInSamples && i < data.Length; i++)
        {
            float t = i / (float)fadeInSamples;
            data[i] *= t;
        }

        for (int i = 0; i < fadeOutSamples && i < data.Length; i++)
        {
            int idx = data.Length - i - 1;
            float t = i / (float)fadeOutSamples;
            data[idx] *= (1 - t);
        }
    }
}
#endif
