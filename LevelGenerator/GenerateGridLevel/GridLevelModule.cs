#if UNITY_EDITOR
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

public class GridLevelModule : ILevelGeneratorModule
{
    private LevelResultDataLoader loader = new();
    private TextAsset csvInput;
    private TextAsset csvOutput;
    private TextAsset csvListRowData;

    private ListRowDataLoader topicLoader = new();
    private bool topicLoaded;

    private int selectedIndex = -1;
    private LevelGenResult selectedResult;
    private GridGenResult gridPreview;
    private Vector2 scrollPos;
    private bool showGridPreview;

    private int topicFullCount;
    private bool isSolvable;

    private List<string> colorList = new List<string>
    {
        "#FF9FB5", "#FFD666", "#A8F0A8", "#6FD3FF", "#B399FF", "#FF91B5",
        "#FFC266", "#B9FEA5", "#66E5F0", "#CC8EFF", "#FFF066", "#99A9FF",
        "#FF9966", "#B3998F", "#D6F56A", "#E6B8FF", "#C8F04A", "#89C7FF",
        "#FFD699", "#9ED6FF", "#D2A4F0", "#9AF5BB", "#FFB0A6", "#FFE866"
    };

    private Dictionary<string, Color> topicColorMap = new();

    public void DrawGUI(float viewWidth)
    {
        if (csvListRowData == null)
            csvListRowData = Resources.Load<TextAsset>("BlueprintData/ListRowData");

        if (csvInput == null)
            csvInput = Resources.Load<TextAsset>("BlueprintData/LevelNormalData");

        if (csvOutput == null)
            csvOutput = Resources.Load<TextAsset>("BlueprintData/LevelData");

        using (new EditorGUILayout.VerticalScope("box"))
        {
            EditorGUILayout.LabelField("Data Source", EditorStyles.boldLabel);
            csvListRowData = (TextAsset)EditorGUILayout.ObjectField("CSV Topic Source (ListRowData)", csvListRowData, typeof(TextAsset), false);
            csvInput = (TextAsset)EditorGUILayout.ObjectField("CSV Input (Level Result)", csvInput, typeof(TextAsset), false);
            csvOutput = (TextAsset)EditorGUILayout.ObjectField("CSV Output (Grid Result)", csvOutput, typeof(TextAsset), false);

            if (GUILayout.Button("Load CSV Data", GUILayout.Height(26)))
            {
                loader.Load(csvInput);
                selectedIndex = -1;
                selectedResult = default;
                gridPreview = default;
                showGridPreview = false;

                topicLoaded = false;
                if (csvListRowData != null)
                {
                    topicLoader = new ListRowDataLoader();
                    topicLoader.Load(csvListRowData);
                    topicLoaded = true;
                }
            }

            if (loader.LevelCount > 0)
                DrawLevelSelector();
            else
                EditorGUILayout.HelpBox("No data loaded. Please select a valid Level Result CSV.", MessageType.Warning);
        }

        if (!string.IsNullOrEmpty(selectedResult.Level))
        {
            DrawButtons(viewWidth);

            if (showGridPreview)
                DrawGridPreview(viewWidth);
            else
                DrawLevelDataPreview();
        }
    }

    private void DrawLevelSelector()
    {
        string[] options = loader.Results.Select(r => $"Level {r.Level}").ToArray();
        int newIndex = EditorGUILayout.Popup("Select Level", selectedIndex, options);

        if (newIndex != selectedIndex && newIndex >= 0 && newIndex < loader.Results.Count)
        {
            selectedIndex = newIndex;
            selectedResult = loader.Results[selectedIndex];
            gridPreview = default;
            showGridPreview = false;
        }
    }

    private void DrawButtons(float viewWidth)
    {
        EditorGUILayout.Space(8);

        float colWidth = Mathf.Max(140f, (viewWidth - 60f) / 2f);
        float h1 = 36f;
        float h2 = 28f;
        bool canOperate = showGridPreview && gridPreview.Visible != null && gridPreview.Visible.Count > 0;

        // ==== Row 1: Generate & Export ====
        using (new EditorGUILayout.HorizontalScope())
        {
            GUILayout.FlexibleSpace();

            if (GUILayout.Button("Generate Preview", GUILayout.Width(colWidth), GUILayout.Height(h1)))
                Generate();

            EditorGUI.BeginDisabledGroup(!canOperate);
            if (GUILayout.Button("Export To CSV", GUILayout.Width(colWidth), GUILayout.Height(h1)))
                Export();
            EditorGUI.EndDisabledGroup();

            GUILayout.FlexibleSpace();
        }

        EditorGUILayout.Space(8);

        // ==== Row 2x2 Grid for Validation ====
        EditorGUI.BeginDisabledGroup(!canOperate);
        {
            GUILayout.BeginVertical();
            {
                GUILayout.BeginHorizontal();
                {
                    GUILayout.FlexibleSpace();

                    if (GUILayout.Button("Check Topic Full", GUILayout.Width(colWidth), GUILayout.Height(h2)))
                    {
                        int fullCount = GridLevelRandomizer.CheckTopicFullInVisible(gridPreview.Visible);
                        EditorUtility.DisplayDialog("Check Topic Full",
                            $"Có {fullCount} topic đủ 4 tile trong Visible Grid.", "OK");
                    }

                    if (GUILayout.Button("Check Solvable", GUILayout.Width(colWidth), GUILayout.Height(h2)))
                    {
                        bool solvable = GridLevelRandomizer.CheckSolvable(gridPreview.Visible, gridPreview.Hidden);
                        EditorUtility.DisplayDialog("Check Solvable",
                            solvable ? "✅ Level có thể giải được (Solvable)"
                                     : "❌ Level có thể bị kẹt (Unsolvable)", "OK");
                    }

                    GUILayout.FlexibleSpace();
                }
                GUILayout.EndHorizontal();

                GUILayout.Space(2);

                GUILayout.BeginHorizontal();
                {
                    GUILayout.FlexibleSpace();

                    if (GUILayout.Button("Check All Complete", GUILayout.Width(colWidth), GUILayout.Height(h2)))
                    {
                        bool complete = GridLevelRandomizer.CheckAllTopicComplete(gridPreview.Visible, gridPreview.Hidden);
                        EditorUtility.DisplayDialog("Check All Topic Complete",
                            complete ? "✅ Tất cả Topic đều có đúng 4 tile."
                                     : "❌ Một số Topic chưa đủ hoặc dư tile.", "OK");
                    }

                    if (GUILayout.Button("Check Difficulty", GUILayout.Width(colWidth), GUILayout.Height(h2)))
                    {
                        var rawTopics = topicLoader.GetAllTopicsRaw();
                        var topicLookup = rawTopics.ToDictionary(t => t.TopicID, t => t);
                        var input = new GridGenInput
                        {
                            Columns = gridPreview.Columns,
                            VisibleRows = gridPreview.VisibleRows,
                            BaseResult = selectedResult,
                            TopicLookup = topicLookup
                        };

                        float diff = GridLevelRandomizer.CheckDifficultyScore(input, gridPreview.Visible, gridPreview.Hidden);
                        EditorUtility.DisplayDialog("Difficulty Score",
                            $"Điểm độ khó của Level hiện tại là:\n\n{diff:F2}", "OK");
                    }

                    GUILayout.FlexibleSpace();
                }
                GUILayout.EndHorizontal();
            }
            GUILayout.EndVertical();
        }
        EditorGUI.EndDisabledGroup();

        GUILayout.Space(8);
    }

    private void DrawLevelDataPreview()
    {
        EditorGUILayout.Space(10);
        EditorGUILayout.LabelField($"Level {selectedResult.Level} Data", EditorStyles.boldLabel);
        EditorGUILayout.LabelField($"Reward: {selectedResult.RewardType} {selectedResult.RewardAmount}");
        EditorGUILayout.LabelField($"RowShow: {selectedResult.RowShow} | Type: {selectedResult.LevelType}");
        EditorGUILayout.Space(4);

        scrollPos = EditorGUILayout.BeginScrollView(scrollPos);
        foreach (var t in selectedResult.Topics)
        {
            using (new EditorGUILayout.VerticalScope("box"))
            {
                EditorGUILayout.LabelField($"Topic {t.TopicID}", EditorStyles.boldLabel);
                EditorGUILayout.LabelField($"Merge→ {t.ResultToTopicID} | Tile {t.ResultToTileID} | Pos {t.ResultToPositionShow}");

                if (topicLoaded)
                {
                    var tiles = topicLoader.GetTilesByTopicId(int.Parse(t.TopicID));
                    foreach (var tile in tiles.tiles)
                        EditorGUILayout.LabelField($"• {tile.id} - {tile.name}");
                }
                else
                {
                    EditorGUILayout.HelpBox("Topic data not loaded.", MessageType.Info);
                }
            }
        }
        EditorGUILayout.EndScrollView();
    }

    private void DrawGridPreview(float viewWidth)
    {
        EditorGUILayout.Space(10);

        using (new EditorGUILayout.HorizontalScope())
        {
            EditorGUILayout.LabelField("Grid Preview", EditorStyles.boldLabel);

            GUILayout.FlexibleSpace();

            bool allComplete = GridLevelRandomizer.CheckAllTopicComplete(gridPreview.Visible, gridPreview.Hidden);

            var rawTopics = topicLoader.GetAllTopicsRaw();
            var topicLookup = rawTopics.ToDictionary(t => t.TopicID, t => t);
            var input = new GridGenInput
            {
                Columns = gridPreview.Columns,
                VisibleRows = gridPreview.VisibleRows,
                BaseResult = selectedResult,
                TopicLookup = topicLookup
            };

            float difficultyScore = GridLevelRandomizer.CheckDifficultyScore(input, gridPreview.Visible, gridPreview.Hidden);

            string solvableIcon = isSolvable ? "🟢" : "🔴";
            string completeIcon = allComplete ? "🟢" : "🔴";

            string info = $"[{topicFullCount}/{gridPreview.Visible.Count}]{solvableIcon}{completeIcon}[{difficultyScore:F2}]";
            var style = new GUIStyle(EditorStyles.label)
            {
                alignment = TextAnchor.MiddleRight,
                fontStyle = FontStyle.Bold
            };

            EditorGUILayout.LabelField(info, style, GUILayout.Width(150));
        }

        scrollPos = EditorGUILayout.BeginScrollView(scrollPos);

        if (gridPreview.Hidden != null && gridPreview.Hidden.Count > 0)
        {
            EditorGUILayout.LabelField("Hidden Rows (top)", EditorStyles.miniBoldLabel);
            foreach (var row in gridPreview.Hidden)
                DrawGridRow(row, viewWidth);
            EditorGUILayout.Space(6);
        }

        EditorGUILayout.LabelField($"Visible Grid ({gridPreview.VisibleRows}x{gridPreview.Columns})", EditorStyles.miniBoldLabel);
        foreach (var row in gridPreview.Visible)
            DrawGridRow(row, viewWidth);

        EditorGUILayout.EndScrollView();
    }


    private void DrawGridRow(List<GridCell> row, float viewWidth)
    {
        using (new EditorGUILayout.HorizontalScope())
        {
            var boxStyle = new GUIStyle(GUI.skin.box)
            {
                alignment = TextAnchor.MiddleCenter,
                wordWrap = true,
                fontSize = 10,
                normal = { textColor = Color.black }
            };

            float margin = 60f;
            float spacing = 6f;
            float cellWidth = (viewWidth - margin - (spacing * (gridPreview.Columns - 1))) / gridPreview.Columns;

            GUILayout.FlexibleSpace();

            foreach (var cell in row)
            {
                string line1 = "-";
                string line2 = "-";

                if (!string.IsNullOrEmpty(cell.TopicID))
                {
                    line1 = $"{cell.TopicID} - {cell.TopicName}";

                    if (cell.IsReservedOutput)
                    {
                        string from = string.IsNullOrEmpty(cell.MergeFromTopicID) ? "?" : cell.MergeFromTopicID;
                        line2 = $"{cell.TileID} - {cell.TileName} - MergeFromTopic{from}";
                    }
                    else
                    {
                        line2 = $"{cell.TileID} - {cell.TileName}";
                    }
                }

                Color bgColor = new Color(0.9f, 0.9f, 0.9f);
                if (!string.IsNullOrEmpty(cell.TopicID))
                {
                    if (!topicColorMap.TryGetValue(cell.TopicID, out bgColor))
                    {
                        int colorIndex = topicColorMap.Count % colorList.Count;
                        if (ColorUtility.TryParseHtmlString(colorList[colorIndex], out var parsed))
                            bgColor = parsed;
                        topicColorMap[cell.TopicID] = bgColor;
                    }
                }

                Rect rect = GUILayoutUtility.GetRect(new GUIContent($"{line1}\n{line2}"), boxStyle,
                    GUILayout.Width(cellWidth), GUILayout.Height(54));

                EditorGUI.DrawRect(rect, bgColor);
                GUI.Box(rect, $"{line1}\n{line2}", boxStyle);
            }

            GUILayout.FlexibleSpace();
        }

        GUILayout.Space(4);
    }


    private void Generate()
    {
        if (string.IsNullOrEmpty(selectedResult.Level))
        {
            Debug.LogWarning("[GridLevelModule] No level selected!");
            return;
        }

        if (!topicLoaded)
        {
            Debug.LogError("GridLevelModule] Topic data (ListRowData) not loaded!");
            return;
        }

        var rawTopics = topicLoader.GetAllTopicsRaw();
        var topicLookup = rawTopics.ToDictionary(t => t.TopicID, t => t);

        var input = new GridGenInput
        {
            Columns = 4,
            VisibleRows = int.TryParse(selectedResult.RowShow, out var rs) ? rs : 4,
            BaseResult = selectedResult,
            TopicLookup = topicLookup
        };

        gridPreview = GridLevelRandomizer.Generate(input);
        showGridPreview = true;

        topicFullCount = GridLevelRandomizer.CheckTopicFullInVisible(gridPreview.Visible);
        isSolvable = GridLevelRandomizer.CheckSolvable(gridPreview.Visible, gridPreview.Hidden);

        Debug.Log($"[GridLevelModule] Generated grid preview for Level {selectedResult.Level}");
    }

    private void Export()
    {
        if (string.IsNullOrEmpty(csvOutput?.name))
        {
            EditorUtility.DisplayDialog("Missing CSV Output", "Please assign CSV Output file first.", "OK");
            return;
        }

        string path = AssetDatabase.GetAssetPath(csvOutput);
        int.TryParse(selectedResult.Level, out int levelId);
        GridLevelExporter.Export(path, levelId, gridPreview);
    }

}
#endif
