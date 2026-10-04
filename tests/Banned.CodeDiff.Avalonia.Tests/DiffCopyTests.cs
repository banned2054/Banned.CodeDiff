using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.NUnit;
using Avalonia.Input;
using Avalonia.Input.Platform;
using Avalonia.VisualTree;
using Banned.CodeDiff.Avalonia.Models;
using Banned.CodeDiff.Avalonia.Views;
using Banned.CodeDiff.Models;
using Banned.CodeDiff.Services;
using Banned.CodeDiff.Utils;
using NUnit.Framework;

namespace Banned.CodeDiff.Avalonia.Tests;

/// <summary>
/// Copy-feature UI wiring (M6 batch 3, native port feature — no upstream counterpart): the
/// DiffView copy commands track the selection lifecycle through CanExecute/CanExecuteChanged and
/// write the generated text to the real clipboard. The Avalonia headless platform provides a
/// working IClipboard (verified by probe), so the tests assert the final clipboard content — the
/// copy tasks complete synchronously there, making .Result safe on the UI thread.
/// </summary>
public class DiffCopyTests
{
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
        file.BuildUnifiedDiffLines();

        return file;
    }

    private static void RunLayoutPass(Window window)
    {
        Assert.That(window.GetLayoutManager(), Is.Not.Null);
        window.GetLayoutManager()!.ExecuteLayoutPass();
    }

    private static (Window Window, ItemsControl Items) ShownInView(DiffView view)
    {
        var window = new Window { Content = view, Width = 900, Height = 600 };
        window.Show();
        RunLayoutPass(window);

        return (window, view.GetVisualDescendants().OfType<ItemsControl>().Single());
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

    /// <summary>Drags an old-side selection over rows 7→9 (old lines 44..88, spanning the
    /// collapsed [45,87]) — the DiffSelectionTests layout.</summary>
    private static void SelectOld44To88(Window window, ItemsControl items, DiffView view)
    {
        window.MouseDown(CellCenter(window, items, view.Rows[7], 0), MouseButton.Left);
        window.MouseMove(CellCenter(window, items, view.Rows[9], 0));
        window.MouseUp(CellCenter(window, items, view.Rows[9], 0), MouseButton.Left);
    }

    private static string? ClipboardText(Window window) => window.Clipboard!.TryGetTextAsync().Result;

    // ---- CopySelectionCommand ----

    [AvaloniaTest]
    public void SelectionCopy_WithoutSelection_CannotExecuteAndLeavesClipboardAlone()
    {
        var view = new DiffView { DiffFile = CreateExpandableFile(), IsSelectionEnabled = true };
        var (window, _) = ShownInView(view);
        window.Clipboard!.SetTextAsync("sentinel").Wait();

        Assert.That(view.CopySelectionCommand.CanExecute(null), Is.False);

        // Silent no-op: nothing thrown, nothing copied (CanExecute=false already says so).
        view.CopySelectionCommand.Execute(null);

        Assert.That(ClipboardText(window), Is.EqualTo("sentinel"));
        Assert.That(view.CopySelectionAsync().Result, Is.False);
        Assert.That(ClipboardText(window), Is.EqualTo("sentinel"));
    }

    [AvaloniaTest]
    public void SelectionCopy_WithSelection_WritesVisibleLinesToClipboard()
    {
        var view = new DiffView { DiffFile = CreateExpandableFile(), IsSelectionEnabled = true };
        var (window, items) = ShownInView(view);
        SelectOld44To88(window, items, view);

        Assert.That(view.CopySelectionCommand.CanExecute(null), Is.True);
        Assert.That(view.CopySelectionAsync().Result, Is.True);

        // The range spans the collapsed [45,87]: only the visible endpoints are copied — what
        // the user sees, line values joined with '\n', no trailing newline.
        Assert.That(ClipboardText(window), Is.EqualTo("ctx 044\nctx 088"));
    }

    [AvaloniaTest]
    public void SelectionCopy_CommandExecute_WritesTheSameTextAsTheMethod()
    {
        var view = new DiffView { DiffFile = CreateExpandableFile(), IsSelectionEnabled = true };
        var (window, items) = ShownInView(view);
        SelectOld44To88(window, items, view);

        view.CopySelectionCommand.Execute(null);

        Assert.That(ClipboardText(window), Is.EqualTo("ctx 044\nctx 088"));
    }

    [AvaloniaTest]
    public void SelectionCopy_CanExecute_FollowsTheSelectionLifecycle()
    {
        var view = new DiffView { DiffFile = CreateExpandableFile(), IsSelectionEnabled = true };
        var (window, items) = ShownInView(view);

        Assert.That(view.CopySelectionCommand.CanExecute(null), Is.False);

        var canExecuteChanged = 0;
        view.CopySelectionCommand.CanExecuteChanged += (_, _) => canExecuteChanged++;

        SelectOld44To88(window, items, view);

        Assert.That(view.CopySelectionCommand.CanExecute(null), Is.True);
        Assert.That(canExecuteChanged, Is.GreaterThan(0));

        view.ClearSelection();

        Assert.That(view.CopySelectionCommand.CanExecute(null), Is.False);

        // Disabling the feature clears the selection — the command must follow.
        view.IsSelectionEnabled = false;

        Assert.That(view.CopySelectionCommand.CanExecute(null), Is.False);
    }

    [AvaloniaTest]
    public void SelectionCopy_ExpansionRevealingHiddenMembers_ExtendsTheCopiedText()
    {
        var view = new DiffView { DiffFile = CreateExpandableFile(), IsSelectionEnabled = true };
        var (window, items) = ShownInView(view);
        SelectOld44To88(window, items, view);

        // Reveal the collapsed middle — the completed range now copies its members too.
        view.ExpandHunkAllCommand.Execute(view.Rows[8]);

        Assert.That(view.CopySelectionAsync().Result, Is.True);
        Assert.That(ClipboardText(window),
                    Is.EqualTo(string.Join("\n",
                                           Enumerable.Range(44, 45).Select(n => $"ctx {n:D3}"))));
    }

    [AvaloniaTest]
    public void SelectionCopy_AllMembersHidden_CannotExecute()
    {
        var view = new DiffView { DiffFile = CreateExpandableFile(), IsSelectionEnabled = true };
        var (window, items) = ShownInView(view);

        // Reveal the collapsed middle (through the model, so no scroll anchoring moves the
        // viewport) and select a range living entirely inside it: the strip is replaced by its
        // rows, so 45 and 46 are now Rows[8]/Rows[9] right below line 44.
        var hunkRow = (DiffSplitHunkRow)view.Rows[8];
        view.DiffFile!.OnSplitHunkExpand(HunkExpandDirection.All, hunkRow.HunkIndex);
        RunLayoutPass(window);

        window.MouseDown(CellCenter(window, items, view.Rows[8], 0), MouseButton.Left);
        window.MouseMove(CellCenter(window, items, view.Rows[9], 0));
        window.MouseUp(CellCenter(window, items, view.Rows[9], 0), MouseButton.Left);

        Assert.That(view.CopySelectionCommand.CanExecute(null), Is.True);

        // Collapse again: every member of the completed range is hidden behind the hunk —
        // the command must go dark and the copy must silently do nothing.
        view.DiffFile!.OnAllCollapse(ExpandViewMode.Split);

        Assert.That(view.CopySelectionCommand.CanExecute(null), Is.False);
        Assert.That(view.CopySelectionAsync().Result, Is.False);
    }

    // ---- CopyOldFileCommand / CopyNewFileCommand ----

    [AvaloniaTest]
    public void FileCopy_Commands_CopyWholeFileContentsAsIs()
    {
        var file = CreateExpandableFile();
        var view = new DiffView { DiffFile = file };
        var (window, _) = ShownInView(view);

        Assert.That(view.CopyOldFileCommand.CanExecute(null), Is.True);

        view.CopyOldFileCommand.Execute(null);

        // Trailing newline kept as-is — the content is not re-processed.
        Assert.That(ClipboardText(window), Is.EqualTo(file.OldFileRaw));
        Assert.That(ClipboardText(window)!.EndsWith("\n"), Is.True);

        view.CopyNewFileCommand.Execute(null);

        Assert.That(ClipboardText(window), Is.EqualTo(file.NewFileRaw));
        Assert.That(ClipboardText(window)!.EndsWith("ctx 100\n"), Is.True);
    }

    [AvaloniaTest]
    public void FileCopy_Methods_ReturnTrueAndCopyContents()
    {
        var file = CreateExpandableFile();
        var view = new DiffView { DiffFile = file };
        var (window, _) = ShownInView(view);

        Assert.That(view.CopyOldFileAsync().Result, Is.True);
        Assert.That(ClipboardText(window), Is.EqualTo(file.OldFileRaw));

        Assert.That(view.CopyNewFileAsync().Result, Is.True);
        Assert.That(ClipboardText(window), Is.EqualTo(file.NewFileRaw));
    }

    [AvaloniaTest]
    public void FileCopy_WithoutDiffFile_CannotExecute()
    {
        var view = new DiffView();
        var (window, _) = ShownInView(view);
        window.Clipboard!.SetTextAsync("sentinel").Wait();

        Assert.That(view.CopyOldFileCommand.CanExecute(null), Is.False);
        Assert.That(view.CopyNewFileCommand.CanExecute(null), Is.False);
        Assert.That(view.CopySelectionCommand.CanExecute(null), Is.False);

        view.CopyOldFileCommand.Execute(null);
        view.CopyNewFileCommand.Execute(null);

        Assert.That(view.CopyOldFileAsync().Result, Is.False);
        Assert.That(view.CopyNewFileAsync().Result, Is.False);
        Assert.That(ClipboardText(window), Is.EqualTo("sentinel"));
    }

    [AvaloniaTest]
    public void FileCopy_CanExecuteChanged_FiresOnDiffFileChange()
    {
        var view = new DiffView();
        var (window, _) = ShownInView(view);

        var oldFileRaised  = 0;
        var newFileRaised  = 0;
        view.CopyOldFileCommand.CanExecuteChanged += (_, _) => oldFileRaised++;
        view.CopyNewFileCommand.CanExecuteChanged += (_, _) => newFileRaised++;

        view.DiffFile = CreateExpandableFile();

        Assert.That(oldFileRaised, Is.GreaterThan(0));
        Assert.That(newFileRaised, Is.GreaterThan(0));
        Assert.That(view.CopyOldFileCommand.CanExecute(null), Is.True);
    }

    // ---- unified mode ----

    [AvaloniaTest]
    public void Unified_SelectionCopy_CopiesTheUnifiedResult()
    {
        const string sample = """
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

        var file = new DiffFile(oldFileName : "", oldFileContent : "", newFileName : "", newFileContent : "",
                                diffList : [sample]);
        file.Init();
        file.BuildSplitDiffLines();
        file.BuildUnifiedDiffLines();

        var view = new DiffView { DiffFile = file, ViewMode = DiffViewMode.Unified, IsSelectionEnabled = true };
        var (window, items) = ShownInView(view);

        // Press the numbers of the (2,2) row, drag to the (3,?) add row — a new-side range over
        // new lines 2..3 (DiffSelectionTests' Unified_PressPicksSideByRowNumbers layout).
        window.MouseDown(CellCenter(window, items, view.Rows[1], 0), MouseButton.Left);
        window.MouseMove(CellCenter(window, items, view.Rows[2], 0));
        window.MouseUp(CellCenter(window, items, view.Rows[2], 0), MouseButton.Left);

        var expected = MultiSelectData.GetSelectedTextFromResult(view.GetSelectionResult());

        Assert.That(expected, Is.Not.Empty);
        Assert.That(view.CopySelectionAsync().Result, Is.True);
        Assert.That(ClipboardText(window), Is.EqualTo(expected));
    }
}
