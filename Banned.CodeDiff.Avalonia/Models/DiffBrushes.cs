using Avalonia.Media;
using Avalonia.Styling;

namespace Banned.CodeDiff.Avalonia.Models;

/// <summary>
///     Resolved brushes for one theme variant. Values mirror the light/dark CSS variables of the
///     upstream git-diff-view <c>_base.css</c>; theme switching rebuilds rows with the other set.
/// </summary>
internal sealed record DiffBrushSet(
    IBrush NumberForeground,
    IBrush AddNumber,
    IBrush AddContent,
    IBrush DeleteNumber,
    IBrush DeleteContent,
    IBrush ContextNumber,
    IBrush ContextContent,
    IBrush ExpandContent,
    IBrush EmptyNumber,
    IBrush EmptyContent,
    IBrush HunkNumber,
    IBrush HunkContent,
    IBrush HunkSide,
    IBrush HunkForeground,
    IBrush AddContentHighlight,
    IBrush DeleteContentHighlight,
    IBrush Splitter,
    IBrush MultiSelectOverlay,
    IBrush MultiSelectBorder,
    IBrush CommentOverlay,
    IBrush CommentCardBackground,
    IBrush CommentCardBorder);

/// <summary>Light/dark brush sets for the diff view (upstream <c>--diff-*--</c> variables).</summary>
internal static class DiffBrushes
{
    private static readonly IBrush NumberForegroundLight = Parse("#555555");
    private static readonly IBrush AddNumberLight        = Parse("#aceebb");
    private static readonly IBrush AddContentLight       = Parse("#dafbe1");
    private static readonly IBrush DeleteNumberLight     = Parse("#ffcecb");
    private static readonly IBrush DeleteContentLight    = Parse("#ffebe9");
    private static readonly IBrush ContextNumberLight    = Parse("#fafafa");
    private static readonly IBrush ContextContentLight   = Parse("#ffffff");
    private static readonly IBrush ExpandContentLight    = Parse("#fafafa");
    private static readonly IBrush EmptyNumberLight      = Parse("#fafafa");
    private static readonly IBrush EmptyContentLight     = Parse("#fafafa");
    private static readonly IBrush HunkNumberLight       = Parse("#b6e3ff");
    private static readonly IBrush HunkContentLight      = Parse("#ddf4ff");
    private static readonly IBrush HunkForegroundLight   = Parse("#777777");
    private static readonly IBrush AddHighlightLight     = Parse("#aceebb");
    private static readonly IBrush DeleteHighlightLight  = Parse("#ffcecb");
    private static readonly IBrush SplitterLight         = Parse("#dedede");
    private static readonly IBrush NumberForegroundDark  = Parse("#a0aaab");
    private static readonly IBrush AddNumberDark         = Parse("#284228");
    private static readonly IBrush AddContentDark        = Parse("#18271f");
    private static readonly IBrush DeleteNumberDark      = Parse("#4f2828");
    private static readonly IBrush DeleteContentDark     = Parse("#23191c");
    private static readonly IBrush ContextNumberDark     = Parse("#161b22");
    private static readonly IBrush ContextContentDark    = Parse("#0d1117");
    private static readonly IBrush ExpandContentDark     = Parse("#161b22");
    private static readonly IBrush EmptyNumberDark       = Parse("#161b22");
    private static readonly IBrush EmptyContentDark      = Parse("#161b22");
    private static readonly IBrush HunkNumberDark        = Parse("#0c2d6b");
    private static readonly IBrush HunkContentDark       = Parse("#131d2e");
    private static readonly IBrush HunkForegroundDark    = Parse("#9298a0");
    private static readonly IBrush AddHighlightDark      = Parse("#2f5732");
    private static readonly IBrush DeleteHighlightDark   = Parse("#713431");
    private static readonly IBrush SplitterDark          = Parse("#3d444d");

    // multiSelect palette (packages/*/src/_com.css): --diff-multi-select-bg #f0c000 at opacity
    // 0.15 for the cell overlay, --diff-multi-select-border #2588fa solid for the edge strip.
    // The CSS variables carry no per-theme definitions — both variants use the same fallbacks.
    private static readonly IBrush MultiSelectOverlay = WithOpacity(Parse("#f0c000"), 0.15);
    private static readonly IBrush MultiSelectBorder  = Parse("#2588fa");

    // Comment presentation (native M7 feature — no upstream variable): the anchored-line
    // overlay tints commented lines (GitHub's highlighted-line yellow), the card uses the
    // subtle canvas/border pair.
    private static readonly IBrush CommentOverlayLight          = WithOpacity(Parse("#fff8c5"), 0.55);
    private static readonly IBrush CommentCardBackgroundLight   = Parse("#f6f8fa");
    private static readonly IBrush CommentCardBorderLight       = Parse("#d1d9e0");
    private static readonly IBrush CommentOverlayDark           = WithOpacity(Parse("#d29922"), 0.28);
    private static readonly IBrush CommentCardBackgroundDark    = Parse("#151b23");
    private static readonly IBrush CommentCardBorderDark        = Parse("#3d444d");

    private static readonly DiffBrushSet LightSet = new(NumberForegroundLight, AddNumberLight, AddContentLight,
                                                        DeleteNumberLight, DeleteContentLight,
                                                        ContextNumberLight, ContextContentLight,
                                                        ExpandContentLight, EmptyNumberLight,
                                                        EmptyContentLight, HunkNumberLight,
                                                        HunkContentLight, HunkContentLight, HunkForegroundLight,
                                                        AddHighlightLight, DeleteHighlightLight,
                                                        SplitterLight, MultiSelectOverlay, MultiSelectBorder,
                                                        CommentOverlayLight, CommentCardBackgroundLight,
                                                        CommentCardBorderLight);

    private static readonly DiffBrushSet DarkSet = new(NumberForegroundDark, AddNumberDark, AddContentDark,
                                                       DeleteNumberDark, DeleteContentDark,
                                                       ContextNumberDark, ContextContentDark,
                                                       ExpandContentDark, EmptyNumberDark,
                                                       EmptyContentDark, HunkNumberDark,
                                                       HunkContentDark, HunkContentDark, HunkForegroundDark,
                                                       AddHighlightDark, DeleteHighlightDark,
                                                       SplitterDark, MultiSelectOverlay, MultiSelectBorder,
                                                       CommentOverlayDark, CommentCardBackgroundDark,
                                                       CommentCardBorderDark);

    public static DiffBrushSet Get(ThemeVariant variant)
    {
        return Get(variant, null);
    }

    /// <summary>
    ///     Resolves the brush set for a variant, applying the host's <see cref="DiffPalette" />
    ///     overrides on top of the built-in values (<c>null</c> slots keep the built-ins).
    /// </summary>
    public static DiffBrushSet Get(ThemeVariant variant, DiffPalette? palette)
    {
        var baseSet = variant == ThemeVariant.Dark ? DarkSet : LightSet;
        var colors  = palette?.GetColors(variant);

        if (colors == null) return baseSet;

        return baseSet with
        {
            AddNumber             = colors.AddLineBackground ?? baseSet.AddNumber,
            AddContent            = colors.AddLineBackground ?? baseSet.AddContent,
            DeleteNumber          = colors.DeleteLineBackground ?? baseSet.DeleteNumber,
            DeleteContent         = colors.DeleteLineBackground ?? baseSet.DeleteContent,
            ContextNumber         = colors.ContextBackground ?? baseSet.ContextNumber,
            ContextContent        = colors.ContextBackground ?? baseSet.ContextContent,
            MultiSelectOverlay    = colors.SelectionHighlight ?? baseSet.MultiSelectOverlay,
            MultiSelectBorder     = colors.SelectionEdge ?? baseSet.MultiSelectBorder,
            CommentOverlay        = colors.CommentLineHighlight ?? baseSet.CommentOverlay,
            CommentCardBackground = colors.CommentCardBackground ?? baseSet.CommentCardBackground,
            CommentCardBorder     = colors.CommentCardBorder ?? baseSet.CommentCardBorder
        };
    }

    private static IBrush Parse(string hex)
    {
        return new SolidColorBrush(Color.Parse(hex));
    }

    private static IBrush WithOpacity(IBrush brush, double opacity)
    {
        var solid = (SolidColorBrush)brush;
        return new SolidColorBrush(solid.Color, opacity);
    }
}
