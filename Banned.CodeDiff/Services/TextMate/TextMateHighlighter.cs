using Banned.CodeDiff.Models;
using Banned.CodeDiff.Utils;
using System.Text;
using TextMateSharp.Grammars;

namespace Banned.CodeDiff.Services.TextMate;

/// <summary>
///     基于 TextMateSharp 的内置高亮器;一次分词保留明暗配色与 scope,切换主题无需重新分词。<br />
///     Built-in TextMateSharp highlighter; tokenization retains light/dark colors and scopes so theme changes need no retokenization.
/// </summary>
/// <remarks>
///     需对应平台的 oniguruma 原生运行时;分词使用进程级共享 Registry。<br />
///     Requires the platform's oniguruma native runtime; tokenization uses a process-wide shared Registry.
/// </remarks>
public sealed class TextMateHighlighter : IDiffHighlighter
{
    /// <summary>进程级共享的单例。<br />The process-wide shared singleton.</summary>
    public static readonly TextMateHighlighter Instance = new();

    /// <summary>
    ///     Style strings shared by every wrapper of the same (light, dark) color pair — the themes
    ///     resolve to a small fixed palette, so each distinct pair is built once per process.
    ///     Strings are immutable, so sharing is safe; the mutable
    ///     <see cref="SyntaxNodeProperties" /> wrapper stays per-node.
    /// </summary>
    private static readonly Dictionary<(string? Light, string? Dark), string> StylePalette = new();

    private readonly List<IgnorePattern> _ignoreSyntaxHighlightList = [];

    private TextMateHighlighter()
    {
    }

    /// <summary>引擎标识,固定为 "textmate"。<br />Engine id, always "textmate".</summary>
    public string Name => "textmate";

    /// <summary>
    ///     固定为 <see cref="HighlighterType.Class" />("class" 型引擎)。<br />Always <see cref="HighlighterType.Class" /> (a
    ///     "class" engine).
    /// </summary>
    public HighlighterType Type => HighlighterType.Class;

    /// <summary>
    ///     原始行数超过该值的文件跳过语法高亮,默认 2000。<br />Files longer than this many raw lines skip syntax highlighting; defaults to
    ///     2000.
    /// </summary>
    public int MaxLineToIgnoreSyntax { get; private set; } = 2000;

    /// <summary>
    ///     按文件名匹配的语法高亮忽略规则(JS:<c>(string | RegExp)[]</c>)。<br />Ignore patterns matched against the file name (JS:
    ///     <c>(string | RegExp)[]</c>).
    /// </summary>
    public IReadOnlyList<IgnorePattern> IgnoreSyntaxHighlightList => _ignoreSyntaxHighlightList;

    /// <summary>
    ///     语言 id/别名是否已注册(能解析到 TextMate scope)。<br />Whether the language id/alias is registered (resolves to a TextMate
    ///     scope).
    /// </summary>
    /// <param name="lang">语言 id 或别名,如 "cs"。The language id or alias, e.g. "cs".</param>
    public bool HasRegisteredCurrentLang(string lang)
    {
        return TextMateResources.Instance.ResolveScope(lang) != null;
    }

    /// <summary>
    ///     把高亮产出的 AST 切分为逐行文本段,委托 <see cref="HighlightAst.ProcessAst" />。<br />Splits the highlighter-produced AST into
    ///     per-line spans, delegating to <see cref="HighlightAst.ProcessAst" />.
    /// </summary>
    /// <param name="ast">高亮器产出的根节点。The root node produced by the highlighter.</param>
    /// <returns>行号从 1 起始的逐行文本段集合与总行数。The per-line span records keyed from line 1, plus the total line count.</returns>
    public SyntaxAstResult ProcessAst(SyntaxNode ast)
    {
        return HighlightAst.ProcessAst(ast);
    }

    /// <summary>
    ///     将完整文件分词为语法树并保留原始 scope;忽略的文件或未知语言返回 <c>null</c>。<br />
    ///     Tokenizes a full file into a syntax tree retaining original scopes; ignored files and unknown languages return null.
    /// </summary>
    /// <param name="raw">完整原始文件文本。The full raw file text.</param>
    /// <param name="fileName">文件名,用于匹配忽略规则,可为 <c>null</c>。File name matched against the ignore patterns; may be <c>null</c>.</param>
    /// <param name="lang">语言 id 或别名,如 "cs"。The language id or alias, e.g. "cs".</param>
    /// <param name="theme">未使用——每个 token 的样式同时携带明暗两套主题颜色。Unused; each token's style carries both light and dark theme colors.</param>
    /// <returns>root 语法节点;需跳过高亮时为 <c>null</c>。The root syntax node, or <c>null</c> when highlighting must be skipped.</returns>
    /// <remarks>
    ///     从首行分词并跨行保留状态;分词异常向调用者传播。<br />
    ///     Tokenizes from the first line with state carried across lines; tokenizer exceptions propagate.
    /// </remarks>
    public SyntaxNode? GetAst(string raw, string? fileName, string? lang, string? theme)
    {
        if (fileName != null && _ignoreSyntaxHighlightList.Any(item => item switch
            {
                RegexIgnorePattern regex   => regex.Regex.IsMatch(fileName),
                FileNameIgnorePattern name => fileName.Equals(name.FileName),
                _                          => false
            }))
            return null;

        var scopeName = TextMateResources.Instance.ResolveScope(lang);

        if (scopeName == null) return null;

        try
        {
            return Tokenize(raw, scopeName);
        }
        catch (Exception e)
        {
            Console.Error.WriteLine(e);
            throw;
        }
        // 分词异常向外传播,与上游捕获错误并返回 undefined 的行为不同。
    }

    /// <summary>
    ///     设置跳过语法高亮的行数阈值,并清空源文件缓存使新阈值立即生效。<br />Sets the line-count threshold for skipping syntax highlighting and clears
    ///     the source file cache so the new value takes effect at once.
    /// </summary>
    /// <param name="value">新的行数阈值。The new threshold.</param>
    public void SetMaxLineToIgnoreSyntax(int value)
    {
        MaxLineToIgnoreSyntax = value;

        // 阈值变化后须清空缓存,避免复用原先允许或跳过的结果。
        SourceFile.ClearFileCache();
    }

    /// <summary>
    ///     整体替换文件名忽略规则列表,并清空源文件缓存使其立即生效。<br />Replaces the file-name ignore pattern list and clears the source file cache
    ///     so it takes effect at once.
    /// </summary>
    /// <param name="items">新的忽略规则集合。The new ignore patterns.</param>
    public void SetIgnoreSyntaxHighlightList(IReadOnlyList<IgnorePattern> items)
    {
        _ignoreSyntaxHighlightList.Clear();

        _ignoreSyntaxHighlightList.AddRange(items);

        SourceFile.ClearFileCache();
    }

    /// <summary>
    ///     从首行分词并跨行保留规则状态;相邻同色 token 合并为包装节点。
    /// </summary>
    private static SyntaxNode Tokenize(string raw, string scopeName)
    {
        var resources = TextMateResources.Instance;

        var grammar = resources.GetRegistry().LoadGrammar(scopeName);

        var (lightTheme, darkTheme) = resources.GetThemes();

        var root = new SyntaxNode { Type = "root" };

        var children = new List<SyntaxNode>();

        root.Children = children;

        var lines = raw.Split('\n');

        // 同色 token 合并范围,保留原始 token 的相对区间与 scope,供各视图重新配色。
        var spans = new List<(int Start, int Length, string? Light, string? Dark, int TokenType,
            List<(int Start, int Length, string[] Scopes)> Tokens)>();

        var lineBuilder = new StringBuilder();

        IStateStack? ruleStack = null;

        for (var i = 0; i < lines.Length; i++)
        {
            var line = lines[i];

            var result = grammar.TokenizeLine(line, ruleStack, TimeSpan.FromMilliseconds(5000));

            ruleStack = result.RuleStack;

            spans.Clear();
            lineBuilder.Clear();

            foreach (var token in result.Tokens)
            {
                // 分词含行尾哨兵,须钳制到实际文本。
                var start = Math.Clamp(token.StartIndex, 0, line.Length);
                var end   = Math.Clamp(token.EndIndex, start, line.Length);

                var length = end - start;

                if (length == 0) continue;

                var light     = lightTheme.MatchForeground(token.Scopes);
                var dark      = darkTheme.MatchForeground(token.Scopes);
                var tokenType = StandardTokenType(token.Scopes);

                // 复制 scope 栈以免分词器复用存储;最内层在前。
                var scopes = token.Scopes is { Count: > 0 } list ? list.ToArray() : [];

                if (spans.Count         > 0      &&
                    spans[^1].Light     == light &&
                    spans[^1].Dark      == dark  &&
                    spans[^1].TokenType == tokenType)
                {
                    var last = spans[^1];

                    last.Tokens.Add((lineBuilder.Length - last.Start, length, scopes));

                    spans[^1] = (last.Start, last.Length + length, light, dark, tokenType, last.Tokens);
                }
                else
                {
                    spans.Add((lineBuilder.Length, length, light, dark, tokenType,
                               [(0, length, scopes)]));
                }

                lineBuilder.Append(line, start, length);
            }

            foreach (var span in spans)
                children.Add(BuildWrapper(lineBuilder.ToString(span.Start, span.Length), span.Light,
                                          span.Dark, span.Tokens));

            if (i < lines.Length - 1) children.Add(new SyntaxNode { Type = "text", Value = "\n" });
        }

        return root;
    }

    /// <summary>
    ///     vscode-textmate's standard token type (from a scope name's segments);
    ///     part of the encoded token metadata that decides whether the binary
    ///     tokenizer coalesces adjacent tokens, alongside the resolved colors.
    /// </summary>
    private static int StandardTokenType(IList<string> scopes)
    {
        foreach (var scopeName in scopes)
        {
            var segmentEnd = scopeName.Length;

            for (var i = scopeName.Length - 1; i >= -1; i--)
            {
                if (i >= 0 && scopeName[i] != '.') continue;

                var segmentLength = segmentEnd - i - 1;

                if (segmentLength > 0)
                {
                    var segment = scopeName.AsSpan(i + 1, segmentLength);

                    switch (segment)
                    {
                        case "comment" :
                            return 1;
                        case "string" :
                            return 2;
                        case "regexp" :
                            return 3;
                    }
                }

                segmentEnd = i;
            }
        }

        return 0;
    }

    private static SyntaxNode BuildWrapper(string value, string? light, string? dark,
                                           List<(int Start, int Length, string[] Scopes)> tokens)
    {
        // 保留 shiki 输出格式:深色变量在前,无末尾分号,未匹配 token 使用主题默认色。
        var style = GetStyle(light, dark);

        return new SyntaxNode
        {
            Type = "element",
            Properties = new SyntaxNodeProperties
            {
                Style  = style.Length > 0 ? style : null,
                Tokens = [.. tokens.Select(t => new SyntaxTokenSpan(t.Start, t.Length, t.Scopes))]
            },
            Children = [new SyntaxNode { Type = "text", Value = value }]
        };
    }

    /// <summary>
    ///     The style string for a color pair, memoized in <see cref="StylePalette" />
    ///     (bounded by the themes' color palette).
    /// </summary>
    private static string GetStyle(string? light, string? dark)
    {
        var key = (light, dark);

        if (StylePalette.TryGetValue(key, out var style)) return style;

        if (dark != null && light != null)
            style = $"--diff-view-dark:{dark};--diff-view-light:{light}";
        else if (dark != null)
            style = $"--diff-view-dark:{dark}";
        else if (light != null)
            style = $"--diff-view-light:{light}";
        else
            style = "";

        StylePalette[key] = style;

        return style;
    }
}
