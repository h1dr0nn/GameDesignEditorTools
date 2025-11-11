using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public static class FillHiddenTileHandler
{
    /// <summary>
    /// Fill hidden rows with merge-output tiles and supporting filler tiles.
    /// Rules:
    /// 1️. Each hidden row corresponds to a merge topic (ResultToTopicID)
    /// 2️. Reserve merge-output cell at ResultToPositionShow
    /// 3️. Fill remaining cells using TilePool (no duplication)
    /// 4️. Skip merge-output tiles
    /// </summary>
    public static void Fill(GridGenInput input, List<List<GridCell>> visible, List<List<GridCell>> hidden, GridRandomizerHelpers.TilePool pool)
    {
        if (hidden == null || hidden.Count == 0)
            return;

        var topics = input.BaseResult.Topics;
        var rng = new System.Random();
        bool isHard = string.Equals(input.BaseResult.LevelType, "Hard", StringComparison.OrdinalIgnoreCase);

        var mergeTopics = topics.Where(t => !string.IsNullOrEmpty(t.ResultToTopicID)).ToList();
        int limit = Mathf.Min(mergeTopics.Count, hidden.Count);

        for (int i = 0; i < limit; i++)
        {
            var merge = mergeTopics[i];
            var row = hidden[i];

            int.TryParse(merge.ResultToPositionShow, out int pos);
            pos = Mathf.Clamp(pos, 0, row.Count - 1);

            var outTopicId = merge.ResultToTopicID;
            var outTileId = merge.ResultToTileID;
            ResolveNames(input, outTopicId, outTileId, out var outTopicName, out var outTileName);

            var reserved = new GridCell
            {
                TopicID = outTopicId,
                TopicName = outTopicName,
                TileID = outTileId,
                TileName = outTileName,
                IsReservedOutput = true,
                MergeFromTopicID = merge.TopicID
            };

            row[pos] = reserved;

            FillRemainingCells(input, row, rng, pool, isHard);
        }

        Debug.Log($"[GridGen] Hidden grid filled ({limit} merge rows | HardMode={isHard}).");
    }

    // =====================================================================
    #region 🔸 Local Helpers

    /// <summary>
    /// Fill empty cells trong một hàng bằng tile chưa dùng trong pool.
    /// </summary>
    private static void FillRemainingCells(
        GridGenInput input,
        List<GridCell> row,
        System.Random rng,
        GridRandomizerHelpers.TilePool pool,
        bool isHard)
    {
        var allTopics = input.BaseResult.Topics.ToList();

        for (int c = 0; c < row.Count; c++)
        {
            if (!string.IsNullOrEmpty(row[c].TopicID))
                continue; // skip reserved ô merge

            GridCell cell;
            bool success = false;

            var shuffled = allTopics.OrderBy(_ => rng.Next()).ToList();

            foreach (var t in shuffled)
            {
                if (pool.TryTake(t.TopicID, out var candidate))
                {
                    cell = candidate;
                    success = true;
                    row[c] = cell;
                    pool.MarkUsed(cell);
                    break;
                }
            }

            if (!success && pool.TryTakeAny(out var fallback))
            {
                row[c] = fallback;
                pool.MarkUsed(fallback);
                success = true;
            }

            if (!success)
                row[c] = new GridCell();
        }
    }

    /// <summary>
    /// Resolve display names for topic/tile IDs.
    /// </summary>
    private static void ResolveNames(
        GridGenInput input,
        string topicIdStr,
        string tileIdStr,
        out string topicName,
        out string tileName)
    {
        topicName = $"Topic {topicIdStr}";
        tileName = string.IsNullOrEmpty(tileIdStr) ? "-" : $"Tile {tileIdStr}";

        if (!int.TryParse(topicIdStr, out var tid)) return;
        if (input.TopicLookup == null || !input.TopicLookup.TryGetValue(tid, out var topicInfo)) return;

        if (!string.IsNullOrEmpty(topicInfo.TopicName))
            topicName = topicInfo.TopicName;

        if (int.TryParse(tileIdStr, out var tileId) && topicInfo.Tiles != null && topicInfo.Tiles.Count > 0)
        {
            RawTileInfo? match = null;

            if (topicInfo.Tiles is List<RawTileInfo> list)
                match = list.FirstOrDefault(t => t.TileID == tileId);
            else
                match = topicInfo.Tiles.FirstOrDefault(t => t.TileID == tileId);

            if (match.HasValue)
                tileName = !string.IsNullOrEmpty(match.Value.TileName)
                    ? match.Value.TileName
                    : $"Tile {tileIdStr}";
        }
    }

    #endregion
}
