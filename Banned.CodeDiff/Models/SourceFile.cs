using Banned.CodeDiff.Services;

namespace Banned.CodeDiff.Models;

/// <summary>
/// Port of packages/core/src/file.ts — raw + syntax state of one source file.
/// The cross-instance file cache (Cache/`getFile`) is a web-specific perf
/// optimization and is intentionally not ported; every source file is fresh.
/// </summary>
public sealed class SourceFile(string row, string lang, string? fileName = null)
{
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

        // JS additionally runs a dev-only #doCheck() comparing syntax lines with raw lines;
        // the C# port covers that equivalence through golden tests instead.

        HasDoSyntax = true;
    }
}
