using Avalonia.Styling;
using Banned.CodeDiff.Avalonia.Models;
using Banned.CodeDiff.Models;
using Banned.CodeDiff.Services;

namespace Banned.CodeDiff.Avalonia.Utils;

/// <summary>
/// Builds the flat row list for <see cref="Views.DiffView"/> in unified mode from a built
/// <see cref="DiffFile"/> unified model. Mirrors the split builder: for each unified row index,
/// the collapsed hunk placeholder (when present) is emitted above the content row, and hidden
/// rows are skipped. The synthetic trailing hunk keyed at <c>UnifiedLineLength</c> is out of the
/// content range and intentionally not rendered (expand rows are M4 scope).
/// </summary>
internal static class DiffUnifiedRowBuilder
{
    public static IReadOnlyList<DiffRow> Build(DiffFile file, ThemeVariant variant)
    {
        var brushes = DiffBrushes.Get(variant);
        var rows = new List<DiffRow>(file.UnifiedLineLength);

        for (var index = 0; index < file.UnifiedLineLength; index++)
        {
            if (file.GetUnifiedHunkLine(index) is { } hunk)
            {
                var text = hunk.UnifiedInfo?.PlainText is { Length: > 0 } plainText ? plainText : hunk.Text;
                rows.Add(new DiffUnifiedHunkRow(text.TrimEnd(), brushes));
            }

            var line = file.GetUnifiedLine(index);

            if (line?.IsHidden == true || line?.Diff is not { } diff)
            {
                continue;
            }

            var lineText = (line.Value ?? diff.Text)?.TrimEnd('\r', '\n') ?? string.Empty;
            var kind = diff.Type switch
            {
                DiffLineType.Add => DiffCellKind.Add,
                DiffLineType.Delete => DiffCellKind.Delete,
                _ => DiffCellKind.Context,
            };

            rows.Add(new DiffUnifiedContentRow(line.OldLineNumber?.ToString(), line.NewLineNumber?.ToString(),
                                               lineText, kind, DiffHighlights.Extract(diff, kind), brushes));
        }

        return rows;
    }
}
