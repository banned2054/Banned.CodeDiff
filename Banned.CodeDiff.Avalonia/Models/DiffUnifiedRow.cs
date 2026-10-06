using Avalonia.Media;
using Banned.CodeDiff.Models;
using System.ComponentModel;

namespace Banned.CodeDiff.Avalonia.Models;

/// <summary>
///     由 <see cref="Views.DiffView" /> 渲染的统一行基类型。<br />
///     Base type of unified rows rendered by <see cref="Views.DiffView" />.
/// </summary>
public abstract class DiffUnifiedRow : DiffRow;

/// <summary>
///     统一内容行:一条 diff 行,带旧/新双行号。<br />
///     携带多选高亮状态(<see cref="IsSelected" />),选区拖动过程中已具体化的行容器
///     原地更新 — 等价于上游 <c>.diff-multi-select-active</c> CSS 类的切换。<br />
///     A unified content row: one diff line with dual (old/new) line numbers.
///     Carries the multi-select highlight state (<see cref="IsSelected" />) so realized row
///     containers update in place while a selection drag moves — the Avalonia equivalent of the
///     upstream <c>.diff-multi-select-active</c> CSS class toggling.
/// </summary>
public sealed class DiffUnifiedContentRow : DiffUnifiedRow, INotifyPropertyChanged
{
    private bool _isSelected;

    internal DiffUnifiedContentRow(int? oldLineNumber, int? newLineNumber, string text, DiffCellKind kind,
                                   IReadOnlyList<DiffHighlight> highlights,
                                   IReadOnlyList<DiffSyntaxRun>? syntaxRuns, DiffBrushSet brushes)
    {
        OldLineNumber = oldLineNumber;
        NewLineNumber = newLineNumber;
        Highlights    = highlights;
        SyntaxRuns    = syntaxRuns;
        OldNumber     = oldLineNumber?.ToString();
        NewNumber     = newLineNumber?.ToString();
        MergedNumber  = (newLineNumber ?? oldLineNumber)?.ToString();
        Text          = text;
        Kind          = kind;
        Sign = kind switch
        {
            DiffCellKind.Add    => "+",
            DiffCellKind.Delete => "-",
            _                   => " "
        };

        NumberBackground = kind switch
        {
            DiffCellKind.Add    => brushes.AddNumber,
            DiffCellKind.Delete => brushes.DeleteNumber,
            _                   => brushes.ContextNumber
        };
        ContentBackground = kind switch
        {
            DiffCellKind.Add    => brushes.AddContent,
            DiffCellKind.Delete => brushes.DeleteContent,
            // Raw revealed rows use --diff-expand-content--; the number cell keeps the plain
            // number value (--diff-expand-lineNumber-- is numerically identical upstream).
            DiffCellKind.Expand => brushes.ExpandContent,
            _                   => brushes.ContextContent
        };
        HighlightBrush = kind switch
        {
            DiffCellKind.Add    => brushes.AddContentHighlight,
            DiffCellKind.Delete => brushes.DeleteContentHighlight,
            _                   => null
        };
        NumberForeground   = brushes.NumberForeground;
        SelectionOverlay   = brushes.MultiSelectOverlay;
        SelectionEdgeStrip = brushes.MultiSelectBorder;
    }

    /// <summary>
    ///     获取旧文件行号;新增行为 <c>null</c> — 对应上游统一 DOM 的
    ///     <c>data-line-old-num</c> 特性,由 multiSelect/dom.ts 消费。<br />
    ///     Gets the old file line number, or <c>null</c> for added lines — the upstream
    ///     unified DOM's <c>data-line-old-num</c> attribute consumed by multiSelect/dom.ts.
    /// </summary>
    public int? OldLineNumber { get; }

    /// <summary>
    ///     获取新文件行号;删除行为 <c>null</c> — 对应上游统一 DOM 的
    ///     <c>data-line-new-num</c> 特性,由 multiSelect/dom.ts 消费。<br />
    ///     Gets the new file line number, or <c>null</c> for deleted lines — the upstream
    ///     unified DOM's <c>data-line-new-num</c> attribute consumed by multiSelect/dom.ts.
    /// </summary>
    public int? NewLineNumber { get; }

    /// <summary>获取旧文件行号文本;新增行为 <c>null</c>。<br />Gets the old file line number text, or <c>null</c> for added lines.</summary>
    public string? OldNumber { get; }

    /// <summary>获取新文件行号文本;删除行为 <c>null</c>。<br />Gets the new file line number text, or <c>null</c> for deleted lines.</summary>
    public string? NewNumber { get; }

    /// <summary>
    ///     获取单列行号模式(<c>DiffView.UseSingleLineNumberColumn</c>)下显示的合并行号:
    ///     新号优先——上下文/新增行显示新号,删除行(无新号)显示旧号。供主题内合并号文本
    ///     绑定使用,非公开 API。<br />
    ///     Gets the merged line number shown by the single-number-column mode
    ///     (<c>DiffView.UseSingleLineNumberColumn</c>): the new number wins — context and added
    ///     lines show the new number, deleted lines (no new number) fall back to the old one.
    ///     Bound by the merged-number text in the theme; not a public API.
    /// </summary>
    internal string? MergedNumber { get; }

    /// <summary>获取去除尾部换行符后的行文本。<br />Gets the line text with the trailing newline removed.</summary>
    public string Text { get; }

    /// <summary>获取行类别。<br />Gets the line kind.</summary>
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

    /// <summary>获取行号单元格背景。<br />Gets the line-number cells background.</summary>
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
    ///     获取或设置该行是否被某个多选范围覆盖;通过 <see cref="PropertyChanged" />
    ///     引发,使已具体化的行容器无需重建行即可更新。<br />
    ///     Gets or sets whether this row is covered by a multi-select range; raised through
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

    /// <inheritdoc />
    public event PropertyChangedEventHandler? PropertyChanged;
}

/// <summary>
///     统一视图的折叠 hunk 占位行,显示 "@@" 头及其展开操作。<br />
///     A unified collapsed hunk placeholder row showing the "@@" header with expand affordances.
/// </summary>
public sealed class DiffUnifiedHunkRow : DiffUnifiedRow
{
    internal DiffUnifiedHunkRow(int    hunkIndex, DiffLine     hunk, bool expandEnabled, int composeLength,
                                string hunkText,  DiffBrushSet brushes)
    {
        HunkIndex = hunkIndex;

        var info = hunk.UnifiedInfo;
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
        HunkForeground    = brushes.HunkForeground;
        ExpandForeground  = brushes.NumberForeground;
    }

    /// <summary>
    ///     获取该 hunk 在统一模型中的索引 — 即传给
    ///     <see cref="global::Banned.CodeDiff.Services.DiffFile.OnUnifiedHunkExpand" /> 的键。<br />
    ///     Gets the unified model index of the hunk — the key passed to
    ///     <see cref="global::Banned.CodeDiff.Services.DiffFile.OnUnifiedHunkExpand" />.
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

    /// <summary>获取行号单元格背景。<br />Gets the line-number cells background.</summary>
    public IBrush NumberBackground { get; }

    /// <summary>获取内容单元格背景。<br />Gets the content cell background.</summary>
    public IBrush ContentBackground { get; }

    /// <summary>获取 hunk 头文本画刷。<br />Gets the hunk header text brush.</summary>
    public IBrush HunkForeground { get; }

    /// <summary>获取展开按钮图标画刷(上游 plainLineNumberColor)。<br />Gets the expand-button icon brush (upstream plainLineNumberColor).</summary>
    public IBrush ExpandForeground { get; }
}
