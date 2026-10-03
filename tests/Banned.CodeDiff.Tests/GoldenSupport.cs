using Banned.CodeDiff.Models;
using Banned.CodeDiff.Services;
using System.Text.Json.Nodes;

namespace Banned.CodeDiff.Tests;

/// <summary>
/// Golden JSON loading + JSON tree dumping/comparison.
///
/// The JS generator drops keys whose value is undefined (and NaN); the C# dumpers
/// emit null for those. Null-valued properties are therefore pruned from both trees
/// before comparison. Array elements are never pruned (missing model items are null).
/// </summary>
public static class GoldenSupport
{
    public static readonly string GoldenPath = LocateGolden();

    private static string LocateGolden()
    {
        var dir = AppContext.BaseDirectory;
        for (var i = 0; i < 8 && dir is not null; i++)
        {
            var candidate = Path.Combine(dir, "Golden", "golden.json");
            if (File.Exists(candidate))
            {
                return candidate;
            }

            dir = Path.GetDirectoryName(dir.TrimEnd(Path.DirectorySeparatorChar));
        }

        throw new FileNotFoundException("golden.json not found");
    }

    private static JsonObject? _golden;

    public static JsonObject Golden =>
        _golden ??= JsonNode.Parse(File.ReadAllText(GoldenPath))!.AsObject();

    // ---- prune ----

    public static JsonNode? PruneNulls(JsonNode? node)
    {
        switch (node)
        {
            case JsonObject obj :
            {
                var clean = new JsonObject();
                foreach (var (k, v) in obj)
                {
                    var pv = PruneNulls(v?.DeepClone());
                    if (pv is not null)
                    {
                        clean[k] = pv;
                    }
                }

                return clean;
            }
            case JsonArray arr :
            {
                var clean = new JsonArray();
                foreach (var v in arr)
                {
                    clean.Add(PruneNulls(v?.DeepClone()));
                }

                return clean;
            }
            default :
                return node;
        }
    }

    // ---- compare ----

    public static List<string> Compare(JsonNode? expected, JsonNode? actual, int maxDiffs = 5)
    {
        var diffs = new List<string>();
        Compare(PruneNulls(expected?.DeepClone()), PruneNulls(actual?.DeepClone()), "$", diffs, maxDiffs);
        return diffs;
    }

    private static void Compare(JsonNode? a, JsonNode? b, string path, List<string> diffs, int maxDiffs)
    {
        if (diffs.Count >= maxDiffs)
        {
            return;
        }

        if (a is null && b is null)
        {
            return;
        }

        if (a is null || b is null)
        {
            diffs.Add($"{path}: expected {(a?.ToJsonString() ?? "missing")}, actual {(b?.ToJsonString() ?? "missing")}");
            return;
        }

        switch (a)
        {
            case JsonObject ao when b is JsonObject bo :
            {
                foreach (var key in ao.Select(kvp => kvp.Key).Union(bo.Select(kvp => kvp.Key)).Distinct())
                {
                    ao.TryGetPropertyValue(key, out var av);
                    bo.TryGetPropertyValue(key, out var bv);
                    Compare(av?.DeepClone(), bv?.DeepClone(), path + "." + key, diffs, maxDiffs);
                    if (diffs.Count >= maxDiffs)
                    {
                        return;
                    }
                }

                return;
            }
            case JsonArray aa when b is JsonArray ab :
            {
                if (aa.Count != ab.Count)
                {
                    diffs.Add($"{path}: array length expected {aa.Count}, actual {ab.Count}");
                    return;
                }

                for (var i = 0; i < aa.Count; i++)
                {
                    Compare(aa[i]?.DeepClone(), ab[i]?.DeepClone(), $"{path}[{i}]", diffs, maxDiffs);
                    if (diffs.Count >= maxDiffs)
                    {
                        return;
                    }
                }

                return;
            }
        }

        var as_ = a.ToJsonString();
        var bs  = b.ToJsonString();
        if (as_ != bs)
        {
            diffs.Add($"{path}: expected {as_}, actual {bs}");
        }
    }

    // ---- dumpers (mirror tests/js-harness/gen.ts) ----

    public static JsonObject? DumpLineRange(LineRange? r)
    {
        if (r is null)
        {
            return null;
        }

        return new JsonObject
        {
            ["range"] = new JsonObject
            {
                ["location"] = r.Range.Location,
                ["length"]   = r.Range.Length,
            },
            ["hasLineChange"] = r.HasLineChange,
            ["newLineSymbol"] = r.NewLineSymbol.HasValue ? (int)r.NewLineSymbol.Value : null,
        };
    }

    public static JsonObject? DumpDiffRange(DiffRange? r)
    {
        if (r is null)
        {
            return null;
        }

        var arr = new JsonArray();
        foreach (var i in r.Range)
        {
            arr.Add(
                    new JsonObject
                    {
                        ["type"]       = i.Type,
                        ["str"]        = i.Str,
                        ["startIndex"] = i.StartIndex,
                        ["endIndex"]   = i.EndIndex,
                        ["length"]     = i.Length,
                    }
                   );
        }

        return new JsonObject
        {
            ["range"]         = arr,
            ["hasLineChange"] = r.HasLineChange,
            ["newLineSymbol"] = r.NewLineSymbol.HasValue ? (int)r.NewLineSymbol.Value : null,
        };
    }

    private static JsonObject? DumpHunkInfo(HunkInfo? h)
    {
        if (h is null)
        {
            return null;
        }

        return new JsonObject
        {
            ["oldStartIndex"]  = h.OldStartIndex,
            ["oldLength"]      = h.OldLength,
            ["newStartIndex"]  = h.NewStartIndex,
            ["newLength"]      = h.NewLength,
            ["_oldStartIndex"] = h._OldStartIndex,
            ["_oldLength"]     = h._OldLength,
            ["_newStartIndex"] = h._NewStartIndex,
            ["_newLength"]     = h._NewLength,
        };
    }

    private static JsonObject? DumpHunkLineInfo(HunkLineInfo? h)
    {
        if (h is null)
        {
            return null;
        }

        return new JsonObject
        {
            ["startHiddenIndex"]  = h.StartHiddenIndex,
            ["endHiddenIndex"]    = h.EndHiddenIndex,
            ["plainText"]         = h.PlainText,
            ["_startHiddenIndex"] = h._StartHiddenIndex,
            ["_endHiddenIndex"]   = h._EndHiddenIndex,
            ["_plainText"]        = h._PlainText,
            ["oldStartIndex"]     = h.OldStartIndex,
            ["oldLength"]         = h.OldLength,
            ["newStartIndex"]     = h.NewStartIndex,
            ["newLength"]         = h.NewLength,
            ["_oldStartIndex"]    = h._OldStartIndex,
            ["_oldLength"]        = h._OldLength,
            ["_newStartIndex"]    = h._NewStartIndex,
            ["_newLength"]        = h._NewLength,
        };
    }

    public static JsonObject? DumpDiffLine(DiffLine? l)
    {
        if (l is null)
        {
            return null;
        }

        return new JsonObject
        {
            ["text"]                = l.Text,
            ["type"]                = (int)l.Type,
            ["originalLineNumber"]  = l.OriginalLineNumber,
            ["oldLineNumber"]       = l.OldLineNumber,
            ["newLineNumber"]       = l.NewLineNumber,
            ["noTrailingNewLine"]   = l.NoTrailingNewLine,
            ["index"]               = l.Index,
            ["isFirst"]             = l.IsFirst,
            ["isLast"]              = l.IsLast,
            ["changes"]             = DumpLineRange(l.Changes),
            ["diffChanges"]         = DumpDiffRange(l.DiffChanges),
            ["internalDiffChanges"] = DumpDiffRange(l.InternalDiffChanges),
            ["prevHunkLineIndex"]   = l.PrevHunkLine?.Index,
            ["hunkInfo"]            = DumpHunkInfo(l.HunkInfo),
            ["splitInfo"]           = DumpHunkLineInfo(l.SplitInfo),
            ["unifiedInfo"]         = DumpHunkLineInfo(l.UnifiedInfo),
        };
    }

    public static JsonObject? DumpRawDiff(RawDiff rd)
    {
        var hunks = new JsonArray();
        foreach (var h in rd.Hunks)
        {
            var lines = new JsonArray();
            foreach (var l in h.Lines)
            {
                lines.Add(DumpDiffLine(l));
            }

            hunks.Add(new JsonObject
            {
                ["header"] = new JsonObject
                {
                    ["oldStartLine"] = h.Header.OldStartLine,
                    ["oldLineCount"] = h.Header.OldLineCount,
                    ["newStartLine"] = h.Header.NewStartLine,
                    ["newLineCount"] = h.Header.NewLineCount,
                },
                ["unifiedDiffStart"] = h.UnifiedDiffStart,
                ["unifiedDiffEnd"]   = h.UnifiedDiffEnd,
                // JS DiffHunkExpansionType is a string enum ("None" | "Up" | ...)
                ["expansionType"] = h.ExpansionType.ToString(), ["lines"] = lines,
            });
        }

        return new JsonObject
        {
            ["header"]             = rd.Header,
            ["contents"]           = rd.Contents,
            ["isBinary"]           = rd.IsBinary,
            ["maxLineNumber"]      = rd.MaxLineNumber,
            ["hasHiddenBidiChars"] = rd.HasHiddenBidiChars,
            ["hunks"]              = hunks,
        };
    }

    public static JsonObject? DumpSplitItem(SplitLineItem? it)
    {
        if (it is null)
        {
            return null;
        }

        return new JsonObject
        {
            ["lineNumber"] = it.LineNumber,
            ["value"]      = it.Value,
            ["isHidden"]   = it.IsHidden,
            ["_isHidden"]  = it._IsHidden,
            ["diff"]       = DumpDiffLine(it.Diff),
        };
    }

    public static JsonObject? DumpUnifiedItem(UnifiedLineItem? it)
    {
        if (it is null)
        {
            return null;
        }

        return new JsonObject
        {
            ["oldLineNumber"] = it.OldLineNumber,
            ["newLineNumber"] = it.NewLineNumber,
            ["value"]         = it.Value,
            ["isHidden"]      = it.IsHidden,
            ["_isHidden"]     = it._IsHidden,
            ["diff"]          = DumpDiffLine(it.Diff),
        };
    }

    public static JsonObject DumpModel(DiffFile df)
    {
        var splitLeft    = new JsonArray();
        var splitRight   = new JsonArray();
        var unified      = new JsonArray();
        var splitHunks   = new JsonArray();
        var unifiedHunks = new JsonArray();
        var oldSeen      = new SortedDictionary<int, DiffLine>();
        var newSeen      = new SortedDictionary<int, DiffLine>();
        for (var i = 0; i < df.SplitLineLength; i++)
        {
            var left  = df.GetSplitLeftLine(i);
            var right = df.GetSplitRightLine(i);
            splitLeft.Add(DumpSplitItem(left));
            splitRight.Add(DumpSplitItem(right));
            foreach (var it in new[] { left, right })
            {
                if (it?.Diff?.OldLineNumber is { } ol)
                {
                    oldSeen[ol] = it.Diff;
                }

                if (it?.Diff?.NewLineNumber is { } nl)
                {
                    newSeen[nl] = it.Diff;
                }
            }

            var h = df.GetSplitHunkLine(i);
            if (h is not null)
            {
                splitHunks.Add(new JsonObject { ["index"] = i, ["hunk"] = DumpDiffLine(h), });
            }
        }

        for (var i = 0; i < df.UnifiedLineLength; i++)
        {
            unified.Add(DumpUnifiedItem(df.GetUnifiedLine(i)));
            var h = df.GetUnifiedHunkLine(i);
            if (h is not null)
            {
                unifiedHunks.Add(new JsonObject { ["index"] = i, ["hunk"] = DumpDiffLine(h), });
            }
        }

        var oldDiffLines = new JsonArray();
        var newDiffLines = new JsonArray();
        foreach (var (n, l) in oldSeen)
        {
            oldDiffLines.Add(new JsonObject { ["lineNumber"] = n, ["line"] = DumpDiffLine(l), });
        }

        foreach (var (n, l) in newSeen)
        {
            newDiffLines.Add(new JsonObject { ["lineNumber"] = n, ["line"] = DumpDiffLine(l), });
        }

        return new JsonObject
        {
            ["oldFileName"]          = df.OldFileName,
            ["newFileName"]          = df.NewFileName,
            ["oldFileLang"]          = df.OldFileLang,
            ["newFileLang"]          = df.NewFileLang,
            ["oldFileContent"]       = df.GetOldFileContent(),
            ["newFileContent"]       = df.GetNewFileContent(),
            ["diffLineLength"]       = df.DiffLineLength,
            ["splitLineLength"]      = df.SplitLineLength,
            ["unifiedLineLength"]    = df.UnifiedLineLength,
            ["fileLineLength"]       = df.FileLineLength,
            ["additionLength"]       = df.AdditionLength,
            ["deletionLength"]       = df.DeletionLength,
            ["hasSomeLineCollapsed"] = df.HasSomeLineCollapsed,
            ["expandEnabled"]        = df.GetExpandEnabled(),
            ["splitLeft"]            = splitLeft,
            ["splitRight"]           = splitRight,
            ["unified"]              = unified,
            ["splitHunks"]           = splitHunks,
            ["unifiedHunks"]         = unifiedHunks,
            ["oldDiffLines"]         = oldDiffLines,
            ["newDiffLines"]         = newDiffLines,
        };
    }

    public static JsonArray DumpFastDiffResult(IReadOnlyList<DiffTuple> result)
    {
        var arr = new JsonArray();
        foreach (var t in result)
        {
            arr.Add(new JsonObject { ["op"] = t.Op, ["text"] = t.Text, });
        }

        return arr;
    }
}