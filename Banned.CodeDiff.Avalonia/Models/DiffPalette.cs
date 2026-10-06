using Avalonia.Media;
using Avalonia.Styling;

namespace Banned.CodeDiff.Avalonia.Models;

/// <summary>
///     统一的语义配色定制入口:宿主在 <see cref="Views.DiffView.Palette" /> 上提供明/暗
///     两套覆盖色,槽位为 <c>null</c> 时保留内置值(即上游 <c>--diff-*--</c> 变量),默认
///     外观因此不变。每个槽位设置后同时作用于该类行的行号格与内容格(如
///     <see cref="DiffPaletteColors.AddLineBackground" /> 同时覆盖新增行的行号格与内容格)。
///     更换 <see cref="Views.DiffView.Palette" /> 会重建行模型,行直接引用画刷实例。<br />
///     The unified semantic color entry point: the host supplies light/dark override sets on
///     <see cref="Views.DiffView.Palette" />, and a <c>null</c> slot keeps the built-in value (the
///     upstream <c>--diff-*--</c> variables), so the default appearance stays untouched. Once set,
///     each slot drives both the number and content cells of its line kind (e.g.
///     <see cref="DiffPaletteColors.AddLineBackground" /> covers the added lines' number and
///     content cells alike). Replacing <see cref="Views.DiffView.Palette" /> rebuilds the rows,
///     which reference the brush instances directly.
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
///     <see cref="DiffPalette" /> 在单个主题变体上的语义配色槽位;<c>null</c> 保留内置值。
///     词级高亮、hunk 头与展开行等衍生色不在定制范围内,保持内置值。<br />
///     The semantic color slots of <see cref="DiffPalette" /> for one theme variant; <c>null</c>
///     keeps the built-in value. Derived colors such as word-level highlights, hunk headers, and
///     expand rows stay outside the customization scope and keep their built-in values.
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
}
