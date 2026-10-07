using Avalonia.Styling;
using Banned.CodeDiff.Avalonia.Models;
using Banned.CodeDiff.Models;
using Banned.CodeDiff.Services;

namespace Banned.CodeDiff.Avalonia.Utils;

/// <summary>
///     构建统一呈现行:跳过隐藏行,保留折叠 hunk 与末尾展开条。<br />
///     Builds unified presentation rows, skipping hidden lines while retaining collapsed hunks and the trailing expansion strip.
/// </summary>
internal static class DiffUnifiedRowBuilder
{
    /// <summary>
    ///     从 <paramref name="file" /> 的统一模型构建扁平行列表。<br />
    ///     Builds the flat row list from the unified model of <paramref name="file" />.
    /// </summary>
    /// <param name="file">已构建统一模型的 diff 文件。The diff file with its unified model built.</param>
    /// <param name="variant">当前主题变体,决定画刷集。The theme variant selecting the brush set.</param>
    /// <param name="palette">
    ///     宿主的语义配色覆盖;可为 <c>null</c>。<br />The host's semantic color overrides; may be <c>null</c>.
    /// </param>
    /// <returns>统一行列表(含 hunk 占位行)。<br />The unified rows (including hunk placeholder rows).</returns>
    public static IReadOnlyList<DiffRow> Build(DiffFile file, ThemeVariant variant, DiffPalette? palette = null)
    {
        return Build(file, new DiffThemeContext(variant, palette));
    }

    /// <summary>
    ///     从 <paramref name="file" /> 的统一模型构建扁平行列表,经
    ///     <paramref name="theme" /> 解析画刷与语法颜色(预设/覆盖链)。<br />
    ///     Builds the flat row list from the unified model of <paramref name="file" />, resolving
    ///     brushes and syntax colors through <paramref name="theme" /> (the preset/override chain).
    /// </summary>
    public static IReadOnlyList<DiffRow> Build(DiffFile file, DiffThemeContext theme)
    {
        var brushes      = theme.ResolveBrushes();
        var syntaxColors = theme.ResolveSyntaxColors();

        var rows = new List<DiffRow>(file.UnifiedLineLength);

        for (var index = 0; index <= file.UnifiedLineLength; index++)
        {
            if (file.GetUnifiedHunkLine(index) is { } hunk &&
                DiffHunkExpand.IsRendered(hunk.UnifiedInfo, file.IsPureDiffRender))
            {
                var text = hunk.UnifiedInfo?.PlainText is { Length: > 0 } plainText ? plainText : hunk.Text;
                rows.Add(new DiffUnifiedHunkRow(index, hunk, file.IsExpandEnabled,
                                                DiffFile.CurrentComposeLength, text.TrimEnd(), brushes));
            }

            var line = file.GetUnifiedLine(index);

            if (line?.IsHidden == true || line == null) continue;

            var lineText = (line.Value ?? line.Diff?.Text ?? string.Empty).TrimEnd('\r', '\n');

            // 统一行优先使用新侧语法,否则用旧侧。
            var syntaxLine = line.NewLineNumber is { } newNumber ? file.GetNewSyntaxLine(newNumber)
                : line.OldLineNumber is { } oldNumber            ? file.GetOldSyntaxLine(oldNumber)
                                                                   : null;

            var syntaxRuns = DiffSyntaxRuns.Extract(syntaxLine, lineText.Length, theme.Variant, syntaxColors);

            // 展开的原文行没有 DiffLine,按展开配色呈现。
            if (line.Diff is not { } diff)
            {
                rows.Add(new DiffUnifiedContentRow(line.OldLineNumber, line.NewLineNumber,
                                                   lineText, DiffCellKind.Expand, [], syntaxRuns, brushes));
                continue;
            }

            var kind = diff.Type switch
            {
                DiffLineType.Add    => DiffCellKind.Add,
                DiffLineType.Delete => DiffCellKind.Delete,
                _                   => DiffCellKind.Context
            };

            rows.Add(new DiffUnifiedContentRow(line.OldLineNumber, line.NewLineNumber,
                                               lineText, kind, DiffHighlights.Extract(diff, kind), syntaxRuns,
                                               brushes));
        }

        return rows;
    }
}
