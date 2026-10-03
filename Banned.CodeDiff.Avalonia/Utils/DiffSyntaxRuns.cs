using Avalonia.Media;
using Avalonia.Styling;
using Banned.CodeDiff.Avalonia.Models;
using Banned.CodeDiff.Models;

namespace Banned.CodeDiff.Avalonia.Utils;

/// <summary>
/// Extracts per-line syntax runs from a <see cref="SyntaxLine"/> for rendering:
/// parses the shiki-style wrapper style ("--diff-view-light:#…;--diff-view-dark:#…"),
/// picks the color for the current theme variant, clamps the spans to the
/// displayed (newline-trimmed) line text, and merges adjacent same-brush runs.
/// Lines with more than 150 spans degrade to plain text, matching the upstream
/// render guard (packages/vue/src/components/DiffContent.tsx).
/// </summary>
internal static class DiffSyntaxRuns
{
    private static readonly Dictionary<string, SolidColorBrush> BrushCache = [];

    public static IReadOnlyList<DiffSyntaxRun>? Extract(SyntaxLine? syntaxLine, int displayLength, ThemeVariant variant)
    {
        var nodeList = syntaxLine?.NodeList;

        if (nodeList == null || nodeList.Count == 0 || displayLength <= 0)
        {
            return null;
        }

        if (nodeList.Count > 150)
        {
            return null;
        }

        var variable = variant == ThemeVariant.Dark ? "--diff-view-dark:" : "--diff-view-light:";

        List<DiffSyntaxRun>? runs = null;

        foreach (var span in nodeList)
        {
            var style = span.Wrapper?.Properties?.Style;

            if (string.IsNullOrEmpty(style))
            {
                continue;
            }

            var color = ParseCssVariable(style, variable);

            if (color == null)
            {
                continue;
            }

            var start = span.Node.StartIndex;
            var end   = span.Node.EndIndex; // inclusive, in newline-keeping coordinates

            start = Math.Clamp(start, 0, displayLength - 1);
            end   = Math.Clamp(end, start, displayLength - 1);

            var length = end - start + 1;

            if (length <= 0)
            {
                continue;
            }

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

        if (index < 0)
        {
            return null;
        }

        var start = index + variable.Length;

        var end = style.IndexOf(';', start);

        if (end < 0)
        {
            end = style.Length;
        }

        var value = style[start..end].Trim();

        return value.Length > 0 ? value : null;
    }

    private static SolidColorBrush GetBrush(string color)
    {
        if (BrushCache.TryGetValue(color, out var cached))
        {
            return cached;
        }

        var brush = new SolidColorBrush(Color.Parse(color));

        BrushCache[color] = brush;

        return brush;
    }
}
