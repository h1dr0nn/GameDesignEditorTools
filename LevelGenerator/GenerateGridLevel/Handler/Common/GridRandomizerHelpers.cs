using System.Collections.Generic;
using System.Linq;

public static class GridRandomizerHelpers
{
    // Common key builder
    public static string Key(string topicId, string tileId)
        => $"{topicId}:{tileId}";

    // Build Topic → Tile map
    public static Dictionary<string, List<GridCell>> BuildTopicTileMap(GridGenInput input, List<ResultTopicInfo> topics)
    {
        var topicTiles = new Dictionary<string, List<GridCell>>();

        foreach (var t in topics)
        {
            if (!int.TryParse(t.TopicID, out int tid) ||
                input.TopicLookup == null ||
                !input.TopicLookup.ContainsKey(tid))
                continue;

            var topicInfo = input.TopicLookup[tid];
            var tiles = new List<GridCell>();

            foreach (var tileData in topicInfo.Tiles)
            {
                tiles.Add(new GridCell
                {
                    TopicID = t.TopicID,
                    TopicName = topicInfo.TopicName,
                    TileID = tileData.TileID.ToString(),
                    TileName = tileData.TileName
                });
            }

            topicTiles[t.TopicID] = tiles;
        }

        return topicTiles;
    }

    // =====================================================================
    #region 🔸 TilePool Definition

    public class TilePool
    {
        private readonly Dictionary<string, Queue<GridCell>> pool = new();
        private readonly HashSet<string> used = new();

        public TilePool(GridGenInput input)
        {
            var topics = input.BaseResult.Topics;
            var map = BuildTopicTileMap(input, topics);
            var rng = new System.Random();

            // 🔹 1. Collect all merge-output keys to exclude
            var mergeOutputKeys = new HashSet<string>();
            foreach (var t in topics)
            {
                if (!string.IsNullOrEmpty(t.ResultToTopicID) && !string.IsNullOrEmpty(t.ResultToTileID))
                    mergeOutputKeys.Add(Key(t.ResultToTopicID, t.ResultToTileID));
            }

            // 🔹 2. Build pool excluding merge-output tiles
            foreach (var kv in map)
            {
                var validTiles = kv.Value
                    .Where(cell => !mergeOutputKeys.Contains(Key(cell.TopicID, cell.TileID)))
                    .OrderBy(_ => rng.Next())
                    .ToList();

                if (validTiles.Count > 0)
                    pool[kv.Key] = new Queue<GridCell>(validTiles);
            }
        }

        /// <summary>Take a tile from a specific topic, if available.</summary>
        public bool TryTake(string topicId, out GridCell cell)
        {
            cell = default;
            if (!pool.TryGetValue(topicId, out var q))
                return false;

            while (q.Count > 0)
            {
                var cand = q.Dequeue();
                var key = Key(cand.TopicID, cand.TileID);
                if (used.Add(key))
                {
                    cell = cand;
                    return true;
                }
            }

            return false;
        }

        /// <summary>Take any remaining unused tile across all topics.</summary>
        public bool TryTakeAny(out GridCell cell, HashSet<string> excludeKeys = null)
        {
            cell = default;
            foreach (var kv in pool.ToList())
            {
                var q = kv.Value;
                while (q.Count > 0)
                {
                    var cand = q.Dequeue();
                    var key = Key(cand.TopicID, cand.TileID);

                    if (excludeKeys != null && excludeKeys.Contains(key))
                        continue;
                    if (used.Contains(key))
                        continue;

                    used.Add(key);
                    cell = cand;
                    return true;
                }

                if (q.Count == 0)
                    pool.Remove(kv.Key);
            }
            return false;
        }

        /// <summary>Mark a tile as used (external fill).</summary>
        public void MarkUsed(GridCell cell)
        {
            if (string.IsNullOrEmpty(cell.TopicID) || string.IsNullOrEmpty(cell.TileID))
                return;

            used.Add(Key(cell.TopicID, cell.TileID));
        }

        /// <summary>Total remaining tiles in the pool.</summary>
        public int RemainingTotal => pool.Values.Sum(q => q.Count);

        /// <summary>Remove an entire topic from pool (used for merge-from exclusion).</summary>
        public void RemoveTopic(string topicId)
        {
            if (pool.ContainsKey(topicId))
                pool.Remove(topicId);
        }

        /// <summary>Count remaining tiles of a specific topic.</summary>
        public int Remaining(string topicId)
        {
            return pool.TryGetValue(topicId, out var q) ? q.Count : 0;
        }
    }

    public static TilePool CreateGlobalTilePool(GridGenInput input)
        => new TilePool(input);

    #endregion
}
