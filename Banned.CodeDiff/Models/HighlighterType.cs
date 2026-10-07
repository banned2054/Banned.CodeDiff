namespace Banned.CodeDiff.Models;

/// <summary>
///     高亮器类型,移植自 packages/lowlight/src/index.ts:Class 延后应用主题,Style 将颜色写入 AST。<br />
///     Highlighter kinds ported from packages/lowlight/src/index.ts: Class applies themes later; Style embeds colors in the AST.
/// </summary>
public enum HighlighterType
{
    /// <summary>JS: "class" —— 主题无关的 AST。<br />JS: "class" — theme-independent AST.</summary>
    Class,

    /// <summary>JS: "style" —— AST 携带依赖主题的内联样式。<br />JS: "style" — AST carries theme-dependent inline styles.</summary>
    Style
}
