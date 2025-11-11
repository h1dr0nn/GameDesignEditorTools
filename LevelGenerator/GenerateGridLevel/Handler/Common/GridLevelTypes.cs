
using System;
using System.Collections.Generic;

[Serializable]
public struct GridGenInput
{
    public int Columns;
    public int VisibleRows;
    public LevelGenResult BaseResult;
    public Dictionary<int, RawTopicInfo> TopicLookup;
}

[Serializable]
public struct GridGenResult
{
    public int Columns;
    public int VisibleRows;
    public List<List<GridCell>> Visible;
    public List<List<GridCell>> Hidden;
}

[Serializable]
public struct GridCell
{
    public string TopicID;
    public string TopicName;
    public string TileID;
    public string TileName;
    public bool IsReservedOutput;
    public string MergeFromTopicID;
}
