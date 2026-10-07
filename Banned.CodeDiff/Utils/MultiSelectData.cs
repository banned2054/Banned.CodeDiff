using Banned.CodeDiff.Models;
using Banned.CodeDiff.Services;

namespace Banned.CodeDiff.Utils;

/// <summary>
///     多选范围与行数据辅助方法,移植自 multiSelect/data.ts、dom.ts 和 visual.ts。<br />
///     Selection range and line helpers ported from multiSelect/data.ts, dom.ts and visual.ts.
/// </summary>
public static class MultiSelectData
{
    /// <summary>
    ///     返回起止行号按升序排列的选区副本。<br />
    ///     Returns a range copy with ascending start and end line numbers.
    /// </summary>
    /// <param name="range">待规范化的选区。The range to normalize.</param>
    /// <returns>起止已交换为 min/max 的选区副本。The range copy with start/end swapped to min/max.</returns>
    public static MultiSelectRange NormalizeRange(MultiSelectRange range)
    {
        var start = Math.Min(range.StartLineNumber, range.EndLineNumber);
        var end   = Math.Max(range.StartLineNumber, range.EndLineNumber);

        return range with { StartLineNumber = start, EndLineNumber = end };
    }

    /// <summary>从 DiffFile 提取分栏模式的选中行数据。<br />Get selected lines data from DiffFile for split mode.</summary>
    /// <param name="diffFile">源 diff 文件。The source diff file.</param>
    /// <param name="range">选区行号范围(含端点,会先规范化)。The selected line-number range (inclusive; normalized first).</param>
    /// <returns>
    ///     逐行选中数据(视图内 index、文件行号、文本与 isHide/isDelete/isAdd/isContext 标记)。<br />The per-line selection data (in-view
    ///     index, file line number, text and the isHide/isDelete/isAdd/isContext flags).
    /// </returns>
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

    /// <summary>从 DiffFile 提取统一模式的选中行数据。<br />Get selected lines data from DiffFile for unified mode.</summary>
    /// <param name="diffFile">源 diff 文件。The source diff file.</param>
    /// <param name="range">选区行号范围(含端点,会先规范化)。The selected line-number range (inclusive; normalized first).</param>
    /// <returns>
    ///     逐行选中数据(视图内 index、文件行号、文本与 isHide/isDelete/isAdd/isContext 标记)。<br />The per-line selection data (in-view
    ///     index, file line number, text and the isHide/isDelete/isAdd/isContext flags).
    /// </returns>
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
    ///     将每侧预选行号合并为 min/max 区间,包含中间所有行;新侧在前。<br />
    ///     Merges each side into an inclusive min/max range, including intervening lines; new side first.
    /// </summary>
    /// <param name="line">两侧的预选行号集合。The preselected line numbers per side.</param>
    /// <returns>新侧/旧侧的 min/max 区间(可能为空列表)。The new/old min-max ranges (possibly empty).</returns>
    public static List<MultiSelectRange> ChangePreselectedLinesToLineRange(MultiSelectPreselectedLines line)
    {
        var ranges = new List<MultiSelectRange>();

        if (line.New is { Count: > 0 }) ranges.Add(new MultiSelectRange(SplitSide.New, line.New.Min(), line.New.Max()));

        if (line.Old is { Count: > 0 }) ranges.Add(new MultiSelectRange(SplitSide.Old, line.Old.Min(), line.Old.Max()));

        return ranges;
    }

    /// <summary>
    ///     复制可见选区文本:去除行尾换行,以 <c>\n</c> 连接;<c>null</c> 行值为空行。<br />
    ///     Copies visible selected text, trimming trailing newlines and joining with <c>\n</c>; null line values become empty lines.
    /// </summary>
    /// <param name="result">多选结果,可为 <c>null</c>。The multi-select result; may be <c>null</c>.</param>
    /// <returns>
    ///     以 <c>\n</c> 连接的纯文本;无可见行时为空字符串。The plain text joined with <c>\n</c>; the empty string when no visible line
    ///     exists.
    /// </returns>
    public static string GetSelectedTextFromResult(MultiSelectResult? result)
    {
        if (result == null) return "";

        var visible = (from line in result.Lines where !line.IsHide select (line.Value ?? "").TrimEnd('\r', '\n'))
           .ToList();

        return string.Join("\n", visible);
    }
}
