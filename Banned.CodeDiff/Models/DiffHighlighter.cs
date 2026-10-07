namespace Banned.CodeDiff.Models;

/// <summary>
///     语法高亮器契约,移植自 packages/lowlight/src/index.ts。<br />
///     Syntax highlighting contract ported from packages/lowlight/src/index.ts.
/// </summary>
public interface IDiffHighlighter
{
    /// <summary>高亮器引擎 id,如上游的 "lowlight" / "shiki"。<br />Engine id, e.g. "lowlight" / "shiki" upstream.</summary>
    string Name { get; }

    /// <summary>
    ///     "class"(主题无关 AST)或 "style";见 <see cref="HighlighterType" />。<br />"class" (theme-independent AST) or "style";
    ///     see <see cref="HighlighterType" />.
    /// </summary>
    HighlighterType Type { get; }

    /// <summary>原始行数超过该值的文件跳过语法高亮。<br />Files longer than this many raw lines skip syntax highlighting.</summary>
    int MaxLineToIgnoreSyntax { get; }

    /// <summary>
    ///     与文件名匹配的忽略规则(JS: (string | RegExp)[]);见 <see cref="IgnorePattern" />。<br />JS: (string | RegExp)[] matched
    ///     against the file name; see <see cref="IgnorePattern" />.
    /// </summary>
    IReadOnlyList<IgnorePattern> IgnoreSyntaxHighlightList { get; }

    /// <summary>
    ///     将完整文件分词为 hast 风格语法树;需跳过高亮时返回 <c>null</c>。<br />
    ///     Tokenizes a full file into a hast-style tree; returns null when highlighting is skipped.
    /// </summary>
    /// <param name="raw">文件原文。<br />The full raw file content.</param>
    /// <param name="fileName">
    ///     文件名,用于忽略规则匹配;可为 <c>null</c>。<br />File name used for ignore-pattern matching; may be <c>null</c>
    ///     .
    /// </param>
    /// <param name="lang">语言 id。<br />The language id.</param>
    /// <param name="theme">主题名。<br />The theme name.</param>
    /// <returns>类 hast 语法树;跳过高亮时为 <c>null</c>。<br />The hast-like syntax tree, or <c>null</c> when highlighting is skipped.</returns>
    SyntaxNode? GetAst(string raw, string? fileName, string? lang, string? theme);

    /// <summary>将语法树按行拆分为逐行文本段列表。<br />JS: processAST(ast) — splits the AST into per-line span lists.</summary>
    /// <param name="ast">待拆分的语法树。<br />The AST to split.</param>
    /// <returns>逐行语法拆分结果。<br />The per-line syntax split result.</returns>
    SyntaxAstResult ProcessAst(SyntaxNode ast);

    /// <summary>引擎是否已注册该语言 id。<br />JS: hasRegisteredCurrentLang(lang) — whether the engine knows this language id.</summary>
    /// <param name="lang">语言 id。<br />The language id.</param>
    bool HasRegisteredCurrentLang(string lang);
}

/// <summary>
///     JS <c>processAST</c> 辅助函数(highlightAST.ts)的结果形态。<br />Result shape of the JS <c>processAST</c> helper
///     (highlightAST.ts).
/// </summary>
/// <param name="SyntaxFileObject">1 基行号 → 该行的语法文本段。<br />The 1-based line number → syntax spans of that line.</param>
/// <param name="SyntaxFileLineNumber">处理后的总行数。<br />Total line count after processing.</param>
public sealed record SyntaxAstResult(Dictionary<int, SyntaxLine> SyntaxFileObject, int SyntaxFileLineNumber);
