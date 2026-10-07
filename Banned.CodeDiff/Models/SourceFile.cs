using Banned.CodeDiff.Services;

namespace Banned.CodeDiff.Models;

/// <summary>
///     单个源文件的原文与语法状态,移植自 packages/core/src/file.ts。语法仅在 <see cref="DoSyntax" /> 后可用。<br />
///     Raw text and syntax state ported from packages/core/src/file.ts; syntax is available only after <see cref="DoSyntax" />.
/// </summary>
public sealed class SourceFile(string row, string lang, string? fileName = null)
{
    private const int SyntaxResultCapacity = 8;

    /// <summary>
    ///     有界语法 LRU 缓存,按内容、语言、文件名、引擎及主题匹配;Class 引擎跨主题复用。全局状态,禁止并发使用。
    /// </summary>
    private static readonly LinkedList<SyntaxResultEntry> SyntaxResultOrder = [];

    private static readonly Dictionary<(string Raw, string Lang, string? FileName, string EngineName,
        HighlighterType EngineType, string? Theme), LinkedListNode<SyntaxResultEntry>> SyntaxResultMap = new();

    /// <summary>处理后的文件原文(已应用 transform 函数)。<br />The processed raw file content (transform function applied).</summary>
    public string Raw { get; } = Transform.ProcessTransformForFile(row);

    /// <summary>语言 id(如 "ts" / "cs")。<br />The language id (e.g. "ts" / "cs").</summary>
    public string Lang { get; } = lang;

    /// <summary>文件名,用于忽略规则匹配;可为 <c>null</c>。<br />The file name used for ignore-pattern matching; may be <c>null</c>.</summary>
    public string? FileName { get; } = fileName;

    /// <summary>
    ///     JS: rawFile —— 1 基行号 → 行内容(除最后一行外保留行尾 "\n")。<br />JS: rawFile — 1-based line number → line content (trailing
    ///     "\n" kept except on the last line).
    /// </summary>
    public Dictionary<int, string> RawFile { get; private set; } = new();

    /// <summary>是否已执行 <see cref="DoRaw" />。<br />Whether <see cref="DoRaw" /> has run.</summary>
    public bool HasDoRaw { get; private set; }

    /// <summary>
    ///     原文行数;执行 <see cref="DoRaw" /> 之前为 <c>null</c>。<br />The raw line count; <c>null</c> before <see cref="DoRaw" />
    ///     runs.
    /// </summary>
    public int? RawLength { get; private set; }

    /// <summary>最大行号(1 基)。<br />The largest line number (1-based).</summary>
    public int MaxLineNumber { get; private set; }

    // ---- syntax state (doSyntax) ----

    /// <summary>JS: ast —— 高亮器产出的语法树(hast Root 等价物)。<br />JS: ast — the highlighter-produced tree (hast Root equivalent).</summary>
    public SyntaxNode? Ast { get; private set; }

    /// <summary>
    ///     JS: theme("light" | "dark");记录用于 hasDoSyntax 的幂等检查。<br />JS: theme ("light" | "dark"); recorded for the
    ///     hasDoSyntax idempotence check.
    /// </summary>
    public string? Theme { get; private set; }

    /// <summary>JS: syntaxFile —— 1 基行号 → 该行的语法文本段。<br />JS: syntaxFile — 1-based line number → syntax spans of that line.</summary>
    public Dictionary<int, SyntaxLine>? SyntaxFile { get; private set; }

    /// <summary>
    ///     是否已执行 <see cref="DoSyntax" /> 并得到语法结果(跳过高亮时不置位)。<br />Whether <see cref="DoSyntax" /> ran and produced a
    ///     syntax result (not set when highlighting is skipped).
    /// </summary>
    public bool HasDoSyntax { get; private set; }

    /// <summary>
    ///     语法处理后的总行数(processAST 的 lineNumber);尚未执行时为 <c>null</c>。<br />Total line count after syntax processing
    ///     (processAST's lineNumber); <c>null</c> before it runs.
    /// </summary>
    public int? SyntaxLength { get; private set; }

    /// <summary>实际使用的高亮器引擎名。<br />Name of the highlighter engine actually used.</summary>
    public string? HighlighterName { get; private set; }

    /// <summary>
    ///     实际使用的引擎类型(<see cref="Models.HighlighterType" />);尚未执行时为 <c>null</c>。<br />Type of the engine actually used (
    ///     <see cref="Models.HighlighterType" />); <c>null</c> before it runs.
    /// </summary>
    public HighlighterType? HighlighterType { get; private set; }

    /// <summary>
    ///     清空语法缓存,使后续高亮使用更新后的全局配置。<br />
    ///     Clears syntax caches so subsequent highlighting uses updated global settings.
    /// </summary>
    public static void ClearFileCache()
    {
        SyntaxResultOrder.Clear();
        SyntaxResultMap.Clear();
    }

    /// <summary>
    ///     The theme component of the cache key: <c>null</c> for class engines
    ///     (theme-independent ASTs), the requested theme otherwise.
    /// </summary>
    private static string? ThemeKey(IDiffHighlighter engine, string? theme)
    {
        return engine.Type == Models.HighlighterType.Class ? null : theme;
    }

    private static SyntaxResultEntry? TryGetSyntaxResult(
        string raw, string lang, string? fileName, IDiffHighlighter engine, string? theme)
    {
        var key = (raw, lang, fileName, engine.Name, engine.Type, ThemeKey(engine, theme));

        if (!SyntaxResultMap.TryGetValue(key, out var node)) return null;

        SyntaxResultOrder.Remove(node);
        SyntaxResultOrder.AddFirst(node);

        return node.Value;
    }

    private static void CacheSyntaxResult(string  raw,   string     lang, string? fileName, IDiffHighlighter engine,
                                          string? theme, SyntaxNode ast,  Dictionary<int, SyntaxLine> syntaxFile,
                                          int     syntaxLength)
    {
        var entry = new SyntaxResultEntry(
                                          (raw, lang, fileName, engine.Name, engine.Type, ThemeKey(engine, theme)), ast,
                                          syntaxFile, syntaxLength);

        var node = SyntaxResultOrder.AddFirst(entry);

        SyntaxResultMap[entry.Key] = node;

        if (SyntaxResultMap.Count > SyntaxResultCapacity)
        {
            var last = SyntaxResultOrder.Last!;

            SyntaxResultOrder.RemoveLast();
            SyntaxResultMap.Remove(last.Value.Key);
        }
    }

    /// <summary>
    ///     按 "\n" 拆分原文,填充 <see cref="RawFile" />、<see cref="RawLength" /> 与 <see cref="MaxLineNumber" />;内容为空或已执行时为空操作。
    ///     <br />Splits the raw content by "\n" to fill <see cref="RawFile" />, <see cref="RawLength" /> and
    ///     <see cref="MaxLineNumber" />; no-op for empty content or when already run.
    /// </summary>
    public void DoRaw()
    {
        if (Raw.Length == 0 || HasDoRaw) return;

        var rawString = Raw;

        var rawArray = rawString.Split('\n');

        RawLength = rawArray.Length;

        MaxLineNumber = rawArray.Length;

        RawFile = new Dictionary<int, string>(rawArray.Length);

        for (var i = 0; i < rawArray.Length; i++)
            RawFile[i + 1] = i < rawArray.Length - 1 ? rawArray[i] + "\n" : rawArray[i];

        HasDoRaw = true;
    }

    /// <summary>
    ///     使用指定高亮器,不支持语言时回退内置引擎;两者均不支持时不高亮。<br />
    ///     Uses the supplied highlighter, falling back to the built-in engine for unsupported languages; otherwise leaves text unhighlighted.
    /// </summary>
    /// <param name="registerHighlighter">
    ///     注入的高亮器;<c>null</c> 时使用内置默认。<br />The highlighter to register; <c>null</c> uses the
    ///     built-in default.
    /// </param>
    /// <param name="theme">主题名("light" / "dark")。<br />The theme name ("light" / "dark").</param>
    public void DoSyntax(IDiffHighlighter? registerHighlighter = null, string? theme = null)
    {
        if (Raw.Length == 0) return;

        var finalHighlighter = registerHighlighter ?? DiffHighlighters.Default;

        if (RawLength is > 0 && RawLength > finalHighlighter.MaxLineToIgnoreSyntax)
            return;

        var supportEngine = finalHighlighter;

        try
        {
            if (!finalHighlighter.HasRegisteredCurrentLang(Lang)) supportEngine = DiffHighlighters.Default;
        }
        catch
        {
            supportEngine = DiffHighlighters.Default;
        }

        if (HasDoSyntax                           &&
            supportEngine.Name == HighlighterName &&
            supportEngine.Type == HighlighterType &&
            (Theme == theme || supportEngine.Type == Models.HighlighterType.Class))
            return;

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

        if (Ast == null) return;

        var result = supportEngine.ProcessAst(Ast);


        SyntaxFile = result.SyntaxFileObject;

        SyntaxLength = result.SyntaxFileLineNumber;

        HighlighterName = supportEngine.Name;

        HighlighterType = supportEngine.Type;

        CacheSyntaxResult(Raw, Lang, FileName, supportEngine, theme, Ast, SyntaxFile,
                          result.SyntaxFileLineNumber);


        HasDoSyntax = true;
    }

    /// <summary>The cache key of one memoized doSyntax outcome, plus the outcome itself.</summary>
    private sealed record SyntaxResultEntry(
        (string Raw, string Lang, string? FileName, string EngineName, HighlighterType EngineType, string? Theme) Key,
        SyntaxNode Ast,
        Dictionary<int, SyntaxLine> SyntaxFile,
        int SyntaxLength);
}
