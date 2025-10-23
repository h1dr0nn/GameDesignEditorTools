#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;

public static class AssetRenameBlueprint
{
    public class CsvRow
    {
        public string guid;
        public string path;
        public string oldName;
        public string newName;
        public int lineNo;
    }

    public class CsvIndex
    {
        public Dictionary<string, List<CsvRow>> byGuid;
        public Dictionary<string, List<CsvRow>> byPath;
        public Dictionary<string, List<CsvRow>> byName;
        public char usedDelimiter;
        public bool usedHeader;
        public string note;
        public int total, validNew, blankNew, malformed;
    }

    public static CsvIndex BuildCsvIndex(TextAsset csv, char configuredDelimiter, bool configuredHasHeader, bool caseInsensitive, bool autoDetect)
    {
        if (csv == null) return null;

        var lines = ReadAllNonEmptyLines(csv.text, out bool hadBOM);
        if (lines.Count == 0)
        {
            return new CsvIndex
            {
                byGuid = new Dictionary<string, List<CsvRow>>(StringComparer.OrdinalIgnoreCase),
                byPath = new Dictionary<string, List<CsvRow>>(StringComparer.OrdinalIgnoreCase),
                byName = new Dictionary<string, List<CsvRow>>(StringComparer.OrdinalIgnoreCase),
                usedDelimiter = configuredDelimiter,
                usedHeader = configuredHasHeader,
                note = "CSV empty.",
                total = 0
            };
        }

        char usedDelim = configuredDelimiter;
        bool usedHeader = configuredHasHeader;
        if (autoDetect)
        {
            usedDelim = GuessDelimiter(lines[0], configuredDelimiter);
            usedHeader = LooksLikeHeader(lines[0], usedDelim);
        }

        var (hGuid, hPath, hOld, hNew) = IdentifyColumns(usedHeader ? lines[0] : null, usedDelim);
        var comparer = caseInsensitive ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal;
        var idxGuid = new Dictionary<string, List<CsvRow>>(comparer);
        var idxPath = new Dictionary<string, List<CsvRow>>(comparer);
        var idxName = new Dictionary<string, List<CsvRow>>(comparer);

        int start = usedHeader ? 1 : 0;
        int total = 0, validNew = 0, blankNew = 0, malformed = 0;

        for (int i = start; i < lines.Count; i++)
        {
            total++;
            var parts = SplitCsvLine(lines[i], usedDelim);
            if (parts.Length < 1) { malformed++; continue; }

            var row = new CsvRow { lineNo = i + 1 };
            row.guid = ReadCol(parts, hGuid)?.Trim();
            row.path = NormalizeAssetPath(ReadCol(parts, hPath)?.Trim());
            row.oldName = ReadCol(parts, hOld)?.Trim();
            row.newName = ReadCol(parts, hNew)?.Trim();

            if (string.IsNullOrEmpty(row.newName)) blankNew++; else validNew++;

            if (!string.IsNullOrEmpty(row.guid)) AddIndex(idxGuid, row.guid, row);
            if (!string.IsNullOrEmpty(row.path)) AddIndex(idxPath, row.path, row);
            if (!string.IsNullOrEmpty(row.oldName)) AddIndex(idxName, row.oldName, row);
        }

        return new CsvIndex
        {
            byGuid = idxGuid,
            byPath = idxPath,
            byName = idxName,
            usedDelimiter = usedDelim,
            usedHeader = usedHeader,
            note = hadBOM ? "BOM removed." : "",
            total = total,
            validNew = validNew,
            blankNew = blankNew,
            malformed = malformed
        };
    }

    private static void AddIndex(Dictionary<string, List<CsvRow>> idx, string key, CsvRow row)
    {
        if (!idx.TryGetValue(key, out var list))
        {
            list = new List<CsvRow>();
            idx[key] = list;
        }
        list.Add(row);
    }

    public static bool TryPick(Dictionary<string, List<CsvRow>> map, string key, out string newName, out string note)
    {
        newName = null;
        note = "";
        if (string.IsNullOrEmpty(key) || map == null) return false;
        if (!map.TryGetValue(key, out var lst) || lst == null || lst.Count == 0) return false;

        var nonEmpty = lst.Where(x => !string.IsNullOrWhiteSpace(x.newName)).ToList();
        if (nonEmpty.Count == 0) { newName = ""; note = " (csv:newName empty)"; return true; }

        string first = nonEmpty[0].newName;
        bool conflict = nonEmpty.Any(x => !string.Equals(x.newName, first, StringComparison.Ordinal));
        if (conflict) { newName = nonEmpty.Last().newName; note = " (csv:conflict multi-rows)"; return true; }

        newName = first;
        return true;
    }

    public static string NormalizeAssetPath(string p)
    {
        if (string.IsNullOrEmpty(p)) return p;
        p = p.Replace("\\", "/");
        int i = p.IndexOf("Assets/", StringComparison.OrdinalIgnoreCase);
        if (i >= 0) p = p.Substring(i);
        return p;
    }

    private static List<string> ReadAllNonEmptyLines(string content, out bool hadBOM)
    {
        hadBOM = false;
        var res = new List<string>();
        using var sr = new StringReader(content);
        string line;
        bool first = true;
        while ((line = sr.ReadLine()) != null)
        {
            if (first)
            {
                string trimmed = line.TrimStart('\uFEFF');
                if (!ReferenceEquals(trimmed, line)) hadBOM = true;
                line = trimmed;
                first = false;
            }
            if (string.IsNullOrWhiteSpace(line)) continue;
            res.Add(line);
        }
        return res;
    }

    private static char GuessDelimiter(string firstLine, char fallback)
    {
        char[] cands = { ',', ';', '\t', '|' };
        int bestScore = -1; char best = fallback;
        foreach (var c in cands)
        {
            int score = SplitCsvLine(firstLine, c).Length;
            if (score > bestScore) { bestScore = score; best = c; }
        }
        return best;
    }

    private static bool LooksLikeHeader(string firstLine, char delim)
    {
        var cols = SplitCsvLine(firstLine, delim).Select(s => s.Trim().Trim('"').ToLowerInvariant()).ToArray();
        if (cols.Length < 1) return false;
        bool hasNew = cols.Any(c => c is "new" or "newname" or "rename" or "to");
        bool hasOldOrKey = cols.Any(c => c is "old" or "oldname" or "name" or "prefab" or "guid" or "path" or "assetpath" or "filename" or "asset");
        return hasNew && hasOldOrKey;
    }

    private static (int guid, int path, int old, int @new) IdentifyColumns(string headerLine, char delim)
    {
        int guid = -1, path = -1, old = -1, nw = -1;
        if (string.IsNullOrEmpty(headerLine)) return (-1, -1, 0, 1);

        var cols = SplitCsvLine(headerLine, delim).Select((s, i) => (s.Trim().Trim('"').ToLowerInvariant(), i)).ToList();
        foreach (var c in cols)
        {
            if (nw == -1 && (c.Item1 == "new" || c.Item1 == "newname" || c.Item1 == "rename" || c.Item1 == "to")) nw = c.Item2;
            if (old == -1 && (c.Item1 == "old" || c.Item1 == "oldname" || c.Item1 == "name" || c.Item1 == "prefab" || c.Item1 == "asset" || c.Item1 == "filename")) old = c.Item2;
            if (guid == -1 && (c.Item1 == "guid" || c.Item1 == "id")) guid = c.Item2;
            if (path == -1 && (c.Item1 == "path" || c.Item1 == "assetpath" || c.Item1 == "filepath")) path = c.Item2;
        }
        if (nw == -1 && cols.Count >= 2) nw = 1;
        if (old == -1 && cols.Count >= 1) old = 0;
        return (guid, path, old, nw);
    }

    private static string ReadCol(string[] parts, int idx) => idx < 0 || idx >= parts.Length ? null : parts[idx];

    private static string[] SplitCsvLine(string line, char delimiter)
    {
        var list = new List<string>();
        bool inQuotes = false;
        var current = new StringBuilder();
        for (int i = 0; i < line.Length; i++)
        {
            char c = line[i];
            if (c == '\"')
            {
                if (inQuotes && i + 1 < line.Length && line[i + 1] == '\"') { current.Append('\"'); i++; }
                else { inQuotes = !inQuotes; }
            }
            else if (c == delimiter && !inQuotes)
            {
                list.Add(current.ToString());
                current.Length = 0;
            }
            else
            {
                current.Append(c);
            }
        }
        list.Add(current.ToString());
        return list.ToArray();
    }
}
#endif
