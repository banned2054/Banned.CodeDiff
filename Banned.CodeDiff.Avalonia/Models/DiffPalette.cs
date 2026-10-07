using Avalonia.Media;
using Avalonia.Styling;

namespace Banned.CodeDiff.Avalonia.Models;

/// <summary>
///     宿主明暗 Diff 配色覆盖;空槽位继承预设。重新赋值 <see cref="Views.DiffView.Palette" /> 刷新行。<br />
///     Host light/dark diff overrides; null slots inherit presets. Reassign <see cref="Views.DiffView.Palette" /> to refresh rows.
/// </summary>
public sealed class DiffPalette
{
    /// <summary>浅色主题的覆盖槽位。<br />The override slots for the light theme.</summary>
    public DiffPaletteColors? Light { get; set; }

    /// <summary>深色主题的覆盖槽位。<br />The override slots for the dark theme.</summary>
    public DiffPaletteColors? Dark { get; set; }

    internal DiffPaletteColors? GetColors(ThemeVariant variant)
    {
        return variant == ThemeVariant.Dark ? Dark : Light;
    }
}

/// <summary>
///     单个主题变体的覆盖槽位;细粒度值优先于行级值,空值继承预设。<br />
///     Overrides for one theme variant; fine-grained values beat line-level values and null inherits presets.
/// </summary>
public sealed class DiffPaletteColors
{
    /// <summary>新增行的背景(行号格与内容格)。<br />The added-line background (number and content cells).</summary>
    public IBrush? AddLineBackground { get; set; }

    /// <summary>删除行的背景(行号格与内容格)。<br />The deleted-line background (number and content cells).</summary>
    public IBrush? DeleteLineBackground { get; set; }

    /// <summary>上下文行的背景(行号格与内容格)。<br />The context-line background (number and content cells).</summary>
    public IBrush? ContextBackground { get; set; }

    /// <summary>多选高亮的行覆盖层。<br />The multi-select line overlay.</summary>
    public IBrush? SelectionHighlight { get; set; }

    /// <summary>多选高亮在行号列边缘的竖条。<br />The multi-select edge strip on the number column.</summary>
    public IBrush? SelectionEdge { get; set; }

    /// <summary>评论锚点行的持久高亮覆盖层。<br />The persistent highlight overlay of commented lines.</summary>
    public IBrush? CommentLineHighlight { get; set; }

    /// <summary>评论卡片背景。<br />The comment card background.</summary>
    public IBrush? CommentCardBackground { get; set; }

    /// <summary>评论卡片边框。<br />The comment card border.</summary>
    public IBrush? CommentCardBorder { get; set; }

    // ---- M8 slots ----

    /// <summary>画布背景(行区域之外的视图底色);<c>null</c> 不改变控件背景。<br />The canvas background (the view backdrop outside the rows); <c>null</c> keeps the control background.</summary>
    public IBrush? CanvasBackground { get; set; }

    /// <summary>行号文本前景。<br />The line-number text foreground.</summary>
    public IBrush? NumberForeground { get; set; }

    /// <summary>新增行的行号格背景(优先于 <see cref="AddLineBackground" />)。<br />The added line's number cell background (wins over <see cref="AddLineBackground" />).</summary>
    public IBrush? AddNumberBackground { get; set; }

    /// <summary>新增行的内容格背景(优先于 <see cref="AddLineBackground" />)。<br />The added line's content cell background (wins over <see cref="AddLineBackground" />).</summary>
    public IBrush? AddContentBackground { get; set; }

    /// <summary>删除行的行号格背景(优先于 <see cref="DeleteLineBackground" />)。<br />The deleted line's number cell background (wins over <see cref="DeleteLineBackground" />).</summary>
    public IBrush? DeleteNumberBackground { get; set; }

    /// <summary>删除行的内容格背景(优先于 <see cref="DeleteLineBackground" />)。<br />The deleted line's content cell background (wins over <see cref="DeleteLineBackground" />).</summary>
    public IBrush? DeleteContentBackground { get; set; }

    /// <summary>上下文行的行号格背景(优先于 <see cref="ContextBackground" />)。<br />The context line's number cell background (wins over <see cref="ContextBackground" />).</summary>
    public IBrush? ContextNumberBackground { get; set; }

    /// <summary>上下文行的内容格背景(优先于 <see cref="ContextBackground" />)。<br />The context line's content cell background (wins over <see cref="ContextBackground" />).</summary>
    public IBrush? ContextContentBackground { get; set; }

    /// <summary>hunk 展开显露的原始行内容格背景。<br />The content cell background of raw rows revealed by hunk expansion.</summary>
    public IBrush? ExpandBackground { get; set; }

    /// <summary>hunk 头的行号格背景。<br />The hunk header's number cell background.</summary>
    public IBrush? HunkNumberBackground { get; set; }

    /// <summary>hunk 头的内容格背景。<br />The hunk header's content cell background.</summary>
    public IBrush? HunkBackground { get; set; }

    /// <summary>hunk 头文本前景。<br />The hunk header text foreground.</summary>
    public IBrush? HunkForeground { get; set; }

    /// <summary>分栏两侧之间的分隔线。<br />The 1px divider between split sides.</summary>
    public IBrush? Splitter { get; set; }

    /// <summary>新增行内的词级强调片段背景。<br />The word-level emphasis background inside added lines.</summary>
    public IBrush? WordAddHighlight { get; set; }

    /// <summary>删除行内的词级强调片段背景。<br />The word-level emphasis background inside deleted lines.</summary>
    public IBrush? WordDeleteHighlight { get; set; }

    /// <summary>被选中(多选)新增行的明确背景——按行类别着色,不依赖半透明通用覆盖层。<br />The explicit background of selected (multi-selected) added lines — colored per line kind, not via a generic translucent overlay.</summary>
    public IBrush? SelectedAddBackground { get; set; }

    /// <summary>被选中(多选)删除行的明确背景。<br />The explicit background of selected deleted lines.</summary>
    public IBrush? SelectedDeleteBackground { get; set; }

    /// <summary>被选中(多选)上下文行的明确背景。<br />The explicit background of selected context lines.</summary>
    public IBrush? SelectedContextBackground { get; set; }

    /// <summary>
    ///     字符(子串)选择背景——M8 仅定义颜色槽位,不实现字符选择交互。<br />
    ///     The text (substring) selection background — M8 only defines the color slot, not the
    ///     character-selection interaction.
    /// </summary>
    public IBrush? TextSelectionBackground { get; set; }

    /// <summary>
    ///     字符(子串)选择前景;默认保留语法色,只有宿主显式设置时才改写所选字符前景。
    ///     <br />The text (substring) selection foreground — syntax colors are kept by default; only
    ///     an explicit host override rewrites the selected characters' foreground.
    /// </summary>
    public IBrush? TextSelectionForeground { get; set; }
}
