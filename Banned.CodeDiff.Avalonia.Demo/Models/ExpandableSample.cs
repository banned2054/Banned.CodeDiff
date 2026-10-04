using System.Text;

namespace Banned.CodeDiff.Avalonia.Demo.Models;

/// <summary>
///     Deterministic synthetic sample with real old/new file contents, so hunk expansion is enabled
///     (paste-only diffs compose from the diff text and cannot expand). The unchanged gaps vary in
///     size to exercise every expand affordance: single up/down, the expand-all button for short
///     hidden ranges, the stacked down+up pair for long ones, and the trailing strip.
/// </summary>
public static class ExpandableSample
{
    private const int Tail = 60;

    // (gap before, deleted lines, added lines) per change cluster; every gap keeps the 3-line
    // contexts of adjacent hunks disjoint (>= 8 unchanged lines in between).
    private static readonly (int Gap, int Deletes, int Adds)[] Clusters =
    [
        (40, 1, 1),
        (120, 1, 0),
        (25, 0, 1),
        (90, 2, 3),
        (15, 1, 1),
        (30, 0, 2)
    ];

    public static (string OldContent, string NewContent, string DiffText) Create()
    {
        var oldLines = new List<string>();
        var newLines = new List<string>();
        var diff     = new StringBuilder("--- a/Sample.cs\n+++ b/Sample.cs\n");

        // 1-based next line numbers; texts of unchanged lines must be identical on both sides.
        var oldNumber = 1;
        var newNumber = 1;

        foreach (var (gap, deletes, adds) in Clusters)
        {
            for (var k = 0; k < gap; k++)
            {
                var text = $"ctx {oldNumber:D3}";
                oldLines.Add(text);
                newLines.Add(text);
                oldNumber++;
                newNumber++;
            }

            // The 3 context lines before the edit are oldNumber-3 .. oldNumber-1 (1-based).
            var oldStart = oldNumber - 3;
            var newStart = newNumber - 3;

            diff.Append("@@ -").Append(oldStart).Append(',').Append(6 + deletes)
                .Append(" +").Append(newStart).Append(',').Append(6   + adds)
                .AppendLine(" @@");

            for (var k = 3; k > 0; k--) diff.Append(' ').AppendLine($"ctx {oldNumber - k:D3}");

            for (var k = 0; k < deletes; k++)
            {
                var text = $"removed {oldNumber:D3}";
                diff.Append('-').AppendLine(text);
                oldLines.Add(text);
                oldNumber++;
            }

            for (var k = 0; k < adds; k++)
            {
                var text = $"added {newNumber:D3}";
                diff.Append('+').AppendLine(text);
                newLines.Add(text);
                newNumber++;
            }

            for (var k = 0; k < 3; k++)
            {
                var text = $"ctx {oldNumber:D3}";
                diff.Append(' ').AppendLine(text);
                oldLines.Add(text);
                newLines.Add(text);
                oldNumber++;
                newNumber++;
            }
        }

        for (var k = 0; k < Tail; k++)
        {
            var text = $"ctx {oldNumber:D3}";
            oldLines.Add(text);
            newLines.Add(text);
            oldNumber++;
            newNumber++;
        }

        return (string.Join("\n", oldLines) + "\n", string.Join("\n", newLines) + "\n", diff.ToString());
    }
}
