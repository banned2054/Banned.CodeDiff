using Avalonia.Styling;
using Banned.CodeDiff.Services.TextMate;
using System.Text;

namespace Banned.CodeDiff.Avalonia.Models;

/// <summary>
///     按控件实例解析语法预设与覆盖;匹配器和画刷共享只读。<br />
///     Resolves per-control syntax presets and overrides using shared read-only matchers and brushes.
/// </summary>
internal sealed class DiffSyntaxColors
{
    private readonly ScopeThemeMatcher _presetMatcher;

    /// <summary>宿主覆盖规则的匹配器;只含显式 scope 规则,不含默认前景。</summary>
    private readonly ScopeThemeMatcher? _overrideRules;

    /// <summary>宿主的默认前景覆盖(token 未命中任何预设规则且未命中覆盖规则时生效)。</summary>
    private readonly string? _defaultForegroundOverride;

    /// <summary>
    ///     宿主设置的默认前景覆盖(归一化后);无语法节点的普通文本整行用它着色。<br />
    ///     The host's normalized default-foreground override; plain text without syntax nodes
    ///     renders the whole line with it.
    /// </summary>
    public string? DefaultForeground => _defaultForegroundOverride;

    private DiffSyntaxColors(ScopeThemeMatcher presetMatcher, ScopeThemeMatcher? overrideRules,
                             string?           defaultForegroundOverride)
    {
        _presetMatcher             = presetMatcher;
        _overrideRules             = overrideRules;
        _defaultForegroundOverride = defaultForegroundOverride;
    }

    /// <summary>
    ///     创建解析器;没有定制(预设为默认且无覆盖)时返回 <c>null</c>——渲染走 wrapper
    ///     style 的最终颜色通路,零额外开销,GitHub 默认外观完全不变。<br />
    ///     Creates the resolver; returns <c>null</c> when nothing is customized (default preset,
    ///     no overrides) — rendering then uses the wrapper style's final colors with zero extra
    ///     cost, keeping the GitHub default look untouched.
    /// </summary>
    public static DiffSyntaxColors? Create(DiffThemePreset?     syntaxPreset, DiffThemePreset? themePreset,
                                           DiffSyntaxOverrides? overrides,    ThemeVariant     variant)
    {
        var preset = syntaxPreset ?? themePreset;

        ScopeThemeMatcher? overrideRules   = null;
        string?            defaultOverride = null;

        // 无有效覆盖时使用已有最终颜色。
        if (overrides != null)
        {
            defaultOverride = NormalizeHex(overrides.DefaultForeground);

            // 默认前景仅替换未命中主题规则的颜色,不能覆盖显式规则。
            var rules = (from rule in overrides.Overrides
                         where !string.IsNullOrWhiteSpace(rule.Scope)
                         let color = NormalizeHex(rule.Color)
                         where color != null
                         select (Scope : rule.Scope.Trim(), Color : color))
               .ToList();

            if (rules.Count > 0) overrideRules = BuildOverrideMatcher(rules);
        }

        var overridden = defaultOverride != null || overrideRules != null;

        if ((preset is null or DiffThemePreset.GitHub) && !overridden) return null;

        // 缺少当前变体时回退对应 GitHub 主题。
        var themeName = DiffThemePresets.GetSyntaxThemeName(preset, variant) ??
                        DiffThemePresets.GetSyntaxThemeName(null, variant)!;

        var fallbackName = DiffThemePresets.GetSyntaxThemeName(null, variant)!;

        var matcher = DiffSyntaxThemes.GetMatcher(themeName) ?? DiffSyntaxThemes.GetMatcher(fallbackName)!;

        return new DiffSyntaxColors(matcher, overrideRules, defaultOverride);
    }

    /// <summary>
    ///     解析一个 token scope 栈的前景色:覆盖规则胜过预设规则;预设规则未命中且宿主设置
    ///     了默认前景覆盖时用覆盖值;否则按预设匹配(含预设主题默认前景回退)。<br />
    ///     Resolves the foreground of a token scope stack: override rules beat preset rules; when
    ///     no preset rule hits and the host set a default-foreground override, that override
    ///     applies; otherwise the preset match (including its theme-default fallback) wins.
    /// </summary>
    public string? MatchForeground(IList<string> scopeStack)
    {
        var overrideColor = _overrideRules?.MatchForeground(scopeStack);

        if (overrideColor != null) return overrideColor;

        var presetRule = _presetMatcher.MatchRuleForeground(scopeStack);

        if (presetRule != null) return presetRule;

        return _defaultForegroundOverride ?? _presetMatcher.MatchForeground(scopeStack);
    }

    /// <summary>
    ///     将覆盖规则交给主题匹配器,复用相同匹配语义。
    /// </summary>
    private static ScopeThemeMatcher BuildOverrideMatcher(IReadOnlyList<(string Scope, string Color)> rules)
    {
        var json = new StringBuilder("""{"tokenColors":[""");

        var first = true;

        foreach (var (scope, color) in rules)
        {
            if (!first) json.Append(',');

            first = false;

            json.Append("{\"scope\":\"").Append(EscapeJson(scope))
                .Append("\",\"settings\":{\"foreground\":\"").Append(color).Append("\"}}");
        }

        json.Append("]}");

        return ScopeThemeMatcher.FromThemeJson(Encoding.UTF8.GetBytes(json.ToString()));
    }

    /// <summary>
    ///     Minimal JSON string escaping for scope names — they only carry letters, digits, dots,
    ///     and spaces in practice, but a hostile host string must not break the theme JSON.
    /// </summary>
    private static string EscapeJson(string value)
    {
        return value.Replace("\\", "\\\\").Replace("\"", "\\\"");
    }

    private static string? NormalizeHex(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;

        var trimmed = value.Trim();

        // 先验证十六进制颜色,避免渲染时解析无效值。
        return trimmed.Length is 4 or 5 or 7 or 9 && trimmed[0] == '#' && trimmed[1..].All(Uri.IsHexDigit)
            ? trimmed.ToUpperInvariant()
            : null;
    }
}
