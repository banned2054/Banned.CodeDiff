using Banned.CodeDiff.Models;
using Banned.CodeDiff.Services;

namespace Banned.CodeDiff.Utils;

/// <summary>
///     packages/core/src/multiSelect/data.ts 的移植,外加 multiSelect/dom.ts 的纯辅助方法
///     <c>normalizeRange</c>(dom.ts 其余部分是 DOM 契约,由 Avalonia 层重新实现,见
///     Banned.CodeDiff.Avalonia/Services/DiffSelectionDom.cs),以及 multiSelect/visual.ts
///     的纯辅助方法 <c>changePreselectedLinesToLineRange</c>;全部为无状态纯函数。
///     data.ts 中未移植:<c>extendDataToPreselectedLines</c>(评论流 extendData 适配,
///     本移植决定不做)。<br />
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
    ///     dom.ts normalizeRange 的移植(泛型 <c>T extends { startLineNumber; endLineNumber }</c>
    ///     具体化为 <see cref="MultiSelectRange" />,即其唯一用法)。
    ///     JS:<c>{ ...range, startLineNumber: min, endLineNumber: max }</c>。<br />
    ///     Port of dom.ts normalizeRange (the generic <c>T extends { startLineNumber; endLineNumber }</c>
    ///     is concretized to <see cref="MultiSelectRange" />, its only use).
    ///     JS: <c>{ ...range, startLineNumber: min, endLineNumber: max }</c>.
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
    ///     visual.ts changePreselectedLinesToLineRange 的移植:把每一侧的预选行号合并为一个
    ///     min/max 大区间——上游已知语义(散列的行列表会高亮 min 到 max 之间的所有行)。
    ///     顺序保持:先新侧区间,后旧侧。<br />
    ///     Port of visual.ts changePreselectedLinesToLineRange: merges each side's preselected line
    ///     numbers into one big min/max range — the upstream-known semantics (a scattered list
    ///     highlights everything between min and max). Order kept: the new-side range first, then old.
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
    ///     原生新增(上游无对应物——git-diff-view 没有复制功能):把选区结果展平为可放入
    ///     剪贴板的纯文本。被折叠 hunk 遮住(即 <see cref="SelectedLine.IsHide" />)的行会被
    ///     跳过——复制内容与视图所见一致——且每个值像渲染层显示前那样去掉结尾换行
    ///     (<c>null</c> 值复制为空行)。其余行以纯 <c>\n</c> 连接(与 diff 文本一致,而非
    ///     <c>Environment.NewLine</c>)。<c>null</c> 结果或不含任何可见行时返回空字符串。<br />
    ///     Native port addition (no upstream counterpart — git-diff-view has no copy feature):
    ///     flattens a selection result into clipboard-ready plain text. Lines currently hidden behind
    ///     a collapsed hunk (<see cref="SelectedLine.IsHide" />) are skipped — the copy matches what
    ///     the view shows — and each value is stripped of its trailing newline exactly like the render
    ///     layer does before display (a <c>null</c> value copies as an empty line). The remaining
    ///     lines join with a plain <c>\n</c> (like the diff text itself, not
    ///     <c>Environment.NewLine</c>). A <c>null</c> result or one without any visible line yields
    ///     the empty string.
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
