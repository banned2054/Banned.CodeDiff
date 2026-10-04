namespace Banned.CodeDiff.Models;

/// <summary>
/// Port of the JS string enum <c>"class" | "style"</c> on
/// <c>DiffHighlighter.type</c> (packages/lowlight/src/index.ts): "class" engines
/// produce a theme-independent AST (theme applied later), "style" engines bake
/// theme colors into the AST.
/// </summary>
public enum HighlighterType
{
    /// <summary>JS: "class" — theme-independent AST.</summary>
    Class,

    /// <summary>JS: "style" — AST carries theme-dependent inline styles.</summary>
    Style,
}
