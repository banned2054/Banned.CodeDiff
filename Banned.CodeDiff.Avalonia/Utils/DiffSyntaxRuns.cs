using Avalonia.Media;
using Avalonia.Styling;
using Banned.CodeDiff.Avalonia.Models;
using Banned.CodeDiff.Models;

namespace Banned.CodeDiff.Avalonia.Utils;

/// <summary>
///     从 <see cref="SyntaxLine" /> 提取每行的语法着色区间用于渲染:解析 shiki 风格的包装样式
///     ("--diff-view-light:#…;--diff-view-dark:#…"),按当前主题变体取色,把区间钳制到显示文本
///     (去除换行符)的范围内,并合并相邻的同画刷区间。span 数超过 150 的行退化为纯文本,与
///     上游的渲染守卫一致(packages/vue/src/components/DiffContent.tsx)。<br />
///     Extracts per-line syntax runs from a <see cref="SyntaxLine" /> for rendering:
///     parses the shiki-style wrapper style ("--diff-view-light:#…;--diff-view-dark:#…"),
///     picks the color for the current theme variant, clamps the spans to the
///     displayed (newline-trimmed) line text, and merges adjacent same-brush runs.
///     Lines with more than 150 spans degrade to plain text, matching the upstream
///     render guard (packages/vue/src/components/DiffContent.tsx).
/// </summary>
internal static class DiffSyntaxRuns
{
    private static readonly Dictionary<string, SolidColorBrush> BrushCache = [];

    /// <summary>
    ///     从 <paramref name="syntaxLine" /> 提取适配显示长度的语法着色区间。<br />
    ///     Extracts the syntax runs fitted to the display length from <paramref name="syntaxLine" />.
    /// </summary>
    /// <param name="syntaxLine">语法高亮行;可为 <c>null</c>。The syntax-highlighted line; may be <c>null</c>.</param>
    /// <param name="displayLength">去除换行符后的显示文本长度。The display length after newline trimming.</param>
    /// <param name="variant">当前主题变体,决定取哪个 CSS 变量。The theme variant selecting the CSS variable.</param>
    /// <returns>
    ///     语法着色区间;无语法信息、无 span、span 超过 150 个、显示长度非法或没有任何可着色 span 时为 <c>null</c>。<br />The runs, or <c>null</c> when there
    ///     is no syntax info, no spans, more than 150 spans, an invalid display length, or no colorable span.
    /// </returns>
    public static IReadOnlyList<DiffSyntaxRun>? Extract(SyntaxLine? syntaxLine, int displayLength, ThemeVariant variant)
    {
        var nodeList = syntaxLine?.NodeList;

        if (nodeList == null || nodeList.Count == 0 || displayLength <= 0) return null;

        if (nodeList.Count > 150) return null;

        var variable = variant == ThemeVariant.Dark ? "--diff-view-dark:" : "--diff-view-light:";

        List<DiffSyntaxRun>? runs = null;

        foreach (var span in nodeList)
        {
            var style = span.Wrapper?.Properties?.Style;

            if (string.IsNullOrEmpty(style)) continue;

            var color = ParseCssVariable(style, variable);

            if (color == null) continue;

            var start = span.Node.StartIndex;
            var end   = span.Node.EndIndex; // inclusive, in newline-keeping coordinates

            start = Math.Clamp(start, 0, displayLength   - 1);
            end   = Math.Clamp(end, start, displayLength - 1);

            var length = end - start + 1;

            if (length <= 0) continue;

            var brush = GetBrush(color);

            if (runs is { Count: > 0 } && runs[^1].Foreground.Equals(brush) &&
                runs[^1].Start + runs[^1].Length == start)
            {
                runs[^1] = runs[^1] with { Length = runs[^1].Length + length };

                continue;
            }

            runs ??= [];

            runs.Add(new DiffSyntaxRun(start, length, brush));
        }

        return runs;
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

    private static SolidColorBrush GetBrush(string color)
    {
        if (BrushCache.TryGetValue(color, out var cached)) return cached;

        var brush = new SolidColorBrush(Color.Parse(color));

        BrushCache[color] = brush;

        return brush;
    }
}
