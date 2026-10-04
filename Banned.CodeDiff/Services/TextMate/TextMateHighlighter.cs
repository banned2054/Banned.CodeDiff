using System.Text.RegularExpressions;
using Banned.CodeDiff.Models;
using Banned.CodeDiff.Utils;
using TextMateSharp.Grammars;

namespace Banned.CodeDiff.Services.TextMate;

/// <summary>
/// The C# port's built-in syntax engine (JS counterpart: the lowlight singleton
/// core programs against by default; here TextMateSharp/oniguruma with the same
/// bundled grammars and github-light/dark themes as the upstream shiki engine).
/// Like shiki it is a "class" engine: one tokenization produces wrappers whose
/// style carries BOTH theme colors as CSS variables
/// ("--diff-view-light:#...;--diff-view-dark:#..."), so theme switches never
/// re-tokenize.
/// </summary>
public sealed class TextMateHighlighter : IDiffHighlighter
{
    public static readonly TextMateHighlighter Instance = new();

    private int _maxLineToIgnoreSyntax = 2000;

    private readonly List<IgnorePattern> _ignoreSyntaxHighlightList = [];

    private TextMateHighlighter() { }

    public string Name => "textmate";

    public HighlighterType Type => HighlighterType.Class;

    public int MaxLineToIgnoreSyntax => _maxLineToIgnoreSyntax;

    public IReadOnlyList<IgnorePattern> IgnoreSyntaxHighlightList => _ignoreSyntaxHighlightList;

    public void SetMaxLineToIgnoreSyntax(int value)
    {
        _maxLineToIgnoreSyntax = value;
    }

    public void SetIgnoreSyntaxHighlightList(IReadOnlyList<IgnorePattern> items)
    {
        _ignoreSyntaxHighlightList.Clear();

        _ignoreSyntaxHighlightList.AddRange(items);
    }

    public bool HasRegisteredCurrentLang(string lang)
    {
        return TextMateResources.Instance.ResolveScope(lang) != null;
    }

    public SyntaxAstResult ProcessAst(SyntaxNode ast)
    {
        return HighlightAst.ProcessAst(ast);
    }

    public SyntaxNode? GetAst(string raw, string? fileName, string? lang, string? theme)
    {
        if (fileName != null && _ignoreSyntaxHighlightList.Any(item => item switch
            {
                RegexIgnorePattern regex  => regex.Regex.IsMatch(fileName),
                FileNameIgnorePattern name => fileName.Equals(name.FileName),
                _                          => false
            }))
        {
            return null;
        }

        var scopeName = TextMateResources.Instance.ResolveScope(lang);

        if (scopeName == null)
        {
            return null;
        }

        try
        {
            return Tokenize(raw, scopeName);
        }
        catch (Exception e)
        {
            Console.Error.WriteLine(e);
            throw;
        }
        // JS shiki getAST catches engine errors and returns undefined.
        // return null;
    }

    /// <summary>
    /// Tokenizes the full file from line 1 (rule stack carried across lines, so
    /// block comments / template literals spanning collapsed hunks keep their
    /// state — same "highlight the whole raw file" contract as upstream), and
    /// emits a flat hast-like tree: one wrapper element per token plus bare
    /// "\n" text nodes between lines. Adjacent tokens resolving to the same
    /// theme colors are merged into one span — vscode-textmate's binary
    /// tokenizer (what shiki consumes) coalesces equal-metadata tokens the
    /// same way (e.g. a "/*" begin capture and its comment body).
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

        // Per line: (token text, light color, dark color, standard token type)
        // with equal-appearance merging.
        var spans = new List<(string Value, string? Light, string? Dark, int TokenType)>();

        IStateStack? ruleStack = null;

        for (var i = 0; i < lines.Length; i++)
        {
            var line = lines[i];

            var result = grammar.TokenizeLine(line, ruleStack, TimeSpan.FromMilliseconds(5000));

            ruleStack = result.RuleStack;

            spans.Clear();

            foreach (var token in result.Tokens)
            {
                // LineText tokenizes with a trailing sentinel; clamp into the real line.
                var start = Math.Clamp(token.StartIndex, 0, line.Length);
                var end   = Math.Clamp(token.EndIndex, start, line.Length);

                var value = line.Substring(start, end - start);

                if (value.Length == 0)
                {
                    continue;
                }

                var light     = lightTheme.MatchForeground(token.Scopes);
                var dark      = darkTheme.MatchForeground(token.Scopes);
                var tokenType = StandardTokenType(token.Scopes);

                if (spans.Count > 0 && spans[^1].Light == light && spans[^1].Dark == dark &&
                    spans[^1].TokenType == tokenType)
                {
                    spans[^1] = (spans[^1].Value + value, light, dark, tokenType);
                }
                else
                {
                    spans.Add((value, light, dark, tokenType));
                }
            }

            foreach (var (value, light, dark, _) in spans)
            {
                children.Add(BuildWrapper(value, light, dark));
            }

            if (i < lines.Length - 1)
            {
                children.Add(new SyntaxNode { Type = "text", Value = "\n" });
            }
        }

        return root;
    }

    /// <summary>
    /// vscode-textmate's standard token type (from a scope name's segments);
    /// part of the encoded token metadata that decides whether the binary
    /// tokenizer coalesces adjacent tokens, alongside the resolved colors.
    /// </summary>
    private static int StandardTokenType(IList<string> scopes)
    {
        foreach (var scopeName in scopes)
        {
            var segments = scopeName.Split('.');

            for (var i = segments.Length - 1; i >= 0; i--)
            {
                if (segments[i] == "comment")
                {
                    return 1;
                }

                if (segments[i] == "string")
                {
                    return 2;
                }

                if (segments[i] == "regexp")
                {
                    return 3;
                }
            }
        }

        return 0;
    }

    private static SyntaxNode BuildWrapper(string value, string? light, string? dark)
    {
        // Same shape as shiki's codeToHast({ themes: { dark, light },
        // cssVariablePrefix: "--diff-view-" }) output: dark variable first,
        // no trailing semicolon; unmatched tokens keep the theme default
        // foreground, so a token span always carries both colors.
        var style = "";

        if (dark != null && light != null)
        {
            style = $"--diff-view-dark:{dark};--diff-view-light:{light}";
        }
        else if (dark != null)
        {
            style = $"--diff-view-dark:{dark}";
        }
        else if (light != null)
        {
            style = $"--diff-view-light:{light}";
        }

        return new SyntaxNode
        {
            Type       = "element",
            Properties = style.Length > 0 ? new SyntaxNodeProperties { Style = style } : null,
            Children   = [new SyntaxNode { Type = "text", Value = value }],
        };
    }
}
