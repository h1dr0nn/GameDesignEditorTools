using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public static class ValidateGridHandler
{
    /// <summary>
    /// Validate the final grid after generation & balancing.
    /// Includes:
    ///  1. Check if all topics have exactly 4 tiles total
    ///  2. Check solvability (can all merges resolve)
    ///  3. Calculate weighted difficulty score
    /// </summary>
    public static void Validate(GridGenInput input, List<List<GridCell>> visible, List<List<GridCell>> hidden)
    {
        int topicShowFull = CheckTopicFullInVisible(visible);
        bool allTopicComplete = CheckTopicTileCompleteness(visible, hidden);
        bool solvable = CheckSolvable(visible, hidden);

        float difficultyScore = CalculateDifficultyScore(input, visible, hidden, topicShowFull);

        Debug.Log($"[GridGen] ✅ Validate Result:");
        Debug.Log($"[GridGen] Topics Complete: {allTopicComplete}");
        Debug.Log($"[GridGen] Solvable: {solvable}");
        Debug.Log($"[GridGen] DifficultyScore: {difficultyScore:F2}");
    }

    // =====================================================================
    #region 🔸 Local Helpers

    private static int CheckTopicFullInVisible(List<List<GridCell>> visible)
    {
        if (visible == null || visible.Count == 0)
            return 0;

        return visible
            .SelectMany(r => r)
            .Where(c => !string.IsNullOrEmpty(c.TopicID))
            .GroupBy(c => c.TopicID)
            .Count(g => g.Count() >= 4);
    }

    private static bool CheckTopicTileCompleteness(List<List<GridCell>> visible, List<List<GridCell>> hidden)
    {
        var allCells = visible.Concat(hidden)
            .SelectMany(r => r)
            .Where(c => !string.IsNullOrEmpty(c.TopicID))
            .ToList();

        var grouped = allCells.GroupBy(c => c.TopicID).ToList();
        bool allComplete = grouped.All(g => g.Count() == 4);

        if (!allComplete)
        {
            var incomplete = grouped.Where(g => g.Count() != 4)
                .Select(g => $"{g.Key}({g.Count()})").ToList();
            Debug.LogWarning($"[GridGen] ❌ Incomplete Topics: {string.Join(", ", incomplete)}");
        }

        return allComplete;
    }

    private static bool CheckSolvable(List<List<GridCell>> visible, List<List<GridCell>> hidden)
    {
        if (visible == null || hidden == null)
        {
            Debug.LogWarning("[GridGen] Solvability check failed: grid is null.");
            return false;
        }

        var flatVisible = Flatten(visible);
        var activeHidden = new List<List<GridCell>>(hidden);
        var mergeOutputs = Flatten(activeHidden)
            .Where(c => c.IsReservedOutput && !string.IsNullOrEmpty(c.MergeFromTopicID))
            .ToList();

        if (mergeOutputs.Count == 0)
            return true;

        var resolved = new HashSet<string>();
        int safety = 0;

        while (activeHidden.Count > 0 && safety++ < 200)
        {
            bool merged = false;
            var pending = mergeOutputs.Where(m => !resolved.Contains(m.MergeFromTopicID)).ToList();

            foreach (var merge in pending)
            {
                var topicTiles = flatVisible.Where(c => c.TopicID == merge.MergeFromTopicID).ToList();
                if (topicTiles.Count >= 4)
                {
                    flatVisible.RemoveAll(c => c.TopicID == merge.MergeFromTopicID);
                    flatVisible.Insert(0, new GridCell
                    {
                        TopicID = merge.TopicID,
                        TopicName = merge.TopicName,
                        TileID = merge.TileID,
                        TileName = merge.TileName,
                        IsReservedOutput = merge.IsReservedOutput,
                        MergeFromTopicID = merge.MergeFromTopicID
                    });

                    var hiddenRow = activeHidden.FirstOrDefault(r => r.Any(c => c.MergeFromTopicID == merge.MergeFromTopicID));
                    if (hiddenRow != null)
                        activeHidden.Remove(hiddenRow);

                    resolved.Add(merge.MergeFromTopicID);
                    merged = true;
                    break;
                }
            }

            if (!merged) break;

            mergeOutputs = Flatten(activeHidden)
                .Where(c => c.IsReservedOutput && !string.IsNullOrEmpty(c.MergeFromTopicID))
                .ToList();
        }

        return activeHidden.Count == 0;
    }

    private static float CalculateDifficultyScore(
        GridGenInput input,
        List<List<GridCell>> visible,
        List<List<GridCell>> hidden,
        int topicShowFull)
    {
        var baseResult = input.BaseResult;
        var topics = baseResult.Topics;
        int totalTopics = topics.Count;
        int hiddenRowCount = Mathf.Max(0, totalTopics - input.VisibleRows);
        int rowShow = input.VisibleRows;

        int mergeChainDepth = CountMergeChainDepth(topics);

        int row3TileTopic = 0;
        int row2TileTopic = 0;
        foreach (var row in visible)
        {
            var grouped = row.GroupBy(c => c.TopicID);
            if (grouped.Any(g => g.Count() == 3)) row3TileTopic++;
            if (grouped.Any(g => g.Count() == 2)) row2TileTopic++;
        }

        float score =
            2 +
            totalTopics * 2.5f +
            hiddenRowCount * 2f +
            mergeChainDepth * 3f +
            rowShow * -0.8f +
            topicShowFull * -1.2f +
            row3TileTopic * -1.8f +
            row2TileTopic * -0.6f;

        return score;
    }

    private static int CountMergeChainDepth(List<ResultTopicInfo> topics)
    {
        var map = topics.ToDictionary(t => t.TopicID, t => t.ResultToTopicID);
        int maxDepth = 0;

        foreach (var t in topics)
        {
            int depth = 0;
            string current = t.TopicID;
            var visited = new HashSet<string>();

            while (map.TryGetValue(current, out var next) && !string.IsNullOrEmpty(next) && !visited.Contains(next))
            {
                visited.Add(next);
                depth++;
                current = next;
            }

            if (depth > maxDepth)
                maxDepth = depth;
        }

        return maxDepth;
    }

    private static List<GridCell> Flatten(List<List<GridCell>> grid)
    {
        return grid?
            .SelectMany(r => r)
            .Where(c => !string.IsNullOrEmpty(c.TopicID))
            .ToList()
            ?? new List<GridCell>();
    }

    #endregion
}
