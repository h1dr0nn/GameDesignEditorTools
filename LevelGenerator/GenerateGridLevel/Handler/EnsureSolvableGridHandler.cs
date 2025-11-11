using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public static class EnsureSolvableGridHandler
{
    private const int MergeSetSize = 4;

    private class HiddenRowInfo
    {
        public int RowIndex;
        public string RequiredTopic;
    }

    public static void Ensure(List<List<GridCell>> visible, List<List<GridCell>> hidden)
    {
        if (visible == null || hidden == null || hidden.Count == 0)
            return;

        var hiddenRows = BuildHiddenRowInfo(hidden);
        if (hiddenRows.Count == 0)
            return;

        var requiredTopics = new HashSet<string>(hiddenRows
            .Select(r => r.RequiredTopic)
            .Where(t => !string.IsNullOrEmpty(t)));

        if (requiredTopics.Count == 0)
            return;

        var visibleCounts = CountTopics(visible);

        foreach (var rowInfo in hiddenRows)
        {
            if (string.IsNullOrEmpty(rowInfo.RequiredTopic))
            {
                // Passive row: once previous merges are cleared it just drops in.
                AddRowToCounts(hidden[rowInfo.RowIndex], visibleCounts);
                continue;
            }

            EnsureTopicAccessible(rowInfo.RequiredTopic, rowInfo.RowIndex, visible, hidden, requiredTopics, visibleCounts);

            if (!visibleCounts.TryGetValue(rowInfo.RequiredTopic, out var count) || count < MergeSetSize)
            {
                Debug.LogWarning($"[GridGen] Unable to surface {rowInfo.RequiredTopic} before hidden row {rowInfo.RowIndex}. Level may be unsolvable.");
                continue;
            }

            visibleCounts[rowInfo.RequiredTopic] = count - MergeSetSize;
            if (visibleCounts[rowInfo.RequiredTopic] <= 0)
                visibleCounts.Remove(rowInfo.RequiredTopic);

            // Once the row is cleared, its tiles become part of the available pool.
            AddRowToCounts(hidden[rowInfo.RowIndex], visibleCounts);
        }
    }

    private static void EnsureTopicAccessible(
        string topicId,
        int currentRowIndex,
        List<List<GridCell>> visible,
        List<List<GridCell>> hidden,
        HashSet<string> requiredTopics,
        Dictionary<string, int> visibleCounts)
    {
        if (string.IsNullOrEmpty(topicId))
            return;

        if (!visibleCounts.TryGetValue(topicId, out var count))
            count = 0;

        int safety = 0;
        while (count < MergeSetSize && safety++ < 32)
        {
            if (!TryPromoteHiddenTile(topicId, currentRowIndex, visible, hidden, requiredTopics, visibleCounts))
                break;

            count = visibleCounts.TryGetValue(topicId, out var updated) ? updated : 0;
        }
    }

    private static bool TryPromoteHiddenTile(
        string topicId,
        int currentRowIndex,
        List<List<GridCell>> visible,
        List<List<GridCell>> hidden,
        HashSet<string> requiredTopics,
        Dictionary<string, int> visibleCounts)
    {
        if (!TrySelectVisibleSwapTarget(topicId, visible, requiredTopics, visibleCounts, out var visiblePos, out var removedTopic))
            return false;

        for (int r = currentRowIndex; r < hidden.Count; r++)
        {
            var row = hidden[r];
            for (int c = 0; c < row.Count; c++)
            {
                var candidate = row[c];
                if (candidate.IsReservedOutput || string.IsNullOrEmpty(candidate.TopicID))
                    continue;
                if (!string.Equals(candidate.TopicID, topicId))
                    continue;

                var swap = visible[visiblePos.row][visiblePos.col];
                row[c] = swap;
                visible[visiblePos.row][visiblePos.col] = candidate;

                Increment(visibleCounts, topicId, +1);
                if (!string.IsNullOrEmpty(removedTopic))
                    Increment(visibleCounts, removedTopic, -1);

                Debug.Log($"[GridGen] Promoted hidden tile ({topicId}) from row {r} to visible to maintain solvability.");
                return true;
            }
        }

        return false;
    }

    private static bool TrySelectVisibleSwapTarget(
        string topicId,
        List<List<GridCell>> visible,
        HashSet<string> requiredTopics,
        Dictionary<string, int> visibleCounts,
        out (int row, int col) position,
        out string removedTopic)
    {
        position = (-1, -1);
        removedTopic = null;

        bool TryFindCell(bool allowRequired, out (int row, int col) pos, out string removed)
        {
            pos = (-1, -1);
            removed = null;

            for (int r = 0; r < visible.Count; r++)
            {
                var row = visible[r];
                for (int c = 0; c < row.Count; c++)
                {
                    var cell = row[c];
                    if (cell.IsReservedOutput || string.IsNullOrEmpty(cell.TopicID))
                        continue;
                    if (string.Equals(cell.TopicID, topicId))
                        continue;

                    bool isRequired = requiredTopics.Contains(cell.TopicID);
                    if (!isRequired)
                    {
                        pos = (r, c);
                        removed = cell.TopicID;
                        return true;
                    }

                    if (allowRequired && visibleCounts.TryGetValue(cell.TopicID, out var count) && count > MergeSetSize)
                    {
                        pos = (r, c);
                        removed = cell.TopicID;
                        return true;
                    }
                }
            }

            return false;
        }

        if (TryFindCell(false, out position, out removedTopic))
            return true;

        if (TryFindCell(true, out position, out removedTopic))
            return true;

        return false;
    }

    private static void AddRowToCounts(List<GridCell> row, Dictionary<string, int> counts)
    {
        foreach (var cell in row)
        {
            if (string.IsNullOrEmpty(cell.TopicID))
                continue;

            Increment(counts, cell.TopicID, +1);
        }
    }

    private static Dictionary<string, int> CountTopics(List<List<GridCell>> grid)
    {
        return grid
            .SelectMany(r => r)
            .Where(c => !string.IsNullOrEmpty(c.TopicID))
            .GroupBy(c => c.TopicID)
            .ToDictionary(g => g.Key, g => g.Count());
    }

    private static List<HiddenRowInfo> BuildHiddenRowInfo(List<List<GridCell>> hidden)
    {
        var rows = new List<HiddenRowInfo>();

        for (int r = 0; r < hidden.Count; r++)
        {
            var row = hidden[r];
            if (row == null || row.Count == 0)
                continue;

            bool hasContent = row.Any(c => !string.IsNullOrEmpty(c.TopicID));
            if (!hasContent)
                continue;

            var mergeCell = row.FirstOrDefault(c => c.IsReservedOutput && !string.IsNullOrEmpty(c.MergeFromTopicID));

            rows.Add(new HiddenRowInfo
            {
                RowIndex = r,
                RequiredTopic = mergeCell.MergeFromTopicID
            });
        }

        return rows;
    }

    private static void Increment(Dictionary<string, int> counts, string topicId, int delta)
    {
        if (string.IsNullOrEmpty(topicId))
            return;

        counts.TryGetValue(topicId, out var current);
        current += delta;

        if (current <= 0)
            counts.Remove(topicId);
        else
            counts[topicId] = current;
    }
}
