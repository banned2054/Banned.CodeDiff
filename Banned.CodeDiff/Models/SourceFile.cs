using Banned.CodeDiff.Services;

namespace Banned.CodeDiff.Models;

/// <summary>
/// Port of packages/core/src/file.ts — raw + syntax state of one source file.
/// The upstream module-level cross-instance cache (cache.ts + getFile) is not ported
/// as-is; the expensive part — the syntax tokenization result — is memoized instead
/// through a bounded LRU inside <see cref="DoSyntax"/>, recorded as a performance
/// enhancement (see Docs/CHANGELOG.md). Caching at the doSyntax level (rather than
/// sharing whole File instances like getFile does) keeps the observable rule "syntax
/// exists only after doSyntax ran on this file" intact.
/// </summary>
public sealed class SourceFile(string row, string lang, string? fileName = null)
{
    /// <summary>The cache key of one memoized doSyntax outcome, plus the outcome itself.</summary>
    private sealed record SyntaxResultEntry(
        (string Raw, string Lang, string? FileName, string EngineName, HighlighterType EngineType, string? Theme) Key,
        SyntaxNode Ast, Dictionary<int, SyntaxLine> SyntaxFile, int SyntaxLength);

    /// <summary>
    /// Bounded LRU of recent syntax results (most recent first), keyed by the inputs that
    /// determine them: content, lang, file name, engine identity, and theme — except for
    /// <c>Class</c>-typed engines (the built-in one), whose ASTs carry both themes
    /// (the upstream otherThemeKey reuse generalized). Like
    /// <c>DiffParser.Shared</c>/<c>TemplateOptions</c> this is global mutable state shared
    /// across <see cref="Services.DiffFile"/> instances — single-threaded use by design.
    /// </summary>
    private static readonly LinkedList<SyntaxResultEntry> SyntaxResultOrder = [];
    private static readonly Dictionary<(string Raw, string Lang, string? FileName, string EngineName,
        HighlighterType EngineType, string? Theme), LinkedListNode<SyntaxResultEntry>> SyntaxResultMap = new();

    private const int SyntaxResultCapacity = 8;

    /// <summary>
    /// Drops every memoized syntax result. Called when global engine configuration changes
    /// (transform function, syntax ignore list / threshold) so the next doSyntax reflects the
    /// new settings — without memoization every run recomputed, and that observable behavior
    /// is preserved.
    /// </summary>
    public static void ClearFileCache()
    {
        SyntaxResultOrder.Clear();
        SyntaxResultMap.Clear();
    }

    /// <summary>The theme component of the cache key: <c>null</c> for class engines
    /// (theme-independent ASTs), the requested theme otherwise.</summary>
    private static string? ThemeKey(IDiffHighlighter engine, string? theme) =>
        engine.Type == Banned.CodeDiff.Models.HighlighterType.Class ? null : theme;

    private static SyntaxResultEntry? TryGetSyntaxResult(string raw, string lang, string? fileName,
                                                         IDiffHighlighter engine, string? theme)
    {
        var key = (raw, lang, fileName, engine.Name, engine.Type, ThemeKey(engine, theme));

        if (!SyntaxResultMap.TryGetValue(key, out var node))
        {
            return null;
        }

        SyntaxResultOrder.Remove(node);
        SyntaxResultOrder.AddFirst(node);

        return node.Value;
    }

    private static void CacheSyntaxResult(string raw, string lang, string? fileName, IDiffHighlighter engine,
                                          string? theme, SyntaxNode ast, Dictionary<int, SyntaxLine> syntaxFile,
                                          int syntaxLength)
    {
        var entry = new SyntaxResultEntry(
            (raw, lang, fileName, engine.Name, engine.Type, ThemeKey(engine, theme)), ast, syntaxFile, syntaxLength);

        var node = SyntaxResultOrder.AddFirst(entry);

        SyntaxResultMap[entry.Key] = node;

        if (SyntaxResultMap.Count > SyntaxResultCapacity)
        {
            var last = SyntaxResultOrder.Last!;

            SyntaxResultOrder.RemoveLast();
            SyntaxResultMap.Remove(last.Value.Key);
        }
    }

    public string  Raw      { get; } = Transform.ProcessTransformForFile(row);
    public string  Lang     { get; } = lang;
    public string? FileName { get; } = fileName;

    /// <summary>JS: rawFile — 1-based line number → line content (trailing "\n" kept except on the last line).</summary>
    public Dictionary<int, string> RawFile { get; private set; } = new();

    public bool HasDoRaw      { get; private set; }
    public int? RawLength     { get; private set; }
    public int  MaxLineNumber { get; private set; }

    // ---- syntax state (doSyntax) ----

    /// <summary>JS: ast — the highlighter-produced tree (hast Root equivalent).</summary>
    public SyntaxNode? Ast { get; private set; }

    /// <summary>JS: theme ("light" | "dark"); recorded for the hasDoSyntax idempotence check.</summary>
    public string? Theme { get; private set; }

    /// <summary>JS: syntaxFile — 1-based line number → syntax spans of that line.</summary>
    public Dictionary<int, SyntaxLine>? SyntaxFile { get; private set; }

    public bool HasDoSyntax  { get; private set; }
    public int? SyntaxLength { get; private set; }

    public string? HighlighterName { get; private set; }

    public HighlighterType? HighlighterType { get; private set; }

    public void DoRaw()
    {
        if (Raw.Length == 0 || HasDoRaw)
        {
            return;
        }

        var rawString = Raw;

        var rawArray = rawString.Split('\n');

        RawLength = rawArray.Length;

        MaxLineNumber = rawArray.Length;

        RawFile = new Dictionary<int, string>(rawArray.Length);

        for (var i = 0; i < rawArray.Length; i++)
        {
            RawFile[i + 1] = i < rawArray.Length - 1 ? rawArray[i] + "\n" : rawArray[i];
        }

        HasDoRaw = true;
    }

    /// <summary>
    /// Port of File.doSyntax({ registerHighlighter, theme }). The upstream falls
    /// back to the built-in lowlight engine when the injected highlighter does not
    /// know the language; the C# port's built-in default is the TextMate engine,
    /// and a language unknown to both simply stays unhighlighted (GetAst → null).
    /// </summary>
    public void DoSyntax(IDiffHighlighter? registerHighlighter = null, string? theme = null)
    {
        if (Raw.Length == 0)
        {
            return;
        }

        var finalHighlighter = registerHighlighter ?? DiffHighlighters.Default;

        if (RawLength is > 0 && RawLength > finalHighlighter.MaxLineToIgnoreSyntax)
        {
            // JS logs a dev warning ("Ignoring syntax highlighting ... exceeds the threshold").
            return;
        }

        var supportEngine = finalHighlighter;

        try
        {
            if (!finalHighlighter.HasRegisteredCurrentLang(Lang))
            {
                supportEngine = DiffHighlighters.Default;
            }
        }
        catch
        {
            supportEngine = DiffHighlighters.Default;
        }

        // NOTE: the enum must be qualified here — the simple name resolves to the
        // HighlighterType property of this instance (C# "Color Color" rule).
        if (HasDoSyntax &&
            supportEngine.Name == HighlighterName &&
            supportEngine.Type == HighlighterType &&
            (Theme == theme || supportEngine.Type == Banned.CodeDiff.Models.HighlighterType.Class))
        {
            return;
        }

        // A memoized result for the same inputs replaces the re-tokenization (the expensive
        // part of this method); the adopted fields are exactly what a fresh run produced.
        if (TryGetSyntaxResult(Raw, Lang, FileName, supportEngine, theme) is { } hit)
        {
            Ast             = hit.Ast;
            Theme           = theme;
            SyntaxFile      = hit.SyntaxFile;
            SyntaxLength    = hit.SyntaxLength;
            HighlighterName = supportEngine.Name;
            HighlighterType = supportEngine.Type;
            HasDoSyntax     = true;

            return;
        }

        Ast = supportEngine.GetAst(Raw, FileName, Lang, theme);

        Theme = theme;

        if (Ast == null)
        {
            return;
        }

        var result = supportEngine.ProcessAst(Ast);

        // The upstream additionally builds HTML string templates here when the
        // global TemplateOptions switch is on; templates are not ported (see AGENTS.md).

        SyntaxFile = result.SyntaxFileObject;

        SyntaxLength = result.SyntaxFileLineNumber;

        HighlighterName = supportEngine.Name;

        HighlighterType = supportEngine.Type;

        CacheSyntaxResult(Raw, Lang, FileName, supportEngine, theme, Ast, SyntaxFile,
                          result.SyntaxFileLineNumber);

        // JS additionally runs a dev-only #doCheck() comparing syntax lines with raw lines;
        // the C# port covers that equivalence through golden tests instead.

        HasDoSyntax = true;
    }
}
