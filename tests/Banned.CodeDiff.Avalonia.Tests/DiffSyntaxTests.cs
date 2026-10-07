using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless.NUnit;
using Avalonia.Media;
using Avalonia.Styling;
using Avalonia.VisualTree;
using Banned.CodeDiff.Avalonia.Models;
using Banned.CodeDiff.Avalonia.Utils;
using Banned.CodeDiff.Avalonia.Views;
using Banned.CodeDiff.Models;
using Banned.CodeDiff.Services;
using NUnit.Framework;

namespace Banned.CodeDiff.Avalonia.Tests;

/// <summary>
///     M5 syntax wiring: DiffView drives DiffFile.InitSyntax (built-in TextMate engine),
///     rows carry syntax runs resolved per theme variant, the toggle disables them,
///     and the 150-span degradation guard holds.
/// </summary>
public class DiffSyntaxTests
{
    private const string CsOld = """
        using System;

        namespace Demo
        {
            public class Calculator
            {
                public int Add(int a, int b)
                {
                    return a + b;
                }
            }
        }
        """;

    private const string CsNew = """
        using System;

        namespace Demo
        {
            public class Calculator
            {
                public int Add(int a, int b)
                {
                    return a + b + 1;
                }
            }
        }
        """;

    private static readonly string[] CsHunks =
    [
        """
        diff --git a/calculator.cs b/calculator.cs
        --- a/calculator.cs
        +++ b/calculator.cs
        @@ -7,7 +7,7 @@ public class Calculator
                 public int Add(int a, int b)
                 {
        -            return a + b;
        +            return a + b + 1;
                 }
             }
        """
    ];

    private static DiffFile CreateCsFile()
    {
        // No Init here — the view owns the init sequence (InitRaw + conditional InitSyntax).
        return new DiffFile("calculator.cs", CsOld, "calculator.cs", CsNew, CsHunks);
    }

    /// <summary>Index of the "return" keyword inside the (indented) line text.</summary>
    private static int ReturnRunStart(string text)
    {
        return text.IndexOf("return", StringComparison.Ordinal);
    }

    [AvaloniaTest]
    public void SplitRows_CarrySyntaxRuns_WithKeywordColor()
    {
        var view = new DiffView { DiffFile = CreateCsFile() };

        var row = view.Rows.OfType<DiffSplitContentRow>().First(r => r.Right.Kind == DiffCellKind.Add);

        var runs = row.Right.SyntaxRuns;

        Assert.That(runs, Is.Not.Null);

        // "return" is a keyword: github-light keyword color #d73a49 (upper-cased like shiki).
        var returnRun = runs!.First(r => r.Start == ReturnRunStart(row.Right.Text));

        Assert.That(((ISolidColorBrush)returnRun.Foreground).Color.ToString(),
                    Is.EqualTo("#FFD73A49".ToLowerInvariant()));

        // The left (old) side resolves from the old file's syntax table as well.
        Assert.That(row.Left.SyntaxRuns, Is.Not.Null);
    }

    [AvaloniaTest]
    public void UnifiedRows_CarrySyntaxRuns()
    {
        var view = new DiffView { DiffFile = CreateCsFile(), ViewMode = DiffViewMode.Unified };

        var row = view.Rows.OfType<DiffUnifiedContentRow>().First(r => r.Kind == DiffCellKind.Add);

        Assert.That(row.SyntaxRuns, Is.Not.Null);

        var returnRun = row.SyntaxRuns!.First(r => r.Start == ReturnRunStart(row.Text));

        Assert.That(((ISolidColorBrush)returnRun.Foreground).Color.ToString(),
                    Is.EqualTo("#FFD73A49".ToLowerInvariant()));
    }

    [AvaloniaTest]
    public void SyntaxHighlight_Off_LeavesRowsPlain()
    {
        // Set the toggle before assigning the model — assigning DiffFile already renders.
        var view = new DiffView { SyntaxHighlight = false };

        view.DiffFile = CreateCsFile();

        var row = view.Rows.OfType<DiffSplitContentRow>().First();

        Assert.That(view.SyntaxHighlight, Is.False);
        Assert.That(row.Right.SyntaxRuns, Is.Null);

        // Toggling on re-runs InitSyntax and rebuilds the rows.
        view.SyntaxHighlight = true;

        var colored = view.Rows.OfType<DiffSplitContentRow>().First(r => r.Right.SyntaxRuns != null);

        Assert.That(colored, Is.Not.Null);
    }

    [AvaloniaTest]
    public void ThemeSwitch_RebuildsRows_WithDarkSyntaxColors()
    {
        var window = new Window { Content = new DiffView { DiffFile = CreateCsFile() } };

        window.Show();

        var view = window.GetVisualDescendants().OfType<DiffView>().Single();

        var lightRow = view.Rows.OfType<DiffSplitContentRow>().First(r => r.Right.Kind == DiffCellKind.Add);

        var lightRun = lightRow.Right.SyntaxRuns!.First(r => r.Start == ReturnRunStart(lightRow.Right.Text));

        Assert.That(((ISolidColorBrush)lightRun.Foreground).Color.ToString(),
                    Is.EqualTo("#FFD73A49".ToLowerInvariant()));

        window.RequestedThemeVariant = ThemeVariant.Dark;

        var darkRow = view.Rows.OfType<DiffSplitContentRow>().First(r => r.Right.Kind == DiffCellKind.Add);

        var darkRun = darkRow.Right.SyntaxRuns!.First(r => r.Start == ReturnRunStart(darkRow.Right.Text));

        Assert.That(((ISolidColorBrush)darkRun.Foreground).Color.ToString(), Is.EqualTo("#FFF97583".ToLowerInvariant()),
                    "dark theme rows must use the --diff-view-dark color");
    }

    [AvaloniaTest]
    public void Extract_DegradesOver150Spans_AndClampsToDisplayText()
    {
        var style = new SyntaxNodeProperties { Style = "--diff-view-dark:#F97583;--diff-view-light:#D73A49" };

        SyntaxLine Line(int spanCount, string value)
        {
            var line = new SyntaxLine { Value = value, LineNumber = 1, ValueLength = value.Length, NodeList = [] };

            for (var i = 0; i < spanCount; i++)
                line.NodeList!.Add(new SyntaxNodeSpan
                {
                    Node    = new SyntaxNode { Value      = "x", StartIndex = i, EndIndex = i },
                    Wrapper = new SyntaxNode { Properties = style }
                });

            return line;
        }

        // Over the 150-span guard → plain text.
        Assert.That(DiffSyntaxRuns.Extract(Line(151, new string('x', 200)), 200, ThemeVariant.Light), Is.Null);

        // Spans clamped to the displayed (newline-trimmed) text: the trailing "\n" span drops out.
        var runs = DiffSyntaxRuns.Extract(Line(1, "ab\n"), 2, ThemeVariant.Light);

        Assert.That(runs, Is.Not.Null);

        // The raw span covers "a" (0..0) within the trimmed text.
        Assert.That(runs![0].Start, Is.EqualTo(0));
        Assert.That(runs[0].Length, Is.EqualTo(1));

        // Dark variant picks the dark variable.
        var dark = DiffSyntaxRuns.Extract(Line(1, "a"), 1, ThemeVariant.Dark);

        Assert.That(((ISolidColorBrush)dark![0].Foreground).Color.ToString(),
                    Is.EqualTo("#FFF97583".ToLowerInvariant()));
    }

    [AvaloniaTest]
    public void DiffSegmentText_RendersWithSyntaxRuns_WithoutThrowing()
    {
        var text = new DiffSegmentText
        {
            Text       = "return a + b;",
            FontFamily = FontFamily.Parse("Menlo, Consolas, monospace"),
            FontSize   = 14,
            Foreground = Brushes.Black,
            SyntaxRuns =
            [
                new DiffSyntaxRun(0, 6, new SolidColorBrush(Color.FromRgb(0xD7, 0x3A, 0x49))),
                new DiffSyntaxRun(6, 8, new SolidColorBrush(Color.FromRgb(0x24, 0x29, 0x2E)))
            ]
        };

        var window = new Window { Content = text };

        window.Show();

        text.Measure(new Size(1000, 200));
        text.Arrange(new Rect(0, 0, 1000, 200));

        Assert.That(text.DesiredSize.Width, Is.GreaterThan(0));
        Assert.That(text.DesiredSize.Height, Is.GreaterThan(0));
    }
}
