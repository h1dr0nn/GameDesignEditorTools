#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using System.Collections.Generic;

public class BaseLevelModule : ILevelGeneratorModule
{
    private ListRowDataLoader loader = new();
    private LevelResultCsvExporter exporter = new();

    private Vector2 scroll;
    private LevelGenInput lastInput;
    private LevelGenResult lastResult;
    private List<int> cacheIds = new();
    private List<ResultTopicInfo> previewList = new();

    private int level = 1;
    private RewardType rewardType = RewardType.Coin;
    private int rewardAmount = 10;
    private LevelType levelType = LevelType.Normal;
    private int totalTopics = 6;
    private int topicShow = 4;
    private bool isDifferentCategory = true;
    private int defaultPositions = 4;
    private List<string> colorList = new List<string> 
    {
        "#FF9FB5", "#FFD666", "#A8F0A8", "#6FD3FF", "#B399FF", "#FF91B5",
        "#FFC266", "#B9FEA5", "#66E5F0", "#CC8EFF", "#FFF066", "#99A9FF",
        "#FF9966", "#B3998F", "#D6F56A", "#E6B8FF", "#C8F04A", "#89C7FF",
        "#FFD699", "#9ED6FF", "#D2A4F0", "#9AF5BB", "#FFB0A6", "#FFE866"
    };

    private TextAsset csvInput;
    private TextAsset csvOutput;

    public void DrawGUI(float viewWidth)
    {
        if (csvInput == null)
            csvInput = Resources.Load<TextAsset>("BlueprintData/ListRowData");

        if (csvOutput == null)
            csvOutput = Resources.Load<TextAsset>("BlueprintData/LevelNormalData");

        using (new EditorGUILayout.VerticalScope("box"))
        {
            EditorGUILayout.LabelField("Data Source", EditorStyles.boldLabel);
            csvInput = (TextAsset)EditorGUILayout.ObjectField("CSV Source (Topic)", csvInput, typeof(TextAsset), false);
            csvOutput = (TextAsset)EditorGUILayout.ObjectField("CSV Output (Result)", csvOutput, typeof(TextAsset), false);

            if (GUILayout.Button("Load CSV Data", GUILayout.Height(26)))
                loader.Load(csvInput);

            if (loader.TopicsCount > 0)
                EditorGUILayout.LabelField($"Loaded Topics: {loader.TopicsCount}");
            else
                EditorGUILayout.HelpBox("No data loaded. Please select and load a valid topic CSV.", MessageType.Warning);
        }

        using (new EditorGUILayout.VerticalScope("box"))
        {
            EditorGUILayout.LabelField("Generate Configuration", EditorStyles.boldLabel);
            level = EditorGUILayout.IntField("Level", level);
            rewardType = (RewardType)EditorGUILayout.EnumPopup("Reward Type", rewardType);
            rewardAmount = EditorGUILayout.IntField("Reward Amount", rewardAmount);
            levelType = EditorEnumToggles.Toolbar("Level Type", levelType);
            totalTopics = EditorGUILayout.IntField("Total Topics", totalTopics);
            topicShow = EditorGUILayout.IntField("Topic Show", topicShow);
            isDifferentCategory = EditorGUILayout.Toggle("Different Category", isDifferentCategory);
            defaultPositions = EditorGUILayout.IntField("Default Positions", defaultPositions);
        }

        using (new EditorGUILayout.VerticalScope("box"))
        {
            EditorGUILayout.LabelField("Colors (#string)", EditorStyles.boldLabel);

            bool showColors = EditorPrefs.GetBool("LevelGen_ShowColors", false);
            showColors = EditorGUILayout.Foldout(showColors, $"Color List ({colorList.Count})", true);
            EditorPrefs.SetBool("LevelGen_ShowColors", showColors);

            if (showColors)
            {
                int newCount = Mathf.Max(0, EditorGUILayout.IntField("Count", colorList.Count));
                while (colorList.Count < newCount) colorList.Add("");
                while (colorList.Count > newCount) colorList.RemoveAt(colorList.Count - 1);

                for (int i = 0; i < colorList.Count; i++)
                {
                    GUILayout.Space(2);

                    using (new EditorGUILayout.HorizontalScope())
                    {
                        colorList[i] = EditorGUILayout.TextField($"Color {i + 1}", colorList[i]);

                        GUILayout.Space(4);

                        if (ColorUtility.TryParseHtmlString(colorList[i], out var parsedColor))
                        {
                            var rect = GUILayoutUtility.GetRect(24, 18, GUILayout.Width(18));
                            EditorGUI.DrawRect(rect, parsedColor);
                            EditorGUI.LabelField(rect, GUIContent.none);
                        }
                        else
                        {
                            var rect = GUILayoutUtility.GetRect(24, 18, GUILayout.Width(18));
                            EditorGUI.DrawRect(rect, new Color(0.8f, 0.8f, 0.8f));
                            EditorGUI.LabelField(rect, GUIContent.none);
                        }
                    }

                    GUILayout.Space(2);
                }

                using (new EditorGUILayout.HorizontalScope())
                {
                    if (GUILayout.Button("Add Color", GUILayout.Width(120)))
                        colorList.Add("");
                    if (GUILayout.Button("Clear Colors", GUILayout.Width(120)))
                        colorList.Clear();
                }
            }
        }

        DrawButtons(viewWidth);
        DrawPreview();
    }

    private void DrawButtons(float viewWidth)
    {
        float margin = 12f;
        float spacing = 6f;
        float colWidth = Mathf.Max(140f, (viewWidth - margin * 2f - spacing) / 2f);
        float h1 = 36f;
        float h2 = 28f;

        using (new EditorGUILayout.HorizontalScope())
        {
            GUILayout.FlexibleSpace();

            if (GUILayout.Button("Generate Preview", GUILayout.Width(colWidth), GUILayout.Height(h1)))
                Generate();

            bool canExport = lastResult.Topics != null && lastResult.Topics.Count > 0 && csvOutput != null;
            EditorGUI.BeginDisabledGroup(!canExport);
            if (GUILayout.Button("Export To CSV", GUILayout.Width(colWidth), GUILayout.Height(h1)))
                ExportToCSV();
            EditorGUI.EndDisabledGroup();

            GUILayout.FlexibleSpace();
        }

        using (new EditorGUILayout.HorizontalScope())
        {
            GUILayout.FlexibleSpace();

            if (GUILayout.Button("Clear Preview", GUILayout.Width(colWidth), GUILayout.Height(h2)))
                ClearPreview();

            if (GUILayout.Button($"Clear Cache ({cacheIds.Count})", GUILayout.Width(colWidth), GUILayout.Height(h2)))
                ClearCache();

            GUILayout.FlexibleSpace();
        }
    }

    private void DrawPreview()
    {
        if (lastResult.Topics == null || lastResult.Topics.Count == 0)
            return;

        EditorGUILayout.Space(8);
        EditorGUILayout.LabelField("Preview Result", EditorStyles.boldLabel);
        using (new EditorGUILayout.VerticalScope("box"))
        {
            EditorGUILayout.LabelField(
                $"Level {lastResult.Level} | Reward {lastResult.RewardType} {lastResult.RewardAmount} | RowShow {lastResult.RowShow} | Type {lastResult.LevelType}"
            );

            foreach (var t in previewList)
            {
                EditorGUILayout.LabelField($"{t.TopicID} | {t.TopicName} | Color {t.ResultColor} | MergeTo {t.ResultToTopicID} / {t.ResultToTileID} | Pos {t.ResultToPositionShow}");
            }
        }
    }

    private void Generate()
    {
        if (loader == null || loader.TopicsCount == 0)
        {
            EditorUtility.DisplayDialog("Missing Data", "No topic data loaded. Please load a CSV file first.", "OK");
            return;
        }

        var input = new LevelGenInput
        {
            Level = level,
            RewardType = rewardType,
            RewardAmount = rewardAmount,
            TotalTopics = totalTopics,
            TopicShow = topicShow,
            IsDifferentCategory = isDifferentCategory,
            LevelType = levelType,
            DefaultPositions = defaultPositions,
            AllColorTopic = colorList,
            AllTopics = loader.GetAllTopicsRaw(),
            CacheTopicIds = cacheIds
        };

        lastInput = input;
        lastResult = LevelBaseRandomizer.Generate(input);
        previewList = lastResult.Topics ?? new List<ResultTopicInfo>();

        Repaint();
    }

    private void ExportToCSV()
    {
        if (csvOutput == null)
        {
            EditorUtility.DisplayDialog("Missing Output File", "Please assign an output CSV file before exporting.", "OK");
            return;
        }

        if (lastResult.Topics == null || lastResult.Topics.Count == 0)
        {
            EditorUtility.DisplayDialog("No Preview", "You must generate a preview before exporting.", "OK");
            return;
        }

        if (lastResult.Topics != null)
        {
            foreach (var r in lastResult.Topics)
            {
                if (int.TryParse(r.TopicID, out var id) && !cacheIds.Contains(id))
                    cacheIds.Add(id);
            }
        }

        exporter.Export(csvOutput, lastResult);
        level += 1;
        Debug.Log($"[BaseLevelModule] Exported Level {lastResult.Level}. Next to Level {level}");
        Repaint();
    }

    private void ClearCache()
    {
        cacheIds.Clear();
        Debug.Log("[BaseLevelModule] Cleared topic cache.");
        Repaint();
    }

    private void ClearPreview()
    {
        lastResult = default;
        previewList.Clear();
        Debug.Log("[BaseLevelModule] Cleared preview data.");
        Repaint();
    }

    private void Repaint()
    {
        SceneView.RepaintAll();
        EditorApplication.QueuePlayerLoopUpdate();
    }
}
#endif

#if UNITY_EDITOR
public static class EditorEnumToggles
{
    private const float LabelWidth = 148f;
    private const float ButtonHeight = 20f;
    private const float ButtonSpace = 0f;

    public static T Toolbar<T>(string label, T current) where T : struct, System.Enum
    {
        using (new EditorGUILayout.HorizontalScope())
        {
            EditorGUILayout.LabelField(label, GUILayout.Width(LabelWidth));

            foreach (T v in System.Enum.GetValues(typeof(T)))
            {
                bool isOn = current.Equals(v);
                bool press = GUILayout.Toggle(isOn, v.ToString(), "Button", GUILayout.Height(ButtonHeight));
                if (press && !isOn) current = v;
                GUILayout.Space(ButtonSpace);
            }
        }

        return current;
    }
}
#endif
