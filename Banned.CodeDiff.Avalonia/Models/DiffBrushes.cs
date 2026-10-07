using Avalonia.Media;
using Avalonia.Styling;

namespace Banned.CodeDiff.Avalonia.Models;

/// <summary>
///     Resolved brushes for one theme variant, after the M8 resolution chain
///     (host palette overrides → independent diff preset → combined theme preset →
///     the GitHub baseline). The GitHub values mirror the light/dark CSS variables of
///     the upstream git-diff-view <c>_base.css</c>; theme switching rebuilds rows with
///     the other set.
/// </summary>
internal sealed record DiffBrushSet
{
    public required IBrush NumberForeground { get; init; }
    public required IBrush AddNumber        { get; init; }
    public required IBrush AddContent       { get; init; }
    public required IBrush DeleteNumber     { get; init; }
    public required IBrush DeleteContent    { get; init; }
    public required IBrush ContextNumber    { get; init; }
    public required IBrush ContextContent   { get; init; }
    public required IBrush ExpandContent    { get; init; }

    /// <summary>Empty half-row cells (upstream colors them like the context number cell).</summary>
    public required IBrush EmptyNumber { get; init; }

    /// <summary>Empty half-row cells (upstream colors them like the expand content cell).</summary>
    public required IBrush EmptyContent { get; init; }

    public required IBrush HunkNumber  { get; init; }
    public required IBrush HunkContent { get; init; }

    /// <summary>
    ///     The hunk row's new-side cells — upstream shares the content background
    ///     (<c>--diff-hunk-line--</c> for both).
    /// </summary>
    public required IBrush HunkSide { get; init; }

    public required IBrush HunkForeground         { get; init; }
    public required IBrush AddContentHighlight    { get; init; }
    public required IBrush DeleteContentHighlight { get; init; }
    public required IBrush Splitter               { get; init; }
    public required IBrush MultiSelectOverlay     { get; init; }
    public required IBrush MultiSelectBorder      { get; init; }
    public required IBrush CommentOverlay         { get; init; }
    public required IBrush CommentCardBackground  { get; init; }
    public required IBrush CommentCardBorder      { get; init; }

    /// <summary>
    ///     Explicit per-kind selected backgrounds (M8) — the template binds these instead of a
    ///     generic overlay; the GitHub baseline keeps the upstream translucent overlay value.
    /// </summary>
    public required IBrush SelectedAdd { get; init; }

    public required IBrush SelectedDelete  { get; init; }
    public required IBrush SelectedContext { get; init; }

    /// <summary>
    ///     Canvas background, or <c>null</c> when neither the preset nor the palette defines one
    ///     (the GitHub baseline never touches the control background).
    /// </summary>
    public IBrush? CanvasBackground { get; init; }

    /// <summary>
    ///     Text (substring) selection colors — M8 defines the slots only, no interaction.
    ///     <c>null</c> foreground keeps the syntax colors for selected characters.
    /// </summary>
    public IBrush? TextSelectionBackground { get; init; }

    public IBrush? TextSelectionForeground { get; init; }
}

/// <summary>
///     Resolves the <see cref="DiffBrushSet" /> for a theme variant through the M8 slot
///     chain: host <see cref="DiffPalette" /> overrides → the independent diff preset →
///     the combined theme preset's diff palette → the GitHub baseline (the golden-locked
///     upstream values). Each slot resolves independently, so a preset switch keeps host
///     overrides and an unset slot keeps the next layer's value.
/// </summary>
internal static class DiffBrushes
{
    public static DiffBrushSet Get(ThemeVariant variant)
    {
        return Get(variant, null);
    }

    public static DiffBrushSet Get(ThemeVariant variant, DiffPalette? palette)
    {
        return Get(variant, palette, null, null);
    }

    /// <summary>
    ///     按宿主覆盖 → 独立预设 → 组合预设 → GitHub 解析画刷;细槽位优先,缺少浅色变体回退 GitHub。<br />
    ///     Resolves brushes by host overrides, independent preset, combined preset, GitHub; fine slots win and missing light variants use GitHub.
    /// </summary>
    public static DiffBrushSet Get(
        ThemeVariant variant, DiffPalette? palette, DiffThemePreset? themePreset, DiffThemePreset? diffPreset)
    {
        var github = DiffThemePresets.GetDiffColors(variant, DiffThemePreset.GitHub)!;

        // 显式独立预设缺少当前变体时回退 GitHub,不继承组合预设。
        var pinnedToGithub = diffPreset != null && DiffThemePresets.GetDiffColors(variant, diffPreset) == null;

        var theme = pinnedToGithub ? null : DiffThemePresets.GetDiffColors(variant, themePreset);

        var diff = pinnedToGithub ? github : DiffThemePresets.GetDiffColors(variant, diffPreset);

        var host = palette?.GetColors(variant);

        // 逐槽位优先级:宿主 → 独立预设 → 组合预设 → GitHub。
        IBrush Chain(Func<DiffPaletteColors, IBrush?> fine)
        {
            if (host != null)
            {
                var hostValue = fine(host);

                if (hostValue != null) return hostValue;
            }

            var value = diff != null ? fine(diff) : null;

            value ??= theme != null ? fine(theme) : null;

            return value ?? fine(github) ?? github.ContextContentBackground!;
        }

        // 宿主细槽位优先于行级槽位。
        IBrush ChainHostCoarse(Func<DiffPaletteColors, IBrush?> fine, Func<DiffPaletteColors, IBrush?> coarse)
        {
            if (host == null) return Chain(fine);
            var hostValue = fine(host) ?? coarse(host);

            return hostValue ?? Chain(fine);
        }

        // GitHub 未定义画布和文本选区颜色,可保持 null。
        IBrush? ChainOptional(Func<DiffPaletteColors, IBrush?> slot)
        {
            var value = host != null ? slot(host) : null;

            value ??= diff != null ? slot(diff) : null;

            return value ?? (theme != null ? slot(theme) : null);
        }

        var overlay = Chain(c => c.SelectionHighlight);

        // 选中背景按细槽位再通用覆盖层解析;宿主优先于预设,GitHub 保留共享覆盖层实例。
        IBrush Selected(Func<DiffPaletteColors, IBrush?> fine)
        {
            if (host != null)
            {
                var hostValue = fine(host) ?? host.SelectionHighlight;

                if (hostValue != null) return hostValue;
            }

            var value = diff != null ? fine(diff) ?? diff.SelectionHighlight : null;

            value ??= theme != null ? fine(theme) ?? theme.SelectionHighlight : null;

            return value ?? overlay;
        }

        return new DiffBrushSet
        {
            NumberForeground = Chain(c => c.NumberForeground),
            AddNumber        = ChainHostCoarse(c => c.AddNumberBackground, c => c.AddLineBackground),
            AddContent       = ChainHostCoarse(c => c.AddContentBackground, c => c.AddLineBackground),
            DeleteNumber     = ChainHostCoarse(c => c.DeleteNumberBackground, c => c.DeleteLineBackground),
            DeleteContent    = ChainHostCoarse(c => c.DeleteContentBackground, c => c.DeleteLineBackground),
            ContextNumber    = ChainHostCoarse(c => c.ContextNumberBackground, c => c.ContextBackground),
            ContextContent   = ChainHostCoarse(c => c.ContextContentBackground, c => c.ContextBackground),
            ExpandContent    = Chain(c => c.ExpandBackground),
            // 空侧跟随预设的上下文/展开配色,不应用宿主行级覆盖。
            EmptyNumber = diff?.ContextNumberBackground ??
                          theme?.ContextNumberBackground ?? github.ContextNumberBackground!,
            EmptyContent            = diff?.ExpandBackground ?? theme?.ExpandBackground ?? github.ExpandBackground!,
            HunkNumber              = Chain(c => c.HunkNumberBackground),
            HunkContent             = Chain(c => c.HunkBackground),
            HunkSide                = Chain(c => c.HunkBackground),
            HunkForeground          = Chain(c => c.HunkForeground),
            AddContentHighlight     = Chain(c => c.WordAddHighlight),
            DeleteContentHighlight  = Chain(c => c.WordDeleteHighlight),
            Splitter                = Chain(c => c.Splitter),
            MultiSelectOverlay      = overlay,
            MultiSelectBorder       = Chain(c => c.SelectionEdge),
            CommentOverlay          = Chain(c => c.CommentLineHighlight),
            CommentCardBackground   = Chain(c => c.CommentCardBackground),
            CommentCardBorder       = Chain(c => c.CommentCardBorder),
            SelectedAdd             = Selected(c => c.SelectedAddBackground),
            SelectedDelete          = Selected(c => c.SelectedDeleteBackground),
            SelectedContext         = Selected(c => c.SelectedContextBackground),
            CanvasBackground        = ChainOptional(c => c.CanvasBackground),
            TextSelectionBackground = ChainOptional(c => c.TextSelectionBackground),
            TextSelectionForeground = ChainOptional(c => c.TextSelectionForeground)
        };
    }
}
