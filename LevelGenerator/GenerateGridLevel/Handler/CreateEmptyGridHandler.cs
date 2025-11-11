using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public static class CreateEmptyGridHandler
{
	/// <summary>
	/// Create the visible and hidden grid containers before filling them.
	/// </summary>
	public static (List<List<GridCell>> visible, List<List<GridCell>> hidden) Create(GridGenInput input)
	{
		var allTopics = input.BaseResult.Topics;
		var visible = new List<List<GridCell>>();
		var hidden = new List<List<GridCell>>();

		int totalTopics = allTopics.Count;
		int hiddenCount = Mathf.Max(0, totalTopics - input.VisibleRows);

		// Create visible rows
		for (int r = 0; r < input.VisibleRows; r++)
		{
			var row = Enumerable.Range(0, input.Columns)
				.Select(_ => new GridCell())
				.ToList();
			visible.Add(row);
		}

		// Create hidden rows (for merge outputs)
		for (int r = 0; r < hiddenCount; r++)
		{
			var row = Enumerable.Range(0, input.Columns)
				.Select(_ => new GridCell())
				.ToList();
			hidden.Add(row);
		}

		Debug.Log($"[GridGen] Created grid: {input.VisibleRows} visible, {hiddenCount} hidden (total topics: {totalTopics})");
		return (visible, hidden);
	}
}
