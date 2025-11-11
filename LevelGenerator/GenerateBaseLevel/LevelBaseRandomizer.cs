using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

[Serializable]
public struct LevelGenInput
{
    public int Level;
    public RewardType RewardType;
    public int RewardAmount;
    public int TotalTopics;
    public int TopicShow;
    public bool IsDifferentCategory;
    public LevelType LevelType;

    public int DefaultPositions;
    public List<string> AllColorTopic;
    public List<RawTopicInfo> AllTopics;
    public List<int> CacheTopicIds;
}

[Serializable]
public struct RawTopicInfo
{
    public int CategoryID;
    public string CategoryName;
    public int TopicID;
    public string TopicName;
    public List<RawTileInfo> Tiles;
}

[Serializable]
public struct RawTileInfo
{
    public int TileID;
    public string TileName;
}

[Serializable]
public struct LevelGenResult
{
    public string Level;
    public string RewardType;
    public string RewardAmount;
    public string RowShow;
    public string LevelType;

    public List<ResultTopicInfo> Topics;
}

[Serializable]
public struct ResultTopicInfo
{
    public string TopicID;
    public string TopicName;

    public string ResultColor;
    public string ResultToTopicID;
    public string ResultToTileID;
    public string ResultToPositionShow;
}

[Serializable]
public enum RewardType
{
    None,
    Coin
}

[Serializable]
public enum LevelType
{
    Normal,
    Hard
}

public static class LevelBaseRandomizer
{
    public static LevelGenResult Generate(LevelGenInput input)
    {
        if (input.AllTopics == null || input.AllTopics.Count == 0)
        {
            Debug.LogWarning("[LevelBaseRandomizer] LevelBaseRandomizer.Generate: AllTopics empty!");
            return new LevelGenResult
            {
                Level = input.Level.ToString(),
                RewardType = RewardType.None.ToString(),
                RewardAmount = "0",
                Topics = new List<ResultTopicInfo>()
            };
        }

        var selectedTopics = SelectTopics(input);

        int mergeCount = GetRandomMergeCount(input, selectedTopics.Count);

        var merges = CreateTopicMerges(selectedTopics, mergeCount);

        RewardType rewardType = input.RewardType;
        int rewardAmount = input.RewardAmount;

        if (rewardType == RewardType.None || rewardAmount <= 0)
        {
            (rewardType, rewardAmount) = GetReward(input.LevelType);
        }

        string rewardTypeStr = rewardType.ToString();
        string rewardAmountStr = rewardAmount.ToString();


        var colorPicker = BuildColorCycle(input.AllColorTopic);

        var result = new LevelGenResult
        {
            Level = input.Level.ToString(),
            RewardType = rewardTypeStr,
            RewardAmount = rewardAmountStr,
            RowShow = input.TopicShow.ToString(),
            LevelType = input.LevelType.ToString(),
            Topics = new List<ResultTopicInfo>(selectedTopics.Count)
        };

        int posMod = Mathf.Max(0, input.DefaultPositions);

        for (int i = 0; i < selectedTopics.Count; i++)
        {
            var t = selectedTopics[i];

            var m = merges.FirstOrDefault(mm => mm.SourceTopicID == t.TopicID);
            bool hasMerge = m.TargetTopicID != 0;

            string resToTopic = hasMerge ? m.TargetTopicID.ToString() : string.Empty;
            string resToTile = hasMerge ? m.TargetTileID.ToString() : string.Empty;

            string color = colorPicker.Count > 0 ? colorPicker[i % colorPicker.Count] : string.Empty;

            string posStr = string.Empty;
            if (hasMerge)
            {
                int pos = (posMod > 0) ? (i % posMod) : i;
                posStr = pos.ToString();
            }

            result.Topics.Add(new ResultTopicInfo
            {
                TopicID = t.TopicID.ToString(),
                TopicName = t.TopicName ?? string.Empty,

                ResultColor = color,
                ResultToTopicID = resToTopic,
                ResultToTileID = resToTile,
                ResultToPositionShow = posStr
            });
        }

        Debug.Log($"[LevelBaseRandomizer] Generate | Level={result.Level} | Topics={result.Topics.Count} | Merge={merges.Count} | Reward={result.RewardType}:{result.RewardAmount}");
        return result;
    }

    private static List<RawTopicInfo> SelectTopics(LevelGenInput input)
    {
        var rnd = new System.Random();
        var all = input.AllTopics.ToList();

        var cache = input.CacheTopicIds ?? new List<int>();
        var available = all.Where(t => !cache.Contains(t.TopicID)).ToList();

        bool exhausted = available.Count < input.TotalTopics;
        var pool = exhausted ? all : available;

        if (input.IsDifferentCategory)
        {
            var chosen = new List<RawTopicInfo>(input.TotalTopics);
            var grouped = pool.GroupBy(t => t.CategoryID).OrderBy(_ => rnd.Next()).ToList();

            foreach (var g in grouped)
            {
                var pick = g.OrderBy(_ => rnd.Next()).First();
                if (!chosen.Any(c => c.TopicID == pick.TopicID))
                    chosen.Add(pick);
                if (chosen.Count >= input.TotalTopics) break;
            }

            if (chosen.Count < input.TotalTopics)
            {
                var remain = pool
                    .Where(t => !chosen.Any(c => c.TopicID == t.TopicID))
                    .OrderBy(_ => rnd.Next())
                    .Take(input.TotalTopics - chosen.Count);
                chosen.AddRange(remain);
            }

            if (chosen.Count < input.TotalTopics)
            {
                var fill = all
                    .Where(t => !chosen.Any(c => c.TopicID == t.TopicID))
                    .OrderBy(_ => rnd.Next())
                    .Take(input.TotalTopics - chosen.Count);
                chosen.AddRange(fill);
            }

            return chosen;
        }
        else
        {
            var pick = pool.OrderBy(_ => rnd.Next()).Take(input.TotalTopics).ToList();

            if (pick.Count < input.TotalTopics)
            {
                var fill = all
                    .Where(t => !pick.Any(c => c.TopicID == t.TopicID))
                    .OrderBy(_ => rnd.Next())
                    .Take(input.TotalTopics - pick.Count);
                pick.AddRange(fill);
            }

            return pick;
        }
    }


    private static int GetRandomMergeCount(LevelGenInput input, int selectedCount)
    {
        int maxMergePossible = Mathf.Max(0, input.TotalTopics - input.TopicShow);
        if (maxMergePossible == 0) return 0;

        return maxMergePossible;
    }

    //private static List<TopicMergeData> CreateTopicMerges(List<RawTopicInfo> selectedTopics, int mergeCount)
    //{
    //    var list = new List<TopicMergeData>();
    //    if (mergeCount <= 0 || selectedTopics.Count < 2) return list;

    //    var rnd = new System.Random();
    //    var shuffled = selectedTopics.OrderBy(_ => rnd.Next()).ToList();

    //    int maxPairs = Mathf.Min(mergeCount, Mathf.FloorToInt(selectedTopics.Count / 2f));
    //    for (int i = 0; i < maxPairs; i++)
    //    {
    //        var topicA = shuffled[i];
    //        var topicB = shuffled[i + maxPairs];
    //        if (topicB.Tiles == null || topicB.Tiles.Count == 0) continue;

    //        var tile = topicB.Tiles[rnd.Next(topicB.Tiles.Count)];

    //        list.Add(new TopicMergeData
    //        {
    //            SourceTopicID = topicA.TopicID,
    //            TargetTopicID = topicB.TopicID,
    //            TargetTileID = tile.TileID
    //        });
    //    }

    //    return list;
    //}

    private static List<TopicMergeData> CreateTopicMerges(List<RawTopicInfo> selectedTopics, int mergeCount)
    {
        var list = new List<TopicMergeData>();
        if (mergeCount <= 0 || selectedTopics.Count < 2)
            return list;

        var rnd = new System.Random();
        var pool = selectedTopics.OrderBy(_ => rnd.Next()).ToList();

        int availableCount = pool.Count;
        int mergeLayer = 1;

        while (mergeCount > 0 && availableCount >= 2)
        {
            int pairs = Mathf.Min(mergeCount, Mathf.FloorToInt(availableCount / 2f));
            for (int i = 0; i < pairs; i++)
            {
                var topicA = pool[i];
                var topicB = pool[i + pairs];
                if (topicB.Tiles == null || topicB.Tiles.Count == 0)
                    continue;

                var tile = topicB.Tiles[rnd.Next(topicB.Tiles.Count)];

                list.Add(new TopicMergeData
                {
                    SourceTopicID = topicA.TopicID,
                    TargetTopicID = topicB.TopicID,
                    TargetTileID = tile.TileID
                });
            }

            mergeCount -= pairs;
            if (mergeCount <= 0) break;

            var nextLayer = list.Select(x => selectedTopics.FirstOrDefault(t => t.TopicID == x.TargetTopicID))
                                .Where(t => t.TopicID != 0)
                                .OrderBy(_ => rnd.Next())
                                .ToList();

            if (nextLayer.Count < 2)
                break;

            pool = nextLayer;
            availableCount = pool.Count;
            mergeLayer++;
        }

        Debug.Log($"[LevelBaseRandomizer] Multi-layer Merge | Layers={mergeLayer} | Total Merges={list.Count}");
        return list;
    }

    private static (RewardType type, int amount) GetReward(LevelType levelType)
    {
        switch (levelType)
        {
            case LevelType.Hard:
                return (RewardType.Coin, UnityEngine.Random.Range(200, 401));
            case LevelType.Normal:
                return (RewardType.Coin, UnityEngine.Random.Range(100, 201));
            default:
                return (RewardType.None, 0);
        }
    }

    private static List<string> BuildColorCycle(List<string> allColors)
    {
        if (allColors == null || allColors.Count == 0) return new List<string>();

        var rnd = new System.Random();
        var shuffled = allColors.Where(c => !string.IsNullOrWhiteSpace(c)).ToList();
        shuffled = shuffled.OrderBy(_ => rnd.Next()).ToList();

        return shuffled;
    }

    private struct TopicMergeData
    {
        public int SourceTopicID;
        public int TargetTopicID;
        public int TargetTileID;
    }
}

