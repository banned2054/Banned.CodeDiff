using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless.NUnit;
using Avalonia.Media;
using Avalonia.Media.TextFormatting;
using Avalonia.VisualTree;
using Banned.CodeDiff.Avalonia.Models;
using Banned.CodeDiff.Avalonia.Views;
using Banned.CodeDiff.Services;
using NUnit.Framework;

namespace Banned.CodeDiff.Avalonia.Tests;

public class DiffViewTests
{
    private const string Sample = """
        diff --git a/Program.cs b/Program.cs
        --- a/Program.cs
        +++ b/Program.cs
        @@ -1,6 +1,7 @@
         using System;
         using System.Text;
         
        -Console.WriteLine("Hello");
        +Console.WriteLine("Hello, World!");
        +var name = Console.ReadLine();
         
         if (args.Length > 0)
        @@ -10,3 +11,4 @@
         
        -    return 1;
        +    return 2;
         }
        +// done
        """;

    private static DiffFile CreateSampleFile()
    {
        var file = new DiffFile("", "", "", "",
                                [Sample]);
        file.Init();
        file.BuildSplitDiffLines();
        return file;
    }

    /// <summary>xunit's Assert.IsType returned the cast value; keep that pattern with exact-type semantics.</summary>
    private static T As<T>(object? value) where T : class
    {
        Assert.That(value, Is.TypeOf<T>());
        return (T)value!;
    }

    [AvaloniaTest]
    public void DiffView_BuildsExpectedRows_FromSampleDiff()
    {
        var view = new DiffView { DiffFile = CreateSampleFile() };

        // 11 content rows + 1 hunk placeholder row: the leading "@@ -1,6 +1,7 @@" starts at line
        // 1, so its hidden range is empty and — matching upstream/GitHub — it is not rendered.
        Assert.That(view.Rows.Count, Is.EqualTo(12));
        Assert.That(view.Rows.OfType<DiffSplitHunkRow>().Count(), Is.EqualTo(1));
        Assert.That(view.Rows.OfType<DiffSplitContentRow>().Count(), Is.EqualTo(11));

        // The remaining hunk placeholder sits above the second hunk's first context row.
        Assert.That(view.Rows[7], Is.TypeOf<DiffSplitHunkRow>());
        Assert.That(As<DiffSplitHunkRow>(view.Rows[7]).HunkText, Does.StartWith("@@"));

        // The deleted/added Console.WriteLine lines pair in one row (probe split index 3), the
        // extra add-only line has an empty left placeholder (probe index 4).
        var pair = As<DiffSplitContentRow>(view.Rows[3]);
        Assert.That(pair.Left.Kind, Is.EqualTo(DiffCellKind.Delete));
        Assert.That(pair.Right.Kind, Is.EqualTo(DiffCellKind.Add));

        var addOnly = As<DiffSplitContentRow>(view.Rows[4]);
        Assert.That(addOnly.Left.Kind, Is.EqualTo(DiffCellKind.Empty));
        Assert.That(addOnly.Right.Kind, Is.EqualTo(DiffCellKind.Add));
    }

    [AvaloniaTest]
    public void DiffView_AppliesControlTheme_And_InstantiatesTemplate()
    {
        var view   = new DiffView { DiffFile = CreateSampleFile() };
        var window = new Window { Content    = view };
        window.Show();

        // The ControlTheme (loaded through the consumer-style include in TestAppStyles) must have
        // produced a visual tree: a ScrollViewer with an ItemsControl inside.
        Assert.That(view.GetVisualDescendants(), Has.Some.InstanceOf<ScrollViewer>());
        var items = view.GetVisualDescendants().OfType<ItemsControl>().Single();
        Assert.That(items.Items.Count(), Is.EqualTo(12));
    }

    [AvaloniaTest]
    public void DiffView_UsesUpstreamLightPalette()
    {
        var view = new DiffView { DiffFile = CreateSampleFile() };

        var pair        = As<DiffSplitContentRow>(view.Rows[3]);
        var addBrush    = As<SolidColorBrush>(pair.Right.ContentBackground);
        var deleteBrush = As<SolidColorBrush>(pair.Left.ContentBackground);

        // Upstream git-diff-view light values: --diff-add-content-- / --diff-del-content--.
        Assert.That(addBrush.Color, Is.EqualTo(Color.Parse("#dafbe1")));
        Assert.That(deleteBrush.Color, Is.EqualTo(Color.Parse("#ffebe9")));
    }

    [AvaloniaTest]
    public void DiffView_ClearsRows_WhenFileRemoved()
    {
        var view = new DiffView { DiffFile = CreateSampleFile() };
        view.DiffFile = null;
        Assert.That(view.Rows, Is.Empty);
    }

    [AvaloniaTest]
    public void DiffView_UnifiedMode_BuildsExpectedRows()
    {
        var view = new DiffView { DiffFile = CreateSampleFile(), ViewMode = DiffViewMode.Unified };

        // 13 content rows + 1 hunk placeholder row (the leading @@ at line 1 is not rendered);
        // deletes and adds are separate rows.
        Assert.That(view.Rows.Count, Is.EqualTo(14));
        Assert.That(view.Rows.OfType<DiffUnifiedHunkRow>().Count(), Is.EqualTo(1));
        Assert.That(view.Rows.OfType<DiffUnifiedContentRow>().Count(), Is.EqualTo(13));

        Assert.That(view.Rows[8], Is.TypeOf<DiffUnifiedHunkRow>());
        Assert.That(As<DiffUnifiedHunkRow>(view.Rows[8]).HunkText, Does.StartWith("@@"));

        // Delete above add (the -Console.WriteLine line is old file line 4).
        var delete = As<DiffUnifiedContentRow>(view.Rows[3]);
        Assert.That(delete.Kind, Is.EqualTo(DiffCellKind.Delete));
        Assert.That(delete.OldNumber, Is.EqualTo("4"));
        Assert.That(delete.NewNumber, Is.Null);

        var add = As<DiffUnifiedContentRow>(view.Rows[4]);
        Assert.That(add.Kind, Is.EqualTo(DiffCellKind.Add));
        Assert.That(add.OldNumber, Is.Null);
        Assert.That(add.NewNumber, Is.EqualTo("4"));

        // 2 deletes and 4 adds render as separate rows (the sample's Deletion/Addition counts).
        var content = view.Rows.OfType<DiffUnifiedContentRow>().ToList();
        Assert.That(content.Count(r => r.Kind == DiffCellKind.Delete), Is.EqualTo(2));
        Assert.That(content.Count(r => r.Kind == DiffCellKind.Add), Is.EqualTo(4));

        var addBrush = As<SolidColorBrush>(add.ContentBackground);
        Assert.That(addBrush.Color, Is.EqualTo(Color.Parse("#dafbe1")));
    }

    [AvaloniaTest]
    public void DiffView_SwitchesRows_OnModeChange()
    {
        var view = new DiffView { DiffFile = CreateSampleFile() };

        Assert.That(view.Rows.Count, Is.EqualTo(12));
        view.ViewMode = DiffViewMode.Unified;
        Assert.That(view.Rows.Count, Is.EqualTo(14));
        Assert.That(view.Rows.OfType<DiffSplitRow>(), Is.Empty);
        view.ViewMode = DiffViewMode.Split;
        Assert.That(view.Rows.Count, Is.EqualTo(12));
        Assert.That(view.Rows.OfType<DiffUnifiedRow>(), Is.Empty);
    }

    [AvaloniaTest]
    public void DiffView_WordHighlights_FastDiffMode()
    {
        TemplateOptions.SetEnableFastDiffTemplate(true);
        var view = new DiffView { DiffFile = CreateSampleFile() };

        // The "return 1;" -> "return 2;" pair is the only change with equal add/delete counts
        // in its hunk, so it is the only one with word-level ranges (upstream pairing rule).
        var pair = As<DiffSplitContentRow>(view.Rows[9]);
        Assert.That(pair.Left.Highlights, Is.EqualTo(new[] { new DiffHighlight(11, 1) }));
        Assert.That(pair.Right.Highlights, Is.EqualTo(new[] { new DiffHighlight(11, 1) }));

        // Highlight brushes follow the line kind (upstream light values).
        Assert.That(As<SolidColorBrush>(pair.Left.HighlightBrush).Color, Is.EqualTo(Color.Parse("#ffcecb")));
        Assert.That(As<SolidColorBrush>(pair.Right.HighlightBrush).Color, Is.EqualTo(Color.Parse("#aceebb")));

        // Unpaired add lines and context rows carry no word-level highlights.
        Assert.That(As<DiffSplitContentRow>(view.Rows[4]).Right.Highlights, Is.Empty);
        Assert.That(As<DiffSplitContentRow>(view.Rows[4]).Right.HighlightBrush, Is.Null);
        Assert.That(As<DiffSplitContentRow>(view.Rows[1]).Left.Highlights, Is.Empty);
    }

    [AvaloniaTest]
    public void DiffView_WordHighlights_RelativeFallback()
    {
        TemplateOptions.SetEnableFastDiffTemplate(false);
        try
        {
            var view = new DiffView { DiffFile = CreateSampleFile() };
            var pair = As<DiffSplitContentRow>(view.Rows[9]);
            Assert.That(pair.Left.Highlights, Is.EqualTo(new[] { new DiffHighlight(11, 1) }));
            Assert.That(pair.Right.Highlights, Is.EqualTo(new[] { new DiffHighlight(11, 1) }));
        }
        finally
        {
            TemplateOptions.SetEnableFastDiffTemplate(true);
        }
    }

    [AvaloniaTest]
    public void DiffView_UnifiedWordHighlights()
    {
        TemplateOptions.SetEnableFastDiffTemplate(true);
        var view = new DiffView { DiffFile = CreateSampleFile(), ViewMode = DiffViewMode.Unified };

        Assert.That(As<DiffUnifiedContentRow>(view.Rows[10]).Highlights,
                    Is.EqualTo(new[] { new DiffHighlight(11, 1) }));
        Assert.That(As<DiffUnifiedContentRow>(view.Rows[11]).Highlights,
                    Is.EqualTo(new[] { new DiffHighlight(11, 1) }));
    }

    [AvaloniaTest]
    public void DiffSegmentText_ComputesHighlightRects()
    {
        const string line = "    return 1;";
        var layout = new TextLayout(line, new Typeface(FontFamily.Parse("Menlo, Consolas, monospace")), 14,
                                    Brushes.Black);

        var rects = DiffSegmentText.ComputeHighlightRects(layout, line.Length, [new DiffHighlight(11, 1)]).ToList();
        Assert.That(rects, Has.Count.EqualTo(1));
        var rect = rects[0];
        Assert.That(rect.X      > 0, Is.True);
        Assert.That(rect.Width  > 0, Is.True);
        Assert.That(rect.Right  <= layout.TextLines[0].WidthIncludingTrailingWhitespace + 0.01, Is.True);
        Assert.That(rect.Height > 0, Is.True);

        // Out-of-range and empty ranges produce no rectangles.
        Assert.That(DiffSegmentText.ComputeHighlightRects(layout, line.Length, [new DiffHighlight(20, 5)]), Is.Empty);
        Assert.That(DiffSegmentText.ComputeHighlightRects(layout, line.Length, [new DiffHighlight(5, 0)]), Is.Empty);
    }

    [AvaloniaTest]
    public void DiffSegmentText_MeasuresAndRenders()
    {
        var segment = new DiffSegmentText
        {
            Text           = "    return 1;",
            FontFamily     = FontFamily.Parse("Menlo, Consolas, monospace"),
            FontSize       = 14,
            Foreground     = Brushes.Black,
            Highlights     = [new DiffHighlight(11, 1)],
            HighlightBrush = Brushes.OrangeRed
        };
        var window = new Window { Content = segment };
        window.Show();
        segment.Measure(new Size(1000, 200));
        segment.Arrange(new Rect(0, 0, 1000, 200));

        Assert.That(segment.DesiredSize.Width  > 0, Is.True);
        Assert.That(segment.DesiredSize.Height > 0, Is.True);
    }
}
