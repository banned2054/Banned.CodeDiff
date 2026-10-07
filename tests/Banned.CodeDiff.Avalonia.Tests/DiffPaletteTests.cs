using Avalonia.Controls;
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
///     M7 unified palette entry: <c>null</c> slots keep the built-in upstream colors, set slots
///     drive the row brushes directly (number + content cells of the line kind), and the dark
///     slots apply on the dark variant.
/// </summary>
public class DiffPaletteTests
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
        var file = new DiffFile("", "", "", "", [Sample]);
        file.Init();
        file.BuildSplitDiffLines();
        file.BuildUnifiedDiffLines();

        return file;
    }

    private static DiffView ShownInView(DiffFile file)
    {
        var view   = new DiffView { DiffFile = file };
        var window = new Window { Content = view, Width = 900, Height = 600 };

        window.Show();
        window.GetLayoutManager()!.ExecuteLayoutPass();

        return view;
    }

    private static void AssertColor(IBrush? brush, string hex)
    {
        Assert.That(brush, Is.InstanceOf<ISolidColorBrush>());
        Assert.That(((ISolidColorBrush)brush!).Color, Is.EqualTo(Color.Parse(hex)));
    }

    /// <summary>
    ///     The preset brushes are immutable with the opacity baked into the alpha channel —
    ///     assert the effective color instead of the (now constant) brush opacity.
    /// </summary>
    private static void AssertOverlay(IBrush? brush, string hex, double opacity)
    {
        var color = Color.Parse(hex);

        Assert.That(brush, Is.InstanceOf<ISolidColorBrush>());
        Assert.That(((ISolidColorBrush)brush!).Color,
                    Is.EqualTo(Color.FromArgb((byte)Math.Round(color.A * opacity), color.R, color.G, color.B)));
    }

    // Baseline rows: [0](ctx 1|1) [1](ctx 2|2) [2](ctx 3|3) [3](del 4|-) [4](-|add 4)
    // [5](-|add 5) [6](ctx 5|6) [7](ctx 6|7) [8]hunk …

    [AvaloniaTest]
    public void DefaultBrushes_KeepUpstreamValues()
    {
        var view = ShownInView(CreateSampleFile());
        var add  = (Banned.CodeDiff.Avalonia.Models.DiffSplitContentRow)view.Rows[4];

        AssertColor(add.Right.ContentBackground, "#dafbe1");
        AssertColor(add.Right.NumberBackground, "#aceebb");
        AssertOverlay(add.Left.SelectionOverlay, "#f0c000", 0.15);
        AssertColor(add.Left.SelectionEdgeStrip, "#2588fa");
    }

    [AvaloniaTest]
    public void Palette_OverridesApplyToNumberAndContentCellsAlike()
    {
        var view  = ShownInView(CreateSampleFile());
        var addBg = new SolidColorBrush(Colors.MistyRose);
        var delBg = new SolidColorBrush(Colors.LavenderBlush);
        var ctxBg = new SolidColorBrush(Colors.Honeydew);

        view.Palette = new DiffPalette
        {
            Light = new DiffPaletteColors
            {
                AddLineBackground     = addBg,
                DeleteLineBackground  = delBg,
                ContextBackground     = ctxBg
            }
        };

        // Fresh row objects after the palette rebuild.
        var add = (Banned.CodeDiff.Avalonia.Models.DiffSplitContentRow)view.Rows[4];

        Assert.That(add.Right.ContentBackground, Is.SameAs(addBg));
        Assert.That(add.Right.NumberBackground, Is.SameAs(addBg));

        var delete = (Banned.CodeDiff.Avalonia.Models.DiffSplitContentRow)view.Rows[3];

        Assert.That(delete.Left.ContentBackground, Is.SameAs(delBg));
        Assert.That(delete.Left.NumberBackground, Is.SameAs(delBg));

        var context = (Banned.CodeDiff.Avalonia.Models.DiffSplitContentRow)view.Rows[0];

        Assert.That(context.Left.ContentBackground, Is.SameAs(ctxBg));
        Assert.That(context.Left.NumberBackground, Is.SameAs(ctxBg));

        // Slots left null keep the built-in values.
        AssertOverlay(context.Right.SelectionOverlay, "#f0c000", 0.15);
        AssertColor(context.Right.SelectionEdgeStrip, "#2588fa");
    }

    [AvaloniaTest]
    public void Palette_OverridesSelectionAndCommentBrushes()
    {
        var view  = ShownInView(CreateSampleFile());
        var sel   = new SolidColorBrush(Colors.LimeGreen);
        var edge  = new SolidColorBrush(Colors.Crimson);
        var line  = new SolidColorBrush(Colors.Gold);
        var card  = new SolidColorBrush(Colors.Gainsboro);
        var cardB = new SolidColorBrush(Colors.Gray);

        view.Palette = new DiffPalette
        {
            Light = new DiffPaletteColors
            {
                SelectionHighlight    = sel,
                SelectionEdge         = edge,
                CommentLineHighlight  = line,
                CommentCardBackground = card,
                CommentCardBorder     = cardB
            }
        };

        var row = (Banned.CodeDiff.Avalonia.Models.DiffSplitContentRow)view.Rows[1];

        Assert.That(row.Left.SelectionOverlay, Is.SameAs(sel));
        Assert.That(row.Left.SelectionEdgeStrip, Is.SameAs(edge));
        Assert.That(row.Left.CommentOverlay, Is.SameAs(line));

        view.Comments = [new DiffComment(new DiffCommentAnchor(null, Banned.CodeDiff.Models.SplitSide.Old, 4, 4),
                                         null, "why")];

        var cardRow = view.Rows.OfType<Banned.CodeDiff.Avalonia.Models.DiffSplitCommentRow>().Single();

        Assert.That(cardRow.CardBackground, Is.SameAs(card));
        Assert.That(cardRow.CardBorder, Is.SameAs(cardB));
    }

    [AvaloniaTest]
    public void Palette_DarkSlots_ApplyOnDarkVariant()
    {
        var darkBg = new SolidColorBrush(Colors.MidnightBlue);

        // One dark window from the start — attaching the same view to a second window is
        // invalid; the view rebuilds on ActualThemeVariantChanged (light before attach, dark
        // after) and the dark palette slots apply.
        var view   = new DiffView
        {
            DiffFile = CreateSampleFile(),
            Palette  = new DiffPalette { Dark = new DiffPaletteColors { AddLineBackground = darkBg } }
        };
        var window = new Window
        {
            Content               = view,
            RequestedThemeVariant = ThemeVariant.Dark,
            Width                 = 900,
            Height                = 600
        };

        window.Show();
        window.GetLayoutManager()!.ExecuteLayoutPass();

        var add = (Banned.CodeDiff.Avalonia.Models.DiffSplitContentRow)view.Rows[3];

        Assert.That(view.ActualThemeVariant, Is.EqualTo(ThemeVariant.Dark));
        Assert.That(add.Right.ContentBackground, Is.SameAs(darkBg));
        Assert.That(add.Right.NumberBackground, Is.SameAs(darkBg));
    }
}
