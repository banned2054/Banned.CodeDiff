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
///     M7 line-range comments: the anchor derived from the selection, the explicit
///     BeginCommentCommand → CommentRequested channel, card rows placed after the last visible
///     line of their range, the persistent comment highlight (independent of the selection
///     channel), and re-location across mode switches and hunk expansions.
/// </summary>
public class DiffCommentTests
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
    ///     the middle placeholder is <c>view.Rows[8]</c> (see DiffSelectionTests for the layout).
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

    private static int IndexOfRow(DiffView view, DiffRow row)
    {
        for (var index = 0; index < view.Rows.Count; index++)
            if (ReferenceEquals(view.Rows[index], row))
                return index;

        return -1;
    }

    [AvaloniaTest]
    public void GetCommentAnchor_NullWithoutSelection()
    {
        var view = new DiffView { DiffFile = CreateSampleFile(), IsSelectionEnabled = true };

        Assert.That(view.GetCommentAnchor(), Is.Null);
    }

    [AvaloniaTest]
    public void Split_SingleLineClick_AnchorCarriesSideRangeAndFilePath()
    {
        var view = new DiffView
        {
            DiffFile = CreateSampleFile(), IsSelectionEnabled = true, FilePath = "Program.cs"
        };
        var (window, items) = ShownInView(view);

        // Press and release on the new-number cell of row 1 (new line 2) — a single-line
        // selection anchors the new side.
        window.MouseDown(CellCenter(window, items, view.Rows[1], 3), MouseButton.Left);
        window.MouseUp(CellCenter(window, items, view.Rows[1], 3), MouseButton.Left);

        Assert.That(view.GetCommentAnchor(),
                    Is.EqualTo(new DiffCommentAnchor("Program.cs", SplitSide.New, 2, 2)));
    }

    [AvaloniaTest]
    public void Split_DragUpwards_AnchorRangeNormalized()
    {
        var view = new DiffView
        {
            DiffFile = CreateSampleFile(), IsSelectionEnabled = true, FilePath = "Program.cs"
        };
        var (window, items) = ShownInView(view);

        window.MouseDown(CellCenter(window, items, view.Rows[3], 0), MouseButton.Left);
        window.MouseMove(CellCenter(window, items, view.Rows[6], 0));
        window.MouseUp(CellCenter(window, items, view.Rows[6], 0), MouseButton.Left);

        Assert.That(view.GetCommentAnchor(),
                    Is.EqualTo(new DiffCommentAnchor("Program.cs", SplitSide.Old, 4, 6)));
    }

    [AvaloniaTest]
    public void Unified_AnchorResolvesSideFromRowNumbers()
    {
        var view = new DiffView
        {
            DiffFile           = CreateSampleFile(), ViewMode = DiffViewMode.Unified,
            IsSelectionEnabled = true
        };
        var (window, items) = ShownInView(view);

        // The (2,2) context row resolves to the new side (new number wins upstream); the old-4
        // delete row has no new number, so it anchors the old side.
        window.MouseDown(CellCenter(window, items, view.Rows[1], 0), MouseButton.Left);
        window.MouseUp(CellCenter(window, items, view.Rows[1], 0), MouseButton.Left);
        Assert.That(view.GetCommentAnchor(), Is.EqualTo(new DiffCommentAnchor(null, SplitSide.New, 2, 2)));

        view.ClearSelection();

        window.MouseDown(CellCenter(window, items, view.Rows[3], 0), MouseButton.Left);
        window.MouseUp(CellCenter(window, items, view.Rows[3], 0), MouseButton.Left);
        Assert.That(view.GetCommentAnchor(), Is.EqualTo(new DiffCommentAnchor(null, SplitSide.Old, 4, 4)));
    }

    [AvaloniaTest]
    public void BeginCommentCommand_FollowsSelectionAndRaisesRequested()
    {
        var view = new DiffView
        {
            DiffFile = CreateSampleFile(), IsSelectionEnabled = true, FilePath = "Program.cs"
        };
        var (window, items) = ShownInView(view);

        Assert.That(view.BeginCommentCommand.CanExecute(null), Is.False);

        var requests = new List<DiffCommentAnchor>();
        view.CommentRequested += (_, e) => requests.Add(e.Anchor);

        window.MouseDown(CellCenter(window, items, view.Rows[1], 3), MouseButton.Left);
        window.MouseMove(CellCenter(window, items, view.Rows[3], 3));
        window.MouseUp(CellCenter(window, items, view.Rows[3], 3), MouseButton.Left);

        Assert.That(view.BeginCommentCommand.CanExecute(null), Is.True);

        view.BeginCommentCommand.Execute(null);

        // The explicit entry fires exactly once with the anchor derived from the selection —
        // no request for an ordinary completed selection without the command.
        Assert.That(requests, Is.EqualTo(new[] { new DiffCommentAnchor("Program.cs", SplitSide.New, 2, 4) }));
    }

    [AvaloniaTest]
    public void Split_CommentCard_AfterLastLineOfRange_WithSideHighlight()
    {
        var view = new DiffView
        {
            DiffFile = CreateSampleFile(), IsSelectionEnabled = true, FilePath = "Program.cs"
        };
        ShownInView(view);

        view.Comments = [new DiffComment(new DiffCommentAnchor("Program.cs", SplitSide.New, 2, 4), "alice", "hi")];

        // Baseline 12 rows (the delete/add pair shares row 3); the card goes after the last row
        // whose new number is inside [2,4] — row 3, the paired delete|add row.
        Assert.That(view.Rows.Count, Is.EqualTo(13));

        var card = As<DiffSplitCommentRow>(view.Rows[4]);
        Assert.That(card.Anchor, Is.EqualTo(new DiffCommentAnchor("Program.cs", SplitSide.New, 2, 4)));
        Assert.That(card.CardColumn, Is.EqualTo(4));
        Assert.That(card.Comments.Select(c => c.Content), Is.EqualTo(new[] { "hi" }));
        Assert.That(As<DiffSplitContentRow>(view.Rows[3]).Right.Number, Is.EqualTo("4"));
        Assert.That(view.Rows[5], Is.TypeOf<DiffSplitContentRow>());

        // Comment semantics flag only the anchor's side — the new-side anchor on the paired row
        // leaves the delete cell untouched (this is where a selection-style both-sides pass would
        // invert the behavior).
        Assert.That(SplitRow(view, 1).Right.IsCommented, Is.True);
        Assert.That(SplitRow(view, 1).Left.IsCommented, Is.False);
        Assert.That(SplitRow(view, 2).Right.IsCommented, Is.True);
        Assert.That(SplitRow(view, 3).Right.IsCommented, Is.True);
        Assert.That(SplitRow(view, 3).Left.IsCommented, Is.False);
        Assert.That(SplitRow(view, 5).Left.IsCommented || SplitRow(view, 5).Right.IsCommented, Is.False);
    }

    [AvaloniaTest]
    public void Split_CommentOnDeletedLine_AnchorsOldColumn()
    {
        var view = new DiffView { DiffFile = CreateSampleFile() };
        ShownInView(view);

        view.Comments = [new DiffComment(new DiffCommentAnchor(null, SplitSide.Old, 4, 4), null, "why removed?")];

        // The old-4 delete row is Rows[3]; the card lands at Rows[4] in the old content column.
        var card = As<DiffSplitCommentRow>(view.Rows[4]);

        Assert.That(card.CardColumn, Is.EqualTo(1));
        Assert.That(SplitRow(view, 3).Left.IsCommented, Is.True);
        Assert.That(SplitRow(view, 3).Right.IsCommented, Is.False);
    }

    [AvaloniaTest]
    public void CommentHighlight_SurvivesNewDragSelection()
    {
        var view = new DiffView { DiffFile = CreateSampleFile(), IsSelectionEnabled = true };
        var (window, items) = ShownInView(view);

        view.Comments = [new DiffComment(new DiffCommentAnchor(null, SplitSide.New, 2, 4), null, "note")];

        // The card inserts after row 3, so the new-7 row moves from index 6 to 7; realize the
        // rebuilt rows before driving the pointer.
        RunLayoutPass(window);

        var target = view.Rows[7];

        // A new single-line selection on the new-7 row replaces the previous selection while
        // the comment anchor highlight stays.
        window.MouseDown(CellCenter(window, items, target, 3), MouseButton.Left);
        window.MouseUp(CellCenter(window, items, target, 3), MouseButton.Left);

        // The new selection replaces the previous one (row 7, new line 7) while the comment
        // anchor highlight stays (commented rows: 1, 2, 3 — the card sits at index 4).
        Assert.That(As<DiffSplitContentRow>(target).Right.IsSelected, Is.True);
        Assert.That(As<DiffSplitContentRow>(target).Right.IsCommented, Is.False);
        Assert.That(SplitRow(view, 1).Right.IsCommented, Is.True);
        Assert.That(SplitRow(view, 1).Right.IsSelected, Is.False);
        Assert.That(SplitRow(view, 3).Right.IsCommented, Is.True);
    }

    [AvaloniaTest]
    public void Comments_FilePathMismatch_DoNotRender()
    {
        var view = new DiffView { DiffFile = CreateSampleFile(), FilePath = "Program.cs" };
        ShownInView(view);

        view.Comments = [
            new DiffComment(new DiffCommentAnchor("Program.cs", SplitSide.New, 2, 4), null, "mine"),
            new DiffComment(new DiffCommentAnchor("Other.cs", SplitSide.New, 2, 4), null, "other file")
        ];

        // Same side, same numbers — only the comment of the file the view shows renders.
        Assert.That(view.Rows.Count, Is.EqualTo(13));

        var card = As<DiffSplitCommentRow>(view.Rows[4]);

        Assert.That(card.Anchor.FilePath, Is.EqualTo("Program.cs"));
        Assert.That(card.Comments.Select(c => c.Content), Is.EqualTo(new[] { "mine" }));
        Assert.That(SplitRow(view, 1).Right.IsCommented, Is.True);

        // Switching the file identity re-scopes the rendering: the other file's comment takes
        // over the identical anchor.
        view.FilePath = "Other.cs";

        var cards = view.Rows.OfType<DiffSplitCommentRow>().ToList();

        Assert.That(cards, Has.Count.EqualTo(1));
        Assert.That(cards[0].Anchor.FilePath, Is.EqualTo("Other.cs"));
        Assert.That(cards[0].Comments.Select(c => c.Content), Is.EqualTo(new[] { "other file" }));
        Assert.That(SplitRow(view, 1).Right.IsCommented, Is.True);

        // A file without comments renders neither cards nor highlights.
        view.FilePath = "Third.cs";

        Assert.That(view.Rows.Count, Is.EqualTo(12));
        Assert.That(view.Rows.OfType<DiffSplitCommentRow>().ToList(), Is.Empty);
        Assert.That(view.Rows.OfType<DiffSplitContentRow>()
                         .Any(row => row.Left.IsCommented || row.Right.IsCommented),
                    Is.False);
    }

    [AvaloniaTest]
    public void Comment_SurvivesModeSwitch_ReanchoredByLineNumbers()
    {
        var view = new DiffView { DiffFile = CreateSampleFile(), FilePath = "Program.cs" };
        ShownInView(view);

        view.Comments = [new DiffComment(new DiffCommentAnchor("Program.cs", SplitSide.New, 2, 4), null, "note")];

        Assert.That(view.Rows.OfType<DiffSplitCommentRow>().ToList(), Has.Count.EqualTo(1));

        view.ViewMode = DiffViewMode.Unified;

        // The unified layout places the same anchor after the row with new line number 4.
        var cards = view.Rows.OfType<DiffUnifiedCommentRow>().ToList();

        Assert.That(cards, Has.Count.EqualTo(1));
        Assert.That(cards[0].Anchor, Is.EqualTo(new DiffCommentAnchor("Program.cs", SplitSide.New, 2, 4)));

        var cardIndex = IndexOfRow(view, cards[0]);

        Assert.That(As<DiffUnifiedContentRow>(view.Rows[cardIndex - 1]).NewLineNumber, Is.EqualTo(4));

        var commented = view.Rows.OfType<DiffUnifiedContentRow>()
                             .Where(row => row.IsCommented)
                             .Select(row => row.NewLineNumber)
                             .ToList();

        // The delete row (old 4, no new number) is not part of a new-side anchor.
        Assert.That(commented, Is.EqualTo(new int?[] { 2, 3, 4 }));
    }

    [AvaloniaTest]
    public void Comment_AllLinesHidden_NoCardUntilExpansionReveals()
    {
        // FilePath must match the anchor's identity — comments are file-scoped.
        var view = new DiffView { DiffFile = CreateExpandableFile(), FilePath = "sample.txt" };
        ShownInView(view);

        view.Comments = [new DiffComment(new DiffCommentAnchor("sample.txt", SplitSide.Old, 45, 87), null, "ctx")];

        // Old lines 45..87 are all behind the middle placeholder — no card, no highlight.
        Assert.That(view.Rows.OfType<DiffCommentRow>().ToList(), Is.Empty);
        Assert.That(view.Rows.OfType<DiffSplitContentRow>().Any(row => row.Left.IsCommented), Is.False);

        view.ExpandHunkAllCommand.Execute(view.Rows[8]);

        var card = view.Rows.OfType<DiffSplitCommentRow>().Single();
        var cardIndex = IndexOfRow(view, card);

        Assert.That(As<DiffSplitContentRow>(view.Rows[cardIndex - 1]).Left.Number, Is.EqualTo("87"));

        var revealed = view.Rows.OfType<DiffSplitContentRow>()
                          .Where(row => row.Left.IsCommented)
                          .Select(row => row.Left.Number)
                          .ToList();

        Assert.That(revealed, Is.EqualTo(Enumerable.Range(45, 43).Select(n => n.ToString()).ToArray()));
    }

    [AvaloniaTest]
    public void Comments_ReassignedOrCleared_RowsFollow()
    {
        var view = new DiffView { DiffFile = CreateSampleFile() };
        ShownInView(view);

        var baseline = view.Rows.Count;

        view.Comments = [
            new DiffComment(new DiffCommentAnchor(null, SplitSide.New, 2, 2), "a", "first"),
            new DiffComment(new DiffCommentAnchor(null, SplitSide.New, 2, 2), "b", "second")
        ];

        // Two comments on the same anchor share one card (placed after row 1, the new-2 line).
        var card = As<DiffSplitCommentRow>(view.Rows[2]);

        Assert.That(view.Rows.Count, Is.EqualTo(baseline + 1));
        Assert.That(card.Comments, Has.Count.EqualTo(2));
        Assert.That(card.Comments[0].Author, Is.EqualTo("a"));

        view.Comments = null;

        Assert.That(view.Rows.Count, Is.EqualTo(baseline));
        Assert.That(view.Rows.OfType<DiffCommentRow>().ToList(), Is.Empty);
        Assert.That(view.Rows.OfType<DiffSplitContentRow>().Any(row => row.Left.IsCommented || row.Right.IsCommented),
                    Is.False);
    }

    [AvaloniaTest]
    public void CommentCard_TemplateRealizesWithCardContent()
    {
        var view = new DiffView { DiffFile = CreateSampleFile() };
        var (window, items) = ShownInView(view);

        view.Comments = [
            new DiffComment(new DiffCommentAnchor(null, SplitSide.Old, 4, 4), "alice", "why?"),
            new DiffComment(new DiffCommentAnchor(null, SplitSide.Old, 4, 4), "bob", "because")
        ];

        // Fresh rows after the rebuild; a layout pass realizes the new containers.
        RunLayoutPass(window);

        var card = view.Rows.OfType<DiffSplitCommentRow>().Single();
        var container = items.ContainerFromItem(card);

        Assert.That(container, Is.Not.Null, "comment card container must be realized");
        RunLayoutPass(window);

        var cardBorder = container!.GetVisualDescendants()
                                  .OfType<Border>()
                                  .Single(border => border.Classes.Contains("diff-comment-card"));

        // The card sits in the old content column (template column 1).
        Assert.That(cardBorder.GetValue(Grid.ColumnProperty), Is.EqualTo(1));

        var comments = cardBorder.GetVisualDescendants()
                                 .OfType<ItemsControl>()
                                 .Single();

        Assert.That(((System.Collections.IEnumerable)comments.ItemsSource!).Cast<object>().ToList(),
                    Has.Count.EqualTo(2));
    }
}
