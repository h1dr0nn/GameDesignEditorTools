#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

public static class GridLevelExporter
{
    private const string Header = "Level,RowId,IsShow,ItemId,TopicId,TileID,ItemName";

    public static void Export(string csvPath, int levelNumber, GridGenResult grid)
    {
        if (grid.Visible == null || grid.Visible.Count == 0)
        {
            Debug.LogError("[GridLevelExporter] No visible grid to export.");
            return;
        }

        var directory = Path.GetDirectoryName(csvPath);
        if (!Directory.Exists(directory))
            Directory.CreateDirectory(directory);

        List<string> lines = new();
        if (File.Exists(csvPath))
            lines = File.ReadAllLines(csvPath).ToList();

        if (lines.Count == 0 || !lines[0].StartsWith("Level"))
            lines.Insert(0, Header);

        int startIndex = -1;
        for (int i = 0; i < lines.Count; i++)
        {
            if (lines[i].StartsWith($"{levelNumber},"))
            {
                startIndex = i;
                break;
            }
        }

        if (startIndex != -1)
        {
            int removeCount = 0;
            for (int i = startIndex; i < lines.Count; i++)
            {
                if (lines[i].StartsWith($"{levelNumber},") || lines[i].StartsWith(","))
                    removeCount++;
                else break;
            }
            lines.RemoveRange(startIndex, removeCount);
        }

        List<string> newLines = new();
        int rowCounter = 0;

        for (int r = 0; r < grid.Visible.Count; r++)
        {
            int rowId = r;
            var row = grid.Visible[grid.Visible.Count - 1 - r];
            bool isShow = true;

            for (int c = 0; c < row.Count; c++)
            {
                var cell = row[c];
                string levelCol = (r == 0 && c == 0) ? levelNumber.ToString() : string.Empty;
                string rowCol = (c == 0) ? rowId.ToString() : string.Empty;
                string showCol = (c == 0) ? isShow.ToString().ToUpper() : string.Empty;

                newLines.Add($"{levelCol},{rowCol},{showCol},{c},{cell.TopicID},{cell.TileID},{Escape(cell.TileName)}");
            }

            rowCounter++;
        }

        if (grid.Hidden != null && grid.Hidden.Count > 0)
        {
            for (int r = 0; r < grid.Hidden.Count; r++)
            {
                int rowId = rowCounter++;
                var row = grid.Hidden[grid.Hidden.Count - 1 - r];
                bool isShow = false;

                for (int c = 0; c < row.Count; c++)
                {
                    var cell = row[c];
                    string levelCol = (r == 0 && c == 0 && grid.Visible.Count == 0) ? levelNumber.ToString() : string.Empty;
                    string rowCol = (c == 0) ? rowId.ToString() : string.Empty;
                    string showCol = (c == 0) ? isShow.ToString().ToUpper() : string.Empty;

                    newLines.Add($"{levelCol},{rowCol},{showCol},{c},{cell.TopicID},{cell.TileID},{Escape(cell.TileName)}");
                }
            }
        }

        int insertIndex = -1;
        for (int i = 0; i < lines.Count; i++)
        {
            if (int.TryParse(lines[i].Split(',')[0], out int oldLevel) && oldLevel > levelNumber)
            {
                insertIndex = i;
                break;
            }
        }

        if (insertIndex >= 0)
            lines.InsertRange(insertIndex, newLines);
        else
            lines.AddRange(newLines);

        File.WriteAllLines(csvPath, lines);
        AssetDatabase.Refresh();

        Debug.Log($"[GridLevelExporter] Exported Level {levelNumber} ({grid.Visible.Count} visible + {grid.Hidden.Count} hidden)");
    }

    private static string Escape(string text)
    {
        if (string.IsNullOrEmpty(text))
            return string.Empty;
        if (text.Contains(",") || text.Contains("\""))
            return $"\"{text.Replace("\"", "\"\"")}\"";
        return text;
    }
}
#endif
