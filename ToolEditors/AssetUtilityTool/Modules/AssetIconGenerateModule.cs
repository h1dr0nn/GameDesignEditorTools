#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using System.Collections.Generic;
using System.IO;
using System.Linq;

public static class AssetIconGenerateModule
{
    private static int size = 256;
    private static Color background = new(0, 0, 0, 0);
    private static Vector2 orbitEuler = new(20f, -30f);
    private static float padding = 1.1f;
    private static float camFov = 30f;

    private static string outputFolder = "Assets/_h1dr0n/Assets/Textures/Icons";
    private static string namePrefix = "";
    private static string nameSuffix = "_Icon";
    private static bool overwrite = true;
    private static bool importAsSprite = true;

    private static RenderMode renderMode = RenderMode.AutoLit;
    private static float exposure = 2f;

    private enum RenderMode { AutoLit, ForceUnlit }

    private static Vector2 _scroll;

    public static void DrawGUI(List<Object> assets)
    {
        using var scroll = new EditorGUILayout.ScrollViewScope(_scroll);
        _scroll = scroll.scrollPosition;

        EditorGUILayout.HelpBox("Generate icons from Prefabs currently added in the hub asset list. Adjust tone, render settings, and export folder below.", MessageType.Info);

        DrawRender();
        DrawTone();
        DrawOutput();

        GUILayout.Space(8);
        using (new EditorGUILayout.HorizontalScope())
        {
            EditorGUI.BeginDisabledGroup(assets == null || assets.Count == 0);
            if (GUILayout.Button($"Generate ({assets?.Count ?? 0})", GUILayout.Height(32)))
                Generate(assets);
            EditorGUI.EndDisabledGroup();

            if (GUILayout.Button("Open Output Folder", GUILayout.Height(32)))
            {
                var abs = AssetIconGeneratorUtils.MakeAbsolute(outputFolder);
                Directory.CreateDirectory(abs);
                EditorUtility.RevealInFinder(abs);
            }
        }
    }

    private static void DrawRender()
    {
        EditorGUILayout.LabelField("Render", EditorStyles.boldLabel);
        using (new EditorGUILayout.VerticalScope("box"))
        {
            size = EditorGUILayout.IntPopup("Icon Size", size, new[] { "128", "256", "512", "1024" }, new[] { 128, 256, 512, 1024 });
            background = EditorGUILayout.ColorField("Background", background);
            orbitEuler = EditorGUILayout.Vector2Field("Orbit (X,Y)", orbitEuler);
            padding = EditorGUILayout.Slider("Padding", padding, 1.0f, 2.0f);
            camFov = EditorGUILayout.Slider("Camera FOV", camFov, 10f, 60f);
            AssetIconRenderer.framingTweak = EditorGUILayout.Slider("Framing Tweak", AssetIconRenderer.framingTweak, 0.5f, 1.5f);
        }
    }

    private static void DrawTone()
    {
        GUILayout.Space(8);
        EditorGUILayout.LabelField("Tone", EditorStyles.boldLabel);
        using (new EditorGUILayout.VerticalScope("box"))
        {
            renderMode = (RenderMode)EditorGUILayout.EnumPopup("Render Mode", renderMode);
            exposure = EditorGUILayout.Slider("Exposure", exposure, 0.5f, 3f);
        }
    }

    private static void DrawOutput()
    {
        GUILayout.Space(8);
        EditorGUILayout.LabelField("Output", EditorStyles.boldLabel);
        using (new EditorGUILayout.VerticalScope("box"))
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                outputFolder = EditorGUILayout.TextField("Folder", outputFolder);
                if (GUILayout.Button("Pick...", GUILayout.Width(64)))
                {
                    var abs = EditorUtility.OpenFolderPanel("Select Output Folder", AssetIconGeneratorUtils.MakeAbsolute(outputFolder), "");
                    if (!string.IsNullOrEmpty(abs))
                    {
                        var rel = AssetIconGeneratorUtils.MakeRelativeToAssets(abs);
                        if (!string.IsNullOrEmpty(rel)) outputFolder = rel;
                    }
                }
            }

            namePrefix = EditorGUILayout.TextField("Name Prefix", namePrefix);
            nameSuffix = EditorGUILayout.TextField("Name Suffix", nameSuffix);
            overwrite = EditorGUILayout.ToggleLeft("Overwrite If Exists", overwrite);
            importAsSprite = EditorGUILayout.ToggleLeft("Import As Sprite", importAsSprite);
        }
    }

    private static void Generate(List<Object> assets)
    {
        var prefabs = assets.OfType<GameObject>().ToList();
        if (prefabs.Count == 0)
        {
            EditorUtility.DisplayDialog("No Prefabs Found", "The current asset list does not contain any prefabs.", "OK");
            return;
        }

        AssetIconGeneratorCore.Generate(prefabs, outputFolder, namePrefix, nameSuffix, size, background,
            orbitEuler, padding, camFov, renderMode == RenderMode.AutoLit, exposure, overwrite, importAsSprite);
    }
}
#endif
