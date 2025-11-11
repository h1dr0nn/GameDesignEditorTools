#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;

public interface ILevelGeneratorModule
{
    public void DrawGUI(float viewWidth);
}

public class LevelGeneratorWindow : EditorWindow
{
    private enum GeneratorMode
    {
        GenerateBaseLevel,
        GenerateGridLevel
    }

    private GeneratorMode currentMode = GeneratorMode.GenerateBaseLevel;

    private ILevelGeneratorModule baseModule;
    private ILevelGeneratorModule gridModule;

    private Vector2 scrollPos;

    [MenuItem("Tools/Game Design/Level Generator")]
    public static void Open()
    {
        var window = GetWindow<LevelGeneratorWindow>();
        window.titleContent = new GUIContent("Level Generator");
        window.minSize = new Vector2(640, 560);
        window.Show();
    }

    private void OnEnable()
    {
        baseModule = new BaseLevelModule();
        gridModule = new GridLevelModule();
    }

    private void OnGUI()
    {
        using (var scroll = new EditorGUILayout.ScrollViewScope(scrollPos))
        {
            scrollPos = scroll.scrollPosition;

            EditorGUILayout.Space(4);
            EditorGUILayout.LabelField("Level Generator Tool", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox("Switch between generation modes to build or analyze level data.", MessageType.Info);

            using (new EditorGUILayout.VerticalScope("box"))
            {
                EditorGUILayout.LabelField("Mode", EditorStyles.boldLabel);
                currentMode = (GeneratorMode)EditorGUILayout.EnumPopup("Generator Mode", currentMode);
            }

            EditorGUILayout.Space(4);

            switch (currentMode)
            {
                case GeneratorMode.GenerateBaseLevel:
                    (baseModule as BaseLevelModule)?.DrawGUI(position.width);
                    break;
                case GeneratorMode.GenerateGridLevel:
                    (gridModule as GridLevelModule)?.DrawGUI(position.width);

                    break;
            }
        }
    }
}
#endif
