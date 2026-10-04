using System.Text;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Presenters;
using Avalonia.Controls.Shapes;
using Avalonia.Headless;
using Avalonia.Headless.NUnit;
using Avalonia.Input;
using Avalonia.Input.Platform;
using Avalonia.Media;
using Avalonia.Media.TextFormatting;
using Avalonia.VisualTree;
using Banned.CodeDiff.Avalonia.Models;
using Banned.CodeDiff.Avalonia.Views;
using Banned.CodeDiff.Models;
using Banned.CodeDiff.Services;
using Banned.CodeDiff.Utils;
using NUnit.Framework;

namespace Banned.CodeDiff.Avalonia.Tests;

/// <summary>
/// Wrap-mode UI wiring (M6 batch 4, upstream diffViewWrap): long lines wrap at the view width
/// (upstream white-space pre-wrap), rows grow over several text lines, split rows keep both
/// sides the height of the taller one, word highlights and syntax segments fragment per
/// wrapped line, the selection overlay covers the wrapped height, and the virtualized panel
/// stays correct while scrolling variable-height rows. Wrap defaults to off — the other
/// fixtures' nowrap assertions are that path's regression guard.
/// </summary>
public class DiffWrapTests
{
    /// <summary>A ~343-character line: 30 repeated words plus a distinguishing tail.</summary>
    private static string LongLine(string tail) =>
        string.Join(" ", Enumerable.Repeat("prefixword", 30)) + " " + tail;

    /// <summary>
    /// A paste-only diff: three context rows around each delete/add pair, the paired lines
    /// ~343 characters so a narrow window wraps them over many text lines. With equal
    /// old/new counts per group the pairs get fast-diff word-level highlight ranges.
    /// </summary>
    private static DiffFile CreateWrapFile(int groups)
    {
        var count = groups * 8;
        var diff = new StringBuilder("--- a/wrap.txt\n+++ b/wrap.txt\n")
            .Append("@@ -1,").Append(count).Append(" +1,").Append(count).Append(" @@\n");

        for (var i = 0; i < groups; i++)
        {
            diff.Append(" context ").AppendLine($"{i:000}");
            diff.Append(" context ").AppendLine($"{i:000}");
            diff.Append(" context ").AppendLine($"{i:000}");
            diff.Append('-').AppendLine(LongLine($"old-tail-{i:000}"));
            diff.Append('+').AppendLine(LongLine($"new-tail-{i:000}"));
            diff.Append(" context ").AppendLine($"{i:000}");
            diff.Append(" context ").AppendLine($"{i:000}");
            diff.Append(" context ").AppendLine($"{i:000}");
        }

        var file = new DiffFile(oldFileName : "", oldFileContent : "", newFileName : "", newFileContent : "",
                                diffList    : [diff.ToString()]);
        file.Init();

        return file;
    }

    /// <summary>A split pair whose old side is long (wraps) and new side short (one line).</summary>
    private static DiffFile CreateAsymFile()
    {
        var diff = "--- a/asym.txt\n+++ b/asym.txt\n@@ -1,3 +1,3 @@\n ctx\n-" + LongLine("old-tail") +
                   "\n+short replacement\n ctx\n";
        var file = new DiffFile(oldFileName : "", oldFileContent : "", newFileName : "", newFileContent : "",
                                diffList    : [diff]);
        file.Init();

        return file;
    }

    /// <summary>
    /// 100-line file with two change hunks — the DiffExpandTests layout (rows[8] is the middle
    /// hunk placeholder; expanding it down reveals 40 rows).
    /// </summary>
    private static DiffFile CreateExpandableFile()
    {
        var oldLines = new List<string>();
        var newLines = new List<string>();

        for (var i = 1; i <= 40; i++)
        {
            oldLines.Add($"ctx {i:D3}");
            newLines.Add($"ctx {i:D3}");
        }

        oldLines.Add("change-me");
        newLines.Add("changed!");

        for (var i = 42; i <= 90; i++)
        {
            oldLines.Add($"ctx {i:D3}");
            newLines.Add($"ctx {i:D3}");
        }

        oldLines.Add("delete-me");
        newLines.Add("replaced-1");
        newLines.Add("replaced-2");

        for (var i = 92; i <= 100; i++)
        {
            oldLines.Add($"ctx {i:D3}");
            newLines.Add($"ctx {i:D3}");
        }

        const string diff = """
            --- a/sample.txt
            +++ b/sample.txt
            @@ -38,7 +38,7 @@
             ctx 038
             ctx 039
             ctx 040
            -change-me
            +changed!
             ctx 042
             ctx 043
             ctx 044
            @@ -88,7 +88,8 @@
             ctx 088
             ctx 089
             ctx 090
            -delete-me
            +replaced-1
            +replaced-2
             ctx 092
             ctx 093
             ctx 094
            """;

        var file = new DiffFile(oldFileName : "sample.txt", oldFileContent : string.Join("\n", oldLines) + "\n",
                                newFileName : "sample.txt", newFileContent : string.Join("\n", newLines) + "\n",
                                diffList    : [diff]);
        file.Init();
        file.BuildSplitDiffLines();

        return file;
    }

    private static T As<T>(object? value) where T : class
    {
        Assert.That(value, Is.TypeOf<T>());

        return (T)value!;
    }

    private static void RunLayoutPass(Window window)
    {
        Assert.That(window.GetLayoutManager(), Is.Not.Null);
        window.GetLayoutManager()!.ExecuteLayoutPass();
    }

    private static (Window Window, ScrollViewer Scroller, ItemsControl Items) ShownInView(DiffView view,
                                                                                          double width = 360,
                                                                                          double height = 240)
    {
        var window = new Window { Content = view, Width = width, Height = height };
        window.Show();
        RunLayoutPass(window);

        return (window,
                view.GetVisualDescendants().OfType<ScrollViewer>().Single(),
                view.GetVisualDescendants().OfType<ItemsControl>().Single());
    }

    /// <summary>Viewport-relative top edge of a realized row container.</summary>
    private static double ViewportTopOf(ItemsControl items, ScrollViewer scroller, object row)
    {
        var container = items.ContainerFromItem(row);

        Assert.That(container, Is.Not.Null, "row container must be realized");
        Assert.That(container!.TransformToVisual(scroller), Is.Not.Null);

        return container.TransformToVisual(scroller)!.Value.Transform(new Point()).Y;
    }

    /// <summary>Window-relative center of a template column of a realized row (DiffCopyTests).</summary>
    private static Point CellCenter(Window window, ItemsControl items, object row, int column)
    {
        var container = items.ContainerFromItem(row);

        Assert.That(container, Is.Not.Null, "row container must be realized");

        var grid = container!.GetVisualDescendants().OfType<Grid>().First();
        var x = grid.ColumnDefinitions.Take(column).Sum(cd => cd.ActualWidth) +
                grid.ColumnDefinitions[column].ActualWidth / 2;
        var point = grid.TranslatePoint(new Point(x, grid.Bounds.Height / 2), window);

        Assert.That(point, Is.Not.Null);

        return point!.Value;
    }

    private static string? ClipboardText(Window window) => window.Clipboard!.TryGetTextAsync().Result;

    private static DiffSegmentText NewSegment(string text)
    {
        return new DiffSegmentText
        {
            Text       = text,
            FontFamily = FontFamily.Parse("Menlo, Consolas, monospace"),
            FontSize   = 14,
            Foreground = Brushes.Black,
        };
    }

    // ---- DiffSegmentText: measure + layout ----

    [AvaloniaTest]
    public void SegmentText_NoWrap_MeasuresFullWidth_EvenUnderNarrowConstraint()
    {
        var segment = NewSegment(LongLine("tail"));
        var window = new Window { Content = segment };
        window.Show();

        segment.Measure(new Size(120, 500));

        // Avalonia clamps DesiredSize to the constraint — the unclamped width lives in the
        // layout: the nowrap line still measures the whole text on one line.
        Assert.That(segment.TextLineCount, Is.EqualTo(1));
        Assert.That(segment.CurrentLayout!.TextLines[0].WidthIncludingTrailingWhitespace,
                    Is.GreaterThan(120));
    }

    [AvaloniaTest]
    public void SegmentText_Wrap_LaysOutMultipleTextLines_AndRebuildsPerWidth()
    {
        var segment = NewSegment(LongLine("tail"));
        segment.Wrap = true;
        var window = new Window { Content = segment };
        window.Show();

        segment.Measure(new Size(120, 4000));

        Assert.That(segment.TextLineCount, Is.GreaterThan(3));
        Assert.That(segment.DesiredSize.Width, Is.LessThanOrEqualTo(120));

        var narrowHeight = segment.DesiredSize.Height;

        // Wider constraint: the cached layout rebuilds for the new width and unwraps.
        segment.Measure(new Size(double.PositiveInfinity, 4000));

        Assert.That(segment.TextLineCount, Is.EqualTo(1));
        Assert.That(segment.DesiredSize.Height, Is.LessThan(narrowHeight / 3));
    }

    [AvaloniaTest]
    public void WrappedLayout_HighlightRects_FragmentPerTextLine()
    {
        var text = LongLine("tail");
        var layout = new TextLayout(text, new Typeface(FontFamily.Parse("Menlo, Consolas, monospace")), 14,
                                    Brushes.Black, textWrapping: TextWrapping.Wrap, maxWidth: 120);

        Assert.That(layout.TextLines.Count, Is.GreaterThan(3));

        var line0 = layout.TextLines[0];
        var line1 = layout.TextLines[1];
        var secondLineStart = line1.FirstTextSourceIndex;

        // HitTestTextPosition reports the second line's own y offset — the fragment anchor.
        Assert.That(layout.HitTestTextPosition(secondLineStart).Y, Is.EqualTo(line0.Height).Within(0.01));

        // A highlight fully inside the second line sits on the second line's band (a single
        // character — the narrow headless layout fits only a handful of characters per line).
        var rects = DiffSegmentText.ComputeHighlightRects(
            layout, text.Length, [new DiffHighlight(secondLineStart + 1, 1)]).ToList();

        Assert.That(rects, Has.Count.EqualTo(1));
        Assert.That(rects[0].Y, Is.EqualTo(line0.Height).Within(0.01));
        Assert.That(rects[0].Height, Is.EqualTo(line1.Height).Within(0.01));

        // A range crossing the first line break fragments: the first piece runs to the first
        // line's end, the second starts at the next line's left edge.
        var spanning = DiffSegmentText.ComputeHighlightRects(
            layout, text.Length, [new DiffHighlight(2, secondLineStart)]).ToList();

        Assert.That(spanning, Has.Count.EqualTo(2));
        Assert.That(spanning[0].Y, Is.EqualTo(0).Within(0.01));
        Assert.That(spanning[0].Right, Is.EqualTo(line0.WidthIncludingTrailingWhitespace).Within(0.5));
        Assert.That(spanning[1].Y, Is.EqualTo(line0.Height).Within(0.01));
        Assert.That(spanning[1].X, Is.EqualTo(0).Within(0.01));
    }

    [AvaloniaTest]
    public void SegmentText_Wrap_SyntaxSegmentSplitsAtLineBreaks()
    {
        var text = LongLine("tail");
        var segment = NewSegment(text);
        segment.Wrap       = true;
        segment.SyntaxRuns = [new DiffSyntaxRun(0, text.Length, Brushes.Red)];
        var window = new Window { Content = segment };
        window.Show();

        segment.Measure(new Size(120, 4000));

        var layout = segment.CurrentLayout;

        Assert.That(layout, Is.Not.Null);
        Assert.That(layout!.TextLines.Count, Is.GreaterThan(3));

        // One run covering the whole text yields exactly one piece per wrapped line, each at
        // its own y offset (the first on the top line, the last below it).
        var pieces = segment.GetSyntaxLayouts(layout)!;

        Assert.That(pieces.Count, Is.EqualTo(layout.TextLines.Count));
        Assert.That(pieces[0].Y, Is.EqualTo(0).Within(0.01));
        Assert.That(pieces[0].X, Is.EqualTo(0).Within(0.01));
        Assert.That(pieces[^1].Y, Is.GreaterThan(0));
        Assert.That(pieces[^1].X, Is.EqualTo(0).Within(0.01));
    }

    // ---- DiffView: row heights and equal-height cells ----

    [AvaloniaTest]
    public void DiffView_Wrap_TogglesWrappedRowHeight()
    {
        var view = new DiffView { DiffFile = CreateWrapFile(1) };

        // Opt-in default: the existing hosts' rows stay one line tall.
        Assert.That(view.Wrap, Is.False);

        var (window, _, items) = ShownInView(view);
        var row = view.Rows.OfType<DiffSplitContentRow>().Single(r => r.Left.Kind == DiffCellKind.Delete);
        var container = items.ContainerFromItem(row)!;

        var nowrapHeight = container.Bounds.Height;

        Assert.That(nowrapHeight, Is.LessThan(30));

        view.Wrap = true;
        RunLayoutPass(window);

        Assert.That(container.Bounds.Height, Is.GreaterThan(nowrapHeight * 5), "the long line must wrap");

        view.Wrap = false;
        RunLayoutPass(window);

        Assert.That(container.Bounds.Height, Is.EqualTo(nowrapHeight).Within(0.5));
    }

    [AvaloniaTest]
    public void DiffView_SplitWrap_LeftAndRightCellsShareTheTallerHeight()
    {
        var view = new DiffView { DiffFile = CreateAsymFile(), Wrap = true };
        var (window, _, items) = ShownInView(view);

        var pair = view.Rows.OfType<DiffSplitContentRow>().Single(r => r.Left.Kind == DiffCellKind.Delete);
        var context = view.Rows.OfType<DiffSplitContentRow>().First(r => r.Left.Kind == DiffCellKind.Context);

        var container = items.ContainerFromItem(pair)!;
        var grid = container.GetVisualDescendants().OfType<Grid>().First(g => g.ColumnDefinitions.Count == 5);

        Border? Cell(int column) => grid.Children.OfType<Border>().FirstOrDefault(b => Grid.GetColumn(b) == column);

        var leftContent = Cell(1);
        var rightContent = Cell(4);

        Assert.That(leftContent, Is.Not.Null);
        Assert.That(rightContent, Is.Not.Null);

        // The left side wraps over many text lines while the right holds one short line — the
        // row (and both content cells, whose backgrounds must cover the full height) take the
        // taller side's height, like the upstream wrap view's synced heights.
        var singleLineHeight = items.ContainerFromItem(context)!.Bounds.Height;

        Assert.That(container.Bounds.Height, Is.GreaterThan(singleLineHeight * 5));
        Assert.That(leftContent!.Bounds.Height, Is.EqualTo(container.Bounds.Height).Within(0.5));
        Assert.That(rightContent!.Bounds.Height, Is.EqualTo(container.Bounds.Height).Within(0.5));
    }

    [AvaloniaTest]
    public void DiffView_UnifiedWrap_RowsGrow_AndNumberColumnsStayFixed()
    {
        var view = new DiffView { DiffFile = CreateWrapFile(1), ViewMode = DiffViewMode.Unified, Wrap = true };
        var (window, _, items) = ShownInView(view, width: 300);

        var delete = view.Rows.OfType<DiffUnifiedContentRow>().Single(r => r.Kind == DiffCellKind.Delete);
        var context = view.Rows.OfType<DiffUnifiedContentRow>().First(r => r.Kind == DiffCellKind.Context);

        var container = items.ContainerFromItem(delete)!;
        var grid = container.GetVisualDescendants().OfType<Grid>().First(g => g.ColumnDefinitions.Count == 3);

        var oldNumber = grid.Children.OfType<Border>().Single(b => Grid.GetColumn(b) == 0);
        var newNumber = grid.Children.OfType<Border>().Single(b => Grid.GetColumn(b) == 1);

        Assert.That(container.Bounds.Height,
                    Is.GreaterThan(items.ContainerFromItem(context)!.Bounds.Height * 5));

        // The number columns keep their resolved width — they never wrap.
        Assert.That(oldNumber.Bounds.Width, Is.EqualTo(view.NumberColumnWidth).Within(0.5));
        Assert.That(newNumber.Bounds.Width, Is.EqualTo(view.NumberColumnWidth).Within(0.5));
        Assert.That(oldNumber.Bounds.Height, Is.EqualTo(container.Bounds.Height).Within(0.5));
    }

    [AvaloniaTest]
    public void DiffView_Wrap_WordHighlightLandsOnTheWrappedLine()
    {
        TemplateOptions.SetEnableFastDiffTemplate(true);
        var view = new DiffView { DiffFile = CreateWrapFile(1), Wrap = true };
        var (window, _, items) = ShownInView(view);

        var row = view.Rows.OfType<DiffSplitContentRow>().Single(r => r.Left.Kind == DiffCellKind.Delete);

        Assert.That(row.Left.Highlights.Count, Is.EqualTo(1));

        // The differing tail sits ~330 characters into the line — deep in the wrapped rows.
        var segment = items.ContainerFromItem(row)!.GetVisualDescendants().OfType<DiffSegmentText>().First();
        var layout = segment.CurrentLayout;

        Assert.That(layout, Is.Not.Null);
        Assert.That(layout!.TextLines.Count, Is.GreaterThan(3));

        var rects = DiffSegmentText.ComputeHighlightRects(layout, row.Left.Text.Length, row.Left.Highlights).ToList();

        Assert.That(rects, Has.Count.EqualTo(1));
        Assert.That(rects[0].Y, Is.GreaterThan(layout.TextLines[0].Height * 2),
                    "the highlight must land on a wrapped (non-first) line");
    }

    // ---- selection + copy under wrap ----

    [AvaloniaTest]
    public void DiffView_Wrap_SelectionOverlay_CoversTheWrappedRowHeight()
    {
        var view = new DiffView { DiffFile = CreateWrapFile(1), Wrap = true, IsSelectionEnabled = true };
        var (window, _, items) = ShownInView(view);

        var row = view.Rows.OfType<DiffSplitContentRow>().Single(r => r.Left.Kind == DiffCellKind.Delete);
        var container = items.ContainerFromItem(row)!;
        var rowHeight = container.Bounds.Height;

        Assert.That(rowHeight, Is.GreaterThan(60));

        // Old line 4 is the delete row's line — select it (rows: ctx, ctx, ctx, delete, …).
        view.SetPreselectedLines(oldLines: [4]);
        RunLayoutPass(window);

        Assert.That(row.Left.IsSelected, Is.True);

        var overlays = container.GetVisualDescendants().OfType<Rectangle>().Where(r => r.IsVisible).ToList();

        Assert.That(overlays.Any(r => Math.Abs(r.Bounds.Height - rowHeight) < 1.0), Is.True,
                    "the overlay must span the whole wrapped height");
    }

    [AvaloniaTest]
    public void DiffView_Wrap_CopySelectionText_IsUnaffectedByWrapping()
    {
        var view = new DiffView { DiffFile = CreateWrapFile(1), Wrap = true, IsSelectionEnabled = true };
        var (window, _, items) = ShownInView(view, height: 760);

        // Old-number cell of the delete row (old line 4) — its rendered height spans many text
        // lines; the drag pipeline must still resolve it.
        window.MouseDown(CellCenter(window, items, view.Rows[3], 0), MouseButton.Left);
        window.MouseUp(CellCenter(window, items, view.Rows[3], 0), MouseButton.Left);

        Assert.That(view.CopySelectionAsync().Result, Is.True);

        // The copy is the model value — the full long line, not the wrapped render fragments.
        Assert.That(ClipboardText(window), Is.EqualTo(LongLine("old-tail-000")));
    }

    // ---- virtualization with variable row heights ----

    [AvaloniaTest]
    public void DiffView_Wrap_VirtualizationScrollsToBottom_LastRowVisible()
    {
        var view = new DiffView { DiffFile = CreateWrapFile(8), Wrap = true };
        var (window, scroller, items) = ShownInView(view);
        var panel = view.GetVisualDescendants().OfType<VirtualizingStackPanel>().Single();

        // The extent refines as wrapped heights realize; re-aim at the bottom until it settles.
        for (var pass = 0; pass < 8; pass++)
        {
            scroller.Offset = new Vector(0, scroller.Extent.Height);
            RunLayoutPass(window);
        }

        var lastRow = view.Rows[^1];
        var container = items.ContainerFromItem(lastRow);

        Assert.That(container, Is.Not.Null, "the last row must be realized at the bottom");
        Assert.That(panel.Children.Count, Is.LessThan(view.Rows.Count), "virtualization must stay active");

        var top = ViewportTopOf(items, scroller, lastRow);

        Assert.That(top, Is.GreaterThanOrEqualTo(-1));
        Assert.That(top, Is.LessThan(scroller.Viewport.Height));
        Assert.That(top + container!.Bounds.Height, Is.GreaterThan(scroller.Viewport.Height - 30),
                    "the last row's bottom edge must sit at the viewport bottom");
    }

    [AvaloniaTest]
    public void DiffView_Wrap_VirtualizationScrollsToMiddle_AndBackToTop()
    {
        var view = new DiffView { DiffFile = CreateWrapFile(8), Wrap = true };
        var (window, scroller, items) = ShownInView(view);
        var panel = view.GetVisualDescendants().OfType<VirtualizingStackPanel>().Single();

        for (var pass = 0; pass < 8; pass++)
        {
            scroller.Offset = new Vector(0, Math.Round(scroller.Extent.Height / 2));
            RunLayoutPass(window);
        }

        // A row intersects the viewport and only a slice of the rows is realized (a wrapped
        // row may cover the whole viewport, so the test is intersection, not top-edge).
        var viewportHeight = scroller.Viewport.Height;
        var visible = panel.Children.OfType<ContentPresenter>().Count(c =>
        {
            var top = c.TransformToVisual(scroller)!.Value.Transform(new Point()).Y;

            return top < viewportHeight - 5 && top + c.Bounds.Height > 5;
        });

        Assert.That(visible, Is.GreaterThanOrEqualTo(1), "a row must be visible mid-scroll");
        Assert.That(panel.Children.Count, Is.LessThan(view.Rows.Count));

        // Scrolling back to the top restores the first row.
        scroller.Offset = new Vector(0, 0);
        RunLayoutPass(window);

        Assert.That(items.ContainerFromItem(view.Rows[0]), Is.Not.Null);
        Assert.That(ViewportTopOf(items, scroller, view.Rows[0]), Is.EqualTo(0).Within(1));
    }

    // ---- hunk expansion under wrap ----

    [AvaloniaTest]
    public void DiffView_Wrap_ExpandHunk_KeepsViewportWithinBounds()
    {
        var view = new DiffView { DiffFile = CreateExpandableFile(), Wrap = true };
        var (window, scroller, items) = ShownInView(view, width: 300, height: 220);

        view.ExpandHunkDownCommand.Execute(As<DiffSplitHunkRow>(view.Rows[8]));
        RunLayoutPass(window);
        RunLayoutPass(window);

        Assert.That(view.Rows.Count, Is.EqualTo(58));

        // Anchoring under wrap uses neighboring row heights as estimates — assert the viewport
        // stays within the content instead of pixel geometry.
        Assert.That(double.IsFinite(scroller.Offset.Y), Is.True);
        Assert.That(scroller.Offset.Y, Is.GreaterThanOrEqualTo(0));
        Assert.That(scroller.Offset.Y, Is.LessThanOrEqualTo(scroller.Extent.Height));
        Assert.That(scroller.Extent.Height, Is.GreaterThan(scroller.Viewport.Height));
        Assert.That(items.ContainerFromItem(view.Rows[0]) ?? items.ContainerFromItem(view.Rows[1]), Is.Not.Null);
    }
}
