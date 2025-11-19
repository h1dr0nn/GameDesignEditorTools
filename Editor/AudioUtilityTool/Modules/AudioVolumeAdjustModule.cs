#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using System.IO;
using System.Collections.Generic;

namespace h1dr0n.EditorTools
{
    public static class AudioVolumeAdjustModule
    {
        private static float gainDb = 0f;
        private static string suffix = "_gain";
        private static bool writeNextToSource = true;
        private static DefaultAsset outputFolder;
        private static bool overwriteSource = false;

        public static void DrawGUI(List<AudioClip> clips)
        {
            using (new EditorGUILayout.VerticalScope("box"))
            {
                EditorGUILayout.LabelField("Volume Adjust (Gain)", EditorStyles.boldLabel);
                EditorGUILayout.HelpBox("Adjust overall volume of selected audio clips by applying gain (in decibels).", MessageType.Info);

                gainDb = EditorGUILayout.Slider("Gain (dB)", gainDb, -24f, 24f);

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
                if (GUILayout.Button("Apply Gain", GUILayout.Height(30)))
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
            float gain = Mathf.Pow(10f, gainDb / 20f);
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

                    for (int i = 0; i < data.Length; i++)
                        data[i] = Mathf.Clamp(data[i] * gain, -1f, 1f);

                    string tempFile = Path.Combine(tempDir, baseName + ".wav");
                    var bytes = AudioWavUtility.Encode(data, clip.channels, clip.frequency);
                    File.WriteAllBytes(tempFile, bytes);

                    string absoluteOutPath = Path.GetFullPath(outPath);
                    File.Copy(tempFile, absoluteOutPath, true);

                    done++;
                    Debug.Log($"[AudioGain] ✅ Saved: {absoluteOutPath}");
                }
                catch (IOException ioEx)
                {
                    Debug.LogWarning($"[AudioGain] ⚠️ Skipped {clip.name} — file locked or in use: {ioEx.Message}");
                    skipped++;
                }
                catch (System.Exception ex)
                {
                    Debug.LogWarning($"[AudioGain] ⚠️ Error processing {clip.name}: {ex.Message}");
                    skipped++;
                }
            }

            AssetDatabase.Refresh();

            EditorUtility.DisplayDialog("Gain Adjustment Complete",
                $"Processed: {clips.Count}\nAdjusted: {done}\nSkipped: {skipped}\n\n" +
                $"{(overwriteSource ? "⚠️ Files were overwritten!" : string.Empty)}",
                "OK");
        }
    }
}
#endif
