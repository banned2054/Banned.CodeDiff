using Banned.CodeDiff.Models;
using Banned.CodeDiff.Services;
using Banned.CodeDiff.Utils;
using NUnit.Framework;

namespace Banned.CodeDiff.Tests;

/// <summary>API-level sanity tests complementing the golden replays.</summary>
public class UnitTests
{
    [Test]
    public void FastDiff_BasicOps()
    {
        // the exact fast-diff@1.3.0 output (its README example is outdated)
        var result = FastDiff.Diff("Hello world.", "Goodbye world.");
        Assert.That(result.Select(t => (t.Op, t.Text)).ToArray(), Is.EqualTo([
            (DiffOp.Delete, "Hell"),
            (DiffOp.Insert, "G"),
            (DiffOp.Equal, "o"),
            (DiffOp.Insert, "odbye"),
            (DiffOp.Equal, " world.")
        ]));
    }

    [Test]
    public void FastDiff_SemanticCleanupMergesEdits()
    {
        var result = FastDiff.Diff("The cat came.", "The came.", (int?)null, cleanup : true);
        Assert.That(result.Select(t => (t.Op, t.Text)).ToArray(), Is.EqualTo([
            (DiffOp.Equal, "The "),
            (DiffOp.Delete, "cat "),
            (DiffOp.Equal, "came.")
        ]));
    }

    [Test]
    public void FastDiff_InsertConstant()
    {
        Assert.That((int)DiffOp.Insert, Is.EqualTo(1));
        Assert.That((int)DiffOp.Delete, Is.EqualTo(-1));
        Assert.That((int)DiffOp.Equal, Is.EqualTo(0));
    }

    [Test]
    public void EscapeHtml_SpecialChars()
    {
        Assert.That(EscapeHtml.Escape("<a & b>"), Is.EqualTo("&lt;a &amp; b&gt;"));
        Assert.That(EscapeHtml.Escape("\"x'"), Is.EqualTo("&quot;x&#39;"));
        Assert.That(EscapeHtml.Escape("plain"), Is.EqualTo("plain"));
        Assert.That(EscapeHtml.Escape(null), Is.EqualTo(""));
    }

    [Test]
    public void GetSymbol_MapsNewLineSymbols()
    {
        Assert.That(Symbol.GetSymbol(NewLineSymbol.LF), Is.EqualTo("␊"));
        Assert.That(Symbol.GetSymbol(NewLineSymbol.CR), Is.EqualTo("␍"));
        Assert.That(Symbol.GetSymbol(NewLineSymbol.CRLF), Is.EqualTo("␍␊"));
        Assert.That(Symbol.GetSymbol(NewLineSymbol.NULL), Is.EqualTo(""));
        Assert.That(Symbol.GetSymbol(null), Is.EqualTo(""));
    }

    [Test]
    public void DiffParser_ParsesBasicHunk()
    {
        var rd = DiffParser.Shared.Parse("--- a/f.txt\n+++ b/f.txt\n@@ -1,2 +1,2 @@\n-old\n+new\n keep\n");
        Assert.That(rd.IsBinary, Is.False);
        Assert.That(rd.Hunks, Has.Count.EqualTo(1));
        Assert.That(rd.Hunks[0].Lines.Count, Is.EqualTo(4)); // hunk header + -old + +new + " keep"
        Assert.That(rd.MaxLineNumber, Is.EqualTo(2));
    }

    [Test]
    public void DiffParser_DetectsBinary()
    {
        var rd = DiffParser.Shared.Parse("diff --git a/x b/x\nBinary files a/x and b/x differ\n");
        Assert.That(rd.IsBinary, Is.True);
        Assert.That(rd.Hunks, Is.Empty);
    }

    [Test]
    public void DiffParser_ThrowsOnInvalidHunkHeader()
    {
        Assert.Catch<Exception>(() => DiffParser.Shared.Parse("--- a/f\n+++ b/f\n@@ not a header @@\n a\n"));
    }

    [Test]
    public void RelativeChanges_TrimsCommonPrefixSuffix()
    {
        var addition = new DiffLine("const a = 2;\n", DiffLineType.Add, null, null, 1);
        var deletion = new DiffLine("const a = 1;\n", DiffLineType.Delete, null, 1, null);
        var (addRange, delRange) = ChangeRange.RelativeChanges(addition, deletion);
        Assert.That(addRange.Range.Location, Is.EqualTo(10));
        Assert.That(addRange.Range.Length, Is.EqualTo(1));
        Assert.That(delRange.Range.Location, Is.EqualTo(10));
        Assert.That(delRange.Range.Length, Is.EqualTo(1));
        Assert.That(addRange.HasLineChange, Is.True);
        Assert.That(delRange.HasLineChange, Is.True);
    }

    [Test]
    public void RelativeChanges_DetectsNewLineSymbolChange()
    {
        var addition = new DiffLine("same text\n", DiffLineType.Add, null, null, 1);
        var deletion = new DiffLine("same text\r\n", DiffLineType.Delete, null, 1, null);
        var (addRange, delRange) = ChangeRange.RelativeChanges(addition, deletion);
        Assert.That(addRange.NewLineSymbol, Is.EqualTo(NewLineSymbol.LF));
        Assert.That(delRange.NewLineSymbol, Is.EqualTo(NewLineSymbol.CRLF));
        Assert.That(addRange.HasLineChange, Is.True);
        Assert.That(delRange.HasLineChange, Is.True);
    }

    [Test]
    public void ChangeMaxLengthToIgnoreLineDiff_AppliesAndResets()
    {
        try
        {
            ChangeRange.ChangeMaxLengthToIgnoreLineDiff(5);
            Assert.That(ChangeRange.GetMaxLengthToIgnoreLineDiff(), Is.EqualTo(5));
            var addition = new DiffLine("abcdef\n", DiffLineType.Add, null, null, 1);
            var deletion = new DiffLine("abcXef\n", DiffLineType.Delete, null, 1, null);
            var (addRange, delRange) = ChangeRange.RelativeChanges(addition, deletion);
            Assert.That(addRange.Range.Length, Is.EqualTo(0));
            Assert.That(delRange.Range.Length, Is.EqualTo(0));
        }
        finally
        {
            ChangeRange.ResetMaxLengthToIgnoreLineDiff();
        }

        Assert.That(ChangeRange.GetMaxLengthToIgnoreLineDiff(), Is.EqualTo(1000));
    }

    [Test]
    public void GetHunkHeaderExpansionType_TopHunk()
    {
        var header = new DiffHunkHeader(1, 5, 1, 5);
        Assert.That(DiffTool.GetHunkHeaderExpansionType(0, header, null), Is.EqualTo(DiffHunkExpansionType.None));
        var headerAt5 = new DiffHunkHeader(5, 5, 5, 5);
        Assert.That(DiffTool.GetHunkHeaderExpansionType(0, headerAt5, null), Is.EqualTo(DiffHunkExpansionType.Up));
    }

    [Test]
    public void DiffFile_ComposesPureDiffWhenContentMissing()
    {
        var df = new DiffFile("", "", "f.txt", "", ["--- a/f.txt\n+++ b/f.txt\n@@ -1,1 +1,1 @@\n-old\n+new\n"]);
        df.InitRaw();
        Assert.That(df.GetIsPureDiffRender(), Is.True);
        Assert.That(df.GetExpandEnabled(), Is.False);
        Assert.That(df.AdditionLength, Is.EqualTo(1));
        Assert.That(df.DeletionLength, Is.EqualTo(1));
        Assert.That(df.OldFileContent, Is.EqualTo("old\n"));
        Assert.That(df.NewFileContent, Is.EqualTo("new\n"));
    }

    [Test]
    public void DiffFile_BuildSplitLineLengths()
    {
        var df = new DiffFile("f.txt",
                              "a\nb\nc\n",
                              "f.txt",
                              "a\nB\nc\n",
                              ["--- a/f.txt\n+++ b/f.txt\n@@ -1,3 +1,3 @@\n a\n-b\n+B\n c\n"]);
        df.InitRaw();
        df.BuildSplitDiffLines();
        Assert.That(df.SplitLineLength, Is.EqualTo(3));
        Assert.That(df.GetSplitLeftLine(0)?.Value, Is.EqualTo("a\n"));
        Assert.That(df.GetSplitRightLine(0)?.Value, Is.EqualTo("a\n"));
        Assert.That(df.GetSplitLeftLine(1)?.Value, Is.EqualTo("b\n"));
        Assert.That(df.GetSplitRightLine(1)?.Value, Is.EqualTo("B\n"));
        Assert.That(df.GetSplitLeftLine(1)?.Diff?.Text, Is.EqualTo("b\n"));
        Assert.That(df.GetSplitRightLine(1)?.Diff?.Text, Is.EqualTo("B\n"));
    }

    [Test]
    public void DiffFile_NotifyAllFiresEvent()
    {
        var df = new DiffFile("f.txt", "a\n", "f.txt", "b\n", ["--- a/f\n+++ b/f\n@@ -1,1 +1,1 @@\n-a\n+b\n"]);
        df.InitRaw();
        var count = 0;
        df.Updated += () => count++;
        df.BuildSplitDiffLines();
        Assert.That(count, Is.EqualTo(1));
        Assert.That(df.UpdateCount, Is.EqualTo(1));
    }
}
