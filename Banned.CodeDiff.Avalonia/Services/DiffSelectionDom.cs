using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Presenters;
using Avalonia.VisualTree;
using Banned.CodeDiff.Avalonia.Models;
using Banned.CodeDiff.Models;

namespace Banned.CodeDiff.Avalonia.Services;

/// <summary>
///     multiSelect/dom.ts 的移植——从指针命中目标解析出行号、侧别与行号单元格,是 dom.ts
///     DOM 契约在 Avalonia 视觉树上的等价物。JS 版查询 DOM 属性与类名(<c>data-line-num</c>、
///     <c>[data-side]</c>、<c>.diff-line-*</c>);本移植沿相同类名遍历 Avalonia 视觉树(类名由
///     Themes/Generic.axaml 的行模板携带),并从行模型读取行号(渲染的行号与模型行号一致——
///     上游是从 DOM 读回行号),由 <see cref="Views.DiffView" /> 在指针事件处理中调用。
///     dom.ts 中未移植:<c>normalizeRange</c>(纯逻辑,位于核心库 Utils/MultiSelectData.cs);
///     <c>.diff-add-widget-wrapper</c> 分支(本移植不渲染添加评论的"+"按钮)。<br />
///     Port of packages/core/src/multiSelect/dom.ts — resolves the line number, side, and line-number
///     cell from a pointer target. The JS version queries DOM attributes and classes
///     (<c>data-line-num</c>, <c>[data-side]</c>, <c>.diff-line-*</c>); this port walks the Avalonia
///     visual tree over the same class names, carried by the row templates in Themes/Generic.axaml,
///     and reads the numbers from the row models (the rendered numbers equal the model numbers —
///     upstream reads them back off the DOM).
///     Not ported from dom.ts: <c>normalizeRange</c> (pure logic, lives in the core
///     Utils/MultiSelectData.cs); the <c>.diff-add-widget-wrapper</c> branch (this port renders no
///     add-comment "+" button).
/// </summary>
internal static class DiffSelectionDom
{
    private const string NewNumClass     = "diff-line-new-num";
    private const string OldNumClass     = "diff-line-old-num";
    private const string UnifiedNumClass = "diff-line-num";
    private const string NewContentClass = "diff-line-new-content";
    private const string OldContentClass = "diff-line-old-content";

    /// <summary>
    ///     getLineNumberFromElement_Split 的移植:返回持有单元格中渲染的行号;该侧为空或文本
    ///     不是纯整数时为 <c>null</c>——JS 的守卫 <c>lineAttr !== line.toString()</c> 原样保留。<br />
    ///     Port of getLineNumberFromElement_Split: the number rendered in the holder cell, or
    ///     <c>null</c> when the side is empty or the text is not a plain integer — the JS guard
    ///     <c>lineAttr !== line.toString()</c> is kept verbatim.
    /// </summary>
    public static int? GetLineNumberFromElement_Split(Control? holder)
    {
        if (holder == null || RowPresenterOf(holder)?.Content is not DiffSplitContentRow row) return null;

        var text = holder.Classes.Contains(NewNumClass) ? row.Right.Number : row.Left.Number;

        if (text == null || !int.TryParse(text, out var line) || text != line.ToString()) return null;

        return line;
    }

    /// <summary>
    ///     getSideFromElement_Split 的移植:持有单元格所属的侧别(JS 读取 <c>data-side</c>
    ///     属性;此处侧别编码在持有单元格的类名中)。<br />
    ///     Port of getSideFromElement_Split: the side of the holder cell (JS reads the
    ///     <c>data-side</c> attribute; here the side is encoded in the holder's cell class).
    /// </summary>
    public static SplitSide? GetSideFromElement_Split(Control? holder)
    {
        if (holder == null) return null;

        if (holder.Classes.Contains(NewNumClass)) return SplitSide.New;

        if (holder.Classes.Contains(OldNumClass)) return SplitSide.Old;

        return null;
    }

    /// <summary>
    ///     getLineNumbersFromElement_Unified 的移植:命中行号单元格所在行的两个行号;目标不在
    ///     行号单元格内时为 <c>null</c>(上游 <c>closest(".diff-line-num")</c>)。<br />
    ///     Port of getLineNumbersFromElement_Unified: both numbers of the row whose number cell was
    ///     hit, or <c>null</c> when the target is not inside a number cell (upstream
    ///     <c>closest(".diff-line-num")</c>).
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
    ///     getNumberHolderElement_Split 的移植:分栏目标的行号单元格。拖拽进行中(上游
    ///     <c>inMouseDown == false</c>)时,内容单元格会解析到同一行中其侧别的行号单元格——
    ///     悬停内容即可延伸选区;按下时只有行号单元格本身才算命中。<br />
    ///     Port of getNumberHolderElement_Split: the line-number cell for a split target. While a
    ///     drag is in progress (<c>inMouseDown == false</c> upstream), content cells resolve to their
    ///     side's number cell in the same row — hovering the content extends the selection; on press
    ///     only a number cell itself qualifies.
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
