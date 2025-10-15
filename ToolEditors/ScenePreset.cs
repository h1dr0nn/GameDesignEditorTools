#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public class ScenePresetTool : EditorWindow
{
    private const string kPrefsKeyLastLibrary = "ScenePresetTool_LastLibraryPath";
    private const string kPrefsKeyLastIndex = "ScenePresetTool_LastPresetIndex";

    [SerializeField] private ScenePresetDataSO library;
    [SerializeField] private int selectedIndex = -1;

    private Vector2 _scrollAll;
    private Vector2 _scrollScenes;

    private string newLibraryName = "ScenePresetLibrary";
    private string newPresetName = "New Preset";

    [MenuItem("Tools/Game Design/Scene Preset Tool")]
    public static void Open() => GetWindow<ScenePresetTool>("Scene Preset Tool");

    private void OnEnable()
    {
        var libPath = EditorPrefs.GetString(kPrefsKeyLastLibrary, "");
        if (!string.IsNullOrEmpty(libPath))
        {
            var obj = AssetDatabase.LoadAssetAtPath<ScenePresetDataSO>(libPath);
            if (obj != null) library = obj;
        }
        selectedIndex = EditorPrefs.GetInt(kPrefsKeyLastIndex, -1);
        if (library == null) selectedIndex = -1;
        if (library != null && (selectedIndex < 0 || selectedIndex >= library.presets.Count)) selectedIndex = library.presets.Count > 0 ? 0 : -1;
    }

    private void OnGUI()
    {
        using (var outer = new EditorGUILayout.ScrollViewScope(_scrollAll))
        {
            _scrollAll = outer.scrollPosition;

            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                EditorGUILayout.LabelField("Library", EditorStyles.boldLabel);
                var newLib = (ScenePresetDataSO)EditorGUILayout.ObjectField("Asset", library, typeof(ScenePresetDataSO), false);
                if (newLib != library)
                {
                    library = newLib;
                    PersistLibraryPath(library);
                    if (library == null) selectedIndex = -1;
                    else if (library.presets.Count > 0) selectedIndex = Mathf.Clamp(selectedIndex, 0, library.presets.Count - 1);
                    else selectedIndex = -1;
                }

                using (new EditorGUILayout.HorizontalScope())
                {
                    if (GUILayout.Button("Create Library", GUILayout.Height(22)))
                    {
                        var path = EditorUtility.SaveFilePanelInProject("Create Scene Preset Library", string.IsNullOrWhiteSpace(newLibraryName) ? "ScenePresetLibrary" : newLibraryName, "asset", "");
                        if (!string.IsNullOrEmpty(path))
                        {
                            var lib = ScriptableObject.CreateInstance<ScenePresetDataSO>();
                            AssetDatabase.CreateAsset(lib, path);
                            AssetDatabase.SaveAssets();
                            AssetDatabase.Refresh();
                            library = lib;
                            selectedIndex = -1;
                            PersistLibraryPath(library);
                            Repaint();
                        }
                    }

                    if (GUILayout.Button("Find Libraries", GUILayout.Height(22)))
                    {
                        var libs = FindAllLibraries();
                        var names = new List<string>();
                        foreach (var l in libs) names.Add(l.name);
                        if (libs.Count == 0) EditorUtility.DisplayDialog("Scene Preset Tool", "No ScenePresetDataSO assets found.", "OK");
                        else
                        {
                            var idx = EditorUtility.DisplayDialogComplex("Select Library", "Pick a library to load.", libs[0].name, libs.Count > 1 ? libs[1].name : "Cancel", "Cancel");
                            if (idx == 0) { library = libs[0]; }
                            else if (idx == 1 && libs.Count > 1) { library = libs[1]; }
                            if (library != null)
                            {
                                PersistLibraryPath(library);
                                selectedIndex = library.presets.Count > 0 ? 0 : -1;
                            }
                        }
                    }

                    if (library != null)
                    {
                        if (GUILayout.Button("Ping", GUILayout.Width(64), GUILayout.Height(22))) EditorGUIUtility.PingObject(library);
                        if (GUILayout.Button("Select", GUILayout.Width(64), GUILayout.Height(22))) Selection.activeObject = library;
                    }
                }
            }

            EditorGUILayout.Space(6);

            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                EditorGUILayout.LabelField("Presets", EditorStyles.boldLabel);

                if (library == null)
                {
                    EditorGUILayout.HelpBox("Assign or create a ScenePresetDataSO.", MessageType.Info);
                }
                else
                {
                    var so = new SerializedObject(library);
                    var presetsProp = so.FindProperty("presets");

                    using (new EditorGUILayout.HorizontalScope())
                    {
                        if (GUILayout.Button("Add Preset", GUILayout.Height(22)))
                        {
                            presetsProp.InsertArrayElementAtIndex(presetsProp.arraySize);
                            var item = presetsProp.GetArrayElementAtIndex(presetsProp.arraySize - 1);
                            item.FindPropertyRelative("name").stringValue = string.IsNullOrWhiteSpace(newPresetName) ? "New Preset" : newPresetName;
                            var scenes = item.FindPropertyRelative("scenes");
                            scenes.ClearArray();
                            so.ApplyModifiedProperties();
                            selectedIndex = presetsProp.arraySize - 1;
                            PersistSelectedIndex();
                        }

                        using (new EditorGUI.DisabledScope(selectedIndex < 0 || selectedIndex >= presetsProp.arraySize))
                        {
                            if (GUILayout.Button("Duplicate", GUILayout.Height(22)))
                            {
                                presetsProp.InsertArrayElementAtIndex(selectedIndex + 1);
                                var src = presetsProp.GetArrayElementAtIndex(selectedIndex);
                                var dst = presetsProp.GetArrayElementAtIndex(selectedIndex + 1);
                                dst.FindPropertyRelative("name").stringValue = src.FindPropertyRelative("name").stringValue + " Copy";
                                var srcScenes = src.FindPropertyRelative("scenes");
                                var dstScenes = dst.FindPropertyRelative("scenes");
                                dstScenes.ClearArray();
                                for (int i = 0; i < srcScenes.arraySize; i++)
                                {
                                    dstScenes.InsertArrayElementAtIndex(i);
                                    dstScenes.GetArrayElementAtIndex(i).objectReferenceValue = srcScenes.GetArrayElementAtIndex(i).objectReferenceValue;
                                }
                                so.ApplyModifiedProperties();
                                selectedIndex = selectedIndex + 1;
                                PersistSelectedIndex();
                            }

                            if (GUILayout.Button("Remove", GUILayout.Height(22)))
                            {
                                presetsProp.DeleteArrayElementAtIndex(selectedIndex);
                                so.ApplyModifiedProperties();
                                selectedIndex = Mathf.Min(selectedIndex, presetsProp.arraySize - 1);
                                PersistSelectedIndex();
                            }

                            if (GUILayout.Button("▲", GUILayout.Width(28), GUILayout.Height(22)))
                            {
                                if (selectedIndex > 0)
                                {
                                    presetsProp.MoveArrayElement(selectedIndex, selectedIndex - 1);
                                    so.ApplyModifiedProperties();
                                    selectedIndex--;
                                    PersistSelectedIndex();
                                }
                            }

                            if (GUILayout.Button("▼", GUILayout.Width(28), GUILayout.Height(22)))
                            {
                                if (selectedIndex >= 0 && selectedIndex < presetsProp.arraySize - 1)
                                {
                                    presetsProp.MoveArrayElement(selectedIndex, selectedIndex + 1);
                                    so.ApplyModifiedProperties();
                                    selectedIndex++;
                                    PersistSelectedIndex();
                                }
                            }
                        }
                    }

                    EditorGUILayout.Space(4);

                    var names = new List<string>();
                    for (int i = 0; i < presetsProp.arraySize; i++)
                        names.Add(presetsProp.GetArrayElementAtIndex(i).FindPropertyRelative("name").stringValue);
                    int newIdx = presetsProp.arraySize == 0 ? -1 : Mathf.Clamp(selectedIndex, 0, presetsProp.arraySize - 1);
                    int shown = presetsProp.arraySize == 0 ? 0 : EditorGUILayout.Popup("Select", newIdx, names.ToArray());
                    if (presetsProp.arraySize == 0) selectedIndex = -1;
                    else if (shown != selectedIndex) { selectedIndex = shown; PersistSelectedIndex(); }

                    using (new EditorGUI.DisabledScope(selectedIndex < 0 || selectedIndex >= presetsProp.arraySize))
                    {
                        if (selectedIndex >= 0 && selectedIndex < presetsProp.arraySize)
                        {
                            var item = presetsProp.GetArrayElementAtIndex(selectedIndex);
                            var nameProp = item.FindPropertyRelative("name");
                            nameProp.stringValue = EditorGUILayout.TextField("Preset Name", nameProp.stringValue);

                            EditorGUILayout.Space(4);
                            EditorGUILayout.LabelField("Scenes", EditorStyles.miniBoldLabel);

                            var scenesProp = item.FindPropertyRelative("scenes");
                            using (var sv = new EditorGUILayout.ScrollViewScope(_scrollScenes, GUILayout.MinHeight(140)))
                            {
                                _scrollScenes = sv.scrollPosition;

                                for (int i = 0; i < scenesProp.arraySize; i++)
                                {
                                    using (new EditorGUILayout.HorizontalScope())
                                    {
                                        var el = scenesProp.GetArrayElementAtIndex(i);
                                        EditorGUILayout.PropertyField(el, GUIContent.none);
                                        if (GUILayout.Button("▲", GUILayout.Width(24))) { if (i > 0) scenesProp.MoveArrayElement(i, i - 1); }
                                        if (GUILayout.Button("▼", GUILayout.Width(24))) { if (i < scenesProp.arraySize - 1) scenesProp.MoveArrayElement(i, i + 1); }
                                        if (GUILayout.Button("X", GUILayout.Width(24))) { scenesProp.DeleteArrayElementAtIndex(i); break; }
                                    }
                                }
                            }

                            using (new EditorGUILayout.HorizontalScope())
                            {
                                if (GUILayout.Button("Add Scene", GUILayout.Height(22)))
                                {
                                    scenesProp.InsertArrayElementAtIndex(scenesProp.arraySize);
                                    scenesProp.GetArrayElementAtIndex(scenesProp.arraySize - 1).objectReferenceValue = null;
                                }

                                if (GUILayout.Button("Add From Selection", GUILayout.Height(22)))
                                {
                                    foreach (var o in Selection.objects)
                                    {
                                        if (o is SceneAsset sa)
                                        {
                                            scenesProp.InsertArrayElementAtIndex(scenesProp.arraySize);
                                            scenesProp.GetArrayElementAtIndex(scenesProp.arraySize - 1).objectReferenceValue = sa;
                                        }
                                    }
                                }

                                if (GUILayout.Button("Remove Nulls", GUILayout.Height(22)))
                                {
                                    for (int i = scenesProp.arraySize - 1; i >= 0; i--)
                                    {
                                        if (scenesProp.GetArrayElementAtIndex(i).objectReferenceValue == null)
                                            scenesProp.DeleteArrayElementAtIndex(i);
                                    }
                                }

                                if (GUILayout.Button("Save Library", GUILayout.Height(22)))
                                {
                                    so.ApplyModifiedProperties();
                                    EditorUtility.SetDirty(library);
                                    AssetDatabase.SaveAssets();
                                    AssetDatabase.Refresh();
                                }
                            }
                        }
                    }

                    so.ApplyModifiedProperties();
                }
            }

            EditorGUILayout.Space(10);

            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                EditorGUILayout.LabelField("Load Preset", EditorStyles.boldLabel);
                using (new EditorGUI.DisabledScope(library == null || selectedIndex < 0 || selectedIndex >= (library != null ? library.presets.Count : 0)))
                {
                    if (GUILayout.Button("Load Preset Scenes (Replace All)", GUILayout.Height(28)))
                    {
                        LoadPreset();
                    }
                }
            }
        }
    }

    private void LoadPreset()
    {
        if (library == null) return;
        if (selectedIndex < 0 || selectedIndex >= library.presets.Count) return;
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

        var item = library.presets[selectedIndex];
        var scenePaths = new List<string>();
        foreach (var s in item.scenes)
        {
            if (s == null) continue;
            var path = AssetDatabase.GetAssetPath(s);
            if (string.IsNullOrEmpty(path)) continue;
            scenePaths.Add(path);
        }
        if (scenePaths.Count == 0)
        {
            EditorUtility.DisplayDialog("Scene Preset", "Preset has no valid scenes.", "OK");
            return;
        }

        var firstScene = EditorSceneManager.OpenScene(scenePaths[0], OpenSceneMode.Single);
        EditorSceneManager.SetActiveScene(firstScene);
        for (int i = 1; i < scenePaths.Count; i++) EditorSceneManager.OpenScene(scenePaths[i], OpenSceneMode.Additive);
        var firstAsset = AssetDatabase.LoadAssetAtPath<SceneAsset>(scenePaths[0]);
        if (firstAsset != null) EditorGUIUtility.PingObject(firstAsset);
    }

    private List<ScenePresetDataSO> FindAllLibraries()
    {
        var list = new List<ScenePresetDataSO>();
        var guids = AssetDatabase.FindAssets("t:ScenePresetDataSO");
        foreach (var g in guids)
        {
            var path = AssetDatabase.GUIDToAssetPath(g);
            var p = AssetDatabase.LoadAssetAtPath<ScenePresetDataSO>(path);
            if (p != null) list.Add(p);
        }
        list.Sort((a, b) => string.Compare(a.name, b.name, System.StringComparison.OrdinalIgnoreCase));
        return list;
    }

    private void PersistLibraryPath(ScenePresetDataSO lib)
    {
        var path = lib == null ? "" : AssetDatabase.GetAssetPath(lib);
        EditorPrefs.SetString(kPrefsKeyLastLibrary, path);
    }

    private void PersistSelectedIndex()
    {
        EditorPrefs.SetInt(kPrefsKeyLastIndex, selectedIndex);
    }
}
#endif
