using Avalonia.Controls;
using Avalonia.Headless.NUnit;
using Avalonia.VisualTree;
using Banned.CodeDiff.Avalonia.Models;
using Banned.CodeDiff.Avalonia.Views;
using Banned.CodeDiff.Services;
using NUnit.Framework;

namespace Banned.CodeDiff.Avalonia.Tests;

/// <summary>
///     The unified single-number-column option (DiffView.UseSingleLineNumberColumn): the merged
///     number prefers the new line number (deleted lines fall back to the old one), the gutter
///     collapses to one column in content and hunk rows, toggling restyles realized containers
///     live without rebuilding rows, and split view ignores the option.
/// </summary>
public class DiffLineNumberColumnTests
{
    // Same shape as DiffViewTests.Sample. The -1/+2 change deliberately leaves the trailing
    // context rows with different old/new numbers (old5→new6, old6→new7): a flipped new/old
    // priority cannot pass. The leading contexts (1/1, 2/2, 3/3) stay equal on purpose.
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

    /// <summary>
    ///     100/101-line file with two change hunks, mirroring DiffExpandTests.CreateExpandableFile
    ///     except the first hunk is a -1/+2 change: every context line revealed after it (the
    ///     43-line middle gap) is staggered (new = old + 1), so revealed expand rows also
    ///     discriminate the merged-number priority.
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
        newLines.Add("inserted");

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
            @@ -38,7 +38,8 @@
             ctx 038
             ctx 039
             ctx 040
            -change-me
            +changed!
            +inserted
             ctx 042
             ctx 043
             ctx 044
            @@ -88,7 +89,8 @@
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

        return file;
    }

    private static DiffFile CreateSampleFile()
    {
        var file = new DiffFile("", "", "", "", [Sample]);
        file.Init();

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
        double                                                                                     width  = 360,
        double                                                                                     height = 240)
    {
        var window = new Window { Content = view, Width = width, Height = height };
        window.Show();
        RunLayoutPass(window);

        return (window,
                view.GetVisualDescendants().OfType<ScrollViewer>().Single(),
                view.GetVisualDescendants().OfType<ItemsControl>().Single());
    }

    /// <summary>The 3-column row template Grid of a realized unified row container.</summary>
    private static Grid UnifiedRowGrid(ItemsControl items, object row)
    {
        var container = items.ContainerFromItem(row);

        Assert.That(container, Is.Not.Null, "row container must be realized");

        return container!.GetVisualDescendants().OfType<Grid>().First(g => g.ColumnDefinitions.Count == 3);
    }

    // ---- model: merged number priority ----

    [AvaloniaTest]
    public void UnifiedRows_MergedNumber_PrefersNewNumber()
    {
        var view = new DiffView { DiffFile = CreateSampleFile(), ViewMode = DiffViewMode.Unified };

        // The discriminator: context rows whose old/new numbers deliberately differ must show
        // the NEW number — catches a flipped (old ?? new) priority.
        var staggered = view.Rows.OfType<DiffUnifiedContentRow>()
                            .Where(r => r.Kind == DiffCellKind.Context && r.NewLineNumber != r.OldLineNumber)
                            .ToList();

        Assert.That(staggered, Is.Not.Empty);
        Assert.That(staggered.All(r => r.MergedNumber == r.NewNumber), Is.True);
        Assert.That(staggered.All(r => r.MergedNumber != r.OldNumber), Is.True);

        var first = staggered[0];

        Assert.That(first.OldLineNumber, Is.EqualTo(5));
        Assert.That(first.NewLineNumber, Is.EqualTo(6));

        // Equal-number context rows and add rows also show the new number.
        var aligned = view.Rows.OfType<DiffUnifiedContentRow>()
                          .First(r => r.Kind == DiffCellKind.Context && r.NewLineNumber == r.OldLineNumber);

        Assert.That(aligned.MergedNumber, Is.EqualTo(aligned.NewNumber));

        var add = view.Rows.OfType<DiffUnifiedContentRow>().First(r => r.Kind == DiffCellKind.Add);

        Assert.That(add.OldNumber, Is.Null);
        Assert.That(add.MergedNumber, Is.EqualTo(add.NewNumber));

        // Deleted lines have no new number and fall back to the old one (both Sample hunks
        // delete a line: old 4 and old 11).
        var deletes = view.Rows.OfType<DiffUnifiedContentRow>().Where(r => r.Kind == DiffCellKind.Delete).ToList();

        Assert.That(deletes.Count, Is.EqualTo(2));
        Assert.That(deletes.All(r => r.NewNumber == null), Is.True);
        Assert.That(deletes.All(r => r.MergedNumber == r.OldNumber), Is.True);
        Assert.That(deletes[0].OldNumber, Is.EqualTo("4"));
    }

    [AvaloniaTest]
    public void UnifiedRows_RevealedExpandLines_ShowNewNumber()
    {
        var view = new DiffView
        {
            DiffFile                   = CreateExpandableFile(),
            ViewMode                   = DiffViewMode.Unified,
            UseSingleLineNumberColumn  = true
        };
        var (window, _, items) = ShownInView(view, 900, 300);

        // The middle placeholder (hidden 43-line gap after the -1/+2 hunk) is realized near the
        // top: in single mode its filler cell is collapsed while the expand-button gutter stays.
        Assert.That(view.Rows.OfType<DiffUnifiedHunkRow>().Count(), Is.EqualTo(3));

        var middle = view.Rows.OfType<DiffUnifiedHunkRow>().ElementAt(1);
        var grid   = UnifiedRowGrid(items, middle);
        var filler = grid.Children.OfType<Border>().Single(b => Grid.GetColumn(b) == 1);

        Assert.That(filler.IsVisible, Is.False);
        Assert.That(filler.Classes, Does.Contain("diff-dual-num"));

        var expandCell = grid.Children.OfType<Border>().Single(b => Grid.GetColumn(b) == 0);

        Assert.That(expandCell.IsVisible, Is.True);
        Assert.That(expandCell.GetVisualDescendants().OfType<Button>(), Is.Not.Empty);

        view.ExpandHunkAllCommand.Execute(middle);
        RunLayoutPass(window);

        // Revealed raw rows (no diff entry) show the new number too — the middle gap is
        // staggered (new = old + 1) because the first hunk deletes one and adds two.
        var expandRows = view.Rows.OfType<DiffUnifiedContentRow>()
                             .Where(r => r.Kind == DiffCellKind.Expand)
                             .ToList();

        Assert.That(expandRows, Is.Not.Empty);

        var staggered = expandRows.Where(r => r.NewLineNumber != r.OldLineNumber).ToList();

        Assert.That(staggered, Is.Not.Empty);
        Assert.That(staggered.All(r => r.OldNumber != null && r.NewNumber != null), Is.True);
        Assert.That(staggered.All(r => r.MergedNumber == r.NewNumber), Is.True);
    }

    // ---- visual: single gutter, live toggle ----

    [AvaloniaTest]
    public void Unified_SingleNumberColumn_ShowsMergedNumberText_InOneGutter()
    {
        var view = new DiffView
        {
            DiffFile                  = CreateSampleFile(),
            ViewMode                  = DiffViewMode.Unified,
            UseSingleLineNumberColumn = true
        };
        var (_, _, items) = ShownInView(view);

        var staggered = view.Rows.OfType<DiffUnifiedContentRow>()
                            .First(r => r.Kind == DiffCellKind.Context && r.NewLineNumber != r.OldLineNumber);

        var grid     = UnifiedRowGrid(items, staggered);
        var borders  = grid.Children.OfType<Border>().Where(b => b.Classes.Contains("diff-line-num")).ToList();
        var oldCell  = borders.Single(b => Grid.GetColumn(b) == 0);
        var newCell  = borders.Single(b => Grid.GetColumn(b) == 1);

        Assert.That(borders, Has.Count.EqualTo(2));
        Assert.That(oldCell.IsVisible, Is.False);
        Assert.That(newCell.IsVisible, Is.True);

        // The surviving cell keeps the multi-select anchor class and shows exactly one number:
        // the merged one — the new number (6) of the staggered row, not the old one (5).
        var numberTexts = newCell.GetVisualDescendants().OfType<TextBlock>()
                                 .Where(t => t.Classes.Contains("diff-line-number")).ToList();

        Assert.That(numberTexts, Has.Count.EqualTo(2));

        var visible = numberTexts.Single(t => t.IsVisible);

        Assert.That(visible.Classes, Does.Contain("diff-merged-num"));
        Assert.That(visible.Text, Is.EqualTo(staggered.MergedNumber));
        Assert.That(visible.Text, Is.EqualTo("6"));
    }

    [AvaloniaTest]
    public void Unified_SingleNumberColumn_TogglesLive_WithoutRebuildingRows()
    {
        var view = new DiffView { DiffFile = CreateSampleFile(), ViewMode = DiffViewMode.Unified };
        var (window, _, items) = ShownInView(view);

        var staggered = view.Rows.OfType<DiffUnifiedContentRow>()
                            .First(r => r.Kind == DiffCellKind.Context && r.NewLineNumber != r.OldLineNumber);
        var rowsBefore = view.Rows;

        var grid       = UnifiedRowGrid(items, staggered);
        var oldCell    = grid.Children.OfType<Border>().Single(b => Grid.GetColumn(b) == 0);
        var newCell    = grid.Children.OfType<Border>().Single(b => Grid.GetColumn(b) == 1);
        var texts      = newCell.GetVisualDescendants().OfType<TextBlock>()
                                .Where(t => t.Classes.Contains("diff-line-number")).ToList();
        var dualText   = texts.Single(t => t.Classes.Contains("diff-dual-num"));
        var mergedText = texts.Single(t => t.Classes.Contains("diff-merged-num"));

        // Dual is the default: both number cells visible, the merged text collapsed.
        Assert.That(view.UseSingleLineNumberColumn, Is.False);
        Assert.That(oldCell.IsVisible, Is.True);
        Assert.That(newCell.IsVisible, Is.True);
        Assert.That(dualText.IsVisible, Is.True);
        Assert.That(mergedText.IsVisible, Is.False);

        // The hunk placeholder still has its dual filler at this point.
        var hunk   = view.Rows.OfType<DiffUnifiedHunkRow>().Single();
        var hGrid  = UnifiedRowGrid(items, hunk);
        var filler = hGrid.Children.OfType<Border>().Single(b => Grid.GetColumn(b) == 1);

        Assert.That(filler.IsVisible, Is.True);

        view.UseSingleLineNumberColumn = true;
        RunLayoutPass(window);

        // Live restyle: same row instances (no rebuild), dual cells collapsed, merged shown.
        Assert.That(view.Rows, Is.SameAs(rowsBefore));
        Assert.That(oldCell.IsVisible, Is.False);
        Assert.That(newCell.IsVisible, Is.True);
        Assert.That(dualText.IsVisible, Is.False);
        Assert.That(mergedText.IsVisible, Is.True);
        Assert.That(mergedText.Text, Is.EqualTo("6"));
        Assert.That(filler.IsVisible, Is.False);

        view.UseSingleLineNumberColumn = false;
        RunLayoutPass(window);

        Assert.That(oldCell.IsVisible, Is.True);
        Assert.That(mergedText.IsVisible, Is.False);
        Assert.That(filler.IsVisible, Is.True);
    }

    // ---- split regression: the option is unified-only ----

    [AvaloniaTest]
    public void Split_IgnoresSingleNumberColumn()
    {
        var view = new DiffView { DiffFile = CreateSampleFile(), UseSingleLineNumberColumn = true };
        var (_, _, items) = ShownInView(view);

        var row = view.Rows.OfType<DiffSplitContentRow>().First(r => r.Left.Kind == DiffCellKind.Delete);

        var container = items.ContainerFromItem(row);

        Assert.That(container, Is.Not.Null);

        var grid   = container!.GetVisualDescendants().OfType<Grid>().First(g => g.ColumnDefinitions.Count == 5);
        var oldNum = grid.Children.OfType<Border>().Single(b => Grid.GetColumn(b) == 0);
        var newNum = grid.Children.OfType<Border>().Single(b => Grid.GetColumn(b) == 3);

        Assert.That(oldNum.IsVisible, Is.True);
        Assert.That(newNum.IsVisible, Is.True);
    }
}
