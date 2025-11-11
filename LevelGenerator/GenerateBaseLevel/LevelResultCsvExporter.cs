#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

public class LevelResultCsvExporter : EditorWindow
{
    public void Export(TextAsset csvFile, LevelGenResult result)
    {
        if (csvFile == null)
        {
            Debug.LogError("[LevelResultCsvExporter] CSV output file not assigned!");

            return;
        }

        var path = AssetDatabase.GetAssetPath(csvFile);
        this.EnsureFolderExists(path);

        var allLines = File.Exists(path)
            ? File.ReadAllLines(path).ToList()
            : new List<string>();

        var header =
            "Level,RewardType,RewardAmount,RowShow,LevelType,Topic,ResultColor,ResultToTopicID,ResultToTileID,ResultToPositionShow";

        if (allLines.Count == 0 || !allLines[0].TrimStart().StartsWith("Level"))
            allLines.Insert(0, header);

        int.TryParse(result.Level, out var levelInt);
        var newBlock = this.BuildBlock(result);

        var existingIndices = this.FindLevelBlockIndices(allLines, levelInt);
        if (existingIndices.HasValue)
        {
            allLines.RemoveRange(existingIndices.Value.start, existingIndices.Value.count);
            allLines.InsertRange(existingIndices.Value.start, newBlock);
        }
        else
        {
            var insertIndex = this.FindInsertIndex(allLines, levelInt);
            allLines.InsertRange(insertIndex, newBlock);
        }

        File.WriteAllLines(path, allLines);
        AssetDatabase.Refresh();
        Debug.Log($"[LevelBaseRandomizer] Exported Level {levelInt} to {path}");
    }

    private List<string> BuildBlock(LevelGenResult result)
    {
        var lines = new List<string>();
        int.TryParse(result.Level, out var levelInt);

        var first = result.Topics.FirstOrDefault();
        lines.Add($"{levelInt},{result.RewardType},{result.RewardAmount},{result.RowShow},{result.LevelType}," +
            $"{first.TopicID},{first.ResultColor},{first.ResultToTopicID},{first.ResultToTileID},{first.ResultToPositionShow}");

        foreach (var topic in result.Topics.Skip(1)) lines.Add($",,,,,{topic.TopicID},{topic.ResultColor},{topic.ResultToTopicID},{topic.ResultToTileID},{topic.ResultToPositionShow}");

        return lines;
    }

    private (int start, int count)? FindLevelBlockIndices(List<string> allLines, int level)
    {
        var startIdx = -1;
        for (var i = 1; i < allLines.Count; i++)
            if (allLines[i].StartsWith(level + ","))
            {
                startIdx = i;

                break;
            }

        if (startIdx == -1) return null;
        var nextStart = allLines.FindIndex(startIdx + 1, l =>
        {
            if (!l.StartsWith(","))
            {
                var parts = l.Split(',');

                if (int.TryParse(parts[0], out var lv) && lv != level) return true;
            }

            return false;
        });
        var endIdx = nextStart == -1 ? allLines.Count : nextStart;
        var count  = endIdx - startIdx;

        return (startIdx, count);
    }

    private int FindInsertIndex(List<string> allLines, int newLevel)
    {
        for (var i = 1; i < allLines.Count; i++)
            if (!allLines[i].StartsWith(","))
            {
                var parts = allLines[i].Split(',');

                if (int.TryParse(parts[0], out var lv) && lv > newLevel)
                    return i;
            }

        return allLines.Count;
    }

    private void EnsureFolderExists(string path)
    {
        var dir = Path.GetDirectoryName(path);
        if (!Directory.Exists(dir))
            Directory.CreateDirectory(dir);
    }
}
#endif