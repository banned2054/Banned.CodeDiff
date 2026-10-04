using Banned.CodeDiff.Models;
using Banned.CodeDiff.Services;
using Banned.CodeDiff.Utils;
using NUnit.Framework;

namespace Banned.CodeDiff.Tests;

/// <summary>
///     Data-layer tests for the multiSelect port (packages/core/src/multiSelect/data.ts + the pure
///     helpers of dom.ts/visual.ts): split/unified line extraction against a real-content DiffFile —
///     line flags (isDelete/isAdd/isContext incl. the missing-DiffLine quirk), hidden-line membership,
///     1-based indexes, range normalization, and the preselected-lines-to-range merge.
/// </summary>
public class MultiSelectTests
{
    /// <summary>
    ///     100-line file with two change hunks — collapsed hidden ranges [1,37] old/new above the
    ///     first hunk (@@ -38,7: old 41 "change-me" → new 41 "changed!") and around the second
    ///     (@@ -88,7 +88,8: old 91 "delete-me" → new 91/92 "replaced-1/2", ctx092 shifts to new 93).
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

    /// <summary>
    ///     The model keeps the raw file line including its trailing newline (upstream
    ///     SplitLineItem.value semantics; the render layer trims it for display).
    /// </summary>
    private static string Raw(string line)
    {
        return line + "\n";
    }


    // ---- NormalizeRange (dom.ts) ----

    [Test]
    public void NormalizeRange_SwapsReversedBounds()
    {
        var range = new MultiSelectRange(SplitSide.Old, 44, 38);

        var normalized = MultiSelectData.NormalizeRange(range);

        Assert.That(normalized.StartLineNumber, Is.EqualTo(38));
        Assert.That(normalized.EndLineNumber, Is.EqualTo(44));
        Assert.That(normalized.Side, Is.EqualTo(SplitSide.Old));
    }

    [Test]
    public void NormalizeRange_KeepsOrderedBounds()
    {
        var range = new MultiSelectRange(SplitSide.New, 3, 9);

        Assert.That(MultiSelectData.NormalizeRange(range), Is.EqualTo(range));
    }

    // ---- split mode (data.ts getSelectedLinesFromDiffFile_Split) ----

    [Test]
    public void Split_OldRange_CarriesLineFlagsAndValues()
    {
        var lines = MultiSelectData.GetSelectedLinesFromDiffFile_Split(CreateFile(),
                                                                       new MultiSelectRange(SplitSide.Old, 38, 44));

        Assert.That(lines.Select(l => l.LineNumber), Is.EqualTo(new[] { 38, 39, 40, 41, 42, 43, 44 }));

        // The side lists are dense, so the 1-based index of line N is N itself.
        Assert.That(lines.Select(l => l.Index), Is.EqualTo(new[] { 38, 39, 40, 41, 42, 43, 44 }));

        var delete = lines.Single(l => l.LineNumber == 41);
        Assert.That(delete.Value, Is.EqualTo(Raw("change-me")));
        Assert.That(delete.IsDelete, Is.True);
        Assert.That(delete.IsAdd, Is.False);
        Assert.That(delete.IsContext, Is.False);
        Assert.That(delete.IsHide, Is.False);

        var context = lines.Single(l => l.LineNumber == 38);
        Assert.That(context.Value, Is.EqualTo(Raw("ctx 038")));
        Assert.That(context.IsContext, Is.True);
        Assert.That(context.IsDelete || context.IsAdd, Is.False);
    }

    [Test]
    public void Split_NewRange_MarksAddition()
    {
        var lines = MultiSelectData.GetSelectedLinesFromDiffFile_Split(CreateFile(),
                                                                       new MultiSelectRange(SplitSide.New, 41, 42));

        Assert.That(lines.Count, Is.EqualTo(2));

        var add = lines.Single(l => l.LineNumber == 41);
        Assert.That(add.Value, Is.EqualTo(Raw("changed!")));
        Assert.That(add.IsAdd, Is.True);
        Assert.That(add.IsContext, Is.False);

        Assert.That(lines.Single(l => l.LineNumber == 42).IsContext, Is.True);
    }

    [Test]
    public void Split_ReversedRange_NormalizesToAscendingLines()
    {
        var lines = MultiSelectData.GetSelectedLinesFromDiffFile_Split(CreateFile(),
                                                                       new MultiSelectRange(SplitSide.Old, 44, 38));

        Assert.That(lines.Select(l => l.LineNumber), Is.EqualTo(new[] { 38, 39, 40, 41, 42, 43, 44 }));
    }

    [Test]
    public void Split_HiddenLines_StayInTheSelectionWithIsHide()
    {
        // Old lines 35..37 sit in the collapsed range above the first hunk; 38..40 are visible.
        var lines = MultiSelectData.GetSelectedLinesFromDiffFile_Split(CreateFile(),
                                                                       new MultiSelectRange(SplitSide.Old, 35, 40));

        Assert.That(lines.Select(l => l.LineNumber), Is.EqualTo(new[] { 35, 36, 37, 38, 39, 40 }));
        Assert.That(lines.Take(3).Select(l => l.IsHide), Is.All.True);
        Assert.That(lines.Skip(3).Select(l => l.IsHide), Is.All.False);

        // Hidden lines keep their value and index — only the visibility flag differs.
        Assert.That(lines[0].Value, Is.EqualTo(Raw("ctx 035")));
        Assert.That(lines[0].Index, Is.EqualTo(35));
    }

    [Test]
    public void Split_NonexistentLineNumbers_AreSkipped()
    {
        var lines = MultiSelectData.GetSelectedLinesFromDiffFile_Split(CreateFile(),
                                                                       new MultiSelectRange(SplitSide.Old, 998, 1000));

        Assert.That(lines, Is.Empty);
    }

    [Test]
    public void Split_ExpandedRawLines_CountAsContext()
    {
        var file = CreateFile();
        file.OnAllExpand(ExpandViewMode.Split);

        // Old line 10 is a raw file line revealed by the expansion — no DiffLine attached, which
        // upstream still reports as isContext (diff?.type === undefined quirk).
        var lines =
            MultiSelectData.GetSelectedLinesFromDiffFile_Split(file, new MultiSelectRange(SplitSide.Old, 10, 11));

        Assert.That(lines.Count, Is.EqualTo(2));
        Assert.That(lines.Select(l => l.IsHide), Is.All.False);
        Assert.That(lines.Select(l => l.IsContext), Is.All.True);
        Assert.That(lines.Select(l => l.IsAdd || l.IsDelete), Is.All.False);
        Assert.That(lines[0].Value, Is.EqualTo(Raw("ctx 010")));
    }

    // ---- unified mode (data.ts getSelectedLinesFromDiffFile_Unified) ----

    [Test]
    public void Unified_OldRange_ReportsOldNumbers()
    {
        var lines = MultiSelectData.GetSelectedLinesFromDiffFile_Unified(CreateFile(),
                                                                         new MultiSelectRange(SplitSide.Old, 91, 92));

        Assert.That(lines.Select(l => l.LineNumber), Is.EqualTo(new[] { 91, 92 }));

        var delete = lines.Single(l => l.LineNumber == 91);
        Assert.That(delete.Value, Is.EqualTo(Raw("delete-me")));
        Assert.That(delete.IsDelete, Is.True);
        Assert.That(delete.IsContext, Is.False);

        // Old 92 is the ctx 092 line — on the unified row its new number is 93, but an old-side
        // selection reports the old number.
        var context = lines.Single(l => l.LineNumber == 92);
        Assert.That(context.Value, Is.EqualTo(Raw("ctx 092")));
        Assert.That(context.IsContext, Is.True);
    }

    [Test]
    public void Unified_NewRange_ReportsNewNumbers()
    {
        var lines = MultiSelectData.GetSelectedLinesFromDiffFile_Unified(CreateFile(),
                                                                         new MultiSelectRange(SplitSide.New, 91, 93));

        Assert.That(lines.Select(l => l.LineNumber), Is.EqualTo(new[] { 91, 92, 93 }));
        Assert.That(lines.Select(l => l.Value),
                    Is.EqualTo(new[] { Raw("replaced-1"), Raw("replaced-2"), Raw("ctx 092") }));
        Assert.That(lines.Select(l => l.IsAdd), Is.EqualTo(new[] { true, true, false }));
        Assert.That(lines.Select(l => l.IsContext), Is.EqualTo(new[] { false, false, true }));

        // The unified track interleaves the delete row and its replacement adds, so the 1-based
        // index runs ahead of the new line number (row order: old-91 delete, new-91, new-92 adds).
        Assert.That(lines.Select(l => l.Index), Is.EqualTo(new[] { 93, 94, 95 }));
    }

    [Test]
    public void Unified_DeleteRow_FoundByOldSide_WithLeadingIndex()
    {
        // The old 91 "delete-me" row has no new number of its own (the new-91 row is "replaced-1"),
        // so only an old-side query reports it; its 1-based index sits ahead of the line number
        // because the unified track starts the file's rows after the top collapsed placeholder.
        var lines = MultiSelectData.GetSelectedLinesFromDiffFile_Unified(CreateFile(),
                                                                         new MultiSelectRange(SplitSide.Old, 91, 91));

        Assert.That(lines.Select(l => l.Value), Is.EqualTo(new[] { Raw("delete-me") }));
        Assert.That(lines.Single().Index, Is.EqualTo(92));
    }

    [Test]
    public void Unified_OutOfRangeNumbers_AreSkipped()
    {
        // JS: rows whose side number is undefined are skipped by the lineNumber !== undefined guard.
        var lines = MultiSelectData.GetSelectedLinesFromDiffFile_Unified(CreateFile(),
                                                                         new MultiSelectRange(SplitSide.New, 200, 201));

        Assert.That(lines, Is.Empty);
    }

    [Test]
    public void Unified_HiddenLines_ReportUnifiedHiddenFlag()
    {
        // Unified hidden range above the second hunk: new 45..87 are behind the placeholder.
        var lines = MultiSelectData.GetSelectedLinesFromDiffFile_Unified(CreateFile(),
                                                                         new MultiSelectRange(SplitSide.New, 45, 47));

        Assert.That(lines.Select(l => l.LineNumber), Is.EqualTo(new[] { 45, 46, 47 }));
        Assert.That(lines.Select(l => l.IsHide), Is.All.True);
    }

    // ---- preselected merge (visual.ts changePreselectedLinesToLineRange) ----

    [Test]
    public void PreselectedLines_MergeIntoOneMinMaxRangePerSide()
    {
        var ranges = MultiSelectData.ChangePreselectedLinesToLineRange(
                                                                       new MultiSelectPreselectedLines([10, 5, 7],
                                                                           [3, 9]));

        // Upstream order: the new-side range first, then old.
        Assert.That(ranges, Has.Count.EqualTo(2));
        Assert.That(ranges[0], Is.EqualTo(new MultiSelectRange(SplitSide.New, 3, 9)));
        Assert.That(ranges[1], Is.EqualTo(new MultiSelectRange(SplitSide.Old, 5, 10)));
    }

    [Test]
    public void PreselectedLines_EmptySides_ProduceNoRanges()
    {
        Assert.That(MultiSelectData.ChangePreselectedLinesToLineRange(MultiSelectPreselectedLines.Empty), Is.Empty);
        Assert.That(MultiSelectData.ChangePreselectedLinesToLineRange(new MultiSelectPreselectedLines([], [])),
                    Is.Empty);
    }
}
