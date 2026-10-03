using Avalonia.Media;
using Banned.CodeDiff.Models;

namespace Banned.CodeDiff.Avalonia.Models;

/// <summary>Kind of one side of a split row; drives the cell brushes.</summary>
public enum DiffCellKind
{
    /// <summary>The side has no line (the opposite side holds an add or delete).</summary>
    Empty = 0,

    /// <summary>Unchanged context line.</summary>
    Context = 1,

    /// <summary>Added line (right side only in split view).</summary>
    Add = 2,

    /// <summary>Deleted line (left side only in split view).</summary>
    Delete = 3,
}

/// <summary>Base type of split rows rendered by <see cref="Views.DiffView"/>.</summary>
public abstract class DiffSplitRow : DiffRow;

/// <summary>One side of a split content row: line number, text, and resolved brushes.</summary>
public sealed class DiffSplitCellModel
{
    internal DiffSplitCellModel(
        string? number, string text, DiffCellKind kind, IReadOnlyList<DiffHighlight> highlights,
        IReadOnlyList<DiffSyntaxRun>? syntaxRuns, DiffBrushSet brushes)
    {
        Highlights  = highlights;
        SyntaxRuns  = syntaxRuns;
        Number      = number;
        Text        = text;
        Kind        = kind;
        Sign = kind switch
        {
            DiffCellKind.Add    => "+",
            DiffCellKind.Delete => "-",
            _                   => " ",
        };

        (NumberBackground, ContentBackground) = kind switch
        {
            DiffCellKind.Add     => (brushes.AddNumber, brushes.AddContent),
            DiffCellKind.Delete  => (brushes.DeleteNumber, brushes.DeleteContent),
            DiffCellKind.Context => (brushes.ContextNumber, brushes.ContextContent),
            _                    => (brushes.EmptyNumber, brushes.EmptyContent),
        };
        HighlightBrush = highlights.Count > 0
            ? kind switch
            {
                DiffCellKind.Add    => brushes.AddContentHighlight,
                DiffCellKind.Delete => brushes.DeleteContentHighlight,
                _                   => (IBrush?)null,
            }
            : null;
        NumberForeground = brushes.NumberForeground;
    }

    /// <summary>Gets the 1-based file line number, or <c>null</c> when the side is empty.</summary>
    public string? Number { get; }

    /// <summary>Gets the line text with the trailing newline removed.</summary>
    public string Text { get; }

    /// <summary>Gets the cell kind.</summary>
    public DiffCellKind Kind { get; }

    /// <summary>Gets the sign glyph ("+", "-", or " ") shown before the text.</summary>
    public string Sign { get; }

    /// <summary>Gets the word-level highlight ranges within <see cref="Text"/>.</summary>
    public IReadOnlyList<DiffHighlight> Highlights { get; }

    /// <summary>Gets the syntax-colored runs within <see cref="Text"/>, or <c>null</c> when the
    /// line renders plain (no syntax data, or over the 150-span degradation guard).</summary>
    public IReadOnlyList<DiffSyntaxRun>? SyntaxRuns { get; }

    /// <summary>Gets the word-level highlight brush, or <c>null</c> when there is nothing to highlight.</summary>
    public IBrush? HighlightBrush { get; }

    /// <summary>Gets the line-number cell background.</summary>
    public IBrush NumberBackground { get; }

    /// <summary>Gets the content cell background.</summary>
    public IBrush ContentBackground { get; }

    /// <summary>Gets the line-number text brush.</summary>
    public IBrush NumberForeground { get; }
}

/// <summary>A split content row pairing the old (left) and new (right) sides.</summary>
public sealed class DiffSplitContentRow : DiffSplitRow
{
    internal DiffSplitContentRow(DiffSplitCellModel left, DiffSplitCellModel right, IBrush splitter)
    {
        Left          = left;
        Right         = right;
        SplitterBrush = splitter;
    }

    /// <summary>Gets the old (left) side.</summary>
    public DiffSplitCellModel Left { get; }

    /// <summary>Gets the new (right) side.</summary>
    public DiffSplitCellModel Right { get; }

    /// <summary>Gets the 1px divider brush between the two sides.</summary>
    public IBrush SplitterBrush { get; }
}

/// <summary>A collapsed hunk placeholder row showing the "@@" header with its expand affordances.</summary>
public sealed class DiffSplitHunkRow : DiffSplitRow
{
    internal DiffSplitHunkRow(int hunkIndex, DiffLine hunk, bool expandEnabled, int composeLength,
                              string hunkText, DiffBrushSet brushes)
    {
        HunkIndex = hunkIndex;

        var info = hunk.SplitInfo;
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
        SideBackground    = brushes.HunkSide;
        HunkForeground    = brushes.HunkForeground;
        ExpandForeground  = brushes.NumberForeground;
        SplitterBrush     = brushes.Splitter;
    }

    /// <summary>Gets the split model index of the hunk — the key passed to
    /// <see cref="Services.DiffFile.OnSplitHunkExpand"/>.</summary>
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

    /// <summary>Gets the old-side line-number cell background.</summary>
    public IBrush NumberBackground { get; }

    /// <summary>Gets the old-side content cell background.</summary>
    public IBrush ContentBackground { get; }

    /// <summary>Gets the new-side cells background.</summary>
    public IBrush SideBackground { get; }

    /// <summary>Gets the hunk header text brush.</summary>
    public IBrush HunkForeground { get; }

    /// <summary>Gets the expand-button icon brush (upstream plainLineNumberColor).</summary>
    public IBrush ExpandForeground { get; }

    /// <summary>Gets the 1px divider brush between the two sides.</summary>
    public IBrush SplitterBrush { get; }
}
