#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using System.IO;
using System.Collections.Generic;

public static class AudioSilencePaddingModule
{
    private static int padStartMs = 200;
    private static int padEndMs = 200;
    private static string suffix = "_pad";
    private static bool writeNextToSource = true;
    private static DefaultAsset outputFolder;
    private static bool overwriteSource = false;

    public static void DrawGUI(List<AudioClip> clips)
    {
        using (new EditorGUILayout.VerticalScope("box"))
        {
            EditorGUILayout.LabelField("Silence Padding Settings", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox("Add silence (in milliseconds) to the start and/or end of audio clips.", MessageType.Info);

            padStartMs = EditorGUILayout.IntSlider("Pad Start (ms)", padStartMs, 0, 2000);
            padEndMs = EditorGUILayout.IntSlider("Pad End (ms)", padEndMs, 0, 2000);

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
                EditorGUILayout.HelpBox("Overwrite is enabled — suffix and folder will be ignored.", MessageType.Warning);
            }

            EditorGUILayout.Space(8);
            EditorGUI.BeginDisabledGroup(clips == null || clips.Count == 0);
            if (GUILayout.Button("Apply Padding", GUILayout.Height(30)))
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
                int total = clip.samples * clip.channels;
                float[] data = new float[total];
                clip.GetData(data, 0);

                int padStartSamples = Mathf.RoundToInt(padStartMs / 1000f * clip.frequency) * clip.channels;
                int padEndSamples = Mathf.RoundToInt(padEndMs / 1000f * clip.frequency) * clip.channels;
                float[] padded = new float[padStartSamples + total + padEndSamples];

                System.Array.Copy(data, 0, padded, padStartSamples, total);

                string tempFile = Path.Combine(tempDir, baseName + ".wav");
                var bytes = AudioWavUtility.Encode(padded, clip.channels, clip.frequency);
                File.WriteAllBytes(tempFile, bytes);

                string absoluteOutPath = Path.GetFullPath(outPath);
                File.Copy(tempFile, absoluteOutPath, true);

                done++;
                Debug.Log($"[AudioPad] ✅ Saved: {absoluteOutPath}");
            }
            catch (IOException ioEx)
            {
                Debug.LogWarning($"[AudioPad] ⚠️ Skipped {clip.name} — file locked or in use: {ioEx.Message}");
                skipped++;
            }
            catch (System.Exception ex)
            {
                Debug.LogWarning($"[AudioPad] ⚠️ Error processing {clip.name}: {ex.Message}");
                skipped++;
            }
        }

        AssetDatabase.Refresh();

        EditorUtility.DisplayDialog("Padding Complete",
            $"Processed: {clips.Count}\nPadded: {done}\nSkipped: {skipped}\n\n" +
            $"{(overwriteSource ? "⚠️ Files were overwritten!" : string.Empty)}",
            "OK");
    }
}
#endif
