using Avalonia.Media;
using Banned.CodeDiff.Models;
using System.ComponentModel;

namespace Banned.CodeDiff.Avalonia.Models;

/// <summary>分栏行某一侧的类别;决定各单元格画刷。<br />Kind of one side of a split row; drives the cell brushes.</summary>
public enum DiffCellKind
{
    /// <summary>该侧没有行(对侧承载新增或删除行)。<br />The side has no line (the opposite side holds an add or delete).</summary>
    Empty = 0,

    /// <summary>未变更的上下文行。<br />Unchanged context line.</summary>
    Context = 1,

    /// <summary>新增行(仅分栏视图的右侧)。<br />Added line (right side only in split view).</summary>
    Add = 2,

    /// <summary>删除行(仅分栏视图的左侧)。<br />Deleted line (left side only in split view).</summary>
    Delete = 3,

    /// <summary>
    ///     由 hunk 展开显露出的 raw 文件行(无 DiffLine);上游用
    ///     <c>--diff-expand-content--</c> 而非普通上下文背景为其着色。<br />
    ///     Raw file line revealed by hunk expansion (no DiffLine); upstream colors it with
    ///     <c>--diff-expand-content--</c> instead of the plain-context background.
    /// </summary>
    Expand = 4
}

/// <summary>
///     由 <see cref="Views.DiffView" /> 渲染的分栏行基类型。<br />
///     Base type of split rows rendered by <see cref="Views.DiffView" />.
/// </summary>
public abstract class DiffSplitRow : DiffRow;

/// <summary>
///     分栏内容行的单侧:行号、文本及解析出的画刷。<br />
///     携带多选高亮状态(<see cref="IsSelected" />),选区拖动过程中已具体化的行容器
///     原地更新 — 等价于上游 <c>.diff-multi-select-active</c> CSS 类的切换。<br />
///     One side of a split content row: line number, text, and resolved brushes.
///     Carries the multi-select highlight state (<see cref="IsSelected" />) so realized row
///     containers update in place while a selection drag moves — the Avalonia equivalent of the
///     upstream <c>.diff-multi-select-active</c> CSS class toggling.
/// </summary>
public sealed class DiffSplitCellModel : INotifyPropertyChanged
{
    private bool _isSelected;

    private bool _isCommented;

    internal DiffSplitCellModel(
        string?                       number, string text, DiffCellKind kind, IReadOnlyList<DiffHighlight> highlights,
        IReadOnlyList<DiffSyntaxRun>? syntaxRuns, DiffBrushSet brushes)
    {
        Highlights = highlights;
        SyntaxRuns = syntaxRuns;
        Number     = number;
        Text       = text;
        Kind       = kind;
        Sign = kind switch
        {
            DiffCellKind.Add    => "+",
            DiffCellKind.Delete => "-",
            _                   => " "
        };

        (NumberBackground, ContentBackground) = kind switch
        {
            DiffCellKind.Add     => (brushes.AddNumber, brushes.AddContent),
            DiffCellKind.Delete  => (brushes.DeleteNumber, brushes.DeleteContent),
            DiffCellKind.Context => (brushes.ContextNumber, brushes.ContextContent),
            // --diff-expand-lineNumber-- shares the plain number value; only the content differs.
            DiffCellKind.Expand => (brushes.ContextNumber, brushes.ExpandContent),
            _                   => (brushes.EmptyNumber, brushes.EmptyContent)
        };
        HighlightBrush = highlights.Count > 0
            ? kind switch
            {
                DiffCellKind.Add    => brushes.AddContentHighlight,
                DiffCellKind.Delete => brushes.DeleteContentHighlight,
                _                   => null
            }
            : null;
        NumberForeground   = brushes.NumberForeground;
        SelectionOverlay   = brushes.MultiSelectOverlay;
        SelectionEdgeStrip = brushes.MultiSelectBorder;
        CommentOverlay     = brushes.CommentOverlay;
    }

    /// <summary>
    ///     获取从 1 开始的文件行号;该侧为空时为 <c>null</c>。<br />
    ///     Gets the 1-based file line number, or <c>null</c> when the side is empty.
    /// </summary>
    public string? Number { get; }

    /// <summary>获取去除尾部换行符后的行文本。<br />Gets the line text with the trailing newline removed.</summary>
    public string Text { get; }

    /// <summary>获取单元格类别。<br />Gets the cell kind.</summary>
    public DiffCellKind Kind { get; }

    /// <summary>获取显示在文本前的符号字形("+"、"-" 或 " ")。<br />Gets the sign glyph ("+", "-", or " ") shown before the text.</summary>
    public string Sign { get; }

    /// <summary>获取 <see cref="Text" /> 中的词级高亮范围。<br />Gets the word-level highlight ranges within <see cref="Text" />.</summary>
    public IReadOnlyList<DiffHighlight> Highlights { get; }

    /// <summary>
    ///     获取 <see cref="Text" /> 中的语法着色区间;行以纯文本渲染时为 <c>null</c>
    ///     (无语法数据,或超出 150 个 span 的降级护栏)。<br />
    ///     Gets the syntax-colored runs within <see cref="Text" />, or <c>null</c> when the
    ///     line renders plain (no syntax data, or over the 150-span degradation guard).
    /// </summary>
    public IReadOnlyList<DiffSyntaxRun>? SyntaxRuns { get; }

    /// <summary>
    ///     获取词级高亮画刷;没有需要高亮的内容时为 <c>null</c>。<br />
    ///     Gets the word-level highlight brush, or <c>null</c> when there is nothing to highlight.
    /// </summary>
    public IBrush? HighlightBrush { get; }

    /// <summary>获取行号单元格背景。<br />Gets the line-number cell background.</summary>
    public IBrush NumberBackground { get; }

    /// <summary>获取内容单元格背景。<br />Gets the content cell background.</summary>
    public IBrush ContentBackground { get; }

    /// <summary>获取行号文本画刷。<br />Gets the line-number text brush.</summary>
    public IBrush NumberForeground { get; }

    /// <summary>
    ///     获取多选覆盖层画刷(不透明度 15% 的 #f0c000,上游
    ///     <c>--diff-multi-select-bg</c>)。<br />
    ///     Gets the multi-select overlay brush (#f0c000 at 15% opacity, upstream
    ///     <c>--diff-multi-select-bg</c>).
    /// </summary>
    public IBrush SelectionOverlay { get; }

    /// <summary>
    ///     获取多选边缘条画刷(#2588fa,上游 <c>--diff-multi-select-border</c>)。<br />
    ///     Gets the multi-select edge-strip brush (#2588fa, upstream
    ///     <c>--diff-multi-select-border</c>).
    /// </summary>
    public IBrush SelectionEdgeStrip { get; }

    /// <summary>
    ///     获取评论锚点行的持久高亮画刷(浅色 #fff8c5/深色 #d29922 的半透明覆盖)。<br />
    ///     Gets the persistent highlight brush of commented lines (#fff8c5 light / #d29922 dark,
    ///     semi-transparent overlays).
    /// </summary>
    public IBrush CommentOverlay { get; }

    /// <summary>
    ///     获取或设置该单元格是否被某个多选范围覆盖;通过 <see cref="PropertyChanged" />
    ///     引发,使已具体化的行容器无需重建行即可更新。<br />
    ///     Gets or sets whether this cell is covered by a multi-select range; raised through
    ///     <see cref="PropertyChanged" /> so realized containers update without a row rebuild.
    /// </summary>
    public bool IsSelected
    {
        get => _isSelected;
        internal set
        {
            if (_isSelected == value) return;

            _isSelected = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsSelected)));
        }
    }

    /// <summary>
    ///     获取或设置该单元格是否落在某个评论锚点的行范围之内(与多选状态独立——新的拖选
    ///     不清除该标志);通过 <see cref="PropertyChanged" /> 引发。<br />
    ///     Gets or sets whether this cell falls inside a comment anchor's line range (independent
    ///     of the multi-select state — a new drag never clears it); raised through
    ///     <see cref="PropertyChanged" />.
    /// </summary>
    public bool IsCommented
    {
        get => _isCommented;
        internal set
        {
            if (_isCommented == value) return;

            _isCommented = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsCommented)));
        }
    }

    /// <inheritdoc />
    public event PropertyChangedEventHandler? PropertyChanged;
}

/// <summary>将旧侧(左)与新侧(右)配对的分栏内容行。<br />A split content row pairing the old (left) and new (right) sides.</summary>
public sealed class DiffSplitContentRow : DiffSplitRow
{
    internal DiffSplitContentRow(int lineIndex, DiffSplitCellModel left, DiffSplitCellModel right, IBrush splitter)
    {
        LineIndex     = lineIndex;
        Left          = left;
        Right         = right;
        SplitterBrush = splitter;
    }

    /// <summary>
    ///     获取该行在分栏模型中的索引(从 1 开始)— 即上游的 <c>data-line</c> 特性;
    ///     多选视觉处理(multiSelect/visual.ts)用它匹配选中的行。<br />
    ///     Gets the 1-based split model index of this row — the upstream <c>data-line</c>
    ///     attribute; the multi-select visual pass (multiSelect/visual.ts) matches selected lines
    ///     against it.
    /// </summary>
    public int LineIndex { get; }

    /// <summary>获取旧侧(左)。<br />Gets the old (left) side.</summary>
    public DiffSplitCellModel Left { get; }

    /// <summary>获取新侧(右)。<br />Gets the new (right) side.</summary>
    public DiffSplitCellModel Right { get; }

    /// <summary>获取两侧之间 1px 分隔线的画刷。<br />Gets the 1px divider brush between the two sides.</summary>
    public IBrush SplitterBrush { get; }
}

/// <summary>
///     折叠的 hunk 占位行,显示 "@@" 头及其展开操作。<br />
///     A collapsed hunk placeholder row showing the "@@" header with its expand affordances.
/// </summary>
public sealed class DiffSplitHunkRow : DiffSplitRow
{
    internal DiffSplitHunkRow(int    hunkIndex, DiffLine     hunk, bool expandEnabled, int composeLength,
                              string hunkText,  DiffBrushSet brushes)
    {
        HunkIndex = hunkIndex;

        var info = hunk.SplitInfo;
        var hiddenCount = info != null ? info.EndHiddenIndex - info.StartHiddenIndex : 0;
        var (up, down, all) = DiffHunkExpand.Buttons(expandEnabled && info != null, hunk.IsFirst == true,
                                                     hunk.IsLast == true, hiddenCount, composeLength);

        IsExpandEnabled   = expandEnabled && info != null;
        CanExpandUp       = up;
        CanExpandDown     = down;
        CanExpandAll      = all;
        HunkText          = hunkText;
        NumberBackground  = brushes.HunkNumber;
        ContentBackground = brushes.HunkContent;
        SideBackground    = brushes.HunkSide;
        HunkForeground    = brushes.HunkForeground;
        ExpandForeground  = brushes.NumberForeground;
        SplitterBrush     = brushes.Splitter;
    }

    /// <summary>
    ///     获取该 hunk 在分栏模型中的索引 — 即传给
    ///     <see cref="global::Banned.CodeDiff.Services.DiffFile.OnSplitHunkExpand" /> 的键。<br />
    ///     Gets the split model index of the hunk — the key passed to
    ///     <see cref="global::Banned.CodeDiff.Services.DiffFile.OnSplitHunkExpand" />.
    /// </summary>
    public int HunkIndex { get; }

    /// <summary>
    ///     获取模型是否允许展开(并非仅由 diff 文本合成)。<br />
    ///     Gets whether the model allows expansion at all (not composed from diff-only text).
    /// </summary>
    public bool IsExpandEnabled { get; }

    /// <summary>获取是否显示向上展开按钮。<br />Gets whether the Expand Up button is shown.</summary>
    public bool CanExpandUp { get; }

    /// <summary>获取是否显示向下展开按钮。<br />Gets whether the Expand Down button is shown.</summary>
    public bool CanExpandDown { get; }

    /// <summary>获取是否显示全部展开按钮。<br />Gets whether the Expand All button is shown.</summary>
    public bool CanExpandAll { get; }

    /// <summary>获取 hunk 头文本("@@ -a,b +c,d @@" 行)。<br />Gets the hunk header text (the "@@ -a,b +c,d @@" line).</summary>
    public string HunkText { get; }

    /// <summary>获取旧侧行号单元格背景。<br />Gets the old-side line-number cell background.</summary>
    public IBrush NumberBackground { get; }

    /// <summary>获取旧侧内容单元格背景。<br />Gets the old-side content cell background.</summary>
    public IBrush ContentBackground { get; }

    /// <summary>获取新侧单元格背景。<br />Gets the new-side cells background.</summary>
    public IBrush SideBackground { get; }

    /// <summary>获取 hunk 头文本画刷。<br />Gets the hunk header text brush.</summary>
    public IBrush HunkForeground { get; }

    /// <summary>获取展开按钮图标画刷(上游 plainLineNumberColor)。<br />Gets the expand-button icon brush (upstream plainLineNumberColor).</summary>
    public IBrush ExpandForeground { get; }

    /// <summary>获取两侧之间 1px 分隔线的画刷。<br />Gets the 1px divider brush between the two sides.</summary>
    public IBrush SplitterBrush { get; }
}
