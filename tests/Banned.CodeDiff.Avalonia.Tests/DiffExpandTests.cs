using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless.NUnit;
using Avalonia.Media;
using Avalonia.VisualTree;
using Banned.CodeDiff.Avalonia.Models;
using Banned.CodeDiff.Avalonia.Views;
using Banned.CodeDiff.Services;
using NUnit.Framework;
using System.Diagnostics;
using System.Text;

namespace Banned.CodeDiff.Avalonia.Tests;

/// <summary>
///     Hunk expand/collapse UI wiring (M4): button placement per hunk position, the three expand
///     directions against the model state machine, template command wiring, virtualization, and the
///     large-diff performance baseline. The fixture supplies real old/new file contents — paste-only
///     diffs compose from the diff text and cannot expand.
/// </summary>
public class DiffExpandTests
{
    /// <summary>
    ///     100-line file with two change hunks: hidden [0,37) above the first hunk, hidden [44,87)
    ///     (43 lines, >= compose length) between them, and a 6-line hidden tail producing the
    ///     synthetic trailing strip keyed at SplitLineLength (101).
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

        var file = new DiffFile("sample.txt", string.Join("\n", oldLines) + "\n",
                                "sample.txt", string.Join("\n", newLines) + "\n",
                                [diff]);
        file.Init();
        file.BuildSplitDiffLines();
        file.BuildUnifiedDiffLines();

        return file;
    }

    private static T As<T>(object? value) where T : class
    {
        Assert.That(value, Is.TypeOf<T>());
        return (T)value!;
    }

    /// <summary>
    ///     Runs one layout pass: template application of nested controls is driven by the
    ///     layout manager, which headless Show() alone does not trigger.
    /// </summary>
    private static void RunLayoutPass(Window window)
    {
        Assert.That(window.GetLayoutManager(), Is.Not.Null);
        window.GetLayoutManager()!.ExecuteLayoutPass();
    }

    /// <summary>Viewport-relative top edge of a realized row container.</summary>
    private static double ViewportTopOf(ItemsControl items, ScrollViewer scroller, object row)
    {
        var container = items.ContainerFromItem(row);

        Assert.That(container, Is.Not.Null, "row container must be realized");
        Assert.That(container!.TransformToVisual(scroller), Is.Not.Null);

        return container.TransformToVisual(scroller)!.Value.Transform(new Point()).Y;
    }

    /// <summary>Shows the view in a short window (roughly ten rows) so the content scrolls.</summary>
    private static (Window Window, ScrollViewer Scroller, ItemsControl Items) ShownInView(DiffView view)
    {
        var window = new Window { Content = view, Width = 900, Height = 220 };
        window.Show();
        RunLayoutPass(window);

        return (window,
                view.GetVisualDescendants().OfType<ScrollViewer>().Single(),
                view.GetVisualDescendants().OfType<ItemsControl>().Single());
    }

    [AvaloniaTest]
    public void ExpandDown_KeepsClickedPlaceholderAnchored()
    {
        var view = new DiffView { DiffFile = CreateExpandableFile() };
        var (window, scroller, items) = ShownInView(view);
        var middle = As<DiffSplitHunkRow>(view.Rows[8]);

        // Scroll so the clicked placeholder sits two rows below the viewport top (the offset is 0
        // before, so the viewport top of a row equals its position in the scroll content).
        var rowHeight    = items.ContainerFromItem(view.Rows[7])!.Bounds.Height;
        var anchorY      = 2 * rowHeight;
        var offsetBefore = ViewportTopOf(items, scroller, middle) - anchorY;
        scroller.Offset = new Vector(0, offsetBefore);
        RunLayoutPass(window);

        Assert.That(ViewportTopOf(items, scroller, middle), Is.EqualTo(anchorY).Within(1));

        view.ExpandHunkDownCommand.Execute(middle);

        // The 40 revealed rows land above the placeholder, so the offset must follow them (once
        // the rebuild's extent change has been laid out) to keep the clicked row at its viewport
        // position instead of jumping 40 rows down.
        RunLayoutPass(window);
        Assert.That(scroller.Offset.Y, Is.EqualTo(offsetBefore + 40 * rowHeight).Within(0.5));

        RunLayoutPass(window);

        // The placeholder must be back at its viewport position (a few pixels of slack for the
        // VirtualizingStackPanel's estimated heights of unrealized rows).
        var shrunk = As<DiffSplitHunkRow>(view.Rows[48]);
        Assert.That(ViewportTopOf(items, scroller, shrunk), Is.EqualTo(anchorY).Within(6));
    }

    [AvaloniaTest]
    public void ExpandUp_OnFirstHunk_KeepsFollowingRowAnchored()
    {
        var view = new DiffView { DiffFile = CreateExpandableFile() };
        var (window, scroller, items) = ShownInView(view);

        // Expanding the first hunk removes its placeholder; the row that followed it (ctx 038)
        // becomes the anchor that must keep its viewport position.
        var top               = As<DiffSplitHunkRow>(view.Rows[0]);
        var following         = view.Rows[1];
        var rowHeight         = items.ContainerFromItem(following)!.Bounds.Height;
        var placeholderHeight = items.ContainerFromItem(top)!.Bounds.Height;
        var anchorY           = ViewportTopOf(items, scroller, following);

        Assert.That(scroller.Offset.Y, Is.EqualTo(0));
        Assert.That(anchorY, Is.EqualTo(placeholderHeight).Within(1));

        view.ExpandHunkUpCommand.Execute(top);

        // The 37 revealed rows land where the placeholder was — above the anchor row: the offset
        // must grow by their height minus the placeholder that disappeared (applied once the
        // rebuild's extent change has been laid out).
        RunLayoutPass(window);
        Assert.That(scroller.Offset.Y, Is.EqualTo(37 * rowHeight - placeholderHeight).Within(0.5));

        RunLayoutPass(window);

        var ctx038 = view.Rows.OfType<DiffSplitContentRow>().Single(r => r.Left.Text == "ctx 038");
        Assert.That(ViewportTopOf(items, scroller, ctx038), Is.EqualTo(anchorY).Within(6));
    }

    [AvaloniaTest]
    public void ExpandUp_OnMiddleHunk_KeepsOffsetStable()
    {
        var view = new DiffView { DiffFile = CreateExpandableFile() };
        var (window, scroller, items) = ShownInView(view);

        var rowHeight    = items.ContainerFromItem(view.Rows[7])!.Bounds.Height;
        var anchorY      = 2 * rowHeight;
        var offsetBefore = ViewportTopOf(items, scroller, view.Rows[8]) - anchorY;
        scroller.Offset = new Vector(0, offsetBefore);
        RunLayoutPass(window);

        // The middle placeholder survives an up expansion in place (its rows above never move),
        // so the offset must stay untouched.
        view.ExpandHunkUpCommand.Execute(As<DiffSplitHunkRow>(view.Rows[8]));

        Assert.That(scroller.Offset.Y, Is.EqualTo(offsetBefore).Within(0.5));

        RunLayoutPass(window);

        var moved = As<DiffSplitHunkRow>(view.Rows[8]);
        Assert.That(moved.HunkIndex, Is.EqualTo(47));
        Assert.That(ViewportTopOf(items, scroller, moved), Is.EqualTo(anchorY).Within(6));
    }

    [AvaloniaTest]
    public void ExpandableFile_RendersHunkRowsWithExpectedButtons()
    {
        var view = new DiffView { DiffFile = CreateExpandableFile() };

        // 1 top hunk + 7 content + 1 middle hunk + 8 content + 1 trailing strip.
        Assert.That(view.Rows.Count, Is.EqualTo(18));

        var top = As<DiffSplitHunkRow>(view.Rows[0]);
        Assert.That(top.IsExpandEnabled, Is.True);
        Assert.That(top.HunkIndex, Is.EqualTo(37));
        Assert.That(top.CanExpandUp, Is.True); // first hunk: single Expand Up
        Assert.That(top.CanExpandDown, Is.False);
        Assert.That(top.CanExpandAll, Is.False);

        var middle = As<DiffSplitHunkRow>(view.Rows[8]);
        Assert.That(middle.HunkIndex, Is.EqualTo(87));
        Assert.That(middle.CanExpandUp, Is.True); // 43 hidden lines: stacked Down+Up pair
        Assert.That(middle.CanExpandDown, Is.True);
        Assert.That(middle.CanExpandAll, Is.False);

        var trailing = As<DiffSplitHunkRow>(view.Rows[17]);
        Assert.That(trailing.HunkIndex, Is.EqualTo(101)); // synthetic hunk keyed at SplitLineLength
        Assert.That(trailing.CanExpandDown, Is.True);     // last strip: single Expand Down
        Assert.That(trailing.CanExpandUp, Is.False);
        Assert.That(trailing.CanExpandAll, Is.False);
        Assert.That(trailing.HunkText, Is.Empty);
    }

    [AvaloniaTest]
    public void ExpandDown_RevealsFortyLines_AndShrinksHunkRow()
    {
        var view   = new DiffView { DiffFile = CreateExpandableFile() };
        var middle = As<DiffSplitHunkRow>(view.Rows[8]);

        view.ExpandHunkDownCommand.Execute(middle);

        Assert.That(view.Rows.Count, Is.EqualTo(58));

        // The 40 hidden rows closest to the previous context become visible, starting at ctx 045.
        var first = As<DiffSplitContentRow>(view.Rows[8]);
        Assert.That(first.Left.Number, Is.EqualTo("45"));
        Assert.That(first.Left.Text, Is.EqualTo("ctx 045"));
        Assert.That(first.Left.Kind, Is.EqualTo(DiffCellKind.Expand));
        Assert.That(first.Right.Number, Is.EqualTo("45"));

        // 3 hidden rows remain (< compose length) — the placeholder turns into a single Expand All.
        var shrunk = As<DiffSplitHunkRow>(view.Rows[48]);
        Assert.That(shrunk.HunkIndex, Is.EqualTo(87));
        Assert.That(shrunk.CanExpandAll, Is.True);
        Assert.That(shrunk.CanExpandUp || shrunk.CanExpandDown, Is.False);

        // The trailing strip moved down by the 40 revealed rows.
        Assert.That(view.Rows[57], Is.TypeOf<DiffSplitHunkRow>());
    }

    [AvaloniaTest]
    public void RevealedRows_UseUpstreamExpandPalette()
    {
        var view = new DiffView { DiffFile = CreateExpandableFile() };
        view.ExpandHunkDownCommand.Execute(As<DiffSplitHunkRow>(view.Rows[8]));

        // Revealed raw rows (no DiffLine) carry --diff-expand-content-- (#fafafa light) instead of
        // the plain-context background; the number cell keeps --diff-expand-lineNumber-- (#fafafa).
        var revealed = As<DiffSplitContentRow>(view.Rows[8]);
        Assert.That(((ISolidColorBrush)revealed.Left.ContentBackground).Color, Is.EqualTo(Color.Parse("#fafafa")));
        Assert.That(((ISolidColorBrush)revealed.Right.ContentBackground).Color, Is.EqualTo(Color.Parse("#fafafa")));
        Assert.That(((ISolidColorBrush)revealed.Left.NumberBackground).Color, Is.EqualTo(Color.Parse("#fafafa")));

        // In-diff context rows (with a DiffLine) keep --diff-plain-content-- (#ffffff light).
        var inDiff = As<DiffSplitContentRow>(view.Rows[1]); // ctx 038, part of the diff text
        Assert.That(inDiff.Left.Kind, Is.EqualTo(DiffCellKind.Context));
        Assert.That(((ISolidColorBrush)inDiff.Left.ContentBackground).Color, Is.EqualTo(Color.Parse("#ffffff")));

        // Unified revealed rows share the expand palette.
        var unified = new DiffView { DiffFile = CreateExpandableFile(), ViewMode = DiffViewMode.Unified };
        unified.ExpandHunkDownCommand.Execute(As<DiffUnifiedHunkRow>(unified.Rows[9]));
        var unifiedRevealed = As<DiffUnifiedContentRow>(unified.Rows[9]);
        Assert.That(unifiedRevealed.Kind, Is.EqualTo(DiffCellKind.Expand));
        Assert.That(((ISolidColorBrush)unifiedRevealed.ContentBackground).Color, Is.EqualTo(Color.Parse("#fafafa")));
        Assert.That(((ISolidColorBrush)unifiedRevealed.NumberBackground).Color, Is.EqualTo(Color.Parse("#fafafa")));
    }

    [AvaloniaTest]
    public void ExpandUp_RevealsFortyLines_AndMovesHunkRow()
    {
        var view   = new DiffView { DiffFile = CreateExpandableFile() };
        var middle = As<DiffSplitHunkRow>(view.Rows[8]);

        view.ExpandHunkUpCommand.Execute(middle);

        Assert.That(view.Rows.Count, Is.EqualTo(58));

        // Up reveals the 40 rows closest to the header; the placeholder moves up to row 47 and
        // now sits directly after the first hunk's content, hiding only 3 lines.
        var moved = As<DiffSplitHunkRow>(view.Rows[8]);
        Assert.That(moved.HunkIndex, Is.EqualTo(47));
        Assert.That(moved.CanExpandAll, Is.True);

        var first = As<DiffSplitContentRow>(view.Rows[9]);
        Assert.That(first.Left.Number, Is.EqualTo("48"));
        Assert.That(first.Left.Text, Is.EqualTo("ctx 048"));
    }

    [AvaloniaTest]
    public void ExpandAll_RemovesHunkRow()
    {
        var view   = new DiffView { DiffFile = CreateExpandableFile() };
        var middle = As<DiffSplitHunkRow>(view.Rows[8]);

        view.ExpandHunkAllCommand.Execute(middle);

        // All 43 hidden rows revealed; the placeholder row itself disappears.
        Assert.That(view.Rows.Count, Is.EqualTo(60));
        Assert.That(view.Rows.OfType<DiffSplitHunkRow>().Count(), Is.EqualTo(2)); // top + trailing
        Assert.That(As<DiffSplitContentRow>(view.Rows[8]).Left.Number, Is.EqualTo("45"));
        Assert.That(As<DiffSplitContentRow>(view.Rows[50]).Left.Number, Is.EqualTo("87"));
    }

    [AvaloniaTest]
    public void ExpandUp_OnFirstHunk_RevealsWholeTop()
    {
        var view = new DiffView { DiffFile = CreateExpandableFile() };

        view.ExpandHunkUpCommand.Execute(As<DiffSplitHunkRow>(view.Rows[0]));

        // The 37 hidden rows above the first hunk; the placeholder disappears entirely.
        Assert.That(view.Rows.Count, Is.EqualTo(54));
        Assert.That(view.Rows[0], Is.TypeOf<DiffSplitContentRow>());
        Assert.That(As<DiffSplitContentRow>(view.Rows[0]).Left.Number, Is.EqualTo("1"));
        Assert.That(As<DiffSplitContentRow>(view.Rows[0]).Left.Text, Is.EqualTo("ctx 001"));
    }

    [AvaloniaTest]
    public void ExpandDown_OnTrailingStrip_RemovesIt()
    {
        var view = new DiffView { DiffFile = CreateExpandableFile() };

        view.ExpandHunkDownCommand.Execute(As<DiffSplitHunkRow>(view.Rows[17]));

        // The 6 hidden rows at the file tail; the strip disappears and ctx 100 is the last row.
        Assert.That(view.Rows.Count, Is.EqualTo(23));
        Assert.That(view.Rows.OfType<DiffSplitHunkRow>().Count(), Is.EqualTo(2));
        var last = As<DiffSplitContentRow>(view.Rows[^1]);
        Assert.That(last.Left.Number, Is.EqualTo("100"));
        Assert.That(last.Left.Text, Is.EqualTo("ctx 100"));
    }

    [AvaloniaTest]
    public void AllCollapse_RestoresCollapsedView()
    {
        var view = new DiffView { DiffFile = CreateExpandableFile() };
        view.DiffFile!.OnAllExpand(ExpandViewMode.Split);
        Assert.That(view.Rows.Count, Is.EqualTo(101)); // everything visible

        view.DiffFile.OnAllCollapse(ExpandViewMode.Split);

        Assert.That(view.Rows.Count, Is.EqualTo(18));
        Assert.That(view.DiffFile.HasSomeLineCollapsed, Is.True);
    }

    [AvaloniaTest]
    public void UnifiedMode_ExpandsDown()
    {
        var view = new DiffView { DiffFile = CreateExpandableFile(), ViewMode = DiffViewMode.Unified };

        // 1 top hunk + 8 content + 1 middle hunk + 9 content + 1 trailing strip. The unified
        // union walk shifts the middle hunk key to 88 (one row ahead of the split model).
        Assert.That(view.Rows.Count, Is.EqualTo(20));
        var middle = As<DiffUnifiedHunkRow>(view.Rows[9]);
        Assert.That(middle.HunkIndex, Is.EqualTo(88));
        Assert.That(middle.CanExpandDown, Is.True);

        view.ExpandHunkDownCommand.Execute(middle);

        Assert.That(view.Rows.Count, Is.EqualTo(60));
        var first = As<DiffUnifiedContentRow>(view.Rows[9]);
        Assert.That(first.OldNumber, Is.EqualTo("45"));
        Assert.That(first.NewNumber, Is.EqualTo("45"));
        Assert.That(first.Text, Is.EqualTo("ctx 045"));
        Assert.That(view.Rows[49], Is.TypeOf<DiffUnifiedHunkRow>());
    }

    [AvaloniaTest]
    public void HunkRowButtons_AreWiredThroughTheTemplate()
    {
        var view   = new DiffView { DiffFile = CreateExpandableFile() };
        var window = new Window { Content    = view, Width = 900, Height = 600 };
        window.Show();

        // Headless Show() does not run a layout pass; template application of nested controls
        // is driven by the layout manager, so execute one explicitly.
        window.Show();
        RunLayoutPass(window);

        // Top hunk (1: up) + middle hunk (2: down/up) + trailing strip (1: down).
        var buttons = view.GetVisualDescendants().OfType<Button>()
                          .Where(b => b.Classes.Contains("diffExpand") && b.IsVisible).ToList();
        Assert.That(buttons.Count, Is.EqualTo(4));

        // Executing the middle hunk's bound Down button expands through the template wiring.
        var middleButton = buttons.Single(b => (b.CommandParameter as DiffSplitHunkRow)?.HunkIndex == 87 &&
                                               Equals(b.Command, view.ExpandHunkDownCommand));
        middleButton.Command!.Execute(middleButton.CommandParameter);
        Assert.That(view.Rows.Count, Is.EqualTo(58));
    }

    [AvaloniaTest]
    public void PureDiffHunkRow_HasNoButtons()
    {
        const string pureDiff = """
            --- a/f.txt
            +++ b/f.txt
            @@ -1,3 +1,3 @@
             a
            -b
            +c
             d
            @@ -10,3 +10,4 @@
             e
            -f
            +g
            +h
             i
            """;
        var file = new DiffFile("", "", "", "",
                                [pureDiff]);
        file.Init();
        file.BuildSplitDiffLines();

        var view   = new DiffView { DiffFile = file };
        var window = new Window { Content    = view, Width = 900, Height = 600 };
        window.Show();
        RunLayoutPass(window);

        var hunk = view.Rows.OfType<DiffSplitHunkRow>().Single();
        Assert.That(hunk.IsExpandEnabled, Is.False); // composed from diff-only text
        Assert.That(hunk.CanExpandUp || hunk.CanExpandDown || hunk.CanExpandAll, Is.False);
        Assert.That(view.GetVisualDescendants().OfType<Button>()
                        .Count(b => b.Classes.Contains("diffExpand") && b.IsVisible), Is.EqualTo(0));
    }

    /// <summary>
    ///     M4 performance baseline: a ~10k-line model with 98 collapsed hunks must build fast and,
    ///     through VirtualizingStackPanel, realize only the visible slice of containers.
    /// </summary>
    [AvaloniaTest]
    public void LargeDiff_VirtualizesRows_AndMeasuresBaseline()
    {
        var (oldContent, newContent, diff) = CreateLargeSample(98, 100);

        var sw = Stopwatch.StartNew();
        var file = new DiffFile("big.txt", oldContent,
                                "big.txt", newContent, [diff]);
        file.Init();
        file.BuildSplitDiffLines();
        sw.Stop();
        TestContext.Out
                   .WriteLine($"[perf] model init+build: {sw.ElapsedMilliseconds} ms, split rows {file.SplitLineLength}");
        Assert.That(file.SplitLineLength, Is.GreaterThan(9000));
        Assert.That(file.HasSomeLineCollapsed, Is.True);

        sw.Restart();
        var view    = new DiffView { DiffFile = file };
        var visible = view.Rows.Count;
        sw.Stop();
        TestContext.Out.WriteLine($"[perf] row build: {sw.ElapsedMilliseconds} ms, visible rows {visible}");

        // Collapsed: each cluster renders 7 content rows + 1 hunk row, plus the trailing strip.
        Assert.That(visible, Is.EqualTo(98 * 8 + 1));

        sw.Restart();
        var window = new Window { Content = view, Width = 800, Height = 600 };
        window.Show();
        RunLayoutPass(window);
        sw.Stop();
        TestContext.Out.WriteLine($"[perf] template+first layout: {sw.ElapsedMilliseconds} ms");

        var panel = view.GetVisualDescendants().OfType<VirtualizingStackPanel>().Single();
        TestContext.Out.WriteLine($"[perf] realized containers: {panel.Children.Count} / {visible} rows");
        Assert.That(panel.Children.Count, Is.LessThan(100), "virtualization must not realize every row");
    }

    /// <summary>One modify (−1/+1) per cluster with 3-line contexts, plus a 120-line tail.</summary>
    private static (string OldContent, string NewContent, string DiffText) CreateLargeSample(int clusters, int gap)
    {
        var oldLines  = new List<string>();
        var newLines  = new List<string>();
        var diff      = new StringBuilder("--- a/big.txt\n+++ b/big.txt\n");
        var oldNumber = 1;
        var newNumber = 1;

        for (var cluster = 0; cluster < clusters; cluster++)
        {
            for (var k = 0; k < gap; k++)
            {
                var text = $"ctx {oldNumber:D5}";
                oldLines.Add(text);
                newLines.Add(text);
                oldNumber++;
                newNumber++;
            }

            var oldStart = oldNumber - 3;

            diff.Append("@@ -").Append(oldStart).Append(",7 +").Append(oldStart).AppendLine(",7 @@");

            for (var k = 3; k > 0; k--) diff.Append(' ').AppendLine($"ctx {oldNumber - k:D5}");

            diff.Append('-').AppendLine($"removed {oldNumber:D5}");
            oldLines.Add($"removed {oldNumber:D5}");
            oldNumber++;
            diff.Append('+').AppendLine($"added {newNumber:D5}");
            newLines.Add($"added {newNumber:D5}");
            newNumber++;

            for (var k = 0; k < 3; k++)
            {
                var text = $"ctx {oldNumber:D5}";
                diff.Append(' ').AppendLine(text);
                oldLines.Add(text);
                newLines.Add(text);
                oldNumber++;
                newNumber++;
            }
        }

        for (var k = 0; k < 120; k++)
        {
            var text = $"ctx {oldNumber:D5}";
            oldLines.Add(text);
            newLines.Add(text);
            oldNumber++;
            newNumber++;
        }

        return (string.Join("\n", oldLines) + "\n", string.Join("\n", newLines) + "\n", diff.ToString());
    }
}
