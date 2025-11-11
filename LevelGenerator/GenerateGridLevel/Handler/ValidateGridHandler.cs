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

        var available = Flatten(visible)
            .GroupBy(c => c.TopicID)
            .ToDictionary(g => g.Key, g => g.Count());

        var pendingHiddenRows = BuildHiddenRowQueue(hidden);
        if (pendingHiddenRows.Count == 0)
            return true;

        int safety = 0;

        while (pendingHiddenRows.Count > 0 && safety++ < 400)
        {
            var mergeableTopics = available
                .Where(kv => kv.Value >= 4)
                .Select(kv => kv.Key)
                .ToList();

            if (mergeableTopics.Count == 0)
                break;

            string selectedTopic = null;
            int bestIndex = int.MaxValue;

            foreach (var topic in mergeableTopics)
            {
                int idx = pendingHiddenRows.FindIndex(r => r.RequiredTopic == topic);
                if (idx >= 0 && idx < bestIndex)
                {
                    bestIndex = idx;
                    selectedTopic = topic;
                }
            }

            if (selectedTopic == null)
            {
                selectedTopic = mergeableTopics[0];
            }

            available[selectedTopic] -= 4;
            if (available[selectedTopic] <= 0)
                available.Remove(selectedTopic);

            HiddenRowState droppedRow = null;

            if (bestIndex != int.MaxValue)
            {
                droppedRow = pendingHiddenRows[bestIndex];
                pendingHiddenRows.RemoveAt(bestIndex);
            }
            else
            {
                int passiveIndex = pendingHiddenRows.FindIndex(r => string.IsNullOrEmpty(r.RequiredTopic));
                if (passiveIndex >= 0)
                {
                    droppedRow = pendingHiddenRows[passiveIndex];
                    pendingHiddenRows.RemoveAt(passiveIndex);
                }
            }

            if (droppedRow != null)
            {
                foreach (var cell in droppedRow.Cells)
                {
                    if (string.IsNullOrEmpty(cell.TopicID))
                        continue;

                    if (!available.TryGetValue(cell.TopicID, out var count))
                        count = 0;

                    available[cell.TopicID] = count + 1;
                }
            }
        }

        if (pendingHiddenRows.Count > 0)
            return false;

        int cleanupSafety = 0;
        bool progress;

        do
        {
            progress = false;
            var residualTopics = available
                .Where(kv => kv.Value >= 4)
                .Select(kv => kv.Key)
                .ToList();

            if (residualTopics.Count == 0)
                break;

            foreach (var topic in residualTopics)
            {
                available[topic] -= 4;
                if (available[topic] <= 0)
                    available.Remove(topic);
                progress = true;
            }
        }
        while (progress && cleanupSafety++ < 200);

        return available.Count == 0;
    }

    private class HiddenRowState
    {
        public string RequiredTopic;
        public List<GridCell> Cells;
    }

    private static List<HiddenRowState> BuildHiddenRowQueue(List<List<GridCell>> hidden)
    {
        var result = new List<HiddenRowState>();
        if (hidden == null)
            return result;

        foreach (var row in hidden)
        {
            if (row == null)
                continue;

            var rowCopy = row.Select(c => c).ToList();
            bool hasContent = rowCopy.Any(c => !string.IsNullOrEmpty(c.TopicID));
            if (!hasContent)
                continue;

            var mergeCell = rowCopy.FirstOrDefault(c => c.IsReservedOutput && !string.IsNullOrEmpty(c.MergeFromTopicID));

            result.Add(new HiddenRowState
            {
                RequiredTopic = mergeCell.MergeFromTopicID,
                Cells = rowCopy
            });
        }

        return result;
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
