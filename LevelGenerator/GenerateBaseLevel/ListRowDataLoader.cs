using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

[Serializable]
public class TileData
{
    public int TileID;
    public string TileName;
    public string TileImg;
}

[Serializable]
public class TopicData
{
    public int TopicID;
    public string TopicName;
    public List<TileData> Tiles = new();
}

[Serializable]
public class CategoryData
{
    public int CategoryID;
    public string CategoryName;
    public List<TopicData> Topics = new();
}

public class ListRowDataLoader
{
    private List<CategoryData> categories = new();

    public int TopicsCount => categories.Sum(c => c.Topics.Count);

    public void Load(TextAsset csvFile)
    {
        if (csvFile == null)
        {
            Debug.LogError("[ListRowDataLoader] CSV file not assigned!");
            return;
        }

        categories = ParseCSV(csvFile.text);
        Debug.Log($"[ListRowDataLoader] Loaded {categories.Count} categories from CSV.");
    }

    private List<CategoryData> ParseCSV(string csvText)
    {
        var lines = csvText.Split(new[] { '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries);
        var dataList = new List<CategoryData>();
        CategoryData currentCategory = null;
        TopicData currentTopic = null;

        for (int i = 1; i < lines.Length; i++)
        {
            var line = lines[i].Trim();
            if (string.IsNullOrWhiteSpace(line)) continue;

            var parts = line.Split(',');
            while (parts.Length < 7) Array.Resize(ref parts, 7);

            string catIdStr = parts[0].Trim();
            string catName = parts[1].Trim();
            string topicIdStr = parts[2].Trim();
            string topicName = parts[3].Trim();
            string tileIdStr = parts[4].Trim();
            string tileName = parts[5].Trim();
            string tileImg = parts[6].Trim();

            if (!string.IsNullOrEmpty(catIdStr))
            {
                int.TryParse(catIdStr, out int catId);
                currentCategory = new CategoryData
                {
                    CategoryID = catId,
                    CategoryName = catName,
                    Topics = new List<TopicData>()
                };
                dataList.Add(currentCategory);
            }

            if (!string.IsNullOrEmpty(topicIdStr))
            {
                int.TryParse(topicIdStr, out int topicId);
                currentTopic = new TopicData
                {
                    TopicID = topicId,
                    TopicName = topicName,
                    Tiles = new List<TileData>()
                };
                currentCategory?.Topics.Add(currentTopic);
            }

            if (!string.IsNullOrEmpty(tileIdStr))
            {
                int.TryParse(tileIdStr, out int tileId);
                var tile = new TileData
                {
                    TileID = tileId,
                    TileName = tileName,
                    TileImg = tileImg
                };
                currentTopic?.Tiles.Add(tile);
            }
        }

        return dataList;
    }

    public List<(int categoryId, string categoryName, int topicId, string topicName)> GetAllTopics()
    {
        var result = new List<(int, string, int, string)>();
        foreach (var cat in categories)
        {
            foreach (var topic in cat.Topics)
                result.Add((cat.CategoryID, cat.CategoryName, topic.TopicID, topic.TopicName));
        }
        return result;
    }

    public List<RawTopicInfo> GetAllTopicsRaw()
    {
        var topics = GetAllTopics();
        var rawTopics = new List<RawTopicInfo>();

        foreach (var t in topics)
        {
            var tiles = GetTilesByTopicId(t.topicId);
            var tileList = tiles.tiles.Select(x => new RawTileInfo
            {
                TileID = x.id,
                TileName = x.name
            }).ToList();

            rawTopics.Add(new RawTopicInfo
            {
                CategoryID = t.categoryId,
                CategoryName = t.categoryName,
                TopicID = t.topicId,
                TopicName = t.topicName,
                Tiles = tileList
            });
        }

        return rawTopics;
    }

    public (int categoryId, string categoryName, List<(int id, string name)> tiles) GetTilesByTopicId(int topicId)
    {
        foreach (var cat in categories)
        {
            var topic = cat.Topics.FirstOrDefault(t => t.TopicID == topicId);
            if (topic != null)
            {
                var tiles = topic.Tiles.Select(t => (t.TileID, t.TileName)).ToList();
                return (cat.CategoryID, cat.CategoryName, tiles);
            }
        }
        return (0, string.Empty, new List<(int, string)>());
    }
}
