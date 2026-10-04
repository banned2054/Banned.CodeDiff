using Avalonia.Styling;
using Banned.CodeDiff.Avalonia.Models;
using Banned.CodeDiff.Models;
using Banned.CodeDiff.Services;

namespace Banned.CodeDiff.Avalonia.Utils;

/// <summary>
///     从已构建的 <see cref="DiffFile" /> 分栏模型生成 <see cref="Views.DiffView" /> 渲染所需的
///     扁平行列表。对应上游的渲染循环:对每个分栏行索引,折叠的 hunk 占位行(存在且仍在隐藏
///     行时)先于内容行输出,隐藏行被跳过。合成的末尾 hunk 以 <c>SplitLineLength</c>(最后一行
///     内容行之后的位置)为键,循环因此多跑一个索引,以渲染底部的展开条。<br />
///     Builds the flat row list rendered by <see cref="Views.DiffView" /> from a built
///     <see cref="DiffFile" /> split model. Mirrors the upstream render loop: for each split row index,
///     the collapsed hunk placeholder (when present and still hiding lines) is emitted above the
///     content row, and hidden rows are skipped. The synthetic trailing hunk is keyed at
///     <c>SplitLineLength</c> — one past the last content row — so the loop runs one extra index to
///     render the bottom expand strip.
/// </summary>
internal static class DiffSplitRowBuilder
{
    /// <summary>
    ///     从 <paramref name="file" /> 的分栏模型构建扁平行列表。<br />
    ///     Builds the flat row list from the split model of <paramref name="file" />.
    /// </summary>
    /// <param name="file">已构建分栏模型的 diff 文件。The diff file with its split model built.</param>
    /// <param name="variant">当前主题变体,决定画刷集。The theme variant selecting the brush set.</param>
    /// <returns>分栏行列表(含 hunk 占位行)。<br />The split rows (including hunk placeholder rows).</returns>
    public static IReadOnlyList<DiffRow> Build(DiffFile file, ThemeVariant variant)
    {
        var brushes = DiffBrushes.Get(variant);
        var rows    = new List<DiffRow>(file.SplitLineLength);

        for (var index = 0; index <= file.SplitLineLength; index++)
        {
            if (file.GetSplitHunkLine(index) is { } hunk &&
                DiffHunkExpand.IsRendered(hunk.SplitInfo, file.IsPureDiffRender))
            {
                var text = hunk.SplitInfo?.PlainText is { Length: > 0 } plainText ? plainText : hunk.Text;
                rows.Add(new DiffSplitHunkRow(index, hunk, file.IsExpandEnabled,
                                              DiffFile.CurrentComposeLength, text.TrimEnd(), brushes));
            }

            var left  = file.GetSplitLeftLine(index);
            var right = file.GetSplitRightLine(index);

            if (left?.IsHidden == true || right?.IsHidden == true) continue;

            if (left == null && right == null) continue;

            rows.Add(new DiffSplitContentRow(index + 1,
                                             CreateCell(file, left, SplitSide.Old, variant, brushes),
                                             CreateCell(file, right, SplitSide.New, variant, brushes),
                                             brushes.Splitter));
        }

        return rows;
    }

    private static DiffSplitCellModel CreateCell(DiffFile     file,    SplitLineItem? item, SplitSide side,
                                                 ThemeVariant variant, DiffBrushSet   brushes)
    {
        // Placeholder half-rows (the opposite side holds an add/delete) are bare SplitLineItem
        // instances with no line number; content rows always carry their DiffLine.
        if (item == null || (item.Diff == null && item.LineNumber == null))
            return new DiffSplitCellModel(null, string.Empty, DiffCellKind.Empty, [], null, brushes);

        var text = (item.Value ?? item.Diff?.Text ?? string.Empty).TrimEnd('\r', '\n');

        // Upstream picks the old file's syntax for the left column and the new file's for the
        // right one, queried by the file-absolute line number.
        var syntaxLine = side == SplitSide.Old && item.LineNumber is { } oldNumber
            ? file.GetOldSyntaxLine(oldNumber)
            : side == SplitSide.New && item.LineNumber is { } newNumber
                ? file.GetNewSyntaxLine(newNumber)
                : null;

        var syntaxRuns = DiffSyntaxRuns.Extract(syntaxLine, text.Length, variant);

        // Raw gap lines revealed by expansion have no DiffLine — they render as plain rows from
        // the file content, colored with the expand palette (upstream getContentBG hasDiff=false).
        if (item.Diff is not { } diff)
            return new DiffSplitCellModel(item.LineNumber?.ToString(), text, DiffCellKind.Expand, [], syntaxRuns,
                                          brushes);

        var kind = diff.Type switch
        {
            DiffLineType.Add    => DiffCellKind.Add,
            DiffLineType.Delete => DiffCellKind.Delete,
            _                   => DiffCellKind.Context
        };

        return new DiffSplitCellModel(item.LineNumber?.ToString(), text, kind,
                                      DiffHighlights.Extract(diff, kind), syntaxRuns, brushes);
    }
}
