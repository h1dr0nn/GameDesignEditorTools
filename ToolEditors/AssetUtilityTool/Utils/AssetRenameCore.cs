#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.VersionControl;
using UnityEngine;

namespace h1dr0n.EditorTools
{
    public static class AssetRenameCore
    {
        public struct RenameResult
        {
            public UnityEngine.Object Asset;
            public string AssetType;
            public string Guid;
            public string Path;
            public string OldName;
            public string NewName;
            public string RequestedNew;
            public string Status;
            public string MatchKey;
            public string Note;
            public string Extension;
        }

        public static void ExportCsvFromAssets(
            List<UnityEngine.Object> assets,
            AssetUtilityTool.AssetTypeFilter assetFilter,
            ref TextAsset linkedCsv)
        {
            if (assets == null || assets.Count == 0)
            {
                EditorUtility.DisplayDialog("Export CSV", "Asset list is empty.", "OK");
                return;
            }

            var uniq = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var names = new List<(string guid, string path, string name, string ext, string type)>();

            foreach (var a in assets.Where(a => a != null))
            {
                var path = AssetDatabase.GetAssetPath(a);
                if (string.IsNullOrEmpty(path) || AssetDatabase.IsValidFolder(path)) continue;
                if (!IsPathMatchesFilter(path, assetFilter)) continue;
                if (!uniq.Add(path)) continue;

                names.Add((
                    AssetDatabase.AssetPathToGUID(path),
                    path,
                    Path.GetFileNameWithoutExtension(path),
                    Path.GetExtension(path) ?? "",
                    GetAssetTypeName(path)
                ));
            }

            if (names.Count == 0)
            {
                EditorUtility.DisplayDialog("Export CSV", "No valid asset found.", "OK");
                return;
            }

            var savePath = EditorUtility.SaveFilePanelInProject(
                "Save CSV Mapping",
                "AssetRenameMapping.csv",
                "csv",
                "Columns: oldName,newName,guid,path,type"
            );
            if (string.IsNullOrEmpty(savePath)) return;

            var sb = new StringBuilder();
            char d = ',';
            sb.Append("oldName").Append(d)
              .Append("newName").Append(d)
              .Append("guid").Append(d)
              .Append("path").Append(d)
              .Append("type").AppendLine();

            foreach (var n in names.OrderBy(x => x.name, StringComparer.OrdinalIgnoreCase))
            {
                sb.Append(CsvEscape(n.name, d)).Append(d)
                  .Append("").Append(d)
                  .Append(CsvEscape(n.guid, d)).Append(d)
                  .Append(CsvEscape(n.path, d)).Append(d)
                  .Append(CsvEscape(n.type, d)).AppendLine();
            }

            try
            {
                File.WriteAllText(savePath, sb.ToString(), new UTF8Encoding(true));
                AssetDatabase.ImportAsset(savePath);
                var asset = AssetDatabase.LoadAssetAtPath<TextAsset>(savePath);
                linkedCsv = asset;
                AssetDatabase.SaveAssets();
                EditorGUIUtility.PingObject(asset);
                EditorUtility.DisplayDialog("Export CSV", $"Exported {names.Count} rows and auto-linked CSV Mapping.", "OK");
            }
            catch (Exception e)
            {
                Debug.LogError($"[AssetRenameCore] ExportCsvFromAssets failed: {e.Message}");
                EditorUtility.DisplayDialog("Export CSV", "Failed to save CSV file.", "OK");
            }
        }

        public static List<RenameResult> ScanPreview(
            IEnumerable<UnityEngine.Object> assets,
            TextAsset csvMapping,
            char delimiter,
            bool hasHeader,
            bool caseInsensitive,
            bool autoDetectCsv,
            bool autoSanitize,
            AssetUtilityTool.AssetTypeFilter assetFilter,
            bool matchByGuid, bool matchByPath, bool matchByName)
        {
            var results = new List<RenameResult>();
            var index = AssetRenameBlueprint.BuildCsvIndex(csvMapping, delimiter, hasHeader, caseInsensitive, autoDetectCsv);
            if (index == null) return results;

            var uniqPath = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var validAssets = new List<(UnityEngine.Object obj, string path)>();

            foreach (var a in assets.Where(a => a != null))
            {
                var path = AssetDatabase.GetAssetPath(a);
                if (string.IsNullOrEmpty(path)) continue;
                if (AssetDatabase.IsValidFolder(path)) continue;
                if (!IsPathMatchesFilter(path, assetFilter)) continue;
                if (!uniqPath.Add(path)) continue;
                validAssets.Add((a, path));
            }

            foreach (var (obj, path) in validAssets)
            {
                var fileName = Path.GetFileNameWithoutExtension(path);
                var guid = AssetDatabase.AssetPathToGUID(path);
                var ext = Path.GetExtension(path) ?? "";
                var (found, fromCsv, keyType) = ResolveNewName(index, guid, path, fileName, matchByGuid, matchByPath, matchByName);
                var assetType = GetAssetTypeName(path);

                if (!found)
                {
                    results.Add(new RenameResult
                    {
                        Asset = obj,
                        AssetType = assetType,
                        Guid = guid,
                        Path = path,
                        OldName = fileName,
                        NewName = "-",
                        RequestedNew = "-",
                        MatchKey = "N/A",
                        Status = "Skipped (not in CSV)",
                        Note = "",
                        Extension = ext
                    });
                    continue;
                }

                if (string.IsNullOrWhiteSpace(fromCsv))
                {
                    results.Add(new RenameResult
                    {
                        Asset = obj,
                        AssetType = assetType,
                        Guid = guid,
                        Path = path,
                        OldName = fileName,
                        NewName = "-",
                        RequestedNew = "",
                        MatchKey = keyType,
                        Status = "Skipped (CSV newName empty)",
                        Note = "",
                        Extension = ext
                    });
                    continue;
                }

                string sanitized = fromCsv;
                string sanitizeNote = "";
                if (autoSanitize && SanitizeName(ref sanitized, out var sNote)) sanitizeNote = sNote;

                var comparer = caseInsensitive ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal;
                if (comparer.Equals(fileName, sanitized))
                {
                    results.Add(new RenameResult
                    {
                        Asset = obj,
                        AssetType = assetType,
                        Guid = guid,
                        Path = path,
                        OldName = fileName,
                        NewName = sanitized,
                        RequestedNew = fromCsv,
                        MatchKey = keyType,
                        Status = "Skipped (already named)",
                        Note = sanitizeNote,
                        Extension = ext
                    });
                    continue;
                }

                results.Add(new RenameResult
                {
                    Asset = obj,
                    AssetType = assetType,
                    Guid = guid,
                    Path = path,
                    OldName = fileName,
                    NewName = sanitized,
                    RequestedNew = fromCsv,
                    MatchKey = keyType,
                    Status = "Pending",
                    Note = sanitizeNote,
                    Extension = ext
                });
            }

            return results;
        }

        public static void ApplyRenames(List<RenameResult> results, bool alsoRenamePrefabRoot, bool autoMakeUniqueIfConflict, bool autoCheckoutIfVCSActive)
        {
            if (results == null || results.Count == 0) return;

            try
            {
                AssetDatabase.StartAssetEditing();

                for (int i = 0; i < results.Count; i++)
                {
                    var row = results[i];
                    if (row.Status != "Pending") continue;

                    if (EditorUtility.DisplayCancelableProgressBar(
                            "Renaming Assets",
                            $"{row.OldName}{row.Extension} → {row.NewName}{row.Extension}",
                            (float)i / results.Count))
                    {
                        row.Status = "Skipped (user cancelled)";
                        results[i] = row;
                        break;
                    }

                    if (autoCheckoutIfVCSActive && Provider.isActive)
                    {
                        try { var task = Provider.Checkout(row.Path, CheckoutMode.Both); task.Wait(); } catch { }
                    }

                    var folder = Path.GetDirectoryName(row.Path)?.Replace("\\", "/") ?? "";
                    var targetPath = $"{folder}/{row.NewName}{row.Extension}";

                    if (File.Exists(targetPath))
                    {
                        if (autoMakeUniqueIfConflict)
                        {
                            string unique = AssetDatabase.GenerateUniqueAssetPath(targetPath);
                            var uniqueNameNoExt = Path.GetFileNameWithoutExtension(unique);
                            row.NewName = uniqueNameNoExt;
                            targetPath = unique;
                            row.Note = AppendNote(row.Note, "auto-unique");
                        }
                        else
                        {
                            row.Status = "Error (new name exists)";
                            results[i] = row;
                            continue;
                        }
                    }

                    string err = AssetDatabase.RenameAsset(row.Path, row.NewName);
                    if (!string.IsNullOrEmpty(err))
                    {
                        row.Status = $"Error (RenameAsset): {err}";
                        results[i] = row;
                        continue;
                    }

                    if (alsoRenamePrefabRoot && string.Equals(row.Extension, ".prefab", StringComparison.OrdinalIgnoreCase))
                    {
                        try
                        {
                            var newPath = AssetDatabase.GUIDToAssetPath(row.Guid);
                            if (string.IsNullOrEmpty(newPath)) newPath = $"{folder}/{row.NewName}{row.Extension}";
                            var root = PrefabUtility.LoadPrefabContents(newPath);
                            if (root != null)
                            {
                                if (root.name != row.NewName)
                                {
                                    root.name = row.NewName;
                                    PrefabUtility.SaveAsPrefabAsset(root, newPath);
                                }
                                PrefabUtility.UnloadPrefabContents(root);
                            }
                        }
                        catch (Exception e)
                        {
                            row.Status = $"Error (Rename Prefab Root): {e.Message}";
                            results[i] = row;
                            continue;
                        }
                    }

                    row.Status = "OK";
                    results[i] = row;
                }
            }
            finally
            {
                AssetDatabase.StopAssetEditing();
                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();
                EditorUtility.ClearProgressBar();
            }
        }

        public static void ExportReportCsv(List<RenameResult> results)
        {
            var savePath = EditorUtility.SaveFilePanelInProject("Save Rename Report", "AssetRenameReport.csv", "csv", "Choose a location for the report.");
            if (string.IsNullOrEmpty(savePath)) return;

            var sb = new StringBuilder();
            sb.AppendLine("guid,oldPath,oldName,requestedNew,finalNew,extension,status,matchBy,note");
            foreach (var r in results)
            {
                sb.AppendLine(string.Join(",",
                    CsvEscape(r.Guid, ','), CsvEscape(r.Path, ','), CsvEscape(r.OldName, ','),
                    CsvEscape(r.RequestedNew, ','), CsvEscape(r.NewName, ','), CsvEscape(r.Extension, ','),
                    CsvEscape(r.Status, ','), CsvEscape(r.MatchKey, ','), CsvEscape(r.Note, ',')));
            }

            try
            {
                File.WriteAllText(savePath, sb.ToString(), new UTF8Encoding(true));
                AssetDatabase.ImportAsset(savePath);
                var asset = AssetDatabase.LoadAssetAtPath<TextAsset>(savePath);
                EditorGUIUtility.PingObject(asset);
            }
            catch (Exception e)
            {
                Debug.LogError($"[AssetRenameCore] Export Report failed: {e.Message}");
                EditorUtility.DisplayDialog("Export Report", "Save report failed.", "OK");
            }
        }

        public static string CsvEscape(string input, char d)
        {
            if (input == null) return "";
            bool needQuote = input.IndexOfAny(new[] { d, '"', '\n', '\r' }) >= 0;
            if (!needQuote) return input;
            return "\"" + input.Replace("\"", "\"\"") + "\"";
        }

        public static bool SanitizeName(ref string s, out string note)
        {
            note = "";
            if (string.IsNullOrEmpty(s)) return false;
            string original = s;
            s = s.Trim();

            var invalid = Path.GetInvalidFileNameChars();
            var sb = new StringBuilder(s.Length);
            foreach (var ch in s) sb.Append(invalid.Contains(ch) ? '_' : ch);
            s = sb.ToString();

            if (string.IsNullOrWhiteSpace(s)) s = "NewAsset";
            const int LIMIT = 100;
            if (s.Length > LIMIT) s = s.Substring(0, LIMIT).TrimEnd();

            if (!string.Equals(original, s, StringComparison.Ordinal)) { note = "sanitized"; return true; }
            return false;
        }

        public static (bool found, string newName, string matchBy) ResolveNewName(
            AssetRenameBlueprint.CsvIndex idx, string guid, string path, string name,
            bool matchByGuid, bool matchByPath, bool matchByName)
        {
            if (matchByGuid && AssetRenameBlueprint.TryPick(idx.byGuid, guid, out var nn, out var noteG)) return (true, nn, "GUID" + noteG);
            if (matchByPath && AssetRenameBlueprint.TryPick(idx.byPath, AssetRenameBlueprint.NormalizeAssetPath(path), out nn, out var noteP)) return (true, nn, "PATH" + noteP);
            if (matchByName && AssetRenameBlueprint.TryPick(idx.byName, name, out nn, out var noteN)) return (true, nn, "NAME" + noteN);
            return (false, null, null);
        }

        public static bool IsPathMatchesFilter(string path, AssetUtilityTool.AssetTypeFilter filter)
        {
            if (string.IsNullOrEmpty(path)) return false;
            var ext = (Path.GetExtension(path) ?? "").ToLowerInvariant();
            var type = AssetDatabase.GetMainAssetTypeAtPath(path);

            switch (filter)
            {
                case AssetUtilityTool.AssetTypeFilter.All:
                    return true;
                case AssetUtilityTool.AssetTypeFilter.Prefab:
                    return ext == ".prefab";
                case AssetUtilityTool.AssetTypeFilter.Audio:
                    return type == typeof(AudioClip) || ext is ".wav" or ".ogg" or ".mp3";
                case AssetUtilityTool.AssetTypeFilter.Image:
                    return type == typeof(Texture2D) || ext is ".png" or ".jpg" or ".jpeg";
                case AssetUtilityTool.AssetTypeFilter.Material:
                    return type == typeof(Material);
                case AssetUtilityTool.AssetTypeFilter.ScriptableObject:
                    return typeof(ScriptableObject).IsAssignableFrom(type);
                case AssetUtilityTool.AssetTypeFilter.Scene:
                    return ext == ".unity";
                case AssetUtilityTool.AssetTypeFilter.Text:
                    return type == typeof(TextAsset) || ext is ".txt" or ".csv" or ".json";
                default:
                    return true;
            }
        }

        public static string GetAssetTypeName(string path)
        {
            var type = AssetDatabase.GetMainAssetTypeAtPath(path);
            if (type == null) return "Unknown";
            return type.Name;
        }

        private static string AppendNote(string a, string b)
            => string.IsNullOrEmpty(a) ? b : (string.IsNullOrEmpty(b) ? a : a + "; " + b);
    }
}
#endif
