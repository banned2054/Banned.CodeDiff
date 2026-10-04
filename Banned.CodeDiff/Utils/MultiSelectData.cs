using Banned.CodeDiff.Models;
using Banned.CodeDiff.Services;

namespace Banned.CodeDiff.Utils;

/// <summary>
///     Port of packages/core/src/multiSelect/data.ts, plus the pure <c>normalizeRange</c> helper from
///     multiSelect/dom.ts (the rest of dom.ts is a DOM contract that the Avalonia layer reimplements,
///     see Banned.CodeDiff.Avalonia/Services/DiffSelectionDom.cs) and the pure
///     <c>changePreselectedLinesToLineRange</c> helper from multiSelect/visual.ts.
///     Not ported from data.ts: <c>extendDataToPreselectedLines</c> (comment-flow extendData adapter,
///     decided against for this port).
/// </summary>
public static class MultiSelectData
{
    /// <summary>
    ///     Port of dom.ts normalizeRange (the generic <c>T extends { startLineNumber; endLineNumber }</c>
    ///     is concretized to <see cref="MultiSelectRange" />, its only use).
    ///     JS: <c>{ ...range, startLineNumber: min, endLineNumber: max }</c>.
    /// </summary>
    public static MultiSelectRange NormalizeRange(MultiSelectRange range)
    {
        var start = Math.Min(range.StartLineNumber, range.EndLineNumber);
        var end   = Math.Max(range.StartLineNumber, range.EndLineNumber);

        return range with { StartLineNumber = start, EndLineNumber = end };
    }

    /// <summary>Get selected lines data from DiffFile for split mode.</summary>
    public static List<SelectedLine> GetSelectedLinesFromDiffFile_Split(DiffFile diffFile, MultiSelectRange range)
    {
        var normalizedRange = NormalizeRange(range);
        var lines           = new List<SelectedLine>();

        var side            = normalizedRange.Side;
        var startLineNumber = normalizedRange.StartLineNumber;
        var endLineNumber   = normalizedRange.EndLineNumber;

        for (var lineNum = startLineNumber; lineNum <= endLineNumber; lineNum++)
        {
            var lineData = diffFile.GetSplitLineByLineNumber(lineNum, side);
            var index    = diffFile.GetSplitLineIndexByLineNumber(lineNum, side);

            if (lineData is not { LineNumber: not null }) continue;
            var diffType = lineData.Diff?.Type; // DiffLineType? — JS `diff?.type` may be undefined

            lines.Add(new SelectedLine(index + 1,
                                       lineData.LineNumber.Value,
                                       lineData.Value,
                                       DiffFileUtils.CheckCurrentLineIsHidden(diffFile, lineNum, side).Split,
                                       diffType == DiffLineType.Delete,
                                       diffType == DiffLineType.Add,
                                       // DiffLineType.Context or no diff (raw expansion line) — JS quirk kept verbatim.
                                       diffType is DiffLineType.Context or null));
        }

        return lines;
    }

    /// <summary>Get selected lines data from DiffFile for unified mode.</summary>
    public static List<SelectedLine> GetSelectedLinesFromDiffFile_Unified(DiffFile diffFile, MultiSelectRange range)
    {
        var normalizedRange = NormalizeRange(range);
        var lines           = new List<SelectedLine>();

        var side            = normalizedRange.Side;
        var startLineNumber = normalizedRange.StartLineNumber;
        var endLineNumber   = normalizedRange.EndLineNumber;

        for (var lineNum = startLineNumber; lineNum <= endLineNumber; lineNum++)
        {
            var lineData = diffFile.GetUnifiedLineByLineNumber(lineNum, side);
            var index    = diffFile.GetUnifiedLineIndexByLineNumber(lineNum, side);

            if (lineData == null) continue;
            var lineNumber = side == SplitSide.Old ? lineData.OldLineNumber : lineData.NewLineNumber;

            if (lineNumber == null) continue;
            var diffType = lineData.Diff?.Type;

            lines.Add(new SelectedLine(index + 1,
                                       lineNumber.Value,
                                       lineData.Value,
                                       DiffFileUtils.CheckCurrentLineIsHidden(diffFile, lineNum, side).Unified,
                                       diffType == DiffLineType.Delete,
                                       diffType == DiffLineType.Add,
                                       diffType is DiffLineType.Context or null));
        }

        return lines;
    }

    /// <summary>
    ///     Port of visual.ts changePreselectedLinesToLineRange: merges each side's preselected line
    ///     numbers into one big min/max range — the upstream-known semantics (a scattered list
    ///     highlights everything between min and max). Order kept: the new-side range first, then old.
    /// </summary>
    public static List<MultiSelectRange> ChangePreselectedLinesToLineRange(MultiSelectPreselectedLines line)
    {
        var ranges = new List<MultiSelectRange>();

        if (line.New is { Count: > 0 }) ranges.Add(new MultiSelectRange(SplitSide.New, line.New.Min(), line.New.Max()));

        if (line.Old is { Count: > 0 }) ranges.Add(new MultiSelectRange(SplitSide.Old, line.Old.Min(), line.Old.Max()));

        return ranges;
    }

    /// <summary>
    ///     Native port addition (no upstream counterpart — git-diff-view has no copy feature):
    ///     flattens a selection result into clipboard-ready plain text. Lines currently hidden behind
    ///     a collapsed hunk (<see cref="SelectedLine.IsHide" />) are skipped — the copy matches what
    ///     the view shows — and each value is stripped of its trailing newline exactly like the render
    ///     layer does before display (a <c>null</c> value copies as an empty line). The remaining
    ///     lines join with a plain <c>\n</c> (like the diff text itself, not
    ///     <c>Environment.NewLine</c>). A <c>null</c> result or one without any visible line yields
    ///     the empty string.
    /// </summary>
    public static string GetSelectedTextFromResult(MultiSelectResult? result)
    {
        if (result == null) return "";

        var visible = (from line in result.Lines where !line.IsHide select (line.Value ?? "").TrimEnd('\r', '\n'))
           .ToList();

        return string.Join("\n", visible);
    }
}
