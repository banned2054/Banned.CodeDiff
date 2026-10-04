using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.NUnit;
using Avalonia.Input;
using Avalonia.VisualTree;
using Banned.CodeDiff.Avalonia.Models;
using Banned.CodeDiff.Avalonia.Views;
using Banned.CodeDiff.Models;
using Banned.CodeDiff.Services;
using NUnit.Framework;

namespace Banned.CodeDiff.Avalonia.Tests;

/// <summary>
///     Line-selection UI wiring (M6 batch 2, upstream multiSelect): drag over line-number cells
///     selects ranges through the pointer-capture pipeline (headless MouseDown/Move/Up drive real
///     routed events), highlights covered rows, fires the public events, and re-applies the
///     highlight when an expansion reveals hidden members of the selection.
/// </summary>
public class DiffSelectionTests
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
        file.BuildUnifiedDiffLines();

        return file;
    }

    /// <summary>
    ///     100-line file with two change hunks — hidden ranges [1,37], [45,87], [96,100] old/new;
    ///     the middle placeholder is <c>view.Rows[8]</c> (see DiffExpandTests for the layout).
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

    private static void RunLayoutPass(Window window)
    {
        Assert.That(window.GetLayoutManager(), Is.Not.Null);
        window.GetLayoutManager()!.ExecuteLayoutPass();
    }

    // Template column indexes: split = old-num(0), old-content(1), splitter(2), new-num(3),
    // new-content(4); unified = old-num(0), new-num(1), content(2).
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

    private static (Window Window, ItemsControl Items) ShownInView(DiffView view)
    {
        var window = new Window { Content = view, Width = 900, Height = 600 };
        window.Show();
        RunLayoutPass(window);

        return (window, view.GetVisualDescendants().OfType<ItemsControl>().Single());
    }

    private static DiffSplitContentRow SplitRow(DiffView view, int index)
    {
        return As<DiffSplitContentRow>(view.Rows[index]);
    }

    [AvaloniaTest]
    public void Selection_IsDisabledByDefault()
    {
        var view = new DiffView { DiffFile = CreateSampleFile() };
        var (window, items) = ShownInView(view);
        var changed   = 0;
        var completed = 0;
        view.SelectionChanged   += (_, _) => changed++;
        view.SelectionCompleted += (_, _) => completed++;

        window.MouseDown(CellCenter(window, items, view.Rows[1], 3), MouseButton.Left);
        window.MouseMove(CellCenter(window, items, view.Rows[3], 3));
        window.MouseUp(CellCenter(window, items, view.Rows[3], 3), MouseButton.Left);

        Assert.That(changed, Is.EqualTo(0));
        Assert.That(completed, Is.EqualTo(0));
        Assert.That(view.GetSelectionResult(), Is.Null);
        Assert.That(view.Rows.OfType<DiffSplitContentRow>().Select(r => r.Left.IsSelected || r.Right.IsSelected),
                    Is.All.False);
    }

    [AvaloniaTest]
    public void Split_DragOverNewNumbers_SelectsRangeAndFlagsCells()
    {
        var view = new DiffView { DiffFile = CreateSampleFile(), IsSelectionEnabled = true };
        var (window, items) = ShownInView(view);

        var                changes   = new List<MultiSelectRange?>();
        MultiSelectResult? completed = null;
        view.SelectionChanged   += (_, e) => changes.Add(e.Range);
        view.SelectionCompleted += (_, e) => completed = e.Result;

        // Press new-number of row 1 (new line 2), drag over the content of row 3 (a hover over
        // the line content extends the range — upstream getNumberHolderElement_Split), release.
        window.MouseDown(CellCenter(window, items, view.Rows[1], 3), MouseButton.Left);
        window.MouseMove(CellCenter(window, items, view.Rows[2], 3));
        window.MouseMove(CellCenter(window, items, view.Rows[3], 4));
        window.MouseUp(CellCenter(window, items, view.Rows[3], 4), MouseButton.Left);

        Assert.That(completed, Is.Not.Null);
        Assert.That(completed!.Range, Is.EqualTo(new MultiSelectRange(SplitSide.New, 2, 4)));
        Assert.That(completed.Lines.Select(l => l.LineNumber), Is.EqualTo(new[] { 2, 3, 4 }));

        // Press and both moves each reported a change (the manager fires on every update).
        Assert.That(changes, Has.Count.EqualTo(3));
        Assert.That(changes[0], Is.EqualTo(new MultiSelectRange(SplitSide.New, 2, 2)));

        // Rows 1-2 are context lines — both sides highlighted; rows 3's new side is an add —
        // only the selected side.
        Assert.That(SplitRow(view, 1).Left.IsSelected && SplitRow(view, 1).Right.IsSelected, Is.True);
        Assert.That(SplitRow(view, 2).Left.IsSelected && SplitRow(view, 2).Right.IsSelected, Is.True);
        Assert.That(SplitRow(view, 3).Right.IsSelected, Is.True);
        Assert.That(SplitRow(view, 3).Left.IsSelected, Is.False);
        Assert.That(SplitRow(view, 0).Left.IsSelected || SplitRow(view, 0).Right.IsSelected, Is.False);

        // The completed selection persists (highlight kept after release) and stays queryable.
        Assert.That(view.GetSelectionResult()!.Range, Is.EqualTo(new MultiSelectRange(SplitSide.New, 2, 4)));
        Assert.That(view.GetSelectionState().IsSelecting, Is.False);
    }

    [AvaloniaTest]
    public void Split_DragOverOldNumbers_FlagsOnlyOldSideForChanges()
    {
        var view = new DiffView { DiffFile = CreateSampleFile(), IsSelectionEnabled = true };
        var (window, items) = ShownInView(view);
        MultiSelectResult? completed = null;
        view.SelectionCompleted += (_, e) => completed = e.Result;

        // Old-number of row 3 (old 4, the delete) to old-number of row 6 (old 6, context).
        window.MouseDown(CellCenter(window, items, view.Rows[3], 0), MouseButton.Left);
        window.MouseMove(CellCenter(window, items, view.Rows[6], 0));
        window.MouseUp(CellCenter(window, items, view.Rows[6], 0), MouseButton.Left);

        Assert.That(completed!.Range, Is.EqualTo(new MultiSelectRange(SplitSide.Old, 4, 6)));

        // Row 3's old side is a delete — only the old cell; row 4 has no old line at all; rows
        // 5-6 are context — both sides.
        Assert.That(SplitRow(view, 3).Left.IsSelected, Is.True);
        Assert.That(SplitRow(view, 3).Right.IsSelected, Is.False);
        Assert.That(SplitRow(view, 4).Left.IsSelected || SplitRow(view, 4).Right.IsSelected, Is.False);
        Assert.That(SplitRow(view, 5).Left.IsSelected && SplitRow(view, 5).Right.IsSelected, Is.True);
        Assert.That(SplitRow(view, 6).Left.IsSelected && SplitRow(view, 6).Right.IsSelected, Is.True);
    }

    [AvaloniaTest]
    public void Split_DragUpwards_NormalizesRange()
    {
        var view = new DiffView { DiffFile = CreateSampleFile(), IsSelectionEnabled = true };
        var (window, items) = ShownInView(view);
        MultiSelectResult? completed = null;
        view.SelectionCompleted += (_, e) => completed = e.Result;

        window.MouseDown(CellCenter(window, items, view.Rows[3], 3), MouseButton.Left);
        window.MouseMove(CellCenter(window, items, view.Rows[1], 3));
        window.MouseUp(CellCenter(window, items, view.Rows[1], 3), MouseButton.Left);

        Assert.That(completed!.Range, Is.EqualTo(new MultiSelectRange(SplitSide.New, 2, 4)));
    }

    [AvaloniaTest]
    public void Split_PressOnContentOrHunkRow_DoesNotStartSelection()
    {
        var view = new DiffView { DiffFile = CreateSampleFile(), IsSelectionEnabled = true };
        var (window, items) = ShownInView(view);
        var changed = 0;
        view.SelectionChanged += (_, _) => changed++;

        // Content column (split column 1) is not a valid start point — only number cells are.
        window.MouseDown(CellCenter(window, items, view.Rows[1], 1), MouseButton.Left);
        window.MouseMove(CellCenter(window, items, view.Rows[3], 1));
        window.MouseUp(CellCenter(window, items, view.Rows[3], 1), MouseButton.Left);
        Assert.That(changed, Is.EqualTo(0));

        // The hunk placeholder row carries no line numbers — no selection there either.
        window.MouseDown(CellCenter(window, items, view.Rows[7], 0), MouseButton.Left);
        window.MouseUp(CellCenter(window, items, view.Rows[7], 0), MouseButton.Left);
        Assert.That(changed, Is.EqualTo(0));
        Assert.That(view.GetSelectionResult(), Is.Null);
    }

    [AvaloniaTest]
    public void Split_ReleaseOutsideTheControl_StillCompletes()
    {
        var view = new DiffView { DiffFile = CreateSampleFile(), IsSelectionEnabled = true };
        var (window, items) = ShownInView(view);
        MultiSelectResult? completed = null;
        view.SelectionCompleted += (_, e) => completed = e.Result;

        // The pointer capture replaces the upstream document-level mouseup: releasing far
        // outside the window still ends the selection.
        window.MouseDown(CellCenter(window, items, view.Rows[1], 3), MouseButton.Left);
        window.MouseMove(CellCenter(window, items, view.Rows[3], 3));
        window.MouseUp(new Point(-200, -200), MouseButton.Left);

        Assert.That(completed, Is.Not.Null);
        Assert.That(completed!.Range, Is.EqualTo(new MultiSelectRange(SplitSide.New, 2, 4)));
    }

    [AvaloniaTest]
    public void Split_NewDragClearsPreviousHighlight()
    {
        var view = new DiffView { DiffFile = CreateSampleFile(), IsSelectionEnabled = true };
        var (window, items) = ShownInView(view);

        window.MouseDown(CellCenter(window, items, view.Rows[1], 3), MouseButton.Left);
        window.MouseMove(CellCenter(window, items, view.Rows[3], 3));
        window.MouseUp(CellCenter(window, items, view.Rows[3], 3), MouseButton.Left);

        Assert.That(SplitRow(view, 2).Right.IsSelected, Is.True);

        // Starting the next drag drops the persisted previous selection (upstream wrapper
        // updateMultiResult(undefined) on isSelecting) — row 6 (new 7) only.
        window.MouseDown(CellCenter(window, items, view.Rows[6], 3), MouseButton.Left);
        window.MouseUp(CellCenter(window, items, view.Rows[6], 3), MouseButton.Left);

        Assert.That(SplitRow(view, 2).Right.IsSelected, Is.False);
        Assert.That(SplitRow(view, 6).Right.IsSelected, Is.True);
        Assert.That(view.GetSelectionResult()!.Range, Is.EqualTo(new MultiSelectRange(SplitSide.New, 7, 7)));
    }

    [AvaloniaTest]
    public void Split_ClearSelection_RemovesHighlightsAndNotifies()
    {
        var view = new DiffView { DiffFile = CreateSampleFile(), IsSelectionEnabled = true };
        var (window, items) = ShownInView(view);

        window.MouseDown(CellCenter(window, items, view.Rows[1], 3), MouseButton.Left);
        window.MouseMove(CellCenter(window, items, view.Rows[3], 3));
        window.MouseUp(CellCenter(window, items, view.Rows[3], 3), MouseButton.Left);
        Assert.That(SplitRow(view, 2).Right.IsSelected, Is.True);

        var cleared = new List<MultiSelectRange?>();
        view.SelectionChanged += (_, e) => cleared.Add(e.Range);
        view.ClearSelection();

        Assert.That(view.Rows.OfType<DiffSplitContentRow>().Select(r => r.Left.IsSelected || r.Right.IsSelected),
                    Is.All.False);
        Assert.That(view.GetSelectionResult(), Is.Null);
        Assert.That(cleared, Is.EqualTo(new MultiSelectRange?[] { null }));
    }

    [AvaloniaTest]
    public void Split_SetPreselectedLines_MergesIntoOneBigRange()
    {
        var view = new DiffView { DiffFile = CreateSampleFile(), IsSelectionEnabled = true };

        // Old lines 4 and 6 with a gap — the upstream-known semantics highlight the gap too.
        view.SetPreselectedLines([4, 6]);

        Assert.That(SplitRow(view, 3).Left.IsSelected, Is.True);
        Assert.That(SplitRow(view, 5).Left.IsSelected && SplitRow(view, 5).Right.IsSelected, Is.True);
        Assert.That(SplitRow(view, 6).Left.IsSelected && SplitRow(view, 6).Right.IsSelected, Is.True);
        Assert.That(SplitRow(view, 0).Left.IsSelected || SplitRow(view, 0).Right.IsSelected, Is.False);
        Assert.That(SplitRow(view, 1).Left.IsSelected || SplitRow(view, 1).Right.IsSelected, Is.False);
    }

    [AvaloniaTest]
    public void Split_SelectionSpansHiddenLines_RevealedRowsPickUpHighlight()
    {
        var view = new DiffView { DiffFile = CreateExpandableFile(), IsSelectionEnabled = true };
        var (window, items) = ShownInView(view);
        MultiSelectResult? completed = null;
        view.SelectionCompleted += (_, e) => completed = e.Result;

        // Old-number of row 7 (old 44, last visible of the first hunk) to row 9 (old 88, first
        // visible of the second) — the range spans the collapsed [45,87].
        window.MouseDown(CellCenter(window, items, view.Rows[7], 0), MouseButton.Left);
        window.MouseMove(CellCenter(window, items, view.Rows[9], 0));
        window.MouseUp(CellCenter(window, items, view.Rows[9], 0), MouseButton.Left);

        Assert.That(completed!.Range, Is.EqualTo(new MultiSelectRange(SplitSide.Old, 44, 88)));
        Assert.That(completed.Lines.Count(l => l.IsHide), Is.EqualTo(43));

        // Only the visible endpoints are highlighted while the middle stays collapsed.
        Assert.That(SplitRow(view, 7).Left.IsSelected, Is.True);
        Assert.That(SplitRow(view, 9).Left.IsSelected, Is.True);

        // Expanding the middle hunk reveals rows 45..87 — the selection re-applies to them.
        view.ExpandHunkAllCommand.Execute(view.Rows[8]);

        var revealed45 = view.Rows.OfType<DiffSplitContentRow>().Single(r => r.Left.Number == "45");
        var revealed60 = view.Rows.OfType<DiffSplitContentRow>().Single(r => r.Left.Number == "60");
        var revealed87 = view.Rows.OfType<DiffSplitContentRow>().Single(r => r.Left.Number == "87");

        Assert.That(revealed45.Left.IsSelected && revealed45.Right.IsSelected, Is.True);
        Assert.That(revealed60.Left.IsSelected && revealed60.Right.IsSelected, Is.True);
        Assert.That(revealed87.Left.IsSelected && revealed87.Right.IsSelected, Is.True);
    }

    [AvaloniaTest]
    public void Unified_PressPicksSideByRowNumbers_AndSkipsNumberlessRows()
    {
        var view = new DiffView
        {
            DiffFile           = CreateSampleFile(), ViewMode = DiffViewMode.Unified,
            IsSelectionEnabled = true
        };
        var (window, items) = ShownInView(view);
        var                changes   = new List<MultiSelectRange?>();
        MultiSelectResult? completed = null;
        view.SelectionChanged   += (_, e) => changes.Add(e.Range);
        view.SelectionCompleted += (_, e) => completed = e.Result;

        // Press inside the numbers cell of the (2,2) row — the upstream unified rule resolves
        // the side from the row's numbers (new wins when present), not from which half was hit.
        window.MouseDown(CellCenter(window, items, view.Rows[1], 0), MouseButton.Left);
        window.MouseMove(CellCenter(window, items, view.Rows[2], 0));

        // Row 3 is the old-4 delete row — it has no new number, so a new-side drag does not
        // extend over it (upstream lineNumber === undefined guard).
        window.MouseMove(CellCenter(window, items, view.Rows[3], 1));
        window.MouseUp(CellCenter(window, items, view.Rows[3], 1), MouseButton.Left);

        Assert.That(completed, Is.Not.Null);
        Assert.That(completed!.Range, Is.EqualTo(new MultiSelectRange(SplitSide.New, 2, 3)));
        Assert.That(changes[0], Is.EqualTo(new MultiSelectRange(SplitSide.New, 2, 2)));
    }

    [AvaloniaTest]
    public void Unified_SelectionFlagsWholeRow()
    {
        var view = new DiffView
        {
            DiffFile           = CreateSampleFile(), ViewMode = DiffViewMode.Unified,
            IsSelectionEnabled = true
        };
        var (window, items) = ShownInView(view);

        // Old-number cell of the old-4 delete row — no new number, so the side resolves to old;
        // dragging to the (5,6) context row extends over old 5.
        window.MouseDown(CellCenter(window, items, view.Rows[3], 0), MouseButton.Left);
        window.MouseMove(CellCenter(window, items, view.Rows[6], 0));
        window.MouseUp(CellCenter(window, items, view.Rows[6], 0), MouseButton.Left);

        Assert.That(view.GetSelectionResult()!.Range, Is.EqualTo(new MultiSelectRange(SplitSide.Old, 4, 5)));

        var row3 = As<DiffUnifiedContentRow>(view.Rows[3]);
        var row4 = As<DiffUnifiedContentRow>(view.Rows[4]);
        var row5 = As<DiffUnifiedContentRow>(view.Rows[5]);
        var row6 = As<DiffUnifiedContentRow>(view.Rows[6]);

        // The whole row highlights (numbers + content); the add rows in between carry no old
        // number, so an old-side range skips them.
        Assert.That(row3.IsSelected, Is.True);
        Assert.That(row4.IsSelected, Is.False);
        Assert.That(row5.IsSelected, Is.False);
        Assert.That(row6.IsSelected, Is.True);
    }
}
