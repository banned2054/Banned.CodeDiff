using Banned.CodeDiff.Services;
using NUnit.Framework;

namespace Banned.CodeDiff.Tests;

/// <summary>
///     Hand-pinned anchors around two non-local invariants of the fast-diff port that the
///     generated golden set does not cover (its cursor values never go below 0):
///     DiffCommonOverlap's loop slices and findCursorEditDiff's editAfter suffix slices.
///     Every expectation below was produced by running fast-diff@1.3.0 in tests/js-harness
///     (node) — the same reference the golden data is generated from.
/// </summary>
public class FastDiffInvariantTests
{
    [Test]
    public void CursorEdit_EditAfterSuffixSlices()
    {
        // (text1, text2, cursor, cleanup) → expected fast-diff@1.3.0 output.
        // Negative cursors drive suffixLength past oldAfter/newAfter.Length where JS
        // slice clamps (whole/empty) — the port must clamp, not throw, and land on the
        // same fallback-to-general-diff result JS produces.
        AssertCursor(
                     "abcdef", "abcXYdef", 0,
                     (0, "abc"), (1, "XY"), (0, "def"));
        AssertCursor(
                     "abcdef", "abcXYdef", 3,
                     (0, "abc"), (1, "XY"), (0, "def"));
        AssertCursor(
                     "abc", "abcdefghij", -5,
                     (0, "abc"), (1, "defghij"));
        AssertCursor(
                     "abcdefghij", "abc", -5,
                     (0, "abc"), (-1, "defghij"));
        AssertCursor(
                     "abc", "abd", -1,
                     (0, "ab"), (-1, "c"), (1, "d"));
        AssertCursor(
                     "abc", "abcd", 10,
                     (0, "abc"), (1, "d"));
        // Equal texts take diff_main's early-return before findCursorEditDiff runs —
        // the editAfter invariant leans on that exclusion for the cursor-beyond-both case.
        AssertCursor(
                     "abc", "abc", -5,
                     (0, "abc"));
    }

    [Test]
    public void CursorEdit_ReplaceRangeSplice()
    {
        var cursor = new CursorInfo { OldRange = new CursorRange(6, 5) };

        var result = FastDiff.Diff("hello world", "hello brave world", cursor);

        Assert.That(Dump(result), Is.EqualTo("(0:'hello ')(1:'brave ')(0:'world')"));
    }

    [Test]
    public void CleanupSemantic_OverlapLoop()
    {
        // Adjacent delete/insert pairs make diff_cleanupSemantic call
        // diff_commonOverlap(deletion, insertion) with inputs that walk its probing
        // loop through `length += found` iterations; the loop-top and compare slices
        // there rely on length staying <= textLength (see the invariant comment).
        AssertCleanup(
                      "The quick xxx fox", "The quick dog xxx",
                      (0, "The quick "), (-1, "xxx fo"), (1, "dog xx"), (0, "x"));
        AssertCleanup(
                      "aaa1234abcdefzzz", "bbb5678;abcdefyyy",
                      (-1, "aaa1234"), (1, "bbb5678;"), (0, "abcdef"), (-1, "zzz"), (1, "yyy"));
        AssertCleanup(
                      "prefix abcxxx suffix", "prefix xxxdef suffix",
                      (0, "prefix "), (-1, "abc"), (0, "xxx"), (1, "def"), (0, " suffix"));
        AssertCleanup(
                      "xxabcd", "abcdxx",
                      (-1, "xx"), (0, "abcd"), (1, "xx"));
    }

    [Test]
    public void CleanupSemanticLossless_ShiftLoop()
    {
        // Single edits between equalities step through the character-by-character
        // right-shift; the trailing-whitespace preference of the >= comparison shows
        // up in which offset wins.
        AssertCleanup(
                      "The xxxcat came.", "The catxxx came.",
                      (0, "The "), (-1, "xxx"), (0, "cat"), (1, "xxx"), (0, " came."));
        AssertCleanup(
                      "New value.  ", "New value. ",
                      (0, "New value. "), (-1, " "));
    }

    private static void AssertCursor(string a, string b, int cursor, params (int Op, string Text)[] expected)
    {
        var result = FastDiff.Diff(a, b, cursor);

        Assert.That(Dump(result), Is.EqualTo(DumpExpected(expected)),
                    $"{a} vs {b} @cursor {cursor}");
    }

    private static void AssertCleanup(string a, string b, params (int Op, string Text)[] expected)
    {
        var result = FastDiff.Diff(a, b, (int?)null, true);

        Assert.That(Dump(result), Is.EqualTo(DumpExpected(expected)),
                    $"{a} vs {b}");
    }

    private static string DumpExpected(params (int Op, string Text)[] expected)
    {
        return string.Concat(expected.Select(t => $"({t.Op}:'{t.Text}')"));
    }

    private static string Dump(List<DiffTuple> diffs)
    {
        return string.Concat(diffs.Select(d => $"({(int)d.Op}:'{d.Text}')"));
    }
}
