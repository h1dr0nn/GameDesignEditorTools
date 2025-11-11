using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;

public class LevelResultDataLoader
{
    private List<LevelGenResult> results = new();

    public int LevelCount => results.Count;
    public IReadOnlyList<LevelGenResult> Results => results;

    public void Load(TextAsset csvFile)
    {
        results.Clear();

        if (csvFile == null)
        {
            Debug.LogError("[LevelResultDataLoader] CSV file not assigned!");
            return;
        }

        var text = csvFile.text;
        if (string.IsNullOrWhiteSpace(text))
        {
            Debug.LogWarning("[LevelResultDataLoader] CSV file is empty or unreadable.");
            return;
        }

        text = text.Replace("\r", "").Trim('\uFEFF', '\u200B');
        var lines = text.Split('\n');
        if (lines.Length <= 1)
        {
            Debug.LogWarning("[LevelResultDataLoader] CSV has no valid lines.");
            return;
        }

        LevelGenResult current = new LevelGenResult { Topics = new List<ResultTopicInfo>() };

        for (int i = 1; i < lines.Length; i++)
        {
            var line = lines[i].Trim();
            if (string.IsNullOrWhiteSpace(line)) continue;

            var parts = line.Split(',');
            if (parts.Length < 10) Array.Resize(ref parts, 10);

            bool isNewLevel = !string.IsNullOrEmpty(parts[0]);
            if (isNewLevel)
            {
                if (current.Topics != null && current.Topics.Count > 0)
                    results.Add(current);

                current = new LevelGenResult
                {
                    Level = parts[0].Trim(),
                    RewardType = parts[1].Trim(),
                    RewardAmount = parts[2].Trim(),
                    RowShow = parts[3].Trim(),
                    LevelType = parts[4].Trim(),
                    Topics = new List<ResultTopicInfo>()
                };
            }

            var topicId = Safe(parts, 5);
            var resultColor = Safe(parts, 6);
            var toTopicId = Safe(parts, 7);
            var toTileId = Safe(parts, 8);
            var toPos = Safe(parts, 9);

            if (!string.IsNullOrEmpty(topicId))
            {
                current.Topics.Add(new ResultTopicInfo
                {
                    TopicID = topicId,
                    TopicName = "",
                    ResultColor = resultColor,
                    ResultToTopicID = toTopicId,
                    ResultToTileID = toTileId,
                    ResultToPositionShow = toPos
                });
            }

            Debug.Log($"[LevelResultDataLoader] Level {current.Level}: ResultToTopicID={toTopicId}, ResultToTileID={toTileId}, ResultToPositionShow={toPos}");
        }

        if (current.Topics != null && current.Topics.Count > 0)
            results.Add(current);

        Debug.Log($"[LevelResultDataLoader] Loaded {results.Count} levels from CSV ({results.Sum(r => r.Topics.Count)} topics total)");

    }

    private string Safe(string[] arr, int idx)
    {
        return (idx < arr.Length && arr[idx] != null) ? arr[idx].Trim() : string.Empty;
    }

}
