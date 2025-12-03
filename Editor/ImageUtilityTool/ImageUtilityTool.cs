#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace h1dr0n.EditorTools
{
    public partial class ImageUtilityTool : EditorWindow
    {
        [SerializeField] private List<Texture2D> textures = new();
        private SerializedObject _so;
        private SerializedProperty _texturesProp;
        private Vector2 _scrollAll, _scrollTextures;

        private enum FunctionMode
        {
            CompressSetting,
            CropToGrid,
            FileSizeCompress,
            FormatConvert,
            GradientColorFinder,
            PaddingAdjustment,
            ResizeOperation
        }

        [SerializeField] private FunctionMode currentMode = FunctionMode.FileSizeCompress;

        [MenuItem("Tools/Game Design/Image Utility Tool")]
        private static void Open()
        {
            var window = GetWindow<ImageUtilityTool>();
            window.titleContent = new GUIContent("Image Utility Tool");
            window.minSize = new Vector2(540, 480);
            window.Show();
        }

        private void OnEnable()
        {
            _so = new SerializedObject(this);
            _texturesProp = _so.FindProperty("textures");
        }

        private void OnGUI()
        {
            using (var outer = new EditorGUILayout.ScrollViewScope(_scrollAll))
            {
                _scrollAll = outer.scrollPosition;

                EditorGUILayout.Space();
                EditorGUILayout.LabelField("Image Utility Tool", EditorStyles.boldLabel);
                EditorGUILayout.HelpBox("Select images from the Project window or drag and drop them into the list below. Then choose a module to process.", MessageType.Info);

                _so.Update();
                DrawTextureList();
                EditorGUILayout.Space(8);

                using (new EditorGUILayout.VerticalScope("box"))
                {
                    EditorGUILayout.LabelField("Function Mode", EditorStyles.boldLabel);
                    currentMode = (FunctionMode)EditorGUILayout.EnumPopup("Mode", currentMode);
                }

                EditorGUILayout.Space(8);
                switch (currentMode)
                {
                    case FunctionMode.FileSizeCompress:
                        ImageFileCompressModule.DrawGUI(textures);
                        break;

                    case FunctionMode.CompressSetting:
                        ImageCompressSettingModule.DrawGUI(textures);
                        break;

                    case FunctionMode.CropToGrid:
                        ImageCropGridModule.DrawGUI(textures);
                        break;

                    case FunctionMode.ResizeOperation:
                        ImageResizeModule.DrawGUI(textures);
                        break;

                    case FunctionMode.FormatConvert:
                        ImageFormatConvertModule.DrawGUI(textures);
                        break;

                    case FunctionMode.PaddingAdjustment:
                        ImagePaddingModule.DrawGUI(textures);
                        break;

                    case FunctionMode.GradientColorFinder:
                        ImageGradientColorModule.DrawGUI(textures);
                        break;

                    default:
                        EditorGUILayout.HelpBox("Module not yet implemented.", MessageType.Info);
                        break;
                }

                _so.ApplyModifiedProperties();
            }
        }

        private void DrawTextureList()
        {
            using (new EditorGUILayout.VerticalScope("box"))
            {
                EditorGUILayout.LabelField("Textures", EditorStyles.boldLabel);

                using (var sv = new EditorGUILayout.ScrollViewScope(_scrollTextures, GUILayout.MinHeight(160)))
                {
                    _scrollTextures = sv.scrollPosition;
                    EditorGUILayout.PropertyField(_texturesProp, includeChildren: true);
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
                        textures.RemoveAll(s => s == null);

                    if (GUILayout.Button("Clear All", GUILayout.Width(buttonWidth), GUILayout.Height(buttonHeight)))
                        textures.Clear();
                    GUILayout.FlexibleSpace();
                }
            }
        }

        private void AddFromSelection()
        {
            foreach (var obj in Selection.objects)
            {
                if (obj is Texture2D tex && !textures.Contains(tex))
                    textures.Add(tex);
            }
        }

        private void AddFromFolderSelection()
        {
            var selected = Selection.objects;
            if (selected == null || selected.Length == 0)
            {
                EditorUtility.DisplayDialog("No Folder Selected", "Please select at least one folder in the Project window first.", "OK");
                return;
            }

            int added = 0;
            foreach (var obj in selected)
            {
                string path = AssetDatabase.GetAssetPath(obj);
                if (string.IsNullOrEmpty(path)) continue;

                if (Directory.Exists(path))
                {
                    string[] files = Directory.GetFiles(path, "*.*", SearchOption.AllDirectories);
                    foreach (var file in files)
                    {
                        string ext = Path.GetExtension(file).ToLowerInvariant();
                        if (IsImageFile(ext))
                        {
                            var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(file);
                            if (tex != null && !textures.Contains(tex))
                            {
                                textures.Add(tex);
                                added++;
                            }
                        }
                    }
                }
            }

            if (added > 0)
                Debug.Log($"[ImageUtilityTool] Added {added} textures from selected folder(s).");
            else
                EditorUtility.DisplayDialog("No Image Found", "No valid image files were found in the selected folder(s).", "OK");
        }

        private static bool IsImageFile(string ext)
        {
            switch (ext)
            {
                case ".png":
                case ".jpg":
                case ".jpeg":
                case ".tga":
                case ".bmp":
                case ".tiff":
                case ".psd":
                    return true;
                default:
                    return false;
            }
        }
    }
}
#endif
