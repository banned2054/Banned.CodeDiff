using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Presenters;
using Avalonia.VisualTree;
using Banned.CodeDiff.Avalonia.Models;
using Banned.CodeDiff.Models;

namespace Banned.CodeDiff.Avalonia.Services;

/// <summary>
/// Port of packages/core/src/multiSelect/dom.ts — resolves the line number, side, and line-number
/// cell from a pointer target. The JS version queries DOM attributes and classes
/// (<c>data-line-num</c>, <c>[data-side]</c>, <c>.diff-line-*</c>); this port walks the Avalonia
/// visual tree over the same class names, carried by the row templates in Themes/Generic.axaml,
/// and reads the numbers from the row models (the rendered numbers equal the model numbers —
/// upstream reads them back off the DOM).
/// Not ported from dom.ts: <c>normalizeRange</c> (pure logic, lives in the core
/// Utils/MultiSelectData.cs); the <c>.diff-add-widget-wrapper</c> branch (this port renders no
/// add-comment "+" button).
/// </summary>
internal static class DiffSelectionDom
{
    private const string NewNumClass     = "diff-line-new-num";
    private const string OldNumClass     = "diff-line-old-num";
    private const string UnifiedNumClass = "diff-line-num";
    private const string NewContentClass = "diff-line-new-content";
    private const string OldContentClass = "diff-line-old-content";

    /// <summary>
    /// Port of getLineNumberFromElement_Split: the number rendered in the holder cell, or
    /// <c>null</c> when the side is empty or the text is not a plain integer — the JS guard
    /// <c>lineAttr !== line.toString()</c> is kept verbatim.
    /// </summary>
    public static int? GetLineNumberFromElement_Split(Control? holder)
    {
        if (holder == null || RowPresenterOf(holder)?.Content is not DiffSplitContentRow row)
        {
            return null;
        }

        var text = holder.Classes.Contains(NewNumClass) ? row.Right.Number : row.Left.Number;

        if (text == null || !int.TryParse(text, out var line) || text != line.ToString())
        {
            return null;
        }

        return line;
    }

    /// <summary>Port of getSideFromElement_Split: the side of the holder cell (JS reads the
    /// <c>data-side</c> attribute; here the side is encoded in the holder's cell class).</summary>
    public static SplitSide? GetSideFromElement_Split(Control? holder)
    {
        if (holder == null)
        {
            return null;
        }

        if (holder.Classes.Contains(NewNumClass))
        {
            return SplitSide.New;
        }

        if (holder.Classes.Contains(OldNumClass))
        {
            return SplitSide.Old;
        }

        return null;
    }

    /// <summary>
    /// Port of getLineNumbersFromElement_Unified: both numbers of the row whose number cell was
    /// hit, or <c>null</c> when the target is not inside a number cell (upstream
    /// <c>closest(".diff-line-num")</c>).
    /// </summary>
    public static (int? Old, int? New)? GetLineNumbersFromElement_Unified(Visual? el)
    {
        if (el == null || ClosestClass(el, UnifiedNumClass) == null)
        {
            return null;
        }

        if (RowPresenterOf(el)?.Content is not DiffUnifiedContentRow row)
        {
            return null;
        }
        var rowLineOld = row.OldLineNumber;
        var rowLineNew = row.NewLineNumber;

        if (rowLineOld == null && rowLineNew == null)
        {
            return null;
        }

        return (rowLineOld, rowLineNew);
    }

    /// <summary>
    /// Port of getNumberHolderElement_Split: the line-number cell for a split target. While a
    /// drag is in progress (<c>inMouseDown == false</c> upstream), content cells resolve to their
    /// side's number cell in the same row — hovering the content extends the selection; on press
    /// only a number cell itself qualifies.
    /// </summary>
    public static Control? GetNumberHolderElement_Split(Visual? el, bool inMouseDown)
    {
        if (el == null)
        {
            return null;
        }

        Control? numberHolder = null;

        if (!inMouseDown)
        {
            var newContentEl = ClosestClass(el, NewContentClass);
            var oldContentEl = ClosestClass(el, OldContentClass);

            if (newContentEl != null)
            {
                numberHolder = FindNumberCellInRow(newContentEl, NewNumClass);
            }

            if (oldContentEl != null)
            {
                numberHolder = FindNumberCellInRow(oldContentEl, OldNumClass);
            }
        }

        if (numberHolder == null)
        {
            numberHolder = ClosestClass(el, NewNumClass) as Control ?? ClosestClass(el, OldNumClass) as Control;
        }

        return numberHolder;
    }

    private static ContentPresenter? RowPresenterOf(Visual el) =>
        el.GetSelfAndVisualAncestors().OfType<ContentPresenter>().FirstOrDefault(p => p.Content is DiffRow);

    private static Control? ClosestClass(Visual el, string className) =>
        el.GetSelfAndVisualAncestors().OfType<Control>().FirstOrDefault(e => e.Classes.Contains(className));

    /// <summary>JS: <c>contentEl.parentElement?.querySelector(".diff-line-*-num")</c> — the same
    /// row's number cell on the same side (searched from the row's container).</summary>
    private static Control? FindNumberCellInRow(Control contentEl, string numClass)
    {
        var presenter = RowPresenterOf(contentEl);

        return presenter?.GetVisualDescendants().OfType<Control>().FirstOrDefault(c => c.Classes.Contains(numClass));
    }
}
