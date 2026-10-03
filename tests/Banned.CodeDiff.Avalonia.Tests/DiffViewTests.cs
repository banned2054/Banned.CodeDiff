using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Media;
using Avalonia.Media.TextFormatting;
using Avalonia.VisualTree;
using Banned.CodeDiff.Avalonia.Models;
using Banned.CodeDiff.Avalonia.Views;
using Banned.CodeDiff.Models;
using Banned.CodeDiff.Services;
using Xunit;

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
        var file = new DiffFile(oldFileName: "", oldFileContent: "", newFileName: "", newFileContent: "",
                                diffList: [Sample]);
        file.Init();
        file.BuildSplitDiffLines();
        return file;
    }

    [AvaloniaFact]
    public void DiffView_BuildsExpectedRows_FromSampleDiff()
    {
        var view = new DiffView { DiffFile = CreateSampleFile() };

        // 11 content rows + 2 hunk placeholder rows.
        Assert.Equal(13, view.Rows.Count);
        Assert.Equal(2, view.Rows.OfType<DiffSplitHunkRow>().Count());
        Assert.Equal(11, view.Rows.OfType<DiffSplitContentRow>().Count());

        var firstHunk = Assert.IsType<DiffSplitHunkRow>(view.Rows[0]);
        Assert.StartsWith("@@", firstHunk.HunkText);

        // Rows[0] is the hunk placeholder; content rows follow. Probe split index 3 pairs the
        // deleted and added Console.WriteLine lines, so it lands at Rows[4].
        var pair = Assert.IsType<DiffSplitContentRow>(view.Rows[4]);
        Assert.Equal(DiffCellKind.Delete, pair.Left.Kind);
        Assert.Equal(DiffCellKind.Add, pair.Right.Kind);

        // The add-only line (probe index 4 -> Rows[5]) has an empty left placeholder.
        var addOnly = Assert.IsType<DiffSplitContentRow>(view.Rows[5]);
        Assert.Equal(DiffCellKind.Empty, addOnly.Left.Kind);
        Assert.Equal(DiffCellKind.Add, addOnly.Right.Kind);
    }

    [AvaloniaFact]
    public void DiffView_AppliesControlTheme_And_InstantiatesTemplate()
    {
        var view = new DiffView { DiffFile = CreateSampleFile() };
        var window = new Window { Content = view };
        window.Show();

        // The ControlTheme (loaded through the consumer-style StyleInclude in TestApp) must have
        // produced a visual tree: a ScrollViewer with an ItemsControl inside.
        Assert.Contains(view.GetVisualDescendants(), d => d is ScrollViewer);
        var items = view.GetVisualDescendants().OfType<ItemsControl>().Single();
        Assert.Equal(13, items.Items.Count());
    }

    [AvaloniaFact]
    public void DiffView_UsesUpstreamLightPalette()
    {
        var view = new DiffView { DiffFile = CreateSampleFile() };

        var pair = Assert.IsType<DiffSplitContentRow>(view.Rows[4]);
        var addBrush = Assert.IsType<SolidColorBrush>(pair.Right.ContentBackground);
        var deleteBrush = Assert.IsType<SolidColorBrush>(pair.Left.ContentBackground);

        // Upstream git-diff-view light values: --diff-add-content-- / --diff-del-content--.
        Assert.Equal(Color.Parse("#dafbe1"), addBrush.Color);
        Assert.Equal(Color.Parse("#ffebe9"), deleteBrush.Color);
    }

    [AvaloniaFact]
    public void DiffView_ClearsRows_WhenFileRemoved()
    {
        var view = new DiffView { DiffFile = CreateSampleFile() };
        view.DiffFile = null;
        Assert.Empty(view.Rows);
    }

    [AvaloniaFact]
    public void DiffView_UnifiedMode_BuildsExpectedRows()
    {
        var view = new DiffView { DiffFile = CreateSampleFile(), ViewMode = DiffViewMode.Unified };

        // 13 content rows + 2 hunk placeholder rows; deletes and adds are separate rows.
        Assert.Equal(15, view.Rows.Count);
        Assert.Equal(2, view.Rows.OfType<DiffUnifiedHunkRow>().Count());
        Assert.Equal(13, view.Rows.OfType<DiffUnifiedContentRow>().Count());

        Assert.StartsWith("@@", Assert.IsType<DiffUnifiedHunkRow>(view.Rows[0]).HunkText);

        // Delete above add (probe unified indexes 3/4 -> visual rows 4/5 with the leading hunk row).
        var delete = Assert.IsType<DiffUnifiedContentRow>(view.Rows[4]);
        Assert.Equal(DiffCellKind.Delete, delete.Kind);
        Assert.Equal("4", delete.OldNumber);
        Assert.Null(delete.NewNumber);

        var add = Assert.IsType<DiffUnifiedContentRow>(view.Rows[5]);
        Assert.Equal(DiffCellKind.Add, add.Kind);
        Assert.Null(add.OldNumber);
        Assert.Equal("4", add.NewNumber);

        // Second hunk placeholder above unified index 8.
        Assert.IsType<DiffUnifiedHunkRow>(view.Rows[9]);

        // 2 deletes and 4 adds render as separate rows (the sample's Deletion/Addition counts).
        var content = view.Rows.OfType<DiffUnifiedContentRow>().ToList();
        Assert.Equal(2, content.Count(r => r.Kind == DiffCellKind.Delete));
        Assert.Equal(4, content.Count(r => r.Kind == DiffCellKind.Add));

        var addBrush = Assert.IsType<SolidColorBrush>(add.ContentBackground);
        Assert.Equal(Color.Parse("#dafbe1"), addBrush.Color);
    }

    [AvaloniaFact]
    public void DiffView_SwitchesRows_OnModeChange()
    {
        var view = new DiffView { DiffFile = CreateSampleFile() };

        Assert.Equal(13, view.Rows.Count);
        view.ViewMode = DiffViewMode.Unified;
        Assert.Equal(15, view.Rows.Count);
        Assert.Empty(view.Rows.OfType<DiffSplitRow>());
        view.ViewMode = DiffViewMode.Split;
        Assert.Equal(13, view.Rows.Count);
        Assert.Empty(view.Rows.OfType<DiffUnifiedRow>());
    }

    [AvaloniaFact]
    public void DiffView_WordHighlights_FastDiffMode()
    {
        TemplateOptions.SetEnableFastDiffTemplate(true);
        var view = new DiffView { DiffFile = CreateSampleFile() };

        // The "return 1;" -> "return 2;" pair is the only change with equal add/delete counts
        // in its hunk, so it is the only one with word-level ranges (upstream pairing rule).
        var pair = Assert.IsType<DiffSplitContentRow>(view.Rows[10]);
        Assert.Equal(new[] { new DiffHighlight(11, 1) }, pair.Left.Highlights);
        Assert.Equal(new[] { new DiffHighlight(11, 1) }, pair.Right.Highlights);

        // Highlight brushes follow the line kind (upstream light values).
        Assert.Equal(Color.Parse("#ffcecb"), Assert.IsType<SolidColorBrush>(pair.Left.HighlightBrush).Color);
        Assert.Equal(Color.Parse("#aceebb"), Assert.IsType<SolidColorBrush>(pair.Right.HighlightBrush).Color);

        // Unpaired add lines and context rows carry no word-level highlights.
        Assert.Empty(Assert.IsType<DiffSplitContentRow>(view.Rows[5]).Right.Highlights);
        Assert.Null(Assert.IsType<DiffSplitContentRow>(view.Rows[5]).Right.HighlightBrush);
        Assert.Empty(Assert.IsType<DiffSplitContentRow>(view.Rows[1]).Left.Highlights);
    }

    [AvaloniaFact]
    public void DiffView_WordHighlights_RelativeFallback()
    {
        TemplateOptions.SetEnableFastDiffTemplate(false);
        try
        {
            var view = new DiffView { DiffFile = CreateSampleFile() };
            var pair = Assert.IsType<DiffSplitContentRow>(view.Rows[10]);
            Assert.Equal(new[] { new DiffHighlight(11, 1) }, pair.Left.Highlights);
            Assert.Equal(new[] { new DiffHighlight(11, 1) }, pair.Right.Highlights);
        }
        finally
        {
            TemplateOptions.SetEnableFastDiffTemplate(true);
        }
    }

    [AvaloniaFact]
    public void DiffView_UnifiedWordHighlights()
    {
        TemplateOptions.SetEnableFastDiffTemplate(true);
        var view = new DiffView { DiffFile = CreateSampleFile(), ViewMode = DiffViewMode.Unified };

        Assert.Equal(new[] { new DiffHighlight(11, 1) }, Assert.IsType<DiffUnifiedContentRow>(view.Rows[11]).Highlights);
        Assert.Equal(new[] { new DiffHighlight(11, 1) }, Assert.IsType<DiffUnifiedContentRow>(view.Rows[12]).Highlights);
    }

    [AvaloniaFact]
    public void DiffSegmentText_ComputesHighlightRects()
    {
        const string line = "    return 1;";
        var layout = new TextLayout(line, new Typeface(FontFamily.Parse("Menlo, Consolas, monospace")), 14,
                                    Brushes.Black);

        var rect = Assert.Single(DiffSegmentText.ComputeHighlightRects(layout, line.Length, [new DiffHighlight(11, 1)]));
        Assert.True(rect.X > 0);
        Assert.True(rect.Width > 0);
        Assert.True(rect.Right <= layout.TextLines[0].WidthIncludingTrailingWhitespace + 0.01);
        Assert.True(rect.Height > 0);

        // Out-of-range and empty ranges produce no rectangles.
        Assert.Empty(DiffSegmentText.ComputeHighlightRects(layout, line.Length, [new DiffHighlight(20, 5)]));
        Assert.Empty(DiffSegmentText.ComputeHighlightRects(layout, line.Length, [new DiffHighlight(5, 0)]));
    }

    [AvaloniaFact]
    public void DiffSegmentText_MeasuresAndRenders()
    {
        var segment = new DiffSegmentText
        {
            Text = "    return 1;",
            FontFamily = FontFamily.Parse("Menlo, Consolas, monospace"),
            FontSize = 14,
            Foreground = Brushes.Black,
            Highlights = [new DiffHighlight(11, 1)],
            HighlightBrush = Brushes.OrangeRed,
        };
        var window = new Window { Content = segment };
        window.Show();
        segment.Measure(new Size(1000, 200));
        segment.Arrange(new Rect(0, 0, 1000, 200));

        Assert.True(segment.DesiredSize.Width > 0);
        Assert.True(segment.DesiredSize.Height > 0);
    }
}
