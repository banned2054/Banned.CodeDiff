using Avalonia.Media;
using Banned.CodeDiff.Models;

namespace Banned.CodeDiff.Avalonia.Models;

/// <summary>Base type of unified rows rendered by <see cref="Views.DiffView"/>.</summary>
public abstract class DiffUnifiedRow : DiffRow;

/// <summary>A unified content row: one diff line with dual (old/new) line numbers.</summary>
public sealed class DiffUnifiedContentRow : DiffUnifiedRow
{
    internal DiffUnifiedContentRow(string? oldNumber, string? newNumber, string text, DiffCellKind kind,
                                   IReadOnlyList<DiffHighlight> highlights, DiffBrushSet brushes)
    {
        Highlights = highlights;
        OldNumber  = oldNumber;
        NewNumber  = newNumber;
        Text       = text;
        Kind       = kind;
        Sign = kind switch
        {
            DiffCellKind.Add    => "+",
            DiffCellKind.Delete => "-",
            _                   => " ",
        };

        NumberBackground = kind switch
        {
            DiffCellKind.Add    => brushes.AddNumber,
            DiffCellKind.Delete => brushes.DeleteNumber,
            _                   => brushes.ContextNumber,
        };
        ContentBackground = kind switch
        {
            DiffCellKind.Add    => brushes.AddContent,
            DiffCellKind.Delete => brushes.DeleteContent,
            _                   => brushes.ContextContent,
        };
        HighlightBrush = kind switch
        {
            DiffCellKind.Add    => brushes.AddContentHighlight,
            DiffCellKind.Delete => brushes.DeleteContentHighlight,
            _                   => null,
        };
        NumberForeground = brushes.NumberForeground;
    }

    /// <summary>Gets the old file line number, or <c>null</c> for added lines.</summary>
    public string? OldNumber { get; }

    /// <summary>Gets the new file line number, or <c>null</c> for deleted lines.</summary>
    public string? NewNumber { get; }

    /// <summary>Gets the line text with the trailing newline removed.</summary>
    public string Text { get; }

    /// <summary>Gets the line kind.</summary>
    public DiffCellKind Kind { get; }

    /// <summary>Gets the sign glyph ("+", "-", or " ") shown before the text.</summary>
    public string Sign { get; }

    /// <summary>Gets the word-level highlight ranges within <see cref="Text"/>.</summary>
    public IReadOnlyList<DiffHighlight> Highlights { get; }

    /// <summary>Gets the word-level highlight brush, or <c>null</c> when there is nothing to highlight.</summary>
    public IBrush? HighlightBrush { get; }

    /// <summary>Gets the line-number cells background.</summary>
    public IBrush NumberBackground { get; }

    /// <summary>Gets the content cell background.</summary>
    public IBrush ContentBackground { get; }

    /// <summary>Gets the line-number text brush.</summary>
    public IBrush NumberForeground { get; }
}

/// <summary>A unified collapsed hunk placeholder row showing the "@@" header with expand affordances.</summary>
public sealed class DiffUnifiedHunkRow : DiffUnifiedRow
{
    internal DiffUnifiedHunkRow(int hunkIndex, DiffLine hunk, bool expandEnabled, int composeLength,
                                string hunkText, DiffBrushSet brushes)
    {
        HunkIndex = hunkIndex;

        var info = hunk.UnifiedInfo;
        var hiddenCount = info != null ? info.EndHiddenIndex - info.StartHiddenIndex : 0;
        var (up, down, all) = DiffHunkExpand.Buttons(expandEnabled && info != null, hunk.IsFirst == true,
                                                     hunk.IsLast == true, hiddenCount, composeLength);

        IsExpandEnabled = expandEnabled && info != null;
        CanExpandUp     = up;
        CanExpandDown   = down;
        CanExpandAll    = all;
        HunkText          = hunkText;
        NumberBackground  = brushes.HunkNumber;
        ContentBackground = brushes.HunkContent;
        HunkForeground    = brushes.HunkForeground;
        ExpandForeground  = brushes.NumberForeground;
    }

    /// <summary>Gets the unified model index of the hunk — the key passed to
    /// <see cref="Services.DiffFile.OnUnifiedHunkExpand"/>.</summary>
    public int HunkIndex { get; }

    /// <summary>Gets whether the model allows expansion at all (not composed from diff-only text).</summary>
    public bool IsExpandEnabled { get; }

    /// <summary>Gets whether the Expand Up button is shown.</summary>
    public bool CanExpandUp { get; }

    /// <summary>Gets whether the Expand Down button is shown.</summary>
    public bool CanExpandDown { get; }

    /// <summary>Gets whether the Expand All button is shown.</summary>
    public bool CanExpandAll { get; }

    /// <summary>Gets the hunk header text (the "@@ -a,b +c,d @@" line).</summary>
    public string HunkText { get; }

    /// <summary>Gets the line-number cells background.</summary>
    public IBrush NumberBackground { get; }

    /// <summary>Gets the content cell background.</summary>
    public IBrush ContentBackground { get; }

    /// <summary>Gets the hunk header text brush.</summary>
    public IBrush HunkForeground { get; }

    /// <summary>Gets the expand-button icon brush (upstream plainLineNumberColor).</summary>
    public IBrush ExpandForeground { get; }
}
