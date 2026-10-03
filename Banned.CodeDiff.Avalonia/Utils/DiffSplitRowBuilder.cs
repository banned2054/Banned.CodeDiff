using Avalonia.Styling;
using Banned.CodeDiff.Avalonia.Models;
using Banned.CodeDiff.Models;
using Banned.CodeDiff.Services;

namespace Banned.CodeDiff.Avalonia.Utils;

/// <summary>
/// Builds the flat row list rendered by <see cref="Views.DiffView"/> from a built
/// <see cref="DiffFile"/> split model. Mirrors the upstream render loop: for each split row index,
/// the collapsed hunk placeholder (when present) is emitted above the content row, and hidden rows
/// are skipped. The synthetic trailing hunk keyed at <c>SplitLineLength</c> is out of the content
/// range and intentionally not rendered (expand rows are M4 scope).
/// </summary>
internal static class DiffSplitRowBuilder
{
    public static IReadOnlyList<DiffRow> Build(DiffFile file, ThemeVariant variant)
    {
        var brushes = DiffBrushes.Get(variant);
        var rows    = new List<DiffRow>(file.SplitLineLength);

        for (var index = 0; index < file.SplitLineLength; index++)
        {
            if (file.GetSplitHunkLine(index) is { } hunk)
            {
                var text = hunk.SplitInfo?.PlainText is { Length: > 0 } plainText ? plainText : hunk.Text;
                rows.Add(new DiffSplitHunkRow(text.TrimEnd(), brushes));
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
        // instances with a null Diff; content rows always carry their DiffLine.
        if (item?.Diff is not { } diff)
        {
            return new DiffSplitCellModel(null, string.Empty, DiffCellKind.Empty, [], brushes);
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