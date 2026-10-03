using Avalonia.Media;

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
        OldNumber = oldNumber;
        NewNumber = newNumber;
        Text = text;
        Kind = kind;
        Sign = kind switch
        {
            DiffCellKind.Add => "+",
            DiffCellKind.Delete => "-",
            _ => " ",
        };

        NumberBackground = kind switch
        {
            DiffCellKind.Add => brushes.AddNumber,
            DiffCellKind.Delete => brushes.DeleteNumber,
            _ => brushes.ContextNumber,
        };
        ContentBackground = kind switch
        {
            DiffCellKind.Add => brushes.AddContent,
            DiffCellKind.Delete => brushes.DeleteContent,
            _ => brushes.ContextContent,
        };
        HighlightBrush = kind switch
        {
            DiffCellKind.Add => brushes.AddContentHighlight,
            DiffCellKind.Delete => brushes.DeleteContentHighlight,
            _ => null,
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

/// <summary>A unified collapsed hunk placeholder row showing the "@@" header.</summary>
public sealed class DiffUnifiedHunkRow : DiffUnifiedRow
{
    internal DiffUnifiedHunkRow(string hunkText, DiffBrushSet brushes)
    {
        HunkText = hunkText;
        NumberBackground = brushes.HunkNumber;
        ContentBackground = brushes.HunkContent;
        HunkForeground = brushes.HunkForeground;
    }

    /// <summary>Gets the hunk header text (the "@@ -a,b +c,d @@" line).</summary>
    public string HunkText { get; }

    /// <summary>Gets the line-number cells background.</summary>
    public IBrush NumberBackground { get; }

    /// <summary>Gets the content cell background.</summary>
    public IBrush ContentBackground { get; }

    /// <summary>Gets the hunk header text brush.</summary>
    public IBrush HunkForeground { get; }
}
