using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Shapes;
using Avalonia.Headless;
using Avalonia.Headless.NUnit;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Media.Immutable;
using Avalonia.Styling;
using Avalonia.VisualTree;
using Banned.CodeDiff.Avalonia.Demo.ViewModels;
using Banned.CodeDiff.Avalonia.Demo.Views;
using Banned.CodeDiff.Avalonia.Models;
using Banned.CodeDiff.Avalonia.Views;
using Banned.CodeDiff.Models;
using Banned.CodeDiff.Services;
using NUnit.Framework;

namespace Banned.CodeDiff.Avalonia.Tests;

/// <summary>
///     Regression tests for the M7/M8 review findings: the opaque preset selection backgrounds
///     must paint below the code, the demo override toggles must reach the view, clearing a
///     theme preset must not wipe a host background (including the latest one assigned before a
///     rebuild), the light-theme fallback of variant-less diff presets pins to the GitHub
///     baseline, invalid override hex is ignored instead of throwing, #RRGGBBAA keeps the
///     documented channel order, the default foreground colors plain text, the preset brushes
///     are immutable, the unified comment card follows the single-number-column mode, the
///     expansion anchor survives a revealed comment card (even one taller than the viewport),
///     and a fully hidden selection cannot start the comment flow.
/// </summary>
public class ReviewRegressionTests
{
    private const string Sample = """
        diff --git a/Program.cs b/Program.cs
        --- a/Program.cs
        +++ b/Program.cs
        @@ -1,6 +1,7 @@
         using System;

        -Console.WriteLine("Hello");
        +Console.WriteLine("Hello, World!");
         if (args.Length > 0)
        +/* done */
         }
        """;

    private const string NoteDiff = "--- a/note.txt\n+++ b/note.txt\n@@ -1 +1 @@\n-hello\n+world\n";

    // Split rows: [0] ctx(1|1) [1] ctx(2|2) [2] del(3|-)+add(-|3) [3] ctx(4|4) [4] add(-|5).
    private static DiffFile CreateSampleFile()
    {
        var file = new DiffFile("Program.cs", "", "Program.cs", "", [Sample]);

        file.Init();
        file.BuildSplitDiffLines();
        file.BuildUnifiedDiffLines();

        return file;
    }

    internal static DiffFile CreateNoteFile()
    {
        var file = new DiffFile("note.txt", "hello\n", "note.txt", "world\n", [NoteDiff]);

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

    private static Window ShownInView(DiffView view)
    {
        var window = new Window { Content = view, Width = 900, Height = 600 };

        window.Show();
        window.GetLayoutManager()!.ExecuteLayoutPass();

        return window;
    }

    private static void RunLayoutPass(Window window)
    {
        Assert.That(window.GetLayoutManager(), Is.Not.Null);

        window.GetLayoutManager()!.ExecuteLayoutPass();
    }

    // ---- M8 [P1]: the opaque preset selection backgrounds paint below the code ----

    [AvaloniaTest]
    public void SelectionBackground_PaintsBelowTheCodeAndTheNumberText()
    {
        var view = new DiffView
        {
            DiffFile           = CreateSampleFile(),
            ThemePreset        = DiffThemePreset.VisualStudio,
            IsSelectionEnabled = true
        };

        var window = ShownInView(view);

        try
        {
            view.SetPreselectedLines(newLines : [3]);

            var changeRow = As<DiffSplitContentRow>(view.Rows[2]);

            Assert.That(changeRow.Right.IsSelected, Is.True);

            var items     = view.GetVisualDescendants().OfType<ItemsControl>().Single();
            var container = items.ContainerFromItem(changeRow);

            Assert.That(container, Is.Not.Null);

            // Content cell: the selected rectangle must precede the code grid (Panel children
            // paint in index order — an opaque background above the text would hide the line).
            var content = container!.GetVisualDescendants().OfType<Border>()
                                    .Single(b => b.Classes.Contains("diff-line-new-content"));
            var contentRect = As<Panel>(content.Child).Children
                                                      .OfType<Rectangle>()
                                                      .Single(r => ReferenceEquals(r.Fill,
                                                                  changeRow.Right.SelectedBackground));
            var codeGrid = As<Panel>(content.Child).Children.OfType<Grid>().Single();

            Assert.That(As<Panel>(content.Child).Children.IndexOf(contentRect),
                        Is.LessThan(As<Panel>(content.Child).Children.IndexOf(codeGrid)));

            // Number cell: same rule against the number text.
            var number = container.GetVisualDescendants().OfType<Border>()
                                  .Single(b => b.Classes.Contains("diff-line-new-num"));
            var numberRect = As<Panel>(number.Child).Children
                                                    .OfType<Rectangle>()
                                                    .Single(r => ReferenceEquals(r.Fill,
                                                                changeRow.Right.SelectedBackground));
            var numberText = As<Panel>(number.Child).Children.OfType<TextBlock>().Single();

            Assert.That(As<Panel>(number.Child).Children.IndexOf(numberRect),
                        Is.LessThan(As<Panel>(number.Child).Children.IndexOf(numberText)));
        }
        finally
        {
            window.Close();
        }
    }

    // ---- M8: the demo override toggles must reach the view ----

    [AvaloniaTest]
    public void DemoOverrideToggles_PushTheirValuesIntoTheView()
    {
        var window = new MainWindow();

        window.Show();
        RunLayoutPass(window);

        try
        {
            var vm   = (MainWindowViewModel)window.DataContext!;
            var view = window.GetVisualDescendants().OfType<DiffView>().Single();

            vm.IsAddBackgroundOverridden = true;
            vm.IsSyntaxScopeOverridden   = true;
            RunLayoutPass(window);

            Assert.That(vm.Palette, Is.Not.Null);
            Assert.That(vm.SyntaxOverrides, Is.Not.Null);
            Assert.That(view.Palette, Is.SameAs(vm.Palette),
                        "the VM must raise the property change so the binding pushes the palette");
            Assert.That(view.SyntaxOverrides, Is.SameAs(vm.SyntaxOverrides));

            vm.IsAddBackgroundOverridden = false;
            vm.IsSyntaxScopeOverridden   = false;
            RunLayoutPass(window);

            Assert.That(view.Palette, Is.Null);
            Assert.That(view.SyntaxOverrides, Is.Null);
        }
        finally
        {
            window.Close();
        }
    }

    // ---- M8: clearing a preset must not wipe a host background ----

    [AvaloniaTest]
    public void ThemePreset_ClearingKeepsAHostBackgroundAssignedAfterThePreset()
    {
        var view   = new DiffView { DiffFile = CreateSampleFile(), ThemePreset = DiffThemePreset.VisualStudio };
        var window = ShownInView(view);

        try
        {
            Assert.That(view.Background, Is.Not.Null, "the preset canvas applies");

            var host = new SolidColorBrush(Colors.HotPink);

            view.Background  = host;
            view.ThemePreset = null;

            Assert.That(view.Background, Is.SameAs(host),
                        "clearing the preset must only undo the canvas it applied itself");
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaTest]
    public void ThemePreset_ClearingRestoresAHostBackgroundCapturedBeforeThePreset()
    {
        var view   = new DiffView { DiffFile = CreateSampleFile() };
        var window = ShownInView(view);

        try
        {
            var host = new SolidColorBrush(Colors.HotPink);

            view.Background  = host;
            view.ThemePreset = DiffThemePreset.VisualStudio;

            Assert.That(view.Background, Is.Not.SameAs(host), "the preset canvas wins while active");

            view.ThemePreset = null;

            Assert.That(view.Background, Is.SameAs(host));
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaTest]
    public void ThemePreset_RebuildAfterAHostAssignment_RestoresTheLatestHostBackground()
    {
        var view   = new DiffView { DiffFile = CreateSampleFile() };
        var window = ShownInView(view);

        try
        {
            var green = new SolidColorBrush(Colors.Green);

            view.Background  = green;
            view.ThemePreset = DiffThemePreset.VisualStudio;

            Assert.That(view.Background, Is.Not.SameAs(green), "the preset canvas applies");

            // The host re-assigns, then a row rebuild (the Comments channel) re-applies the
            // canvas over it — the saved background must track the latest host value, so
            // clearing the preset later restores pink, not the stale green.
            var pink = new SolidColorBrush(Colors.HotPink);

            view.Background = pink;
            view.Comments   = [new DiffComment(new DiffCommentAnchor(null, SplitSide.New, 1, 1), null, "rebuild")];

            Assert.That(view.Background, Is.Not.SameAs(pink), "the rebuild re-applies the canvas");

            view.ThemePreset = null;

            Assert.That(view.Background, Is.SameAs(pink),
                        "clearing must restore the latest host background, not the one from before the first canvas");
        }
        finally
        {
            window.Close();
        }
    }

    // ---- M8: a variant-less independent diff preset pins the diff side to GitHub ----

    [AvaloniaTest]
    public void IndependentDiffPreset_WithoutLightVariant_FallsBackToGitHubBaseline()
    {
        var baseline = DiffBrushes.Get(ThemeVariant.Light);
        var actual = DiffBrushes.Get(ThemeVariant.Light, null, DiffThemePreset.VisualStudio,
                                     DiffThemePreset.Monokai);

        // The combined preset's light values must not leak through — the documented fallback
        // pins the diff side to GitHub light, exactly like the syntax side's github-light.
        Assert.That(((ISolidColorBrush)actual.AddContent!).Color,
                    Is.EqualTo(((ISolidColorBrush)baseline.AddContent!).Color));
        Assert.That(actual.CanvasBackground, Is.Null,
                    "the combined preset's canvas must not leak through the fallback either");

        // The same contract through the view: rows keep the GitHub light values.
        var view = new DiffView
        {
            DiffFile    = CreateSampleFile(),
            ThemePreset = DiffThemePreset.VisualStudio,
            DiffPreset  = DiffThemePreset.Monokai
        };

        var window = ShownInView(view);

        try
        {
            Assert.That(((ISolidColorBrush)As<DiffSplitContentRow>(view.Rows[2]).Right.ContentBackground!).Color,
                        Is.EqualTo(Color.Parse("#dafbe1")));
        }
        finally
        {
            window.Close();
        }
    }

    // ---- M8: invalid override hex is ignored instead of throwing at render time ----

    [AvaloniaTest]
    public void SyntaxOverrides_InvalidDefaultForegroundHex_IsIgnored()
    {
        var overrides = new DiffSyntaxOverrides { DefaultForeground = "#GGGGGG" };

        // Nothing valid → nothing customized → no resolver at all.
        Assert.That(DiffSyntaxColors.Create(null, null, overrides, ThemeVariant.Dark), Is.Null);

        var view   = new DiffView { DiffFile = CreateSampleFile(), SyntaxOverrides = overrides };
        var window = ShownInView(view);

        try
        {
            Assert.DoesNotThrow(() => view.SyntaxOverrides = new DiffSyntaxOverrides { DefaultForeground = "#GGGGGG" },
                                "an invalid hex must not surface as a render-time FormatException");
        }
        finally
        {
            window.Close();
        }
    }

    // ---- M8: #RRGGBBAA keeps the documented channel order ----

    [AvaloniaTest]
    public void SyntaxOverrides_RgbaHex_KeepsTheDocumentedChannelOrder()
    {
        var view = new DiffView
        {
            DiffFile        = CreateNoteFile(),
            SyntaxOverrides = new DiffSyntaxOverrides { DefaultForeground = "#FF000080" }
        };

        var window = ShownInView(view);

        try
        {
            // The note diff pairs into one split row: the add side carries the "world" text.
            var worldRow = view.Rows.OfType<DiffSplitContentRow>().First(r => r.Right.Text == "world");
            var runs     = worldRow.Right.SyntaxRuns;

            Assert.That(runs, Is.Not.Empty);

            Assert.That(runs!.Select(r => ((ISolidColorBrush)r.Foreground).Color),
                        Has.Some.EqualTo(Color.FromArgb(128, 255, 0, 0)),
                        "#RRGGBBAA means 50% red — Avalonia's #AARRGGBB reading turns it into opaque navy");
            Assert.That(runs.Select(r => r.Foreground), Has.All.InstanceOf<ImmutableSolidColorBrush>());
        }
        finally
        {
            window.Close();
        }
    }

    // ---- M8: the default foreground colors plain text (no syntax nodes) ----

    [AvaloniaTest]
    public void SyntaxOverrides_DefaultForegroundColorsPlainTextRows()
    {
        var view = new DiffView
        {
            DiffFile        = CreateNoteFile(),
            SyntaxOverrides = new DiffSyntaxOverrides { DefaultForeground = "#123456" }
        };

        var window = ShownInView(view);

        try
        {
            // The note diff pairs into one split row: the add side carries the "world" text.
            var worldRow = view.Rows.OfType<DiffSplitContentRow>().First(r => r.Right.Text == "world");
            var runs     = worldRow.Right.SyntaxRuns;

            Assert.That(runs, Is.Not.Null,
                        "a .txt row has no syntax nodes — the default-foreground override must still color it");
            Assert.That(runs!.Select(r => ((ISolidColorBrush)r.Foreground).Color),
                        Has.Some.EqualTo(Color.Parse("#123456")));
        }
        finally
        {
            window.Close();
        }
    }

    // ---- M8: the shared preset brushes are immutable ----

    [AvaloniaTest]
    public void PresetBrushes_AreImmutableSoSharedInstancesStaySafe()
    {
        var viewA = new DiffView { DiffFile = CreateSampleFile(), ThemePreset = DiffThemePreset.VisualStudio };
        var viewB = new DiffView { DiffFile = CreateSampleFile(), ThemePreset = DiffThemePreset.VisualStudio };

        var windowA = ShownInView(viewA);
        var windowB = ShownInView(viewB);

        try
        {
            var brushA = As<DiffSplitContentRow>(viewA.Rows[2]).Right.ContentBackground;
            var brushB = As<DiffSplitContentRow>(viewB.Rows[2]).Right.ContentBackground;

            // Both views resolve the same process-wide preset instance — safe only because the
            // exposed brushes cannot be mutated (a mutable brush let one view repaint the other).
            Assert.That(brushA, Is.InstanceOf<ImmutableSolidColorBrush>());
            Assert.That(brushB, Is.InstanceOf<ImmutableSolidColorBrush>());
            Assert.That(((ISolidColorBrush)brushB!).Color, Is.EqualTo(((ISolidColorBrush)brushA!).Color));
        }
        finally
        {
            windowA.Close();
            windowB.Close();
        }
    }

    // ---- M7: the unified comment card follows the single-number-column mode ----

    [AvaloniaTest]
    public void UnifiedCommentCard_SingleNumberColumn_AlignsWithTheContent()
    {
        var view = new DiffView
        {
            DiffFile                  = CreateNoteFile(),
            ViewMode                  = DiffViewMode.Unified,
            UseSingleLineNumberColumn = true,
            Comments =
            [
                new DiffComment(new DiffCommentAnchor(null, SplitSide.New, 1, 1),
                                null, "note")
            ]
        };

        var window = ShownInView(view);

        try
        {
            var items      = view.GetVisualDescendants().OfType<ItemsControl>().First();
            var contentRow = view.Rows.OfType<DiffUnifiedContentRow>().First(r => r.NewLineNumber == 1);
            var card       = view.Rows.OfType<DiffUnifiedCommentRow>().Single();

            var content = items.ContainerFromItem(contentRow)!.GetVisualDescendants().OfType<Border>()
                               .Single(b => b.Classes.Contains("diff-line-content"));
            var cardBorder = items.ContainerFromItem(card)!.GetVisualDescendants().OfType<Border>()
                                  .Single(b => b.Classes.Contains("diff-comment-card"));

            var contentX = content.TransformToVisual(view)!.Value.Transform(new Point()).X;
            var cardX    = cardBorder.TransformToVisual(view)!.Value.Transform(new Point()).X;

            // The card carries the same 8px margin in both gutter modes — the collapsed first
            // number gutter must not shift it by a number-column width.
            Assert.That(cardX - contentX, Is.EqualTo(8).Within(0.1));
        }
        finally
        {
            window.Close();
        }
    }

    // ---- M7: the expansion anchor survives a revealed comment card ----

    [AvaloniaTest]
    public void HunkExpansion_WithRevealedCommentCard_KeepsTheViewportAnchor()
    {
        var view = new DiffView
        {
            DiffFile = DiffCopyTests.CreateExpandableFile(),
            Comments =
            [
                new DiffComment(new DiffCommentAnchor(null, SplitSide.Old, 45, 87),
                                null, "comment revealed by expansion")
            ]
        };

        var window = new Window { Content = view, Width = 900, Height = 220 };

        window.Show();
        RunLayoutPass(window);

        try
        {
            var items    = view.GetVisualDescendants().OfType<ItemsControl>().Single();
            var scroller = view.GetVisualDescendants().OfType<ScrollViewer>().Single();

            // The expandable fixture's flat list: [0] hunk (hides old 1-37), …, [8] hunk (hides
            // old 45-87), …, [17] trailing hunk. The comment anchors 45-87, so the hunk whose
            // down-expansion reveals the card is the second placeholder.
            var middle    = view.Rows.OfType<DiffSplitHunkRow>().ElementAt(1);
            var rowHeight = items.ContainerFromItem(view.Rows[6])!.Bounds.Height;

            double Top(object row) => items.ContainerFromItem(row)!
                                           .TransformToVisual(scroller)!.Value.Transform(new Point()).Y;

            scroller.Offset = new Vector(0, Top(middle) - 2 * rowHeight);
            RunLayoutPass(window);

            var before = Top(middle);
            var key    = middle.HunkIndex;

            view.ExpandHunkDownCommand.Execute(middle);

            // Pass 1 applies the height estimate; the following passes realize the anchor around
            // it and let the post-layout correction converge on the real delta (the card next to
            // the anchor is measured with an estimated height on its first realization).
            for (var pass = 0; pass < 5; pass++) RunLayoutPass(window);

            var updated = view.Rows.OfType<DiffSplitHunkRow>().Single(r => r.HunkIndex == key);
            var card    = view.Rows.OfType<DiffSplitCommentRow>().Single();

            Assert.That(items.ContainerFromItem(card)!.Bounds.Height, Is.GreaterThan(rowHeight),
                        "fixture check: the revealed card must be the variable-height row the estimate misses");
            Assert.That(Top(updated), Is.EqualTo(before).Within(1),
                        "the clicked placeholder must keep its viewport position despite the card height");
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaTest]
    public void HunkExpansion_WithCardTallerThanTheViewport_KeepsTheViewportAnchor()
    {
        // The 20-line comment body renders a ~330px card inside a 220px viewport: the height
        // estimate misses by more than the viewport, the anchor lands outside the realized
        // window, and the correction used to give up after three layout passes.
        var comment = string.Join('\n', Enumerable.Range(1, 20).Select(i => $"comment line {i}"));

        var view = new DiffView
        {
            DiffFile = DiffCopyTests.CreateExpandableFile(),
            Comments = [new DiffComment(new DiffCommentAnchor(null, SplitSide.Old, 45, 87), null, comment)]
        };

        var window = new Window { Content = view, Width = 900, Height = 220 };

        window.Show();
        RunLayoutPass(window);

        try
        {
            var items    = view.GetVisualDescendants().OfType<ItemsControl>().Single();
            var scroller = view.GetVisualDescendants().OfType<ScrollViewer>().Single();

            var middle    = view.Rows.OfType<DiffSplitHunkRow>().ElementAt(1);
            var rowHeight = items.ContainerFromItem(view.Rows[6])!.Bounds.Height;

            double Top(object row) => items.ContainerFromItem(row)!
                                           .TransformToVisual(scroller)!.Value.Transform(new Point()).Y;

            scroller.Offset = new Vector(0, Top(middle) - 2 * rowHeight);
            RunLayoutPass(window);

            var before = Top(middle);
            var key    = middle.HunkIndex;

            view.ExpandHunkDownCommand.Execute(middle);

            // The estimate lands first; the probe then walks the unrealized anchor into the
            // realized window (several viewport-sized scrolls for a card this tall) and the
            // post-layout correction re-anchors exactly.
            for (var pass = 0; pass < 24; pass++) RunLayoutPass(window);

            var updated = view.Rows.OfType<DiffSplitHunkRow>().Single(r => r.HunkIndex == key);
            var card    = view.Rows.OfType<DiffSplitCommentRow>().Single();

            Assert.That(items.ContainerFromItem(card)!.Bounds.Height, Is.GreaterThan(scroller.Viewport.Height),
                        "fixture check: the card must be taller than the viewport for this regression");
            Assert.That(Top(updated), Is.EqualTo(before).Within(1),
                        "the clicked placeholder must keep its viewport position even when the card exceeds the viewport");
        }
        finally
        {
            window.Close();
        }
    }

    // ---- M7: a fully hidden selection cannot start the comment flow ----

    [AvaloniaTest]
    public void CommentCommand_CollapsedSelection_DisabledLikeTheCopyCommand()
    {
        var view   = new DiffView { DiffFile = DiffCopyTests.CreateExpandableFile(), IsSelectionEnabled = true };
        var window = ShownInView(view);

        try
        {
            var items = view.GetVisualDescendants().OfType<ItemsControl>().Single();

            // Reveal the hidden middle (through the model — no scroll anchoring), select two of
            // the revealed rows, then collapse again: the whole selection hides behind the hunk.
            // The fixture's second hunk placeholder (Rows[8]) hides the old lines 45-87.
            var hunkRow = (DiffSplitHunkRow)view.Rows[8];

            view.DiffFile!.OnSplitHunkExpand(HunkExpandDirection.All, hunkRow.HunkIndex);
            RunLayoutPass(window);

            var first  = view.Rows.OfType<DiffSplitContentRow>().First(r => r.Left.Text == "ctx 045");
            var second = view.Rows.OfType<DiffSplitContentRow>().First(r => r.Left.Text == "ctx 046");

            window.MouseDown(DiffCopyTests.CellCenter(window, items, first, 0), MouseButton.Left);
            window.MouseMove(DiffCopyTests.CellCenter(window, items, second, 0));
            window.MouseUp(DiffCopyTests.CellCenter(window, items, second, 0), MouseButton.Left);

            Assert.That(view.BeginCommentCommand.CanExecute(null), Is.True, "sanity: visible selection can comment");

            view.DiffFile!.OnAllCollapse(ExpandViewMode.Split);

            Assert.That(view.CopySelectionCommand.CanExecute(null), Is.False, "parity: copy goes dark");
            Assert.That(view.BeginCommentCommand.CanExecute(null), Is.False,
                        "a selection hidden behind a collapsed hunk must not open the comment editor");
        }
        finally
        {
            window.Close();
        }
    }
}
