using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public static class FillVisibleTileHandler
{
    /// <summary>
    /// Fill the visible grid with randomized topic tiles.
    /// </summary>
    public static void Fill(GridGenInput input, List<List<GridCell>> visible, GridRandomizerHelpers.TilePool pool)
    {
        var topics = input.BaseResult.Topics;
        if (topics == null || topics.Count == 0)
            return;

        var rng = new System.Random();

        // 1. Split topics
        var (priorityTopics, normalTopics) = SplitTopics(topics);

        // 2. Target numbers
        int totalCells = visible.Count * input.Columns;
        int priorityTarget = Mathf.RoundToInt(totalCells * 0.6f);
        int normalTarget = totalCells - priorityTarget;

        // 3. Pull tiles from pool
        var priorityTiles = TakeTilesFromPool(priorityTopics, pool, priorityTarget);
        var normalTiles = TakeTilesFromPool(normalTopics, pool, normalTarget);

        // 5. Merge & pad
        var combinedTiles = new List<GridCell>(priorityTiles.Concat(normalTiles));

        while (combinedTiles.Count < totalCells && pool.TryTakeAny(out var extra))
            combinedTiles.Add(extra);

        // 5. Shuffle & fill
        var shuffled = combinedTiles.Take(totalCells).OrderBy(_ => rng.Next()).ToList();

        int idx = 0;
        for (int r = 0; r < visible.Count; r++)
        {
            for (int c = 0; c < input.Columns; c++)
            {
                var cell = shuffled[idx++];
                visible[r][c] = cell;
                pool.MarkUsed(cell);
            }
        }

        Debug.Log($"[GridGen] Visible grid filled ({totalCells} cells | remain={pool.RemainingTotal}).");
    }

    #region 🔸 Local Helpers

    private static HashSet<string> CollectMergeOutputKeys(List<ResultTopicInfo> topics)
    {
        var keys = new HashSet<string>();
        foreach (var t in topics)
        {
            if (!string.IsNullOrEmpty(t.ResultToTopicID) && !string.IsNullOrEmpty(t.ResultToTileID))
                keys.Add(GridRandomizerHelpers.Key(t.ResultToTopicID, t.ResultToTileID));
        }
        return keys;
    }

    private static (List<ResultTopicInfo> priority, List<ResultTopicInfo> normal) SplitTopics(List<ResultTopicInfo> topics)
    {
        var priority = topics.Where(t => !string.IsNullOrEmpty(t.ResultToTopicID)).ToList();
        var normal = topics.Where(t => string.IsNullOrEmpty(t.ResultToTopicID)).ToList();
        return (priority, normal);
    }

    private static List<GridCell> TakeTilesFromPool(
        List<ResultTopicInfo> topicGroup,
        GridRandomizerHelpers.TilePool pool,
        int targetCount)
    {
        var result = new List<GridCell>();
        var rng = new System.Random();

        while (result.Count < targetCount)
        {
            if (topicGroup == null || topicGroup.Count == 0)
                break;

            var t = topicGroup.OrderBy(_ => rng.Next()).First();


            if (pool.TryTake(t.TopicID, out var cell))
            {
                result.Add(cell);
            }
            else
            {
                topicGroup.Remove(t);
                if (topicGroup.Count == 0) break;
            }
        }

        return result;
    }

    #endregion
}
