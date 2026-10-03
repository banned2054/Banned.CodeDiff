using Banned.CodeDiff.Models;
using Banned.CodeDiff.Services;
using Banned.CodeDiff.Utils;
using Xunit;

namespace Banned.CodeDiff.Tests;

/// <summary>API-level sanity tests complementing the golden replays.</summary>
public class UnitTests
{
    [Fact]
    public void FastDiff_BasicOps()
    {
        // the exact fast-diff@1.3.0 output (its README example is outdated)
        var result = FastDiff.Diff("Hello world.", "Goodbye world.");
        Assert.Equal(
                     new[]
                     {
                         (FastDiff.Delete, "Hell"),
                         (FastDiff.Insert, "G"),
                         (FastDiff.Equal, "o"),
                         (FastDiff.Insert, "odbye"),
                         (FastDiff.Equal, " world."),
                     },
                     result.Select(t => (t.Op, t.Text)).ToArray()
                    );
    }

    [Fact]
    public void FastDiff_SemanticCleanupMergesEdits()
    {
        var result = FastDiff.Diff("The cat came.", "The came.", (int?)null, cleanup : true);
        Assert.Equal(
                     new[]
                     {
                         (FastDiff.Equal, "The "),
                         (FastDiff.Delete, "cat "),
                         (FastDiff.Equal, "came."),
                     },
                     result.Select(t => (t.Op, t.Text)).ToArray()
                    );
    }

    [Fact]
    public void FastDiff_InsertConstant()
    {
        Assert.Equal(1, FastDiff.Insert);
        Assert.Equal(-1, FastDiff.Delete);
        Assert.Equal(0, FastDiff.Equal);
    }

    [Fact]
    public void EscapeHtml_SpecialChars()
    {
        Assert.Equal("&lt;a &amp; b&gt;", EscapeHtml.Escape("<a & b>"));
        Assert.Equal("&quot;x&#39;", EscapeHtml.Escape("\"x'"));
        Assert.Equal("plain", EscapeHtml.Escape("plain"));
        Assert.Equal("", EscapeHtml.Escape(null));
    }

    [Fact]
    public void GetSymbol_MapsNewLineSymbols()
    {
        Assert.Equal("␊", Symbol.GetSymbol(NewLineSymbol.LF));
        Assert.Equal("␍", Symbol.GetSymbol(NewLineSymbol.CR));
        Assert.Equal("␍␊", Symbol.GetSymbol(NewLineSymbol.CRLF));
        Assert.Equal("", Symbol.GetSymbol(NewLineSymbol.NULL));
        Assert.Equal("", Symbol.GetSymbol(null));
    }

    [Fact]
    public void DiffParser_ParsesBasicHunk()
    {
        var rd = DiffParser.Shared.Parse(
                                         "--- a/f.txt\n+++ b/f.txt\n@@ -1,2 +1,2 @@\n-old\n+new\n keep\n"
                                        );
        Assert.False(rd.IsBinary);
        Assert.Single(rd.Hunks);
        Assert.Equal(4, rd.Hunks[0].Lines.Count); // hunk header + -old + +new + " keep"
        Assert.Equal(2, rd.MaxLineNumber);
    }

    [Fact]
    public void DiffParser_DetectsBinary()
    {
        var rd = DiffParser.Shared.Parse(
                                         "diff --git a/x b/x\nBinary files a/x and b/x differ\n"
                                        );
        Assert.True(rd.IsBinary);
        Assert.Empty(rd.Hunks);
    }

    [Fact]
    public void DiffParser_ThrowsOnInvalidHunkHeader()
    {
        Assert.ThrowsAny<Exception>(() => DiffParser.Shared.Parse("--- a/f\n+++ b/f\n@@ not a header @@\n a\n")
                                   );
    }

    [Fact]
    public void RelativeChanges_TrimsCommonPrefixSuffix()
    {
        var addition = new DiffLine("const a = 2;\n", DiffLineType.Add, null, null, 1);
        var deletion = new DiffLine("const a = 1;\n", DiffLineType.Delete, null, 1, null);
        var (addRange, delRange) = ChangeRange.RelativeChanges(addition, deletion);
        Assert.Equal(10, addRange.Range.Location);
        Assert.Equal(1, addRange.Range.Length);
        Assert.Equal(10, delRange.Range.Location);
        Assert.Equal(1, delRange.Range.Length);
        Assert.True(addRange.HasLineChange);
        Assert.True(delRange.HasLineChange);
    }

    [Fact]
    public void RelativeChanges_DetectsNewLineSymbolChange()
    {
        var addition = new DiffLine("same text\n", DiffLineType.Add, null, null, 1);
        var deletion = new DiffLine("same text\r\n", DiffLineType.Delete, null, 1, null);
        var (addRange, delRange) = ChangeRange.RelativeChanges(addition, deletion);
        Assert.Equal(NewLineSymbol.LF, addRange.NewLineSymbol);
        Assert.Equal(NewLineSymbol.CRLF, delRange.NewLineSymbol);
        Assert.True(addRange.HasLineChange);
        Assert.True(delRange.HasLineChange);
    }

    [Fact]
    public void ChangeMaxLengthToIgnoreLineDiff_AppliesAndResets()
    {
        try
        {
            ChangeRange.ChangeMaxLengthToIgnoreLineDiff(5);
            Assert.Equal(5, ChangeRange.GetMaxLengthToIgnoreLineDiff());
            var addition = new DiffLine("abcdef\n", DiffLineType.Add, null, null, 1);
            var deletion = new DiffLine("abcXef\n", DiffLineType.Delete, null, 1, null);
            var (addRange, delRange) = ChangeRange.RelativeChanges(addition, deletion);
            Assert.Equal(0, addRange.Range.Length);
            Assert.Equal(0, delRange.Range.Length);
        }
        finally
        {
            ChangeRange.ResetMaxLengthToIgnoreLineDiff();
        }

        Assert.Equal(1000, ChangeRange.GetMaxLengthToIgnoreLineDiff());
    }

    [Fact]
    public void GetHunkHeaderExpansionType_TopHunk()
    {
        var header = new DiffHunkHeader(1, 5, 1, 5);
        Assert.Equal(DiffHunkExpansionType.None, DiffTool.GetHunkHeaderExpansionType(0, header, null));
        var headerAt5 = new DiffHunkHeader(5, 5, 5, 5);
        Assert.Equal(DiffHunkExpansionType.Up, DiffTool.GetHunkHeaderExpansionType(0, headerAt5, null));
    }

    [Fact]
    public void DiffFile_ComposesPureDiffWhenContentMissing()
    {
        var df = new DiffFile("", "", "f.txt", "", ["--- a/f.txt\n+++ b/f.txt\n@@ -1,1 +1,1 @@\n-old\n+new\n"]);
        df.InitRaw();
        Assert.True(df.GetIsPureDiffRender());
        Assert.False(df.GetExpandEnabled());
        Assert.Equal(1, df.AdditionLength);
        Assert.Equal(1, df.DeletionLength);
        Assert.Equal("old\n", df.OldFileContent);
        Assert.Equal("new\n", df.NewFileContent);
    }

    [Fact]
    public void DiffFile_BuildSplitLineLengths()
    {
        var df = new DiffFile("f.txt",
                              "a\nb\nc\n",
                              "f.txt",
                              "a\nB\nc\n",
                              ["--- a/f.txt\n+++ b/f.txt\n@@ -1,3 +1,3 @@\n a\n-b\n+B\n c\n"]);
        df.InitRaw();
        df.BuildSplitDiffLines();
        Assert.Equal(3, df.SplitLineLength);
        Assert.Equal("a\n", df.GetSplitLeftLine(0)?.Value);
        Assert.Equal("a\n", df.GetSplitRightLine(0)?.Value);
        Assert.Equal("b\n", df.GetSplitLeftLine(1)?.Value);
        Assert.Equal("B\n", df.GetSplitRightLine(1)?.Value);
        Assert.Equal("b\n", df.GetSplitLeftLine(1)?.Diff?.Text);
        Assert.Equal("B\n", df.GetSplitRightLine(1)?.Diff?.Text);
    }

    [Fact]
    public void DiffFile_NotifyAllFiresEvent()
    {
        var df = new DiffFile("f.txt", "a\n", "f.txt", "b\n", ["--- a/f\n+++ b/f\n@@ -1,1 +1,1 @@\n-a\n+b\n"]);
        df.InitRaw();
        var count = 0;
        df.Updated += () => count++;
        df.BuildSplitDiffLines();
        Assert.Equal(1, count);
        Assert.Equal(1, df.UpdateCount);
    }
}