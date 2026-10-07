using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Presenters;
using Avalonia.VisualTree;
using Banned.CodeDiff.Avalonia.Models;
using Banned.CodeDiff.Models;

namespace Banned.CodeDiff.Avalonia.Services;

/// <summary>
///     从 Avalonia 视觉树解析行号与侧别,移植自 multiSelect/dom.ts。<br />
///     Resolves line numbers and sides from the Avalonia visual tree, ported from multiSelect/dom.ts.
/// </summary>
internal static class DiffSelectionDom
{
    private const string NewNumClass     = "diff-line-new-num";
    private const string OldNumClass     = "diff-line-old-num";
    private const string UnifiedNumClass = "diff-line-num";
    private const string NewContentClass = "diff-line-new-content";
    private const string OldContentClass = "diff-line-old-content";

    /// <summary>
    ///     返回分栏行号;空侧或非整数文本返回 <c>null</c>。<br />
    ///     Returns the split line number, or null for an empty side or non-integer text.
    /// </summary>
    public static int? GetLineNumberFromElement_Split(Control? holder)
    {
        if (holder == null || RowPresenterOf(holder)?.Content is not DiffSplitContentRow row) return null;

        var text = holder.Classes.Contains(NewNumClass) ? row.Right.Number : row.Left.Number;

        if (text == null || !int.TryParse(text, out var line) || text != line.ToString()) return null;

        return line;
    }

    /// <summary>
    ///     返回分栏行号格的侧别。<br />
    ///     Returns the side of a split line-number cell.
    /// </summary>
    public static SplitSide? GetSideFromElement_Split(Control? holder)
    {
        if (holder == null) return null;

        if (holder.Classes.Contains(NewNumClass)) return SplitSide.New;

        if (holder.Classes.Contains(OldNumClass)) return SplitSide.Old;

        return null;
    }

    /// <summary>
    ///     返回命中行号格所在行的旧/新行号;未命中行号格时为 <c>null</c>。<br />
    ///     Returns both numbers of the row containing the hit number cell; null outside number cells.
    /// </summary>
    public static (int? Old, int? New)? GetLineNumbersFromElement_Unified(Visual? el)
    {
        if (el == null || ClosestClass(el, UnifiedNumClass) == null) return null;

        if (RowPresenterOf(el)?.Content is not DiffUnifiedContentRow row) return null;
        var rowLineOld = row.OldLineNumber;
        var rowLineNew = row.NewLineNumber;

        if (rowLineOld == null && rowLineNew == null) return null;

        return (rowLineOld, rowLineNew);
    }

    /// <summary>
    ///     按下仅命中行号格;拖选时内容格也可解析到同侧行号格。<br />
    ///     Press accepts only number cells; drag also resolves content cells to their side's number cell.
    /// </summary>
    public static Control? GetNumberHolderElement_Split(Visual? el, bool inMouseDown)
    {
        if (el == null) return null;

        Control? numberHolder = null;

        if (!inMouseDown)
        {
            var newContentEl = ClosestClass(el, NewContentClass);
            var oldContentEl = ClosestClass(el, OldContentClass);

            if (newContentEl != null) numberHolder = FindNumberCellInRow(newContentEl, NewNumClass);

            if (oldContentEl != null) numberHolder = FindNumberCellInRow(oldContentEl, OldNumClass);
        }

        numberHolder ??= ClosestClass(el, NewNumClass) ?? ClosestClass(el, OldNumClass);

        return numberHolder;
    }

    private static ContentPresenter? RowPresenterOf(Visual el)
    {
        return el.GetSelfAndVisualAncestors().OfType<ContentPresenter>().FirstOrDefault(p => p.Content is DiffRow);
    }

    private static Control? ClosestClass(Visual el, string className)
    {
        return el.GetSelfAndVisualAncestors().OfType<Control>().FirstOrDefault(e => e.Classes.Contains(className));
    }

    /// <summary>
    ///     JS: <c>contentEl.parentElement?.querySelector(".diff-line-*-num")</c> — the same
    ///     row's number cell on the same side (searched from the row's container).
    /// </summary>
    private static Control? FindNumberCellInRow(Control contentEl, string numClass)
    {
        var presenter = RowPresenterOf(contentEl);

        return presenter?.GetVisualDescendants().OfType<Control>().FirstOrDefault(c => c.Classes.Contains(numClass));
    }
}
