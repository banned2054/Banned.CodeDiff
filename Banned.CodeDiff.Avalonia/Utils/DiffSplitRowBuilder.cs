using Avalonia.Styling;
using Banned.CodeDiff.Avalonia.Models;
using Banned.CodeDiff.Models;
using Banned.CodeDiff.Services;

namespace Banned.CodeDiff.Avalonia.Utils;

/// <summary>
///     构建分栏呈现行:跳过隐藏行,保留折叠 hunk 与末尾展开条。<br />
///     Builds split presentation rows, skipping hidden lines while retaining collapsed hunks and the trailing expansion strip.
/// </summary>
internal static class DiffSplitRowBuilder
{
    /// <summary>
    ///     从 <paramref name="file" /> 的分栏模型构建扁平行列表。<br />
    ///     Builds the flat row list from the split model of <paramref name="file" />.
    /// </summary>
    /// <param name="file">已构建分栏模型的 diff 文件。The diff file with its split model built.</param>
    /// <param name="variant">当前主题变体,决定画刷集。The theme variant selecting the brush set.</param>
    /// <param name="palette">
    ///     宿主的语义配色覆盖;可为 <c>null</c>。<br />The host's semantic color overrides; may be <c>null</c>.
    /// </param>
    /// <returns>分栏行列表(含 hunk 占位行)。<br />The split rows (including hunk placeholder rows).</returns>
    public static IReadOnlyList<DiffRow> Build(DiffFile file, ThemeVariant variant, DiffPalette? palette = null)
    {
        return Build(file, new DiffThemeContext(variant, palette));
    }

    /// <summary>
    ///     从 <paramref name="file" /> 的分栏模型构建扁平行列表,经
    ///     <paramref name="theme" /> 解析画刷与语法颜色(预设/覆盖链)。<br />
    ///     Builds the flat row list from the split model of <paramref name="file" />, resolving
    ///     brushes and syntax colors through <paramref name="theme" /> (the preset/override chain).
    /// </summary>
    public static IReadOnlyList<DiffRow> Build(DiffFile file, DiffThemeContext theme)
    {
        var brushes      = theme.ResolveBrushes();
        var syntaxColors = theme.ResolveSyntaxColors();

        var rows = new List<DiffRow>(file.SplitLineLength);

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
                                             CreateCell(file, left, SplitSide.Old, theme, brushes, syntaxColors),
                                             CreateCell(file, right, SplitSide.New, theme, brushes, syntaxColors),
                                             brushes.Splitter));
        }

        return rows;
    }

    private static DiffSplitCellModel CreateCell(DiffFile          file,  SplitLineItem? item, SplitSide side,
                                                 DiffThemeContext  theme, DiffBrushSet   brushes,
                                                 DiffSyntaxColors? syntaxColors)
    {
        // 空侧没有行号或 DiffLine。
        if (item == null || (item.Diff == null && item.LineNumber == null))
            return new DiffSplitCellModel(null, string.Empty, DiffCellKind.Empty, [], null, brushes);

        var text = (item.Value ?? item.Diff?.Text ?? string.Empty).TrimEnd('\r', '\n');

        var syntaxLine = side == SplitSide.Old && item.LineNumber is { } oldNumber
            ? file.GetOldSyntaxLine(oldNumber)
            : side == SplitSide.New && item.LineNumber is { } newNumber
                ? file.GetNewSyntaxLine(newNumber)
                : null;

        var syntaxRuns = DiffSyntaxRuns.Extract(syntaxLine, text.Length, theme.Variant, syntaxColors);

        // 展开的原文行没有 DiffLine,按展开配色呈现。
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
