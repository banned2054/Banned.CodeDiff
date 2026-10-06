using Avalonia.Styling;
using Banned.CodeDiff.Avalonia.Models;
using Banned.CodeDiff.Models;
using Banned.CodeDiff.Services;

namespace Banned.CodeDiff.Avalonia.Utils;

/// <summary>
///     从已构建的 <see cref="DiffFile" /> 统一模型为 <see cref="Views.DiffView" /> 的统一模式生成
///     扁平行列表。与分栏构建器相同:对每个统一行索引,折叠的 hunk 占位行(存在且仍在隐藏行
///     时)先于内容行输出,隐藏行被跳过。合成的末尾 hunk 以 <c>UnifiedLineLength</c>(最后一行
///     内容行之后的位置)为键,循环因此多跑一个索引,以渲染底部的展开条。<br />
///     Builds the flat row list for <see cref="Views.DiffView" /> in unified mode from a built
///     <see cref="DiffFile" /> unified model. Mirrors the split builder: for each unified row index,
///     the collapsed hunk placeholder (when present and still hiding lines) is emitted above the
///     content row, and hidden rows are skipped. The synthetic trailing hunk is keyed at
///     <c>UnifiedLineLength</c> — one past the last content row — so the loop runs one extra index to
///     render the bottom expand strip.
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
        var brushes = DiffBrushes.Get(variant, palette);
        var rows    = new List<DiffRow>(file.UnifiedLineLength);

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

            // Upstream prefers the new file's syntax line, falling back to the old one.
            var syntaxLine = line.NewLineNumber is { } newNumber ? file.GetNewSyntaxLine(newNumber)
                : line.OldLineNumber is { } oldNumber            ? file.GetOldSyntaxLine(oldNumber)
                                                                   : null;

            var syntaxRuns = DiffSyntaxRuns.Extract(syntaxLine, lineText.Length, variant);

            // Raw gap lines revealed by expansion have no DiffLine — they render as plain rows
            // from the file content, colored with the expand palette (upstream hasDiff=false).
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
