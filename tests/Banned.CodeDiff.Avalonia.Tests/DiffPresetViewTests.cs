using Avalonia.Controls;
using Avalonia.Controls.Shapes;
using Avalonia.Headless.NUnit;
using Avalonia.Media;
using Avalonia.Styling;
using Avalonia.VisualTree;

using Banned.CodeDiff.Avalonia.Models;
using Banned.CodeDiff.Avalonia.Views;
using Banned.CodeDiff.Services;

using NUnit.Framework;

namespace Banned.CodeDiff.Avalonia.Tests;

/// <summary>
///     M8 preset wiring on the control: <see cref="DiffView.ThemePreset" /> /
///     <see cref="DiffView.DiffPreset" /> / <see cref="DiffView.SyntaxPreset" /> /
///     <see cref="DiffView.SyntaxOverrides" /> rebuild the rows with the resolved brushes, the
///     canvas background applies only when a preset defines one, selected rows bind the explicit
///     per-kind background, and separate <see cref="DiffView" /> instances never share theme
///     state.
/// </summary>
public class DiffPresetViewTests
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

    private static DiffFile CreateSampleFile()
    {
        // The file name drives the language detection ("Program.cs" → C#) for the syntax runs.
        var file = new DiffFile("Program.cs", "", "Program.cs", "", [Sample]);

        file.Init();
        file.BuildSplitDiffLines();
        file.BuildUnifiedDiffLines();

        return file;
    }

    private static Window ShownInView(DiffView view, ThemeVariant? variant = null)
    {
        var window = new Window { Content = view, Width = 900, Height = 600 };

        if (variant != null) window.RequestedThemeVariant = variant;

        window.Show();
        window.GetLayoutManager()!.ExecuteLayoutPass();

        return window;
    }

    private static T As<T>(DiffRow row) where T : class
    {
        Assert.That(row, Is.TypeOf<T>());

        return (T)(object)row;
    }

    private static void AssertColor(IBrush? brush, string hex)
    {
        Assert.That(brush, Is.InstanceOf<ISolidColorBrush>());
        Assert.That(((ISolidColorBrush)brush!).Color, Is.EqualTo(Color.Parse(hex)));
    }

    // Split rows: [0] ctx(1|1) [1] ctx(2|2) [2] del(3|-)+add(-|3) [3] ctx(4|4) [4] add(-|5).

    [AvaloniaTest]
    public void ThemePreset_RebuildsRowsWithPresetBrushes()
    {
        var view = new DiffView { DiffFile = CreateSampleFile() };

        ShownInView(view);

        AssertColor(As<DiffSplitContentRow>(view.Rows[2]).Right.ContentBackground, "#dafbe1");

        view.ThemePreset = DiffThemePreset.VisualStudio;

        // Fresh row objects after the rebuild — Visual Studio light values.
        var add = As<DiffSplitContentRow>(view.Rows[2]);

        AssertColor(add.Right.ContentBackground, "#e6ffec");
        AssertColor(add.Right.NumberBackground, "#ccffd8");
        AssertColor(add.Left.ContentBackground, "#ffebe9");
        AssertColor(add.Right.SelectedBackground, "#add6ff");
    }

    [AvaloniaTest]
    public void ThemePreset_InDarkWindow_UsesTheDarkVariantValues()
    {
        var view = new DiffView { DiffFile = CreateSampleFile(), ThemePreset = DiffThemePreset.Monokai };

        ShownInView(view, ThemeVariant.Dark);

        Assert.That(view.ActualThemeVariant, Is.EqualTo(ThemeVariant.Dark));

        var add = As<DiffSplitContentRow>(view.Rows[2]);

        AssertColor(add.Right.ContentBackground, "#1f3a26");
        AssertColor(add.Right.NumberBackground, "#2a4d33");
    }

    [AvaloniaTest]
    public void CanvasBackground_AppliesOnPreset_AndClearsBackToGitHub()
    {
        var view = new DiffView { DiffFile = CreateSampleFile() };

        ShownInView(view);

        // The GitHub baseline defines no canvas — the host-controlled background stays null.
        Assert.That(view.Background, Is.Null);

        view.ThemePreset = DiffThemePreset.VisualStudio;

        Assert.That(view.Background, Is.InstanceOf<ISolidColorBrush>());
        Assert.That(((ISolidColorBrush)view.Background!).Color, Is.EqualTo(Color.Parse("#ffffff")));

        view.ThemePreset = null;

        Assert.That(view.Background, Is.Null);
    }

    [AvaloniaTest]
    public void DiffPreset_KeepsGitHubSyntax_WhileRecoloringRows()
    {
        var view = new DiffView
        {
            DiffFile = CreateSampleFile(),
            // Monokai diff backgrounds with the default GitHub syntax theme.
            ThemePreset = DiffThemePreset.Monokai,
            DiffPreset  = DiffThemePreset.GitHub
        };

        ShownInView(view, ThemeVariant.Dark);

        var add = As<DiffSplitContentRow>(view.Rows[2]);

        // Diff side: pinned GitHub dark values.
        AssertColor(add.Right.ContentBackground, "#18271f");
        AssertColor(add.Left.ContentBackground, "#23191c");

        // Syntax side: the Monokai comment line ("+/* done */") re-resolves to Monokai
        // (github-dark block comment → #6A737D, per the syntax golden).
        var commentRow = view.Rows.OfType<DiffSplitContentRow>()
                               .First(row => row.Right.Text.Contains("/* done */"));
        var runs = commentRow.Right.SyntaxRuns;

        Assert.That(runs, Is.Not.Null);
        Assert.That(runs!.Select(r => ((ISolidColorBrush)r.Foreground).Color),
                    Has.Some.EqualTo(Color.Parse("#88846F")));
        Assert.That(runs.Select(r => ((ISolidColorBrush)r.Foreground).Color),
                    Has.None.EqualTo(Color.Parse("#6A737D")));
    }

    [AvaloniaTest]
    public void SyntaxOverrides_RebuildRows_AndOnlyRewriteTheTargetScope()
    {
        var view = new DiffView { DiffFile = CreateSampleFile() };

        ShownInView(view);

        var commentRow = view.Rows.OfType<DiffSplitContentRow>()
                               .First(row => row.Right.Text.Contains("/* done */"));
        var before = commentRow.Right.SyntaxRuns;

        Assert.That(before!, Is.Not.Empty);
        Assert.That(before.Select(r => ((ISolidColorBrush)r.Foreground).Color),
                    Has.Some.EqualTo(Color.Parse("#6A737D"))); // github block comment

        view.SyntaxOverrides = new DiffSyntaxOverrides
        {
            Overrides = [new DiffSyntaxOverride { Scope = "comment", Color = "#ff0000" }]
        };

        commentRow = view.Rows.OfType<DiffSplitContentRow>()
                           .First(row => row.Right.Text.Contains("/* done */"));

        Assert.That(commentRow.Right.SyntaxRuns!.Select(r => ((ISolidColorBrush)r.Foreground).Color),
                    Has.Some.EqualTo(Color.Parse("#FF0000")));

        // A non-comment row keeps the preset (github-light) colors: the "using" keyword.
        var keywordRow = view.Rows.OfType<DiffSplitContentRow>()
                               .First(row => row.Right.Text.StartsWith("using", StringComparison.Ordinal));
        var keywordRuns = keywordRow.Right.SyntaxRuns;

        Assert.That(keywordRuns, Is.Not.Null);
        AssertColor(keywordRuns![0].Foreground, "#D73A49"); // github-light keyword (bundled github theme)
    }

    [AvaloniaTest]
    public void SelectedBackground_PerKind_BindsIntoTheTemplate()
    {
        var selectedAdd = new SolidColorBrush(Colors.LimeGreen);

        var view = new DiffView
        {
            DiffFile           = CreateSampleFile(),
            IsSelectionEnabled = true,
            Palette            = new DiffPalette
            {
                Light = new DiffPaletteColors
                {
                    // Only the add kind gets an explicit selected background — per-slot independence.
                    SelectedAddBackground = selectedAdd
                }
            }
        };

        var window = ShownInView(view);

        var addRow = As<DiffSplitContentRow>(view.Rows[2]);
        var delRow = As<DiffSplitContentRow>(view.Rows[1]);

        // The add side resolves the explicit slot; the delete side keeps the M7 overlay.
        Assert.That(addRow.Right.SelectedBackground, Is.SameAs(selectedAdd));
        Assert.That(delRow.Left.SelectedBackground, Is.SameAs(delRow.Left.SelectionOverlay));

        view.SetPreselectedLines(newLines : [3]);

        Assert.That(addRow.Right.IsSelected, Is.True, "the add cell (new line 3) must be flagged selected");

        var items     = view.GetVisualDescendants().OfType<ItemsControl>().Single();
        var container = items.ContainerFromItem(view.Rows[2]);

        Assert.That(container, Is.Not.Null);

        // The template binds the explicit per-kind brush — a realized, visible rectangle
        // painted with it proves the binding (the M7 overlay brush differs here).
        var selectedRects = container!.GetVisualDescendants()
                                      .OfType<Rectangle>()
                                      .Where(r => r.IsVisible && ReferenceEquals(r.Fill, selectedAdd))
                                      .ToList();

        Assert.That(selectedRects, Is.Not.Empty);

        window.Close();
    }

    [AvaloniaTest]
    public void Instances_ThemeStateIsIsolated()
    {
        var viewA = new DiffView { DiffFile = CreateSampleFile(), ThemePreset = DiffThemePreset.VisualStudio };
        var viewB = new DiffView { DiffFile = CreateSampleFile() };

        var windowA = ShownInView(viewA);
        var windowB = ShownInView(viewB);

        // A: VS light; B: GitHub baseline.
        AssertColor(As<DiffSplitContentRow>(viewA.Rows[2]).Right.ContentBackground, "#e6ffec");
        AssertColor(As<DiffSplitContentRow>(viewB.Rows[2]).Right.ContentBackground, "#dafbe1");

        // Mutating B does not touch A.
        viewB.ThemePreset = DiffThemePreset.Monokai;

        AssertColor(As<DiffSplitContentRow>(viewA.Rows[2]).Right.ContentBackground, "#e6ffec");

        windowA.Close();
        windowB.Close();
    }
}
