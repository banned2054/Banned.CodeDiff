using Banned.CodeDiff.Models;
using Banned.CodeDiff.Services;
using Banned.CodeDiff.Utils;
using NUnit.Framework;

namespace Banned.CodeDiff.Tests;

/// <summary>
///     Tests for the native copy-text generation (no upstream counterpart — the JS library has no
///     copy feature): <see cref="MultiSelectData.GetSelectedTextFromResult" /> flattens a selection
///     result into clipboard-ready plain text — hidden lines skipped, trailing newlines trimmed like
///     the render layer, lines joined with a plain '\n'.
/// </summary>
public class MultiSelectCopyTextTests
{
    /// <summary>
    ///     100-line file with two change hunks — collapsed hidden ranges [1,37], [45,87], [96,100]
    ///     old/new; visible windows 38-44 around hunk 1 (old 41 "change-me" → new 41 "changed!") and
    ///     88-95 around hunk 2 (old 91 "delete-me" → new 91/92 "replaced-1/2", ctx 092 → new 93).
    ///     Same fixture as MultiSelectTests.
    /// </summary>
    private static DiffFile CreateFile()
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

    private static MultiSelectResult SplitResult(DiffFile file, SplitSide side, int start, int end)
    {
        return new MultiSelectResult(new MultiSelectRange(side, start, end),
                                     MultiSelectData.GetSelectedLinesFromDiffFile_Split(file,
                                         new MultiSelectRange(side, start, end)));
    }

    private static MultiSelectResult UnifiedResult(DiffFile file, SplitSide side, int start, int end)
    {
        return new MultiSelectResult(new MultiSelectRange(side, start, end),
                                     MultiSelectData.GetSelectedLinesFromDiffFile_Unified(file,
                                         new MultiSelectRange(side, start, end)));
    }

    [Test]
    public void NullResult_YieldsEmptyText()
    {
        Assert.That(MultiSelectData.GetSelectedTextFromResult(null), Is.EqualTo(""));
    }

    [Test]
    public void EmptyLineList_YieldsEmptyText()
    {
        var result = new MultiSelectResult(new MultiSelectRange(SplitSide.Old, 998, 1000), []);

        Assert.That(MultiSelectData.GetSelectedTextFromResult(result), Is.EqualTo(""));
    }

    [Test]
    public void PlainSplitSelection_JoinsValuesWithLf_NoTrailingNewline()
    {
        var result = SplitResult(CreateFile(), SplitSide.Old, 38, 44);

        // Values carry their trailing "\n" in the model; the copy strips it per line (render
        // parity) and joins with a single '\n' — unlike the file contents, no trailing newline.
        Assert.That(MultiSelectData.GetSelectedTextFromResult(result),
                    Is.EqualTo("ctx 038\nctx 039\nctx 040\nchange-me\nctx 042\nctx 043\nctx 044"));
    }

    [Test]
    public void UnifiedSelection_JoinsValuesWithLf()
    {
        var result = UnifiedResult(CreateFile(), SplitSide.New, 91, 93);

        Assert.That(MultiSelectData.GetSelectedTextFromResult(result),
                    Is.EqualTo("replaced-1\nreplaced-2\nctx 092"));
    }

    [Test]
    public void HiddenLines_AreSkipped()
    {
        // Old 35..37 sit in the collapsed range above the first hunk; 38..40 are visible.
        var result = SplitResult(CreateFile(), SplitSide.Old, 35, 40);

        Assert.That(MultiSelectData.GetSelectedTextFromResult(result),
                    Is.EqualTo("ctx 038\nctx 039\nctx 040"));
    }

    [Test]
    public void CrossHunkSelection_KeepsOnlyTheVisibleWindows()
    {
        // Old 30..90 spans the collapsed [45,87] between the two hunks: visible are 38..44 and
        // 88..90.
        var result = SplitResult(CreateFile(), SplitSide.Old, 30, 90);

        Assert.That(MultiSelectData.GetSelectedTextFromResult(result),
                    Is.EqualTo("ctx 038\nctx 039\nctx 040\nchange-me\nctx 042\nctx 043\nctx 044\n" +
                               "ctx 088\nctx 089\nctx 090"));
    }

    [Test]
    public void AllHiddenSelection_YieldsEmptyText()
    {
        // The whole range sits inside the collapsed [1,37] above the first hunk.
        var result = SplitResult(CreateFile(), SplitSide.Old, 10, 20);

        Assert.That(MultiSelectData.GetSelectedTextFromResult(result), Is.EqualTo(""));
    }

    [Test]
    public void NullValuedLines_CopyAsEmptyLines()
    {
        var result = new MultiSelectResult(new MultiSelectRange(SplitSide.New, 1, 3),
        [
            new SelectedLine(1, 1, null, false, false, true, false),
            new SelectedLine(2, 2, "b\n", false, false, true, false),
            new SelectedLine(3, 3, null, false, false, false, true)
        ]);

        // A null value renders as an empty line, so the copy keeps the line slot.
        Assert.That(MultiSelectData.GetSelectedTextFromResult(result), Is.EqualTo("\nb\n"));
    }

    [Test]
    public void TrailingCarriageReturnAndNewline_AreTrimmedLikeTheView()
    {
        var result = new MultiSelectResult(new MultiSelectRange(SplitSide.Old, 1, 2),
        [
            new SelectedLine(1, 1, "a\r\n", false, false, false, true),
            new SelectedLine(2, 2, "b\n\n", false, false, false, true)
        ]);

        // Render parity: the row builders TrimEnd('\r', '\n') every value before display.
        Assert.That(MultiSelectData.GetSelectedTextFromResult(result), Is.EqualTo("a\nb"));
    }

    [Test]
    public void MixedHiddenAndNullValues_KeepLineSlotsInOrder()
    {
        var result = new MultiSelectResult(new MultiSelectRange(SplitSide.New, 1, 4),
        [
            new SelectedLine(1, 1, "a\n", true, false, false, true),
            new SelectedLine(2, 2, null, false, false, false, true),
            new SelectedLine(3, 3, "c\n", false, true, false, false),
            new SelectedLine(4, 4, "d\n", true, false, true, false)
        ]);

        Assert.That(MultiSelectData.GetSelectedTextFromResult(result), Is.EqualTo("\nc"));
    }
}
