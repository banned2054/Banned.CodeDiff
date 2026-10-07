using Avalonia.Headless.NUnit;
using Avalonia.Media;
using Avalonia.Styling;

using Banned.CodeDiff.Avalonia.Models;
using Banned.CodeDiff.Avalonia.Utils;
using Banned.CodeDiff.Services;
using Banned.CodeDiff.Services.TextMate;

using NUnit.Framework;

namespace Banned.CodeDiff.Avalonia.Tests;

/// <summary>
///     M8 resolution chain: host palette overrides beat the independent diff preset, which beats
///     the combined theme preset, which beats the GitHub baseline; every slot resolves
///     independently; presets without a light variant fall back to GitHub light; selected lines
///     carry explicit per-kind backgrounds; the syntax resolver's override semantics hold at the
///     scope level.
/// </summary>
public class DiffThemePresetTests
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
        +/* done */
        """;

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

    // ---- Diff palette resolution ----

    [AvaloniaTest]
    public void Resolution_GitHubPresetEqualsUnsetBaseline()
    {
        var unset  = DiffBrushes.Get(ThemeVariant.Light);
        var github = DiffBrushes.Get(ThemeVariant.Light, null, DiffThemePreset.GitHub, null);

        Assert.That(github.AddContent, Is.SameAs(unset.AddContent));
        Assert.That(github.ContextContent, Is.SameAs(unset.ContextContent));
        Assert.That(github.MultiSelectOverlay, Is.SameAs(unset.MultiSelectOverlay));
    }

    [AvaloniaTest]
    public void Resolution_IndependentDiffPresetBeatsCombinedPreset()
    {
        // Combined = Visual Studio, but the independent diff preset pins the diff side to
        // GitHub: row backgrounds keep GitHub values.
        var set = DiffBrushes.Get(ThemeVariant.Light, null, DiffThemePreset.VisualStudio,
                                  DiffThemePreset.GitHub);

        AssertColor(set.AddContent, "#dafbe1");
        AssertColor(set.ContextContent, "#ffffff");

        // Without the independent pin the combined preset wins.
        var combinedOnly = DiffBrushes.Get(ThemeVariant.Light, null, DiffThemePreset.VisualStudio, null);

        AssertColor(combinedOnly.AddContent, "#e6ffec");
        AssertColor(combinedOnly.ContextContent, "#ffffff");
    }

    [AvaloniaTest]
    public void Resolution_HostOverrideBeatsEveryPreset_OtherSlotsKeepPresetValues()
    {
        var hostContext = new SolidColorBrush(Colors.Honeydew);

        var palette = new DiffPalette { Light = new DiffPaletteColors { ContextBackground = hostContext } };

        var set = DiffBrushes.Get(ThemeVariant.Light, palette, DiffThemePreset.VisualStudio, null);

        // The overridden slot wins over both presets...
        Assert.That(set.ContextContent, Is.SameAs(hostContext));
        Assert.That(set.ContextNumber, Is.SameAs(hostContext));

        // ...every other slot keeps the preset value.
        AssertColor(set.AddContent, "#e6ffec");
        AssertColor(set.Splitter, "#e0e0e0");
        AssertColor(set.SelectedAdd, "#add6ff");
    }

    [AvaloniaTest]
    public void Resolution_ClearingHostOverride_FallsBackToCurrentPresetValue()
    {
        var palette = new DiffPalette
        {
            Light = new DiffPaletteColors { AddLineBackground = new SolidColorBrush(Colors.MistyRose) }
        };

        var withOverride = DiffBrushes.Get(ThemeVariant.Light, palette, DiffThemePreset.VisualStudio, null);

        // The host override wins over the Visual Studio preset.
        Assert.That(withOverride.AddContent, Is.InstanceOf<ISolidColorBrush>());
        Assert.That(((ISolidColorBrush)withOverride.AddContent).Color, Is.EqualTo(Colors.MistyRose));

        // Drop the override — the same chain resolves to the Visual Studio preset value,
        // not back to GitHub.
        var withoutOverride = DiffBrushes.Get(ThemeVariant.Light, null, DiffThemePreset.VisualStudio, null);

        AssertColor(withoutOverride.AddContent, "#e6ffec");
        Assert.That(withOverride.AddContent, Is.Not.SameAs(withoutOverride.AddContent));
    }

    [AvaloniaTest]
    public void Resolution_FineGrainedSlotBeatsM7CoarseSlot()
    {
        var coarse = new SolidColorBrush(Colors.MistyRose);
        var fine   = new SolidColorBrush(Colors.Honeydew);

        var palette = new DiffPalette
        {
            Light = new DiffPaletteColors { AddLineBackground = coarse, AddNumberBackground = fine }
        };

        var set = DiffBrushes.Get(ThemeVariant.Light, palette, null, null);

        Assert.That(set.AddNumber, Is.SameAs(fine));
        Assert.That(set.AddContent, Is.SameAs(coarse));
    }

    [AvaloniaTest]
    public void Resolution_MonokaiAndCodex_LightVariantFallsBackToGitHubLight()
    {
        var monokaiLight = DiffBrushes.Get(ThemeVariant.Light, null, DiffThemePreset.Monokai, null);
        var githubLight  = DiffBrushes.Get(ThemeVariant.Light);

        Assert.That(monokaiLight.AddContent, Is.SameAs(githubLight.AddContent));
        Assert.That(monokaiLight.ContextContent, Is.SameAs(githubLight.ContextContent));

        var codexLight = DiffBrushes.Get(ThemeVariant.Light, null, DiffThemePreset.Codex, null);

        Assert.That(codexLight.DeleteContent, Is.SameAs(githubLight.DeleteContent));

        // The dark variants do define their own values.
        var monokaiDark = DiffBrushes.Get(ThemeVariant.Dark, null, DiffThemePreset.Monokai, null);

        AssertColor(monokaiDark.ContextContent, "#272822");
        Assert.That(monokaiDark.ContextContent, Is.Not.SameAs(githubLight.ContextContent));
    }

    [AvaloniaTest]
    public void Resolution_SelectedBackgrounds_PerKind_GitHubBaselineKeepsTheOverlay()
    {
        var baseline = DiffBrushes.Get(ThemeVariant.Light);

        // GitHub resolves every per-kind slot through the M7 coarse overlay — the M7 look and
        // the M7 API behavior are unchanged.
        Assert.That(baseline.SelectedAdd, Is.SameAs(baseline.MultiSelectOverlay));
        Assert.That(baseline.SelectedDelete, Is.SameAs(baseline.MultiSelectOverlay));
        Assert.That(baseline.SelectedContext, Is.SameAs(baseline.MultiSelectOverlay));
        AssertOverlay(baseline.SelectedAdd, "#f0c000", 0.15);

        var vs = DiffBrushes.Get(ThemeVariant.Dark, null, DiffThemePreset.VisualStudio, null);

        // A preset defining the slots replaces all three kinds together.
        AssertColor(vs.SelectedAdd, "#264f78");
        AssertColor(vs.SelectedDelete, "#264f78");
        AssertColor(vs.SelectedContext, "#264f78");
        Assert.That(vs.SelectedAdd, Is.Not.SameAs(vs.MultiSelectOverlay));
    }

    [AvaloniaTest]
    public void Resolution_HostCoarseSelection_BeatsPresetFineSlots()
    {
        var hotPink = new SolidColorBrush(Colors.HotPink);

        var palette = new DiffPalette
        {
            Light = new DiffPaletteColors { SelectionHighlight = hotPink }
        };

        var set = DiffBrushes.Get(ThemeVariant.Light, palette, DiffThemePreset.VisualStudio, null);

        // The host's M7 coarse slot colors the selected rows even against the combined
        // preset's fine-grained ones — "host overrides first" holds for selection too.
        Assert.That(set.SelectedAdd, Is.SameAs(hotPink));
        Assert.That(set.SelectedDelete, Is.SameAs(hotPink));
        Assert.That(set.SelectedContext, Is.SameAs(hotPink));
        Assert.That(set.MultiSelectOverlay, Is.SameAs(hotPink),
                    "the drag overlay resolves from the same coarse slot");
    }

    [AvaloniaTest]
    public void Resolution_IndependentGitHubPreset_PinsSelectionToItsGenericColor()
    {
        var set = DiffBrushes.Get(ThemeVariant.Light, null, DiffThemePreset.VisualStudio,
                                  DiffThemePreset.GitHub);

        var githubSelection = DiffThemePresets.GetDiffColors(ThemeVariant.Light, DiffThemePreset.GitHub)!
                              .SelectionHighlight;

        // GitHub defines no per-kind fine slots — its generic SelectionHighlight must pin the
        // selection instead of letting the combined preset's #add6ff leak through.
        Assert.That(set.SelectedAdd, Is.SameAs(githubSelection));
        Assert.That(set.SelectedDelete, Is.SameAs(githubSelection));
        Assert.That(set.SelectedContext, Is.SameAs(githubSelection));

        // Row backgrounds still come from the pinned GitHub preset as before.
        AssertColor(set.AddContent, "#dafbe1");
    }

    [AvaloniaTest]
    public void Resolution_CanvasAndTextSelection_GitHubNull_PresetsResolve()
    {
        var github = DiffBrushes.Get(ThemeVariant.Light);

        Assert.That(github.CanvasBackground, Is.Null);
        Assert.That(github.TextSelectionBackground, Is.Null);
        Assert.That(github.TextSelectionForeground, Is.Null);

        var vs = DiffBrushes.Get(ThemeVariant.Dark, null, DiffThemePreset.VisualStudio, null);

        AssertColor(vs.CanvasBackground!, "#1e1e1e");

        // The host palette can still define them on top of the GitHub baseline.
        var palette = new DiffPalette
        {
            Light = new DiffPaletteColors
            {
                TextSelectionBackground = new SolidColorBrush(Colors.Gold)
            }
        };

        var overridden = DiffBrushes.Get(ThemeVariant.Light, palette);

        Assert.That(overridden.TextSelectionBackground, Is.InstanceOf<ISolidColorBrush>());
        Assert.That(((ISolidColorBrush)overridden.TextSelectionBackground!).Color,
                    Is.EqualTo(Colors.Gold));
    }

    // ---- Syntax resolver semantics (scope level, synthetic stacks) ----

    [AvaloniaTest]
    public void SyntaxResolver_Uncustomized_ReturnsNullForTheFinalColorPath()
    {
        Assert.That(DiffSyntaxColors.Create(null, null, null, ThemeVariant.Dark), Is.Null);
        Assert.That(DiffSyntaxColors.Create(DiffThemePreset.GitHub, null, null, ThemeVariant.Dark), Is.Null);
    }

    [AvaloniaTest]
    public void SyntaxResolver_Monokai_RecolorsScopes_OverrideBeatsPresetRule()
    {
        // Override rules beat preset rules; preset rules beat the default.
        var overrides = new DiffSyntaxOverrides { Overrides = [new DiffSyntaxOverride { Scope = "comment", Color = "#ff0000" }] };

        var colors = DiffSyntaxColors.Create(DiffThemePreset.Monokai, null, overrides, ThemeVariant.Dark);

        Assert.That(colors, Is.Not.Null);

        Assert.That(colors!.MatchForeground(["comment", "source.cs"]), Is.EqualTo("#FF0000"));
        Assert.That(colors.MatchForeground(["keyword", "source.cs"]), Is.EqualTo("#F92672"));
        Assert.That(colors.MatchForeground(["string.quoted.double.cs", "source.cs"]),
                    Is.EqualTo("#E6DB74"));
        Assert.That(colors.MatchForeground(["constant.numeric.cs", "source.cs"]), Is.EqualTo("#AE81FF"));
    }

    [AvaloniaTest]
    public void SyntaxResolver_DefaultForegroundOverride_NeverRewritesRuleHits()
    {
        var overrides = new DiffSyntaxOverrides { DefaultForeground = "#123456" };

        var colors = DiffSyntaxColors.Create(DiffThemePreset.Monokai, null, overrides, ThemeVariant.Dark);

        Assert.That(colors, Is.Not.Null);

        // A token hitting a preset rule keeps the preset color...
        Assert.That(colors!.MatchForeground(["keyword", "source.cs"]), Is.EqualTo("#F92672"));

        // ...a token that would take the theme default gets the override instead.
        Assert.That(colors.MatchForeground(["punctuation.terminator.cs", "source.cs"]),
                    Is.EqualTo("#123456"));
    }

    [AvaloniaTest]
    public void SyntaxResolver_ScopeOverridesSurvivePresetSwitch()
    {
        var overrides = new DiffSyntaxOverrides
        {
            Overrides = [new DiffSyntaxOverride { Scope = "comment", Color = "#ff0000" }]
        };

        var monokai = DiffSyntaxColors.Create(DiffThemePreset.Monokai, null, overrides, ThemeVariant.Dark);

        Assert.That(monokai!.MatchForeground(["comment", "source.cs"]), Is.EqualTo("#FF0000"));
        Assert.That(monokai.MatchForeground(["keyword", "source.cs"]), Is.EqualTo("#F92672"));

        // Switching the preset keeps the overrides; the untouched scopes re-resolve.
        var vs = DiffSyntaxColors.Create(DiffThemePreset.VisualStudio, null, overrides, ThemeVariant.Dark);

        Assert.That(vs!.MatchForeground(["comment", "source.cs"]), Is.EqualTo("#FF0000"));
        Assert.That(vs.MatchForeground(["keyword", "source.cs"]), Is.EqualTo("#C586C0"));
    }

    [AvaloniaTest]
    public void SyntaxResolver_MonokaiLight_FallsBackToGitHubLightTheme()
    {
        var monokaiLight = DiffSyntaxColors.Create(DiffThemePreset.Monokai, null, null, ThemeVariant.Light);

        Assert.That(monokaiLight, Is.Not.Null);

        // The uncustomized reference is the github-light matcher itself (Create returns null
        // for it — the final-color path).
        var githubLight = DiffSyntaxThemes.GetMatcher(DiffSyntaxThemes.GitHubLight);

        Assert.That(githubLight, Is.Not.Null);

        // The fallback resolver matches the GitHub light colors (no color inversion).
        Assert.That(monokaiLight!.MatchForeground(["keyword", "source.cs"]),
                    Is.EqualTo(githubLight!.MatchForeground(["keyword", "source.cs"])));
        Assert.That(monokaiLight.MatchForeground(["comment", "source.cs"]),
                    Is.EqualTo(githubLight.MatchForeground(["comment", "source.cs"])));
    }

    [AvaloniaTest]
    public void SyntaxResolver_InvalidOverrideValues_AreIgnored()
    {
        var overrides = new DiffSyntaxOverrides
        {
            DefaultForeground = "not-a-color",
            Overrides = [new DiffSyntaxOverride { Scope = "comment", Color = "zzz" }]
        };

        // Everything invalid → resolver stays null (nothing customized effectively).
        Assert.That(DiffSyntaxColors.Create(null, null, overrides, ThemeVariant.Dark), Is.Null);

        // A valid override next to an invalid one keeps only the valid rule.
        var mixed = new DiffSyntaxOverrides
        {
            Overrides =
            [
                new DiffSyntaxOverride { Scope = "comment", Color = "zzz" },
                new DiffSyntaxOverride { Scope = "keyword", Color = "#112233" }
            ]
        };

        var colors = DiffSyntaxColors.Create(null, null, mixed, ThemeVariant.Dark);

        Assert.That(colors, Is.Not.Null);
        Assert.That(colors!.MatchForeground(["keyword", "source.cs"]), Is.EqualTo("#112233"));
    }

    // ---- Integration through the row builder (tokenization output) ----

    private static DiffFile CreateSampleFile()
    {
        // The file name drives the language detection ("Program.cs" → C#) for the syntax runs.
        var file = new DiffFile("Program.cs", "", "Program.cs", "", [Sample]);

        file.Init();
        file.BuildSplitDiffLines();
        file.BuildUnifiedDiffLines();

        return file;
    }

    /// <summary>Row indexes in the split model (see DiffPaletteTests): [4] is the
    /// "Console.WriteLine…" add row, the trailing add comment row sits after the second hunk.</summary>
    private static DiffSyntaxColors? Resolver(DiffThemePreset? syntaxPreset, DiffThemePreset? themePreset,
                                              DiffSyntaxOverrides? overrides, ThemeVariant variant)
    {
        return DiffSyntaxColors.Create(syntaxPreset, themePreset, overrides, variant);
    }

    [AvaloniaTest]
    public void SyntaxRuns_CommentRow_MonokaiRecolors_WholeLine()
    {
        var file = CreateSampleFile();

        // The trailing "+/* done */" add row is the last content row of the split model.
        var lastContent = file.GetSplitRightLine(file.SplitLineLength - 1);

        Assert.That(lastContent, Is.Not.Null);
        Assert.That(lastContent!.Diff?.Text, Does.Contain("/* done */"));

        var syntaxLine = file.GetNewSyntaxLine(14);

        Assert.That(syntaxLine, Is.Not.Null);

        var github = DiffSyntaxRuns.Extract(syntaxLine, 11, ThemeVariant.Dark);
        var monokai = DiffSyntaxRuns.Extract(syntaxLine, 11, ThemeVariant.Dark,
                                             Resolver(DiffThemePreset.Monokai, null, null, ThemeVariant.Dark));

        Assert.That(github, Is.Not.Null);
        Assert.That(monokai, Is.Not.Null);

        // The whole line is comment text; the preset recolors it (github-dark → #6A737D per
        // the syntax golden, monokai → #88846F). A block comment provides a compact
        // whole-line sample for the recoloring assertions.
        Assert.That(RunColors(github!), Has.Some.EqualTo(Color.Parse("#6A737D")));
        Assert.That(RunColors(monokai!), Has.Some.EqualTo(Color.Parse("#88846F")));
        Assert.That(RunColors(monokai!), Has.None.EqualTo(Color.Parse("#6A737D")));

        // The scope override rewrites only the comment scope.
        var overridden = DiffSyntaxRuns.Extract(syntaxLine, 11, ThemeVariant.Dark,
                                                Resolver(DiffThemePreset.Monokai, null,
                                                         new DiffSyntaxOverrides
                                                         {
                                                             Overrides =
                                                             [
                                                                 new DiffSyntaxOverride
                                                                 {
                                                                     Scope = "comment",
                                                                     Color = "#ff0000"
                                                                 }
                                                             ]
                                                         },
                                                         ThemeVariant.Dark));

        Assert.That(RunColors(overridden!), Has.Some.EqualTo(Color.Parse("#FF0000")));
        Assert.That(RunColors(overridden!), Has.None.EqualTo(Color.Parse("#88846F")));
    }

    private static IReadOnlyList<Color> RunColors(IReadOnlyList<DiffSyntaxRun> runs)
    {
        return [.. runs.Select(run => ((ISolidColorBrush)run.Foreground).Color)];
    }
}
