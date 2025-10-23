#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using System.IO;
using System.Collections.Generic;

public static class AudioCompressSettingsModule
{
    private static AudioCompressionFormat compressionFormat = AudioCompressionFormat.Vorbis;
    private static float quality = 0.5f;
    private static bool forceToMono = false;
    private static bool loadInBackground = true;
    private static AudioClipLoadType loadType = AudioClipLoadType.CompressedInMemory;
    private static AudioSampleRateSetting sampleRate = AudioSampleRateSetting.PreserveSampleRate;
    private static bool preloadAudioData = true;
    private static bool overwriteSource = false;

    private static bool applyToAndroid = true;
    private static bool applyToiOS = true;
    private static bool applyToStandalone = true;

    public static void DrawGUI(List<AudioClip> clips)
    {
        using (new EditorGUILayout.VerticalScope("box"))
        {
            EditorGUILayout.LabelField("Audio Compression Settings", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox("Adjust compression, load, and quality settings for selected audio clips.", MessageType.Info);

            compressionFormat = (AudioCompressionFormat)EditorGUILayout.EnumPopup("Compression Format", compressionFormat);
            quality = EditorGUILayout.Slider("Quality", quality, 0f, 1f);
            forceToMono = EditorGUILayout.Toggle("Force To Mono", forceToMono);
            loadInBackground = EditorGUILayout.Toggle("Load In Background", loadInBackground);
            preloadAudioData = EditorGUILayout.Toggle("Preload Audio Data", preloadAudioData);
            loadType = (AudioClipLoadType)EditorGUILayout.EnumPopup("Load Type", loadType);
            sampleRate = (AudioSampleRateSetting)EditorGUILayout.EnumPopup("Sample Rate Setting", sampleRate);

            EditorGUILayout.Space(6);
            EditorGUILayout.LabelField("Apply To Platforms", EditorStyles.boldLabel);

            using (new EditorGUILayout.VerticalScope("box"))
            {
                applyToAndroid = EditorGUILayout.ToggleLeft("Android", applyToAndroid);
                applyToiOS = EditorGUILayout.ToggleLeft("iOS", applyToiOS);
                applyToStandalone = EditorGUILayout.ToggleLeft("Standalone", applyToStandalone);
            }

            EditorGUILayout.Space(6);
            overwriteSource = EditorGUILayout.ToggleLeft("Apply To Source Files", overwriteSource);

            EditorGUILayout.Space(10);
            EditorGUI.BeginDisabledGroup(clips == null || clips.Count == 0);
            if (GUILayout.Button("Apply Compression Settings", GUILayout.Height(30)))
                ProcessClips(clips);
            EditorGUI.EndDisabledGroup();
        }
    }

    private static void ProcessClips(List<AudioClip> clips)
    {
        if (clips == null || clips.Count == 0) return;

        int done = 0, skipped = 0;

        foreach (var clip in clips)
        {
            if (clip == null) continue;
            string path = AssetDatabase.GetAssetPath(clip);
            if (string.IsNullOrEmpty(path)) continue;

            try
            {
                var importer = AssetImporter.GetAtPath(path) as AudioImporter;
                if (importer == null)
                {
                    skipped++;
                    continue;
                }

                importer.forceToMono = forceToMono;
                importer.loadInBackground = loadInBackground;

                var settings = new AudioImporterSampleSettings
                {
                    compressionFormat = compressionFormat,
                    quality = quality,
                    loadType = loadType,
                    sampleRateSetting = sampleRate,
                    preloadAudioData = preloadAudioData
                };

                importer.defaultSampleSettings = settings;

                if (applyToAndroid)
                    importer.SetOverrideSampleSettings("Android", settings);

                if (applyToiOS)
                    importer.SetOverrideSampleSettings("iOS", settings);

                if (applyToStandalone)
                    importer.SetOverrideSampleSettings("Standalone", settings);

                importer.SaveAndReimport();
                done++;
            }
            catch (System.Exception ex)
            {
                Debug.LogWarning($"[AudioCompress] ⚠️ Skipped {clip.name} — error: {ex.Message}");
                skipped++;
            }
        }

        EditorUtility.DisplayDialog("Compression Settings Applied",
            $"Processed: {clips.Count}\nUpdated: {done}\nSkipped: {skipped}", "OK");

        AssetDatabase.Refresh();
    }
}
#endif
