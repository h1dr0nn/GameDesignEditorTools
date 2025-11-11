using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public static class BalanceGridHandler
{
    /// <summary>
    /// Balance visible & hidden grid distribution.
    /// Rules:
    /// 1️. Prevent too-large topic groups (>limitPerRow)
    /// 2️. Ensure enough merge opportunities (minMerge, maxMerge)
    /// 3️. Swap cells randomly between visible/hidden to balance difficulty
    /// </summary>
    public static void Balance(GridGenInput input, List<List<GridCell>> visible, List<List<GridCell>> hidden)
    {
        var rng = new System.Random();
        bool isHard = string.Equals(input.BaseResult.LevelType, "Hard", StringComparison.OrdinalIgnoreCase);

        int totalCells = visible.Count * input.Columns;
        int totalGroups = Mathf.Max(1, totalCells / 4);

        // Define thresholds
        float minRatio = isHard ? 0.3f : 0.6f;
        float maxRatio = isHard ? 0.5f : 0.8f;

        int minMerge = Mathf.CeilToInt(totalGroups * minRatio);
        int maxMerge = Mathf.CeilToInt(totalGroups * maxRatio);
        int limitPerRow = isHard ? 2 : 3;

        // Collect editable cells (non-reserved)
        var allEditable = GetEditableCells(visible, hidden);

        if (allEditable.Count == 0)
            return;

        // Step 1️: Break too-large groups in visible
        foreach (var row in visible)
        {
            var grouped = row
                .Where(x => !x.IsReservedOutput)
                .GroupBy(x => x.TopicID);

            foreach (var g in grouped)
            {
                if (g.Count() >= 4)
                {
                    var target = allEditable
                        .Where(c => c.TopicID != g.Key)
                        .OrderBy(_ => rng.Next())
                        .FirstOrDefault();

                    if (!string.IsNullOrEmpty(target.TopicID))
                        SwapCells(g.First(), target, visible, hidden);
                }
            }
        }

        // Step 2️: Limit per-row group size
        foreach (var row in visible)
        {
            var grouped = row
                .Where(x => !x.IsReservedOutput)
                .GroupBy(x => x.TopicID)
                .ToList();

            foreach (var g in grouped)
            {
                if (g.Count() > limitPerRow)
                {
                    int excess = g.Count() - limitPerRow;
                    var toMove = g.Take(excess).ToList();

                    foreach (var cell in toMove)
                    {
                        var target = allEditable
                            .Where(x => x.TopicID != cell.TopicID)
                            .OrderBy(_ => rng.Next())
                            .FirstOrDefault();

                        if (!string.IsNullOrEmpty(target.TopicID))
                            SwapCells(cell, target, visible, hidden);
                    }
                }
            }
        }

        // Step 3️: Adjust total merge opportunities
        int mergeGroups = CountMergeGroups(visible);
        int safety = 0;

        // Ensure minimum merge groups
        while (mergeGroups < minMerge && safety++ < 50)
        {
            var diff = allEditable.OrderBy(_ => rng.Next()).Take(6).ToList();
            if (diff.Count >= 2)
                SwapCells(diff[0], diff[1], visible, hidden);

            allEditable = GetEditableCells(visible, hidden); // update after swap
            mergeGroups = CountMergeGroups(visible);
        }

        // Ensure maximum merge groups not exceeded
        safety = 0;
        while (mergeGroups > maxMerge && safety++ < 50)
        {
            var same = allEditable
                .GroupBy(c => c.TopicID)
                .Where(g => g.Count() >= 4)
                .SelectMany(g => g.Take(2))
                .OrderBy(_ => rng.Next())
                .ToList();

            if (same.Count >= 2)
                SwapCells(same[0], same[1], visible, hidden);

            allEditable = GetEditableCells(visible, hidden); // update after swap
            mergeGroups = CountMergeGroups(visible);
        }

        Debug.Log($"[GridGen] Balanced grid (Hard={isHard}, mergeGroups={mergeGroups}, min={minMerge}, max={maxMerge})");
    }

    // ============================================================
    #region 🔸 Local Helpers

    /// <summary>Collect all editable cells (non-reserved, valid topic).</summary>
    private static List<GridCell> GetEditableCells(List<List<GridCell>> visible, List<List<GridCell>> hidden)
    {
        return visible.Concat(hidden)
            .SelectMany(r => r)
            .Where(c => !c.IsReservedOutput && !string.IsNullOrEmpty(c.TopicID))
            .ToList();
    }

    /// <summary>Count how many mergeable topic groups (>=4 tiles) exist.</summary>
    private static int CountMergeGroups(List<List<GridCell>> grid)
    {
        return grid
            .SelectMany(r => r)
            .Where(c => !string.IsNullOrEmpty(c.TopicID))
            .GroupBy(c => c.TopicID)
            .Count(g => g.Count() >= 4);
    }

    /// <summary>Swap between visible / hidden grids.</summary>
    public static void SwapCells(GridCell cellA, GridCell cellB,
        List<List<GridCell>> visible, List<List<GridCell>> hidden)
    {
        bool swapped = false;

        void TrySwap(List<List<GridCell>> grid)
        {
            for (int r = 0; r < grid.Count; r++)
            {
                for (int c = 0; c < grid[r].Count; c++)
                {
                    if (swapped) return;

                    if (Equals(grid[r][c], cellA))
                    {
                        for (int rr = 0; rr < grid.Count; rr++)
                        {
                            for (int cc = 0; cc < grid[rr].Count; cc++)
                            {
                                if (Equals(grid[rr][cc], cellB))
                                {
                                    (grid[r][c], grid[rr][cc]) = (grid[rr][cc], grid[r][c]);
                                    swapped = true;
                                    return;
                                }
                            }
                        }
                    }
                }
            }
        }

        TrySwap(visible);
        if (!swapped) TrySwap(hidden);
    }

    #endregion
}
