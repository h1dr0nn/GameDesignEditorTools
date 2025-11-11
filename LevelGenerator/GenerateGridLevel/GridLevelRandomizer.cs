using System.Collections.Generic;
using UnityEngine;

public static class GridLevelRandomizer
{
    /// <summary>
    /// Master entry point to generate a level grid.
    /// Pipeline:
    ///  1️. Create empty grid containers
    ///  2️. Fill visible tiles
    ///  3️. Fill hidden tiles (merge outputs)
    ///  4️. Balance grid (difficulty tuning)
    ///  5️. Validate final solvability
    /// </summary>
    public static GridGenResult Generate(GridGenInput input)
    {
        Debug.Log("========== [GridGen] START LEVEL RANDOMIZER ==========");

        // 1️. Create grid containers
        var (visible, hidden) = CreateEmptyGridHandler.Create(input);

        var pool = GridRandomizerHelpers.CreateGlobalTilePool(input);

        // 2️. Fill visible grid
        FillVisibleTileHandler.Fill(input, visible, pool);

        // 3️. Fill hidden grid (merge outputs)
        FillHiddenTileHandler.Fill(input, visible, hidden, pool);

        // 4️. Balance difficulty
        BalanceGridHandler.Balance(input, visible, hidden);

        // 5️. Validate final solvability
        ValidateGridHandler.Validate(input, visible, hidden);

        Debug.Log("========== [GridGen] LEVEL RANDOMIZER COMPLETE ==========");

        return new GridGenResult
        {
            Columns = input.Columns,
            VisibleRows = input.VisibleRows,
            Visible = visible,
            Hidden = hidden
        };
    }

    /// <summary>
    /// Count how many topics in the visible grid already have at least 4 tiles.
    /// Used for quick analytics or auto-balance previews.
    /// </summary>
    public static int CheckTopicFullInVisible(List<List<GridCell>> visible)
    {
        return ValidateGridHandlerCheckTopicFull(visible);
    }

    /// <summary>
    /// Check whether a level is solvable given visible and hidden grids.
    /// Can be called externally for quick verification.
    /// </summary>
    public static bool CheckSolvable(List<List<GridCell>> visible, List<List<GridCell>> hidden)
    {
        return ValidateGridHandlerCheckSolvable(visible, hidden);
    }

    /// <summary>
    /// Check whether all topics in the grid have exactly 4 tiles.
    /// Used for final validation or QA tools.
    /// </summary>
    public static bool CheckAllTopicComplete(List<List<GridCell>> visible, List<List<GridCell>> hidden)
    {
        return ValidateGridHandlerCheckTopicComplete(visible, hidden);
    }

    /// <summary>
    /// Calculate the difficulty score of the current grid using the same formula as validation.
    /// Used for analytics, balancing, or auto-tuning systems.
    /// </summary>
    public static float CheckDifficultyScore(GridGenInput input, List<List<GridCell>> visible, List<List<GridCell>> hidden)
    {
        return ValidateGridHandlerCheckDifficultyScore(input, visible, hidden);
    }

    #region 🔸 Local Helpers
    private static float ValidateGridHandlerCheckDifficultyScore(GridGenInput input, List<List<GridCell>> visible, List<List<GridCell>> hidden)
    {
        var topicFull = ValidateGridHandlerCheckTopicFull(visible);
        var args = new object[] { input, visible, hidden, topicFull };

        var result = typeof(ValidateGridHandler)
            .GetMethod("CalculateDifficultyScore", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static)
            ?.Invoke(null, args);

        return result is float f ? f : 0f;
    }

    private static bool ValidateGridHandlerCheckTopicComplete(List<List<GridCell>> visible, List<List<GridCell>> hidden)
    {
        var args = new object[] { visible, hidden };
        var result = typeof(ValidateGridHandler)
            .GetMethod("CheckTopicTileCompleteness", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static)
            ?.Invoke(null, args);
        return result is bool b && b;
    }


    private static int ValidateGridHandlerCheckTopicFull(List<List<GridCell>> visible)
    {
        var temp = new List<List<GridCell>>(visible);
        return typeof(ValidateGridHandler)
            .GetMethod("CheckTopicFullInVisible", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static)
            ?.Invoke(null, new object[] { temp }) is int count ? count : 0;
    }

    private static bool ValidateGridHandlerCheckSolvable(List<List<GridCell>> visible, List<List<GridCell>> hidden)
    {
        var args = new object[] { visible, hidden };
        var result = typeof(ValidateGridHandler)
            .GetMethod("CheckSolvable", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static)
            ?.Invoke(null, args);
        return result is bool b && b;
    }
    #endregion
}
