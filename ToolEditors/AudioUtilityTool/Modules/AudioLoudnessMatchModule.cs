#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using System.IO;
using System.Collections.Generic;

namespace h1dr0n.EditorTools
{
    public static class AudioLoudnessMatchModule
    {
        private static float targetLUFS = -14f;
        private static string suffix = "_lufs";
        private static bool writeNextToSource = true;
        private static DefaultAsset outputFolder;
        private static bool overwriteSource = false;

        public static void DrawGUI(List<AudioClip> clips)
        {
            using (new EditorGUILayout.VerticalScope("box"))
            {
                EditorGUILayout.LabelField("Loudness Match (LUFS)", EditorStyles.boldLabel);
                EditorGUILayout.HelpBox("Normalize loudness levels of selected audio clips to a target LUFS value.", MessageType.Info);

                targetLUFS = EditorGUILayout.Slider("Target LUFS", targetLUFS, -30f, -8f);

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
                if (GUILayout.Button("Match Loudness (LUFS)", GUILayout.Height(30)))
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

                    float currentLUFS = EstimateLUFS(data);
                    float gainDb = targetLUFS - currentLUFS;
                    float gain = Mathf.Pow(10f, gainDb / 20f);

                    for (int i = 0; i < data.Length; i++)
                        data[i] = Mathf.Clamp(data[i] * gain, -1f, 1f);

                    string tempFile = Path.Combine(tempDir, baseName + ".wav");
                    var bytes = AudioWavUtility.Encode(data, clip.channels, clip.frequency);
                    File.WriteAllBytes(tempFile, bytes);

                    string absoluteOutPath = Path.GetFullPath(outPath);
                    File.Copy(tempFile, absoluteOutPath, true);

                    done++;
                    Debug.Log($"[AudioLUFS] ✅ Saved: {absoluteOutPath}");
                }
                catch (IOException ioEx)
                {
                    Debug.LogWarning($"[AudioLUFS] ⚠️ Skipped {clip.name} — file locked or in use: {ioEx.Message}");
                    skipped++;
                }
                catch (System.Exception ex)
                {
                    Debug.LogWarning($"[AudioLUFS] ⚠️ Error processing {clip.name}: {ex.Message}");
                    skipped++;
                }
            }

            AssetDatabase.Refresh();

            EditorUtility.DisplayDialog("Loudness Match Complete",
                $"Processed: {clips.Count}\nMatched: {done}\nSkipped: {skipped}\n\n" +
                $"{(overwriteSource ? "⚠️ Files were overwritten!" : string.Empty)}",
                "OK");
        }

        private static float EstimateLUFS(float[] samples)
        {
            double sumSq = 0;
            for (int i = 0; i < samples.Length; i++)
                sumSq += samples[i] * samples[i];

            double rms = Mathf.Sqrt((float)(sumSq / samples.Length));
            return 20f * Mathf.Log10((float)rms + 1e-6f) - 0.691f;
        }
    }
}
#endif
