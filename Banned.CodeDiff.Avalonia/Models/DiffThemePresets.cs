using Avalonia.Media;
using Avalonia.Media.Immutable;
using Avalonia.Styling;

namespace Banned.CodeDiff.Avalonia.Models;

/// <summary>
///     内置 Diff 调色板的共享只读预设;未定制槽位回退 GitHub 基线。<br />
///     Shared read-only diff palettes; unspecified slots fall back to the GitHub baseline.
/// </summary>
internal static class DiffThemePresets
{
    // ---- GitHub (the golden baseline; values = the upstream --diff-*-- variables) ----

    private static readonly DiffPaletteColors GitHubLight = new()
    {
        AddNumberBackground      = Brush("#aceebb"),
        AddContentBackground     = Brush("#dafbe1"),
        DeleteNumberBackground   = Brush("#ffcecb"),
        DeleteContentBackground  = Brush("#ffebe9"),
        ContextNumberBackground  = Brush("#fafafa"),
        ContextContentBackground = Brush("#ffffff"),
        ExpandBackground         = Brush("#fafafa"),
        HunkNumberBackground     = Brush("#b6e3ff"),
        HunkBackground           = Brush("#ddf4ff"),
        HunkForeground           = Brush("#777777"),
        Splitter                 = Brush("#dedede"),
        NumberForeground         = Brush("#555555"),
        WordAddHighlight         = Brush("#aceebb"),
        WordDeleteHighlight      = Brush("#ffcecb"),
        // 空的细选中槽位回退共享 SelectionHighlight,保持默认外观。
        SelectionHighlight    = WithOpacity("#f0c000", 0.15),
        SelectionEdge         = Brush("#2588fa"),
        CommentLineHighlight  = WithOpacity("#fff8c5", 0.55),
        CommentCardBackground = Brush("#f6f8fa"),
        CommentCardBorder     = Brush("#d1d9e0")
        // GitHub 不定义画布和文本选区颜色。
    };

    private static readonly DiffPaletteColors GitHubDark = new()
    {
        AddNumberBackground      = Brush("#284228"),
        AddContentBackground     = Brush("#18271f"),
        DeleteNumberBackground   = Brush("#4f2828"),
        DeleteContentBackground  = Brush("#23191c"),
        ContextNumberBackground  = Brush("#161b22"),
        ContextContentBackground = Brush("#0d1117"),
        ExpandBackground         = Brush("#161b22"),
        HunkNumberBackground     = Brush("#0c2d6b"),
        HunkBackground           = Brush("#131d2e"),
        HunkForeground           = Brush("#9298a0"),
        Splitter                 = Brush("#3d444d"),
        NumberForeground         = Brush("#a0aaab"),
        WordAddHighlight         = Brush("#2f5732"),
        WordDeleteHighlight      = Brush("#713431"),
        SelectionHighlight       = WithOpacity("#f0c000", 0.15),
        SelectionEdge            = Brush("#2588fa"),
        CommentLineHighlight     = WithOpacity("#d29922", 0.28),
        CommentCardBackground    = Brush("#151b23"),
        CommentCardBorder        = Brush("#3d444d")
    };

    // Monokai 仅深色;语法参考 VS Code,Diff 背景基于 #272822 派生。

    private static readonly DiffPaletteColors MonokaiDark = new()
    {
        AddNumberBackground       = Brush("#2a4d33"),
        AddContentBackground      = Brush("#1f3a26"),
        DeleteNumberBackground    = Brush("#4d2a30"),
        DeleteContentBackground   = Brush("#3a1f24"),
        ContextNumberBackground   = Brush("#1e1f1c"),
        ContextContentBackground  = Brush("#272822"),
        ExpandBackground          = Brush("#221f26"),
        HunkNumberBackground      = Brush("#16384a"),
        HunkBackground            = Brush("#1d2b33"),
        HunkForeground            = Brush("#75715e"),
        Splitter                  = Brush("#3e3d32"),
        NumberForeground          = Brush("#90908a"),
        WordAddHighlight          = Brush("#395c40"),
        WordDeleteHighlight       = Brush("#5c393f"),
        SelectedAddBackground     = Brush("#49483e"),
        SelectedDeleteBackground  = Brush("#49483e"),
        SelectedContextBackground = Brush("#49483e"),
        SelectionHighlight        = WithOpacity("#f0c000", 0.15),
        SelectionEdge             = Brush("#a6e22e"),
        CommentLineHighlight      = WithOpacity("#75715e", 0.35),
        CommentCardBackground     = Brush("#1e1f1c"),
        CommentCardBorder         = Brush("#3e3d32"),
        CanvasBackground          = Brush("#1e1f1c")
    };

    // Visual Studio 配色参考 VS Code Dark+ / Light+ 及其 Git diff 行色。

    private static readonly DiffPaletteColors VsDark = new()
    {
        AddNumberBackground       = Brush("#2a5038"),
        AddContentBackground      = Brush("#294436"),
        DeleteNumberBackground    = Brush("#5a383b"),
        DeleteContentBackground   = Brush("#4b2f32"),
        ContextNumberBackground   = Brush("#252526"),
        ContextContentBackground  = Brush("#1e1e1e"),
        ExpandBackground          = Brush("#252526"),
        HunkNumberBackground      = Brush("#1b3a5c"),
        HunkBackground            = Brush("#20303f"),
        HunkForeground            = Brush("#8b949e"),
        Splitter                  = Brush("#333333"),
        NumberForeground          = Brush("#a0a0a0"),
        WordAddHighlight          = Brush("#3f604a"),
        WordDeleteHighlight       = Brush("#664044"),
        SelectedAddBackground     = Brush("#264f78"),
        SelectedDeleteBackground  = Brush("#264f78"),
        SelectedContextBackground = Brush("#264f78"),
        SelectionHighlight        = WithOpacity("#f0c000", 0.15),
        SelectionEdge             = Brush("#0078d4"),
        CommentLineHighlight      = WithOpacity("#4d6ea8", 0.28),
        CommentCardBackground     = Brush("#252526"),
        CommentCardBorder         = Brush("#454545"),
        CanvasBackground          = Brush("#1e1e1e")
    };

    private static readonly DiffPaletteColors VsLight = new()
    {
        AddNumberBackground       = Brush("#ccffd8"),
        AddContentBackground      = Brush("#e6ffec"),
        DeleteNumberBackground    = Brush("#ffd7d5"),
        DeleteContentBackground   = Brush("#ffebe9"),
        ContextNumberBackground   = Brush("#f3f3f3"),
        ContextContentBackground  = Brush("#ffffff"),
        ExpandBackground          = Brush("#f6f8fa"),
        HunkNumberBackground      = Brush("#cfe4fb"),
        HunkBackground            = Brush("#e9eef5"),
        HunkForeground            = Brush("#5a5a5a"),
        Splitter                  = Brush("#e0e0e0"),
        NumberForeground          = Brush("#6e6e6e"),
        WordAddHighlight          = Brush("#b4e8c9"),
        WordDeleteHighlight       = Brush("#ffc4bd"),
        SelectedAddBackground     = Brush("#add6ff"),
        SelectedDeleteBackground  = Brush("#add6ff"),
        SelectedContextBackground = Brush("#add6ff"),
        SelectionHighlight        = WithOpacity("#f0c000", 0.15),
        SelectionEdge             = Brush("#005fb8"),
        CommentLineHighlight      = WithOpacity("#fff8c5", 0.55),
        CommentCardBackground     = Brush("#f3f3f3"),
        CommentCardBorder         = Brush("#d0d0d0"),
        CanvasBackground          = Brush("#ffffff")
    };

    // Codex 初版深色参考配色,待校准。

    private static readonly DiffPaletteColors CodexDark = new()
    {
        AddNumberBackground       = Brush("#1d3324"),
        AddContentBackground      = Brush("#16281c"),
        DeleteNumberBackground    = Brush("#3a2222"),
        DeleteContentBackground   = Brush("#2d1a1a"),
        ContextNumberBackground   = Brush("#0e0e0e"),
        ContextContentBackground  = Brush("#131313"),
        ExpandBackground          = Brush("#131313"),
        HunkNumberBackground      = Brush("#16233a"),
        HunkBackground            = Brush("#182333"),
        HunkForeground            = Brush("#8b8b8b"),
        Splitter                  = Brush("#2a2a2a"),
        NumberForeground          = Brush("#9d9d97"),
        WordAddHighlight          = Brush("#24462c"),
        WordDeleteHighlight       = Brush("#462424"),
        SelectedAddBackground     = Brush("#2f3b46"),
        SelectedDeleteBackground  = Brush("#2f3b46"),
        SelectedContextBackground = Brush("#2f3b46"),
        SelectionHighlight        = WithOpacity("#f0c000", 0.15),
        SelectionEdge             = Brush("#10a37f"),
        CommentLineHighlight      = WithOpacity("#10a37f", 0.25),
        CommentCardBackground     = Brush("#1a1a1a"),
        CommentCardBorder         = Brush("#333333"),
        CanvasBackground          = Brush("#0e0e0e")
    };

    /// <summary>
    ///     按预设与变体获取调色板;未选择或缺少变体时返回 <c>null</c>。<br />
    ///     Gets a preset palette for a variant; returns null when unselected or the variant is unavailable.
    /// </summary>
    public static DiffPaletteColors? GetDiffColors(ThemeVariant variant, DiffThemePreset? preset)
    {
        switch (preset)
        {
            case null :                   return null;
            case DiffThemePreset.GitHub : return variant == ThemeVariant.Dark ? GitHubDark : GitHubLight;
            case DiffThemePreset.VisualStudio :
                return variant == ThemeVariant.Dark ? VsDark : VsLight;
            case DiffThemePreset.Monokai : return variant == ThemeVariant.Dark ? MonokaiDark : null;
            case DiffThemePreset.Codex :   return variant == ThemeVariant.Dark ? CodexDark : null;
            default :                      return null;
        }
    }

    /// <summary>按预设与变体解析内置语法主题的资源名;无对应主题时返回 <c>null</c>。<br />Resolves the bundled syntax theme resource name for a preset and variant; <c>null</c> when the preset has none.</summary>
    public static string? GetSyntaxThemeName(DiffThemePreset? preset, ThemeVariant variant)
    {
        return preset switch
        {
            null or DiffThemePreset.GitHub => variant == ThemeVariant.Dark ? "github-dark" : "github-light",
            DiffThemePreset.VisualStudio   => variant == ThemeVariant.Dark ? "vs-dark" : "vs-light",
            DiffThemePreset.Monokai        => variant == ThemeVariant.Dark ? "monokai" : null,
            DiffThemePreset.Codex          => variant == ThemeVariant.Dark ? "codex-dark" : null,
            _                              => null
        };
    }

    internal static IBrush Brush(string hex)
    {
        // 预设画刷共享且暴露给行模型,须不可变以隔离各视图。
        return new ImmutableSolidColorBrush(Color.Parse(hex));
    }

    internal static IBrush WithOpacity(string hex, double opacity)
    {
        // 不可变画刷将不透明度写入 alpha。
        var color = Color.Parse(hex);

        return new ImmutableSolidColorBrush(new Color((byte)Math.Round(color.A * opacity), color.R, color.G,
                                                      color.B));
    }
}
