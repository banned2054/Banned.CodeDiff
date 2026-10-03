using System.Text.Json;

namespace Banned.CodeDiff.Services.TextMate;

/// <summary>
/// Foreground-only port of the vscode-textmate theme matching
/// (parseTheme / resolveParsedThemeRules / ThemeTrieElement / Theme.match —
/// source: @shikijs/vscode-textmate, the engine shiki uses, MIT).
/// TextMateSharp's own Theme.Match mis-resolves descendant selectors on scope
/// stacks (e.g. "source.cs" matching "string … embedded source"), so the C#
/// port carries this straight port instead; golden tests compare against
/// shiki's actual output.
/// </summary>
internal sealed class ScopeThemeMatcher
{
    private readonly ThemeTrieElement _root;

    /// <summary>
    /// JS: Theme.getDefaults().foreground — rules with an empty scope become the
    /// theme defaults (github themes carry the default foreground there). shiki
    /// falls back to it for tokens whose scopes match nothing.
    /// </summary>
    private readonly string? _defaultForeground;

    private ScopeThemeMatcher(ThemeTrieElement root, string? defaultForeground)
    {
        _root              = root;
        _defaultForeground = defaultForeground;
    }

    public static ScopeThemeMatcher FromThemeJson(byte[] themeJson)
    {
        using var doc = JsonDocument.Parse(themeJson);

        // vscode-textmate parseTheme reads rawTheme.settings; the bundled
        // @shikijs/themes files (and shiki itself) use the equivalent tokenColors.
        if (!doc.RootElement.TryGetProperty("tokenColors", out var tokenColors) ||
            tokenColors.ValueKind != JsonValueKind.Array)
        {
            return new ScopeThemeMatcher(new ThemeTrieElement(null, []), null);
        }

        var parsed = new List<ParsedThemeRule>();

        var index = 0;

        foreach (var entry in tokenColors.EnumerateArray())
        {
            index++;

            if (!entry.TryGetProperty("settings", out var settings) ||
                settings.ValueKind != JsonValueKind.Object)
            {
                continue;
            }

            List<string> selectors;

            if (entry.TryGetProperty("scope", out var scopeElement))
            {
                if (scopeElement.ValueKind == JsonValueKind.String)
                {
                    var scope = scopeElement.GetString()!.Trim(',', ' ');

                    selectors = scope.Length == 0 ? [""] : [.. scope.Split(',')];
                }
                else if (scopeElement.ValueKind == JsonValueKind.Array)
                {
                    selectors = [.. scopeElement.EnumerateArray().Select(s => s.GetString() ?? "")];
                }
                else
                {
                    selectors = [""];
                }
            }
            else
            {
                selectors = [""];
            }

            string? foreground = null;

            if (settings.TryGetProperty("foreground", out var foregroundElement) &&
                foregroundElement.ValueKind == JsonValueKind.String)
            {
                var value = foregroundElement.GetString()!;

                if (IsValidHexColor(value))
                {
                    // ColorMap.getId upper-cases colors; shiki's output uses the
                    // upper-cased form.
                    foreground = value.ToUpperInvariant();
                }
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

            if (r != 0)
            {
                return r;
            }

            r = StrArrCmp(a.ParentScopes, b.ParentScopes);

            if (r != 0)
            {
                return r;
            }

            return a.Index.CompareTo(b.Index);
        });

        // JS: resolveParsedThemeRules shifts the empty-scope rules off the sorted
        // list into the theme defaults (later entries overwrite earlier ones).
        var defaultForeground = (string?)null;

        while (parsed.Count >= 1 && parsed[0].Scope.Length == 0)
        {
            var incoming = parsed[0];

            parsed.RemoveAt(0);

            if (incoming.Foreground != null)
            {
                defaultForeground = incoming.Foreground;
            }
        }

        // shiki additionally falls back to the theme's editor.foreground for
        // tokens whose scopes match nothing (the github themes only define the
        // default there).
        if (doc.RootElement.TryGetProperty("colors", out var colors) &&
            colors.ValueKind == JsonValueKind.Object &&
            colors.TryGetProperty("editor.foreground", out var editorForeground) &&
            editorForeground.ValueKind == JsonValueKind.String)
        {
            var value = editorForeground.GetString()!;

            if (IsValidHexColor(value))
            {
                defaultForeground = value.ToUpperInvariant();
            }
        }

        var root = new ThemeTrieElement(null, []);

        foreach (var rule in parsed)
        {
            root.Insert(0, rule.Scope, rule.ParentScopes, rule.Foreground);
        }

        return new ScopeThemeMatcher(root, defaultForeground);
    }

    /// <summary>
    /// Resolves the foreground color for a token scope stack the way
    /// vscode-textmate's encoded metadata does: scopes are matched
    /// innermost-first and the deepest layer with a theme hit wins — a layer
    /// with no hit inherits the enclosing scope's color (that is why e.g. a
    /// JSON key's quote punctuation renders in the key color). Falls back to
    /// the theme default foreground (shiki's colorMap[0]).
    /// </summary>
    public string? MatchForeground(IList<string> scopeStack)
    {
        for (var i = scopeStack.Count - 1; i >= 0; i--)
        {
            var rules = _root.Match(scopeStack[i]);

            // The metadata provider resolves one scope name at a time with no
            // stack context, so descendant-selector rules cannot apply — only
            // the trie node's main rule (no parent scopes) counts. A main rule
            // with no foreground keeps the enclosing scope's color.
            foreach (var rule in rules)
            {
                if (rule.ParentScopes is not { Length: > 0 } && rule.Foreground != null)
                {
                    return rule.Foreground;
                }
            }
        }

        return _defaultForeground;
    }

    // ---- _scopePathMatchesParentScopes: scopePath walks from the token's
    // parent scope towards the root; parentScopes are ordered inner → outer.

    private static bool ScopePathMatchesParentScopes(IList<string> stack, int from, string[]? parentScopes)
    {
        if (parentScopes == null || parentScopes.Length == 0)
        {
            return true;
        }

        var position = from;

        for (var index = 0; index < parentScopes.Length; index++)
        {
            var pattern = parentScopes[index];

            var scopeMustMatch = false;

            if (pattern == ">")
            {
                if (index == parentScopes.Length - 1)
                {
                    return false;
                }

                pattern = parentScopes[++index];

                scopeMustMatch = true;
            }

            while (position >= 0)
            {
                if (MatchesScope(stack[position], pattern))
                {
                    break;
                }

                if (scopeMustMatch)
                {
                    return false;
                }

                position--;
            }

            if (position < 0)
            {
                return false;
            }

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
        if (a == null && b == null)
        {
            return 0;
        }

        if (a == null)
        {
            return -1;
        }

        if (b == null)
        {
            return 1;
        }

        if (a.Length == b.Length)
        {
            for (var i = 0; i < a.Length; i++)
            {
                var r = StrCmp(a[i], b[i]);

                if (r != 0)
                {
                    return r;
                }
            }

            return 0;
        }

        return a.Length < b.Length ? -1 : 1;
    }

    private static bool IsValidHexColor(string hex)
    {
        return hex.Length is 4 or 5 or 7 or 9 && hex[0] == '#' && hex[1..].All(Uri.IsHexDigit);
    }

    private sealed record ParsedThemeRule(string Scope, string[]? ParentScopes, int Index, string? Foreground);

    private sealed class Rule
    {
        public Rule(int scopeDepth, string[]? parentScopes, string? foreground)
        {
            ScopeDepth   = scopeDepth;
            ParentScopes = parentScopes;
            Foreground   = foreground;
        }

        public int      ScopeDepth   { get; set; }
        public string[]? ParentScopes { get; }
        public string?  Foreground   { get; set; }

        public void AcceptOverwrite(int scopeDepth, string? foreground)
        {
            if (ScopeDepth <= scopeDepth)
            {
                ScopeDepth = scopeDepth;
            }

            if (foreground != null)
            {
                Foreground = foreground;
            }
        }

        public Rule Clone() => new(ScopeDepth, ParentScopes, Foreground);
    }

    private sealed class ThemeTrieElement
    {
        private readonly Rule _mainRule;
        private readonly List<Rule> _rulesWithParentScopes;
        private readonly Dictionary<string, ThemeTrieElement> _children = new();

        public ThemeTrieElement(Rule? mainRule, List<Rule> rulesWithParentScopes)
        {
            _mainRule           = mainRule ?? new Rule(0, null, null);
            _rulesWithParentScopes = rulesWithParentScopes;
        }

        public List<Rule> Match(string scope)
        {
            if (scope.Length != 0)
            {
                var dotIndex = scope.IndexOf('.');

                var head = dotIndex == -1 ? scope : scope[..dotIndex];

                var tail = dotIndex == -1 ? "" : scope[(dotIndex + 1)..];

                if (_children.TryGetValue(head, out var child))
                {
                    return child.Match(tail);
                }
            }

            var rules = new List<Rule>(_rulesWithParentScopes) { _mainRule };

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
                child = new ThemeTrieElement(_mainRule.Clone(), [.. _rulesWithParentScopes.Select(r => r.Clone())]);

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

            foreach (var rule in _rulesWithParentScopes)
            {
                if (StrArrCmp(rule.ParentScopes, parentScopes) == 0)
                {
                    rule.AcceptOverwrite(scopeDepth, foreground);

                    return;
                }
            }

            _rulesWithParentScopes.Add(new Rule(scopeDepth, parentScopes, foreground ?? _mainRule.Foreground));
        }

        private static int CompareBySpecificity(Rule a, Rule b)
        {
            if (a.ScopeDepth != b.ScopeDepth)
            {
                return b.ScopeDepth - a.ScopeDepth;
            }

            var aParents = a.ParentScopes ?? [];
            var bParents = b.ParentScopes ?? [];

            var aIndex = 0;
            var bIndex = 0;

            while (true)
            {
                if (aIndex < aParents.Length && aParents[aIndex] == ">")
                {
                    aIndex++;
                }

                if (bIndex < bParents.Length && bParents[bIndex] == ">")
                {
                    bIndex++;
                }

                if (aIndex >= aParents.Length || bIndex >= bParents.Length)
                {
                    break;
                }

                var lengthDiff = bParents[bIndex].Length - aParents[aIndex].Length;

                if (lengthDiff != 0)
                {
                    return lengthDiff;
                }

                aIndex++;
                bIndex++;
            }

            return bParents.Length - aParents.Length;
        }
    }
}
