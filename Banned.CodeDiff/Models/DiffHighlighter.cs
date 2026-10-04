namespace Banned.CodeDiff.Models;

/// <summary>
/// Port of the <c>DiffHighlighter</c> interface shape
/// (packages/lowlight/src/index.ts — the interface the core package programs
/// against; lowlight/shiki/lezer implement it upstream).
/// <see cref="Type"/> ports the JS string enum (<c>"class"</c> / <c>"style"</c>)
/// as <see cref="HighlighterType"/>.
/// </summary>
public interface IDiffHighlighter
{
    /// <summary>Engine id, e.g. "lowlight" / "shiki" upstream.</summary>
    string Name { get; }

    /// <summary>"class" (theme-independent AST) or "style"; see <see cref="HighlighterType"/>.</summary>
    HighlighterType Type { get; }

    /// <summary>Files longer than this many raw lines skip syntax highlighting.</summary>
    int MaxLineToIgnoreSyntax { get; }

    /// <summary>JS: (string | RegExp)[] matched against the file name; see <see cref="IgnorePattern"/>.</summary>
    IReadOnlyList<IgnorePattern> IgnoreSyntaxHighlightList { get; }

    /// <summary>JS: getAST(raw, fileName, lang, theme) — tokenizes the full file into a
    /// hast-like tree (root SyntaxNode whose children are per-token wrapper elements).
    /// Returns <c>null</c> when highlighting must be skipped (ignored file, failure).</summary>
    SyntaxNode? GetAst(string raw, string? fileName, string? lang, string? theme);

    /// <summary>JS: processAST(ast) — splits the AST into per-line span lists.</summary>
    SyntaxAstResult ProcessAst(SyntaxNode ast);

    /// <summary>JS: hasRegisteredCurrentLang(lang) — whether the engine knows this language id.</summary>
    bool HasRegisteredCurrentLang(string lang);
}

/// <summary>Result shape of the JS <c>processAST</c> helper (highlightAST.ts).</summary>
public sealed record SyntaxAstResult(
    Dictionary<int, SyntaxLine> SyntaxFileObject,
    int SyntaxFileLineNumber
);
