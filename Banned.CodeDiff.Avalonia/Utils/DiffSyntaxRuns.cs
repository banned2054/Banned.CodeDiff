using Avalonia.Media;
using Avalonia.Media.Immutable;
using Avalonia.Styling;
using Banned.CodeDiff.Avalonia.Models;
using Banned.CodeDiff.Models;

namespace Banned.CodeDiff.Avalonia.Utils;

/// <summary>
///     提取显示文本内的着色区间;可按 scope 重新配色,相邻同色区间合并。超过 150 个 span 时回退纯文本。<br />
///     Extracts syntax runs within displayed text, optionally resolving scopes; merges adjacent equal colors and falls back to plain text above 150 spans.
/// </summary>
internal static class DiffSyntaxRuns
{
    private static readonly Dictionary<string, IBrush> BrushCache = [];

    /// <summary>
    ///     从 <paramref name="syntaxLine" /> 提取适配显示长度的语法着色区间。<br />
    ///     Extracts the syntax runs fitted to the display length from <paramref name="syntaxLine" />.
    /// </summary>
    /// <param name="syntaxLine">语法高亮行;可为 <c>null</c>。The syntax-highlighted line; may be <c>null</c>.</param>
    /// <param name="displayLength">去除换行符后的显示文本长度。The display length after newline trimming.</param>
    /// <param name="variant">当前主题变体,决定取哪个 CSS 变量。The theme variant selecting the CSS variable.</param>
    /// <param name="syntaxColors">
    ///     控件实例的语法预设/覆盖解析器;<c>null</c> 走 wrapper style 的最终颜色通路。
    ///     <br />The control instance's syntax preset/override resolver; <c>null</c> uses the
    ///     wrapper style's final colors.
    /// </param>
    /// <returns>
    ///     语法着色区间;无语法信息、无 span、span 超过 150 个、显示长度非法或没有任何可着色 span 时为 <c>null</c>。<br />The runs, or <c>null</c> when there
    ///     is no syntax info, no spans, more than 150 spans, an invalid display length, or no colorable span.
    /// </returns>
    public static IReadOnlyList<DiffSyntaxRun>? Extract(SyntaxLine?       syntaxLine, int displayLength,
                                                        ThemeVariant      variant,
                                                        DiffSyntaxColors? syntaxColors = null)
    {
        var nodeList = syntaxLine?.NodeList;

        if (nodeList == null || nodeList.Count == 0 || displayLength <= 0)
        {
            // 无语法 span 的纯文本仍应用宿主默认前景色。
            if (displayLength > 0 && syntaxColors?.DefaultForeground is { } plain)
                return [new DiffSyntaxRun(0, displayLength, GetBrush(plain))];

            return null;
        }

        if (nodeList.Count > 150) return null;

        var variable = variant == ThemeVariant.Dark ? "--diff-view-dark:" : "--diff-view-light:";

        List<DiffSyntaxRun>? runs = null;

        foreach (var span in nodeList)
        {
            if (syntaxColors is { } resolver)
            {
                // token 范围相对包装文本,保留换行位置。
                var tokens = span.Wrapper?.Properties?.Tokens;

                if (tokens is { Count: > 0 })
                {
                    var spanStart = span.Node.StartIndex;

                    foreach (var token in tokens)
                    {
                        var color = resolver.MatchForeground(token.Scopes);

                        if (color == null) continue;

                        Emit(ref runs, GetBrush(color), spanStart + token.Start, token.Length, displayLength);
                    }

                    continue;
                }
            }

            var style = span.Wrapper?.Properties?.Style;

            if (string.IsNullOrEmpty(style)) continue;

            var spanColor = ParseCssVariable(style, variable);

            if (spanColor == null) continue;

            Emit(ref runs, GetBrush(spanColor), span.Node.StartIndex,
                 span.Node.EndIndex - span.Node.StartIndex + 1, displayLength);
        }

        return runs;
    }

    /// <summary>
    ///     钳制区间到显示文本内,合并相邻同画刷区间。
    /// </summary>
    private static void Emit(ref List<DiffSyntaxRun>? runs, IBrush brush, int start, int length, int displayLength)
    {
        start = Math.Clamp(start, 0, displayLength - 1);

        var end = Math.Clamp(start + length - 1, start, displayLength - 1);

        var clamped = end - start + 1;

        if (clamped <= 0) return;

        if (runs is { Count: > 0 } && runs[^1].Foreground.Equals(brush) &&
            runs[^1].Start + runs[^1].Length == start)
        {
            runs[^1] = runs[^1] with { Length = runs[^1].Length + clamped };

            return;
        }

        runs ??= [];

        runs.Add(new DiffSyntaxRun(start, clamped, brush));
    }

    private static string? ParseCssVariable(string style, string variable)
    {
        var index = style.IndexOf(variable, StringComparison.Ordinal);

        if (index < 0) return null;

        var start = index + variable.Length;

        var end = style.IndexOf(';', start);

        if (end < 0) end = style.Length;

        var value = style[start..end].Trim();

        return value.Length > 0 ? value : null;
    }

    private static IBrush GetBrush(string color)
    {
        if (BrushCache.TryGetValue(color, out var cached)) return cached;

        // 共享画刷须不可变,避免一个视图改色影响其他视图。
        var brush = new ImmutableSolidColorBrush(ParseThemeHex(color));

        BrushCache[color] = brush;

        return brush;
    }

    /// <summary>
    ///     解析 TextMate 十六进制颜色;尾部 alpha 转为 Avalonia 的头部 alpha。<br />
    ///     Parses TextMate hex colors, converting trailing alpha to Avalonia's leading alpha.
    /// </summary>
    private static Color ParseThemeHex(string color)
    {
        switch (color.Length)
        {
            case 9 when color[0] == '#' :
                return Color.FromArgb(Convert.ToByte(color[7..9], 16), Convert.ToByte(color[1..3], 16),
                                      Convert.ToByte(color[3..5], 16), Convert.ToByte(color[5..7], 16));
            case 5 when color[0] == '#' :
                return Color.FromArgb((byte)(Convert.ToByte(color[4..5], 16) * 17),
                                      (byte)(Convert.ToByte(color[1..2], 16) * 17),
                                      (byte)(Convert.ToByte(color[2..3], 16) * 17),
                                      (byte)(Convert.ToByte(color[3..4], 16) * 17));
            default :
                return Color.Parse(color);
        }
    }
}
