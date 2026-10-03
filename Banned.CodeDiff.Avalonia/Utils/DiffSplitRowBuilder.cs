using Avalonia.Styling;
using Banned.CodeDiff.Avalonia.Models;
using Banned.CodeDiff.Models;
using Banned.CodeDiff.Services;

namespace Banned.CodeDiff.Avalonia.Utils;

/// <summary>
/// Builds the flat row list rendered by <see cref="Views.DiffView"/> from a built
/// <see cref="DiffFile"/> split model. Mirrors the upstream render loop: for each split row index,
/// the collapsed hunk placeholder (when present and still hiding lines) is emitted above the
/// content row, and hidden rows are skipped. The synthetic trailing hunk is keyed at
/// <c>SplitLineLength</c> — one past the last content row — so the loop runs one extra index to
/// render the bottom expand strip.
/// </summary>
internal static class DiffSplitRowBuilder
{
    public static IReadOnlyList<DiffRow> Build(DiffFile file, ThemeVariant variant)
    {
        var brushes = DiffBrushes.Get(variant);
        var rows    = new List<DiffRow>(file.SplitLineLength);

        for (var index = 0; index <= file.SplitLineLength; index++)
        {
            if (file.GetSplitHunkLine(index) is { } hunk &&
                DiffHunkExpand.IsRendered(hunk.SplitInfo, file.GetIsPureDiffRender()))
            {
                var text = hunk.SplitInfo?.PlainText is { Length: > 0 } plainText ? plainText : hunk.Text;
                rows.Add(new DiffSplitHunkRow(index, hunk, file.GetExpandEnabled(),
                                              DiffFile.GetCurrentComposeLength(), text.TrimEnd(), brushes));
            }

            var left  = file.GetSplitLeftLine(index);
            var right = file.GetSplitRightLine(index);

            if (left?.IsHidden == true || right?.IsHidden == true)
            {
                continue;
            }

            if (left == null && right == null)
            {
                continue;
            }

            rows.Add(new DiffSplitContentRow(CreateCell(left, brushes), CreateCell(right, brushes), brushes.Splitter));
        }

        return rows;
    }

    private static DiffSplitCellModel CreateCell(SplitLineItem? item, DiffBrushSet brushes)
    {
        // Placeholder half-rows (the opposite side holds an add/delete) are bare SplitLineItem
        // instances with no line number; content rows always carry their DiffLine.
        if (item == null || (item.Diff == null && item.LineNumber == null))
        {
            return new DiffSplitCellModel(null, string.Empty, DiffCellKind.Empty, [], brushes);
        }

        // Raw gap lines revealed by expansion have no DiffLine — they render as plain context
        // cells from the file content.
        if (item.Diff is not { } diff)
        {
            return new DiffSplitCellModel(item.LineNumber?.ToString(),
                                          (item.Value ?? string.Empty).TrimEnd('\r', '\n'),
                                          DiffCellKind.Context, [], brushes);
        }

        var number = item.LineNumber?.ToString();
        var text   = (item.Value ?? diff.Text)?.TrimEnd('\r', '\n') ?? string.Empty;
        var kind = diff.Type switch
        {
            DiffLineType.Add    => DiffCellKind.Add,
            DiffLineType.Delete => DiffCellKind.Delete,
            _                   => DiffCellKind.Context,
        };

        return new DiffSplitCellModel(number, text, kind, DiffHighlights.Extract(diff, kind), brushes);
    }
}
