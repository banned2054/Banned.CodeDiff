namespace Banned.CodeDiff.Models;

/// <summary>
///     packages/lowlight/src/index.ts 中 <c>DiffHighlighter.type</c> 的 JS 字符串枚举
///     <c>"class" | "style"</c> 的移植:"class" 引擎产出主题无关的 AST(主题后置应用),
///     "style" 引擎把主题颜色直接烘焙进 AST。<br />
///     Port of the JS string enum <c>"class" | "style"</c> on
///     <c>DiffHighlighter.type</c> (packages/lowlight/src/index.ts): "class" engines
///     produce a theme-independent AST (theme applied later), "style" engines bake
///     theme colors into the AST.
/// </summary>
public enum HighlighterType
{
    /// <summary>JS: "class" —— 主题无关的 AST。<br />JS: "class" — theme-independent AST.</summary>
    Class,

    /// <summary>JS: "style" —— AST 携带依赖主题的内联样式。<br />JS: "style" — AST carries theme-dependent inline styles.</summary>
    Style
}
