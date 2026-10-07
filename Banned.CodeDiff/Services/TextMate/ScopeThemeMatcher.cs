using System.Text.Json;

namespace Banned.CodeDiff.Services.TextMate;

/// <summary>
///     vscode-textmate 前景色匹配器的移植(@shikijs/vscode-textmate, MIT),用于解析主题与 scope 覆盖。<br />
///     Foreground matcher ported from @shikijs/vscode-textmate (MIT), used for themes and scope overrides.
/// </summary>
// 保留 vscode-textmate 的 scope 栈匹配语义,避免 TextMateSharp 后代选择器的行为差异。
public sealed class ScopeThemeMatcher
{
    /// <summary>
    ///     未命中任何规则时使用的主题默认前景色。<br />
    ///     Theme default foreground used when no rule matches.
    /// </summary>
    private readonly string? _defaultForeground;

    private readonly ThemeTrieElement _root;

    private ScopeThemeMatcher(ThemeTrieElement root, string? defaultForeground)
    {
        _root              = root;
        _defaultForeground = defaultForeground;
    }

    /// <summary>
    ///     从主题 JSON 构建 scope→前景色匹配器,走 vscode-textmate 的 parseTheme /
    ///     resolveParsedThemeRules 流水线(规则排序、空 scope 规则提升为主题默认值、
    ///     十六进制色统一大写);缺失 tokenColors 或无有效规则时返回不带规则的空匹配器。<br />
    ///     Builds a scope-to-foreground matcher from theme JSON, running the vscode-textmate
    ///     parseTheme / resolveParsedThemeRules pipeline (rule sorting, empty-scope rules
    ///     lifted into the theme defaults, upper-cased hex colors); returns a rule-less empty
    ///     matcher when tokenColors is missing or no valid rule exists.
    /// </summary>
    /// <param name="themeJson">主题 JSON 文本(UTF-8)。The theme JSON document (UTF-8).</param>
    /// <returns>解析完成的匹配器。The parsed matcher.</returns>
    public static ScopeThemeMatcher FromThemeJson(byte[] themeJson)
    {
        using var doc = JsonDocument.Parse(themeJson);

        // vscode-textmate parseTheme reads rawTheme.settings; the bundled
        // @shikijs/themes files (and shiki itself) use the equivalent tokenColors.
        if (!doc.RootElement.TryGetProperty("tokenColors", out var tokenColors) ||
            tokenColors.ValueKind != JsonValueKind.Array)
            return new ScopeThemeMatcher(new ThemeTrieElement(null, []), null);

        var parsed = new List<ParsedThemeRule>();

        var index = 0;

        foreach (var entry in tokenColors.EnumerateArray())
        {
            index++;

            if (!entry.TryGetProperty("settings", out var settings) ||
                settings.ValueKind != JsonValueKind.Object)
                continue;

            List<string> selectors;

            if (entry.TryGetProperty("scope", out var scopeElement))
                switch (scopeElement.ValueKind)
                {
                    case JsonValueKind.String :
                    {
                        var scope = scopeElement.GetString()!.Trim(',', ' ');

                        selectors = scope.Length == 0 ? [""] : [.. scope.Split(',')];
                        break;
                    }
                    case JsonValueKind.Array :
                        selectors = [.. scopeElement.EnumerateArray().Select(s => s.GetString() ?? "")];
                        break;
                    default :
                        selectors = [""];
                        break;
                }
            else
                selectors = [""];

            string? foreground = null;

            if (settings.TryGetProperty("foreground", out var foregroundElement) &&
                foregroundElement.ValueKind == JsonValueKind.String)
            {
                var value = foregroundElement.GetString()!;

                if (IsValidHexColor(value))
                    // ColorMap.getId upper-cases colors; shiki's output uses the
                    // upper-cased form.
                    foreground = value.ToUpperInvariant();
            }

            foreach (var selector in selectors)
            {
                var segments = selector.Trim().Split(' ');

                var scope = segments[^1];

                string[]? parentScopes = null;

                if (segments.Length > 1)
                {
                    parentScopes = segments[..^1];

                    Array.Reverse(parentScopes);
                }

                parsed.Add(new ParsedThemeRule(scope, parentScopes, index, foreground));
            }
        }

        parsed.Sort((a, b) =>
        {
            var r = StrCmp(a.Scope, b.Scope);

            if (r != 0) return r;

            r = StrArrCmp(a.ParentScopes, b.ParentScopes);

            return r != 0 ? r : a.Index.CompareTo(b.Index);
        });

        // JS: resolveParsedThemeRules shifts the empty-scope rules off the sorted
        // list into the theme defaults (later entries overwrite earlier ones).
        string? defaultForeground = null;

        while (parsed is [{ Scope.Length: 0 }, ..])
        {
            var incoming = parsed[0];

            parsed.RemoveAt(0);

            if (incoming.Foreground != null) defaultForeground = incoming.Foreground;
        }

        // shiki additionally falls back to the theme's editor.foreground for
        // tokens whose scopes match nothing (the github themes only define the
        // default there).
        if (doc.RootElement.TryGetProperty("colors", out var colors)             &&
            colors.ValueKind == JsonValueKind.Object                             &&
            colors.TryGetProperty("editor.foreground", out var editorForeground) &&
            editorForeground.ValueKind == JsonValueKind.String)
        {
            var value = editorForeground.GetString()!;

            if (IsValidHexColor(value)) defaultForeground = value.ToUpperInvariant();
        }

        var root = new ThemeTrieElement(null, []);

        foreach (var rule in parsed) root.Insert(0, rule.Scope, rule.ParentScopes, rule.Foreground);

        return new ScopeThemeMatcher(root, defaultForeground);
    }

    /// <summary>
    ///     按 vscode-textmate 编码元数据的方式解析 token 作用域栈的前景色:作用域从最内层
    ///     向外逐层匹配,命中主题规则的最深层生效——未命中的层继承外层作用域的颜色
    ///     (例如 JSON key 的引号标点会因此渲染成 key 的颜色)。全部未命中时回退到主题默认
    ///     前景色(shiki 的 colorMap[0])。<br />
    ///     Resolves the foreground color for a token scope stack the way
    ///     vscode-textmate's encoded metadata does: scopes are matched
    ///     innermost-first and the deepest layer with a theme hit wins — a layer
    ///     with no hit inherits the enclosing scope's color (that is why e.g. a
    ///     JSON key's quote punctuation renders in the key color). Falls back to
    ///     the theme default foreground (shiki's colorMap[0]).
    /// </summary>
    /// <param name="scopeStack">从最内到最外排列的作用域栈。The scope stack, ordered innermost first.</param>
    /// <returns>
    ///     大写十六进制前景色;无命中时为主题默认前景色,可能为 <c>null</c>。<br />The upper-cased hex foreground color, or the theme default when
    ///     nothing matches; may be <c>null</c>.
    /// </returns>
    public string? MatchForeground(IList<string> scopeStack)
    {
        var ruleColor = MatchRuleForeground(scopeStack);

        return ruleColor ?? _defaultForeground;
    }

    /// <summary>
    ///     匹配 scope 前景色,不使用主题默认色;未命中显式规则时返回 <c>null</c>。<br />
    ///     Matches scope foregrounds without the theme default; returns null when no explicit rule matches.
    /// </summary>
    /// <param name="scopeStack">从最内到最外排列的作用域栈。The scope stack, ordered innermost first.</param>
    public string? MatchRuleForeground(IList<string> scopeStack)
    {
        for (var i = scopeStack.Count - 1; i >= 0; i--)
        {
            var rules = _root.Match(scopeStack[i]);

            // 逐层匹配无父 scope 上下文,仅主规则可用;无前景色时继承外层。
            foreach (var rule in rules)
                if (rule.ParentScopes is not { Length: > 0 } && rule.Foreground != null)
                    return rule.Foreground;
        }

        return null;
    }

    // 父 scope 按内到外匹配。

    private static bool ScopePathMatchesParentScopes(IList<string> stack, int from, string[]? parentScopes)
    {
        if (parentScopes == null || parentScopes.Length == 0) return true;

        var position = from;

        for (var index = 0; index < parentScopes.Length; index++)
        {
            var pattern = parentScopes[index];

            var scopeMustMatch = false;

            if (pattern == ">")
            {
                if (index == parentScopes.Length - 1) return false;

                pattern = parentScopes[++index];

                scopeMustMatch = true;
            }

            while (position >= 0)
            {
                if (MatchesScope(stack[position], pattern)) break;

                if (scopeMustMatch) return false;

                position--;
            }

            if (position < 0) return false;

            position--;
        }

        return true;
    }

    private static bool MatchesScope(string scopeName, string scopePattern)
    {
        return scopePattern == scopeName ||
               (scopeName.StartsWith(scopePattern, StringComparison.Ordinal) &&
                scopeName[scopePattern.Length] == '.');
    }

    private static int StrCmp(string a, string b)
    {
        return string.CompareOrdinal(a, b);
    }

    private static int StrArrCmp(string[]? a, string[]? b)
    {
        switch (a)
        {
            case null when b == null :
                return 0;
            case null :
                return -1;
        }

        if (b == null) return 1;

        if (a.Length == b.Length) return a.Select((t, i) => StrCmp(t, b[i])).FirstOrDefault(r => r != 0);

        return a.Length < b.Length ? -1 : 1;
    }

    private static bool IsValidHexColor(string hex)
    {
        return hex.Length is 4 or 5 or 7 or 9 && hex[0] == '#' && hex[1..].All(Uri.IsHexDigit);
    }

    private sealed record ParsedThemeRule(string Scope, string[]? ParentScopes, int Index, string? Foreground);

    private sealed class Rule(int scopeDepth, string[]? parentScopes, string? foreground)
    {
        public int       ScopeDepth   { get; set; } = scopeDepth;
        public string[]? ParentScopes { get; }      = parentScopes;
        public string?   Foreground   { get; set; } = foreground;

        public void AcceptOverwrite(int scopeDepth, string? foreground)
        {
            if (ScopeDepth <= scopeDepth) ScopeDepth = scopeDepth;

            if (foreground != null) Foreground = foreground;
        }

        public Rule Clone()
        {
            return new Rule(ScopeDepth, ParentScopes, Foreground);
        }
    }

    private sealed class ThemeTrieElement(Rule? mainRule, List<Rule> rulesWithParentScopes)
    {
        private readonly Dictionary<string, ThemeTrieElement> _children = new();

        private readonly Rule _mainRule = mainRule ?? new Rule(0, null, null);

        public List<Rule> Match(string scope)
        {
            if (scope.Length != 0)
            {
                var dotIndex = scope.IndexOf('.');

                var head = dotIndex == -1 ? scope : scope[..dotIndex];

                var tail = dotIndex == -1 ? "" : scope[(dotIndex + 1)..];

                if (_children.TryGetValue(head, out var child)) return child.Match(tail);
            }

            var rules = new List<Rule>(rulesWithParentScopes) { _mainRule };

            rules.Sort(CompareBySpecificity);

            return rules;
        }

        public void Insert(int scopeDepth, string scope, string[]? parentScopes, string? foreground)
        {
            if (scope.Length == 0)
            {
                DoInsertHere(scopeDepth, parentScopes, foreground);

                return;
            }

            var dotIndex = scope.IndexOf('.');

            var head = dotIndex == -1 ? scope : scope[..dotIndex];

            var tail = dotIndex == -1 ? "" : scope[(dotIndex + 1)..];

            if (!_children.TryGetValue(head, out var child))
            {
                child = new ThemeTrieElement(_mainRule.Clone(), [.. rulesWithParentScopes.Select(r => r.Clone())]);

                _children[head] = child;
            }

            child.Insert(scopeDepth + 1, tail, parentScopes, foreground);
        }

        private void DoInsertHere(int scopeDepth, string[]? parentScopes, string? foreground)
        {
            if (parentScopes == null)
            {
                _mainRule.AcceptOverwrite(scopeDepth, foreground);

                return;
            }

            foreach (var rule in rulesWithParentScopes.Where(rule => StrArrCmp(rule.ParentScopes, parentScopes) == 0))
            {
                rule.AcceptOverwrite(scopeDepth, foreground);

                return;
            }

            rulesWithParentScopes.Add(new Rule(scopeDepth, parentScopes, foreground ?? _mainRule.Foreground));
        }

        private static int CompareBySpecificity(Rule a, Rule b)
        {
            if (a.ScopeDepth != b.ScopeDepth) return b.ScopeDepth - a.ScopeDepth;

            var aParents = a.ParentScopes ?? [];
            var bParents = b.ParentScopes ?? [];

            var aIndex = 0;
            var bIndex = 0;

            while (true)
            {
                if (aIndex < aParents.Length && aParents[aIndex] == ">") aIndex++;

                if (bIndex < bParents.Length && bParents[bIndex] == ">") bIndex++;

                if (aIndex >= aParents.Length || bIndex >= bParents.Length) break;

                var lengthDiff = bParents[bIndex].Length - aParents[aIndex].Length;

                if (lengthDiff != 0) return lengthDiff;

                aIndex++;
                bIndex++;
            }

            return bParents.Length - aParents.Length;
        }
    }
}
