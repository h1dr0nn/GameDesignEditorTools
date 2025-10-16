#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using System.Collections.Generic;
using System.IO;

public partial class AudioUtilityTool : EditorWindow
{
    [SerializeField] private List<AudioClip> audioClips = new();
    private SerializedObject _so;
    private SerializedProperty _clipsProp;
    private Vector2 _scrollAll, _scrollClips;

    private enum FunctionMode
    {
        CompressSettings,
        LoudnessMatch,
        NormalizeVolume,
        PeakAnalyzer,
        SilencePadding,
        StretchAndPitch,
        TrimSilence,
        VolumeAdjust,
        WaveformVisualizer
    }

    [SerializeField] private FunctionMode currentMode = FunctionMode.CompressSettings;

    [MenuItem("Tools/Game Design/Audio Utility Tool")]
    private static void Open()
    {
        var window = GetWindow<AudioUtilityTool>();
        window.titleContent = new GUIContent("Audio Utility Tool");
        window.minSize = new Vector2(540, 480);
        window.Show();
    }

    private void OnEnable()
    {
        _so = new SerializedObject(this);
        _clipsProp = _so.FindProperty("audioClips");
    }

    private void OnGUI()
    {
        using (var outer = new EditorGUILayout.ScrollViewScope(_scrollAll))
        {
            _scrollAll = outer.scrollPosition;

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Audio Utility Tool", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox("Drag AudioClips from Project into the list, or add from selection/folder.", MessageType.Info);

            _so.Update();
            DrawClipList();
            EditorGUILayout.Space(8);

            using (new EditorGUILayout.VerticalScope("box"))
            {
                EditorGUILayout.LabelField("Function Mode", EditorStyles.boldLabel);
                currentMode = (FunctionMode)EditorGUILayout.EnumPopup("Mode", currentMode);
            }

            EditorGUILayout.Space(8);
            switch (currentMode)
            {
                case FunctionMode.TrimSilence:
                    AudioTrimSilenceModule.DrawGUI(audioClips);
                    break;

                case FunctionMode.NormalizeVolume:
                    AudioNormalizeVolumeModule.DrawGUI(audioClips);
                    break;

                case FunctionMode.VolumeAdjust:
                    AudioVolumeAdjustModule.DrawGUI(audioClips);
                    break;

                case FunctionMode.SilencePadding:
                    AudioSilencePaddingModule.DrawGUI(audioClips);
                    break;

                case FunctionMode.CompressSettings:
                    AudioCompressSettingsModule.DrawGUI(audioClips);
                    break;

                case FunctionMode.PeakAnalyzer:
                    AudioPeakAnalyzerModule.DrawGUI(audioClips);
                    break;

                case FunctionMode.LoudnessMatch:
                    AudioLoudnessMatchModule.DrawGUI(audioClips);
                    break;

                case FunctionMode.WaveformVisualizer:
                    AudioWaveformVisualizerModule.DrawGUI(audioClips);
                    break;

                case FunctionMode.StretchAndPitch:
                    AudioStretchPitchModule.DrawGUI(audioClips);
                    break;

                default:
                    EditorGUILayout.HelpBox("Module chưa được cài đặt.", MessageType.Info);
                    break;
            }

            _so.ApplyModifiedProperties();
        }
    }

    private void DrawClipList()
    {
        using (new EditorGUILayout.VerticalScope("box"))
        {
            EditorGUILayout.LabelField("Audio Clips", EditorStyles.boldLabel);

            using (var sv = new EditorGUILayout.ScrollViewScope(_scrollClips, GUILayout.MinHeight(160)))
            {
                _scrollClips = sv.scrollPosition;
                EditorGUILayout.PropertyField(_clipsProp, includeChildren: true);
            }

            float buttonWidth = (EditorGUIUtility.currentViewWidth - 80f) / 2f;
            float buttonHeight = 24f;

            GUILayout.Space(4);

            using (new EditorGUILayout.HorizontalScope())
            {
                GUILayout.FlexibleSpace();
                if (GUILayout.Button("Add From Selection", GUILayout.Width(buttonWidth), GUILayout.Height(buttonHeight)))
                    AddFromSelection();

                if (GUILayout.Button("Add From Folder Selection", GUILayout.Width(buttonWidth), GUILayout.Height(buttonHeight)))
                    AddFromFolderSelection();
                GUILayout.FlexibleSpace();
            }

            using (new EditorGUILayout.HorizontalScope())
            {
                GUILayout.FlexibleSpace();
                if (GUILayout.Button("Remove Nulls", GUILayout.Width(buttonWidth), GUILayout.Height(buttonHeight)))
                    audioClips.RemoveAll(s => s == null);

                if (GUILayout.Button("Clear All", GUILayout.Width(buttonWidth), GUILayout.Height(buttonHeight)))
                    audioClips.Clear();
                GUILayout.FlexibleSpace();
            }


        }
    }

    private void AddFromSelection()
    {
        foreach (var obj in Selection.objects)
        {
            if (obj is AudioClip clip && !audioClips.Contains(clip))
                audioClips.Add(clip);
        }
    }

    private void AddFromFolderSelection()
    {
        var selected = Selection.objects;
        if (selected == null || selected.Length == 0)
        {
            EditorUtility.DisplayDialog("No Folder Selected", "Hãy chọn ít nhất 1 thư mục trong Project trước.", "OK");
            return;
        }

        int added = 0;
        foreach (var obj in selected)
        {
            string path = AssetDatabase.GetAssetPath(obj);
            if (string.IsNullOrEmpty(path)) continue;

            string absFolder = MakeAbsolutePath(path);
            if (!Directory.Exists(absFolder)) continue;

            string[] files = Directory.GetFiles(absFolder, "*.*", SearchOption.AllDirectories);
            foreach (var file in files)
            {
                string ext = Path.GetExtension(file).ToLowerInvariant();
                if (!IsAudioFile(ext)) continue;

                string assetPath = MakeAssetRelativePath(file);
                if (string.IsNullOrEmpty(assetPath)) continue;

                var clip = AssetDatabase.LoadAssetAtPath<AudioClip>(assetPath);
                if (clip != null && !audioClips.Contains(clip))
                {
                    audioClips.Add(clip);
                    added++;
                }
            }
        }

        if (added > 0)
            Debug.Log($"[AudioUtilityTool] Added {added} clips from selected folder(s).");
        else
            EditorUtility.DisplayDialog("No Audio Found", "Không tìm thấy file âm thanh nào trong thư mục đã chọn.", "OK");
    }

    private static string MakeAbsolutePath(string projectRelative)
    {
        if (string.IsNullOrEmpty(projectRelative)) return string.Empty;
        var root = Application.dataPath.Substring(0, Application.dataPath.Length - "Assets".Length);
        return Path.GetFullPath(Path.Combine(root, projectRelative)).Replace('\\', '/');
    }

    private static string MakeAssetRelativePath(string anyPath)
    {
        if (string.IsNullOrEmpty(anyPath)) return null;
        var normalized = Path.GetFullPath(anyPath).Replace('\\', '/');
        var root = Application.dataPath.Substring(0, Application.dataPath.Length - "Assets".Length).Replace('\\', '/');
        if (!normalized.StartsWith(root))
            return normalized.StartsWith("Assets/") ? normalized : null;

        var relative = normalized.Substring(root.Length).TrimStart('/');
        return relative.StartsWith("Assets") ? relative : null;
    }

    private static bool IsAudioFile(string ext)
    {
        switch (ext)
        {
            case ".wav":
            case ".ogg":
            case ".mp3":
            case ".aiff":
            case ".aif":
            case ".flac":
            case ".aac":
                return true;
            default:
                return false;
        }
    }
}
#endif
