using Avalonia.Styling;

namespace Banned.CodeDiff.Avalonia.Models;

/// <summary>
///     行构建的配色输入。优先级:宿主覆盖 → 独立预设 → 组合预设 → GitHub;独立预设仅影响对应侧。<br />
///     Row color inputs. Priority: host overrides, independent presets, combined preset, GitHub; independent presets affect only their own side.
/// </summary>
internal sealed record DiffThemeContext(
    ThemeVariant         Variant,
    DiffPalette?         Palette         = null,
    DiffThemePreset?     ThemePreset     = null,
    DiffThemePreset?     DiffPreset      = null,
    DiffThemePreset?     SyntaxPreset    = null,
    DiffSyntaxOverrides? SyntaxOverrides = null)
{
    /// <summary>解析画刷集(逐槽位链)。<br />Resolves the brush set (the per-slot chain).</summary>
    public DiffBrushSet ResolveBrushes()
    {
        return DiffBrushes.Get(Variant, Palette, ThemePreset, DiffPreset);
    }

    /// <summary>
    ///     解析语法颜色解析器;无定制时为 <c>null</c>(走 wrapper style 最终颜色通路)。
    ///     <br />Resolves the syntax color resolver; <c>null</c> when uncustomized (the wrapper
    ///     style final-color path).
    /// </summary>
    public DiffSyntaxColors? ResolveSyntaxColors()
    {
        return DiffSyntaxColors.Create(SyntaxPreset, ThemePreset, SyntaxOverrides, Variant);
    }
}
