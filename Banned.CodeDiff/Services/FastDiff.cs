// Port of the fast-diff@1.3.0 npm package (diff.js, single file).
//
// This library modifies the diff-patch-match library by Neil Fraser
// by removing the patch and match functionality and certain advanced
// options in the diff function. The original license is as follows:
//
// ===
//
// Diff Match and Patch
//
// Copyright 2006 Google Inc.
// http://code.google.com/p/google-diff-match-patch/
//
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
//
//   http://www.apache.org/licenses/LICENSE-2.0
//
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.

namespace Banned.CodeDiff.Services;

/// <summary>
/// The data structure representing a diff is a list of tuples:
/// [[DELETE, 'Hello'], [INSERT, 'Goodbye'], [EQUAL, ' world.']]
/// which means: delete 'Hello', add 'Goodbye' and keep ' world.'.
/// Mutable by design — the cleanup passes rewrite tuples in place, matching the JS arrays.
/// </summary>
public sealed class DiffTuple(int op, string text)
{
    public int Op { get; set; } = op;

    public string Text { get; set; } = text;

    public void Deconstruct(out int op, out string text)
    {
        op   = Op;
        text = Text;
    }

    public override string ToString()
    {
        return $"[{Op}, \"{Text}\"]";
    }
}

/// <summary>JS: the {oldRange, newRange} cursor_pos object form.</summary>
public sealed record CursorRange(int Index, int Length);

public sealed class CursorInfo
{
    public CursorRange? OldRange { get; set; }

    public CursorRange? NewRange { get; set; }
}

public static class FastDiff
{
    public const int Delete = -1;
    public const int Insert = 1;
    public const int Equal  = 0;

    /// <summary>
    /// Find the differences between two texts. Simplifies the problem by stripping any
    /// common prefix or suffix off the texts before diffing.
    /// </summary>
    /// <param name="text1">Old string to be diffed.</param>
    /// <param name="text2">New string to be diffed.</param>
    /// <param name="cursorPos">Edit position in text1 (JS number form).</param>
    /// <param name="cleanup">Apply semantic cleanup before returning.</param>
    public static List<DiffTuple> Diff(string text1, string text2, int? cursorPos = null, bool cleanup = false)
    {
        var cursor = cursorPos.HasValue
            ? new CursorInfo { OldRange = new CursorRange(cursorPos.Value, 0) }
            : null;
        return DiffMain(text1, text2, cursor, cleanup, fixUnicode : true, depth : 0);
    }

    /// <summary>JS: diff(text1, text2, {oldRange, newRange}, cleanup).</summary>
    public static List<DiffTuple> Diff(string text1, string text2, CursorInfo cursor, bool cleanup = false)
    {
        return DiffMain(text1, text2, cursor, cleanup, fixUnicode : true, depth : 0);
    }

    private static List<DiffTuple> DiffMain(
        string      text1,
        string      text2,
        CursorInfo? cursorPos,
        bool        cleanup,
        bool        fixUnicode,
        int         depth
    )
    {
        // fast-diff infinite-recurses on a handful of pathological inputs (JS dies with
        // RangeError: maximum call stack). A .NET StackOverflowException is process-fatal
        // and uncatchable, so recursion is depth-guarded instead. Legitimate depth is
        // O(log n) (bisect/half-match splits); 1000 is far beyond any real input.
        if (depth > 1000)
        {
            throw new InvalidOperationException("fast-diff recursion depth exceeded on pathological input");
        }

        // Check for equality
        if (text1 == text2)
        {
            if (text1.Length > 0)
            {
                return [new DiffTuple(Equal, text1)];
            }

            return [];
        }

        if (cursorPos != null)
        {
            var editDiff = FindCursorEditDiff(text1, text2, cursorPos);
            if (editDiff != null)
            {
                return editDiff;
            }
        }

        // Trim off common prefix (speedup).
        var commonLength = DiffCommonPrefix(text1, text2);
        var commonPrefix = text1[..commonLength];
        text1 = text1[commonLength..];
        text2 = text2[commonLength..];

        // Trim off common suffix (speedup).
        commonLength = DiffCommonSuffix(text1, text2);
        var commonSuffix = text1[^commonLength..];
        text1 = text1[..^commonLength];
        text2 = text2[..^commonLength];

        // Compute the diff on the middle block.
        var diffs = DiffCompute(text1, text2, depth);

        // Restore the prefix and suffix.
        if (commonPrefix.Length > 0)
        {
            diffs.Insert(0, new DiffTuple(Equal, commonPrefix));
        }

        if (commonSuffix.Length > 0)
        {
            diffs.Add(new DiffTuple(Equal, commonSuffix));
        }

        DiffCleanupMerge(diffs, fixUnicode);
        if (cleanup)
        {
            DiffCleanupSemantic(diffs);
        }

        return diffs;
    }

    /// <summary>
    /// Find the differences between two texts. Assumes that the texts do not have any
    /// common prefix or suffix.
    /// </summary>
    private static List<DiffTuple> DiffCompute(string text1, string text2, int depth)
    {
        if (text1.Length == 0)
        {
            // Just add some text (speedup).
            return [new DiffTuple(Insert, text2)];
        }

        if (text2.Length == 0)
        {
            // Just delete some text (speedup).
            return [new DiffTuple(Delete, text1)];
        }

        var longText = text1.Length > text2.Length ? text1 : text2;
        var shorText = text1.Length > text2.Length ? text2 : text1;
        var i        = longText.IndexOf(shorText, StringComparison.Ordinal);
        if (i != -1)
        {
            // Shorter text is inside the longer text (speedup).
            var diffs = new List<DiffTuple>
            {
                new(Insert, longText[..i]),
                new(Equal, shorText),
                new(Insert, longText[(i + shorText.Length)..]),
            };
            // Swap insertions for deletions if diff is reversed.
            if (text1.Length <= text2.Length) return diffs;
            diffs[0].Op = Delete;
            diffs[2].Op = Delete;

            return diffs;
        }

        if (shorText.Length == 1)
        {
            // Single character string.
            // After the previous speedup, the character can't be an equality.
            return [new DiffTuple(Delete, text1), new DiffTuple(Insert, text2)];
        }

        // Check to see if the problem can be split in two.
        var hm = DiffHalfMatch(text1, text2);
        if (hm == null) return DiffBisect(text1, text2, depth);
        // A half-match was found, sort out the return data.
        var hmv = hm.Value;
        var (text1a, text1b, text2a, text2b, midCommon) = hmv;
        // Send both pairs off for separate processing.
        var diffsa = DiffMain(text1a, text2a, null, false, false, depth + 1);
        var diffsb = DiffMain(text1b, text2b, null, false, false, depth + 1);
        // Merge the results.
        var result = new List<DiffTuple>(diffsa) { new(Equal, midCommon) };
        result.AddRange(diffsb);
        return result;
    }

    /// <summary>
    /// Find the 'middle snake' of a diff, split the problem in two and return the
    /// recursively constructed diff. See Myers 1986 paper: An O(ND) Difference
    /// Algorithm and Its Variations.
    /// </summary>
    private static List<DiffTuple> DiffBisect(string text1, string text2, int depth)
    {
        // Cache the text lengths to prevent multiple calls.
        var text1Length = text1.Length;
        var text2Length = text2.Length;
        var maxD        = (text1Length + text2Length + 1) / 2;
        var vOffset     = maxD;
        var vLength     = 2 * maxD;
        var v1          = new int[vLength];
        var v2          = new int[vLength];
        // Setting all elements to -1 is faster in Chrome & Firefox than mixing
        // integers and undefined.
        for (var x = 0; x < vLength; x++)
        {
            v1[x] = -1;
            v2[x] = -1;
        }

        v1[vOffset + 1] = 0;
        v2[vOffset + 1] = 0;
        var delta = text1Length - text2Length;
        // If the total number of characters is odd, then the front path will collide
        // with the reverse path.
        var front = delta % 2 != 0;
        // Offsets for start and end of k loop. Prevents mapping of space beyond the grid.
        var k1start = 0;
        var k1end   = 0;
        var k2start = 0;
        var k2end   = 0;
        for (var d = 0; d < maxD; d++)
        {
            // Walk the front path one step.
            for (var k1 = -d + k1start; k1 <= d - k1end; k1 += 2)
            {
                var k1Offset = vOffset + k1;
                int x1;
                if (k1 == -d || (k1 != d && v1[k1Offset - 1] < v1[k1Offset + 1]))
                {
                    x1 = v1[k1Offset + 1];
                }
                else
                {
                    x1 = v1[k1Offset - 1] + 1;
                }

                var y1 = x1 - k1;
                while (x1 < text1Length && y1 < text2Length && text1[x1] == text2[y1])
                {
                    x1++;
                    y1++;
                }

                v1[k1Offset] = x1;
                if (x1 > text1Length)
                {
                    // Ran off the right of the graph.
                    k1end += 2;
                }
                else if (y1 > text2Length)
                {
                    // Ran off the bottom of the graph.
                    k1start += 2;
                }
                else if (front)
                {
                    var k2Offset = vOffset + delta - k1;
                    if (k2Offset < 0 || k2Offset >= vLength || v2[k2Offset] == -1) continue;
                    // Mirror x2 onto top-left coordinate system.
                    var x2 = text1Length - v2[k2Offset];
                    if (x1 >= x2)
                    {
                        // Overlap detected.
                        return DiffBisectSplit(text1, text2, x1, y1, depth);
                    }
                }
            }

            // Walk the reverse path one step.
            for (var k2 = -d + k2start; k2 <= d - k2end; k2 += 2)
            {
                var k2Offset = vOffset + k2;
                int x2;
                if (k2 == -d || (k2 != d && v2[k2Offset - 1] < v2[k2Offset + 1]))
                {
                    x2 = v2[k2Offset + 1];
                }
                else
                {
                    x2 = v2[k2Offset - 1] + 1;
                }

                var y2 = x2 - k2;
                while (x2                          < text1Length && y2 < text2Length &&
                       text1[text1Length - x2 - 1] == text2[text2Length - y2 - 1])
                {
                    x2++;
                    y2++;
                }

                v2[k2Offset] = x2;
                if (x2 > text1Length)
                {
                    // Ran off the left of the graph.
                    k2end += 2;
                }
                else if (y2 > text2Length)
                {
                    // Ran off the top of the graph.
                    k2start += 2;
                }
                else if (!front)
                {
                    var k1Offset = vOffset + delta - k2;
                    if (k1Offset < 0 || k1Offset >= vLength || v1[k1Offset] == -1) continue;
                    var x1 = v1[k1Offset];
                    var y1 = vOffset + x1 - k1Offset;
                    // Mirror x2 onto top-left coordinate system.
                    x2 = text1Length - x2;
                    if (x1 >= x2)
                    {
                        // Overlap detected.
                        return DiffBisectSplit(text1, text2, x1, y1, depth);
                    }
                }
            }
        }

        // Diff took too long and hit the deadline or
        // number of diffs equals number of characters, no commonality at all.
        return [new DiffTuple(Delete, text1), new DiffTuple(Insert, text2)];
    }

    /// <summary>
    /// Given the location of the 'middle snake', split the diff in two parts and recurse.
    /// </summary>
    private static List<DiffTuple> DiffBisectSplit(string text1, string text2, int x, int y, int depth)
    {
        var text1a = text1[..x];
        var text2a = text2[..y];
        var text1b = text1[x..];
        var text2b = text2[y..];

        // Compute both diffs serially.
        var diffs  = DiffMain(text1a, text2a, null, false, false, depth + 1);
        var diffsb = DiffMain(text1b, text2b, null, false, false, depth + 1);

        diffs.AddRange(diffsb);
        return diffs;
    }

    /// <summary>Determine the common prefix of two strings.</summary>
    private static int DiffCommonPrefix(string text1, string text2)
    {
        // Quick check for common null cases.
        if (text1.Length == 0 || text2.Length == 0 || text1[0] != text2[0])
        {
            return 0;
        }

        // Binary search.
        // Performance analysis: http://neil.fraser.name/news/2007/10/09/
        var pointerMin   = 0;
        var pointerMax   = Math.Min(text1.Length, text2.Length);
        var pointerMid   = pointerMax;
        var pointerStart = 0;
        while (pointerMin < pointerMid)
        {
            if (
                string.CompareOrdinal(text1, pointerStart, text2, pointerStart, pointerMid - pointerStart) == 0
            )
            {
                pointerMin   = pointerMid;
                pointerStart = pointerMin;
            }
            else
            {
                pointerMax = pointerMid;
            }

            pointerMid = (pointerMax - pointerMin) / 2 + pointerMin;
        }

        if (IsSurrogatePairStart(text1[pointerMid - 1]))
        {
            pointerMid--;
        }

        return pointerMid;
    }

    /// <summary>Determine if the suffix of one string is the prefix of another.</summary>
    private static int DiffCommonOverlap(string text1, string text2)
    {
        // Cache the text lengths to prevent multiple calls.
        var text1Length = text1.Length;
        var text2Length = text2.Length;
        // Eliminate the null case.
        if (text1Length == 0 || text2Length == 0)
        {
            return 0;
        }

        // Truncate the longer string.
        if (text1Length > text2Length)
        {
            text1 = text1[(text1Length - text2Length)..];
        }
        else if (text1Length < text2Length)
        {
            text2 = text2[..text1Length];
        }

        var textLength = Math.Min(text1Length, text2Length);
        // Quick check for the worst case.
        if (text1 == text2)
        {
            return textLength;
        }

        // Start by looking for a single character match and increase length until no
        // match is found.
        // Performance analysis: http://neil.fraser.name/news/2010/11/04/
        var best   = 0;
        var length = 1;
        while (true)
        {
            var pattern = text1[(textLength - length)..];
            var found   = text2.IndexOf(pattern, StringComparison.Ordinal);
            if (found == -1)
            {
                return best;
            }

            length += found;
            if (found != 0 && text1[^length..] != text2[..length]) continue;
            best = length;
            length++;
        }
    }

    /// <summary>Determine the common suffix of two strings.</summary>
    private static int DiffCommonSuffix(string text1, string text2)
    {
        // Quick check for common null cases.
        if (text1.Length == 0 || text2.Length == 0 || text1[^1] != text2[^1])
        {
            return 0;
        }

        // Binary search.
        // Performance analysis: http://neil.fraser.name/news/2007/10/09/
        var pointerMin = 0;
        var pointerMax = Math.Min(text1.Length, text2.Length);
        var pointerMid = pointerMax;
        var pointerEnd = 0;
        while (pointerMin < pointerMid)
        {
            if (string.CompareOrdinal(text1, text1.Length - pointerMid, text2, text2.Length - pointerMid,
                                      pointerMid          - pointerEnd) == 0)
            {
                pointerMin = pointerMid;
                pointerEnd = pointerMin;
            }
            else
            {
                pointerMax = pointerMid;
            }

            pointerMid = (pointerMax - pointerMin) / 2 + pointerMin;
        }

        if (IsSurrogatePairEnd(text1[^pointerMid]))
        {
            pointerMid--;
        }

        return pointerMid;
    }

    /// <summary>
    /// Do the two texts share a substring which is at least half the length of the
    /// longer text? This speedup can produce non-minimal diffs.
    /// Returns (prefix of text1, suffix of text1, prefix of text2, suffix of text2,
    /// common middle) or null when there was no match.
    /// </summary>
    private static (string Text1A, string Text1B, string Text2A, string Text2B, string MidCommon)? DiffHalfMatch(
        string text1,
        string text2
    )
    {
        var longText  = text1.Length > text2.Length ? text1 : text2;
        var shortText = text1.Length > text2.Length ? text2 : text1;
        if (longText.Length < 4 || shortText.Length * 2 < longText.Length)
        {
            return null; // Pointless.
        }

        // First check if the second quarter is the seed for a half-match.
        var hm1 = DiffHalfMatchI(longText, shortText, (longText.Length + 3) / 4);
        // Check again based on the third quarter.
        var                                       hm2 = DiffHalfMatchI(longText, shortText, (longText.Length + 1) / 2);
        (string, string, string, string, string)? hm;
        if (hm1 == null && hm2 == null)
        {
            return null;
        }
        else if (hm2 == null)
        {
            hm = hm1;
        }
        else if (hm1 == null)
        {
            hm = hm2;
        }
        else
        {
            // Both matched. Select the longest.
            hm = hm1.Value.Item5.Length > hm2.Value.Item5.Length ? hm1 : hm2;
        }

        // A half-match was found, sort out the return data.
        string text1a, text1b, text2a, text2b;
        var    hmv = hm!.Value;
        if (text1.Length > text2.Length)
        {
            text1a = hmv.Item1;
            text1b = hmv.Item2;
            text2a = hmv.Item3;
            text2b = hmv.Item4;
        }
        else
        {
            text2a = hmv.Item1;
            text2b = hmv.Item2;
            text1a = hmv.Item3;
            text1b = hmv.Item4;
        }

        var midCommon = hmv.Item5;
        return (text1a, text1b, text2a, text2b, midCommon);

        (string, string, string, string, string)? DiffHalfMatchI(string longtext, string shortText, int i)
        {
            // Start with a 1/4 length substring at position i as a seed.
            var seed           = longtext.Substring(i, Math.Min(longtext.Length / 4, longtext.Length - i));
            var j              = -1;
            var bestCommon     = "";
            var bestLongTextA  = "";
            var bestLongTextB  = "";
            var bestShortTextA = "";
            var bestShortTextB = "";
            while ((j = shortText.IndexOf(seed, j + 1, StringComparison.Ordinal)) != -1)
            {
                var prefixLength = DiffCommonPrefix(longtext[i..], shortText[j..]);
                var suffixLength = DiffCommonSuffix(longtext[..i], shortText[..j]);
                if (bestCommon.Length >= suffixLength + prefixLength) continue;
                bestCommon = shortText.Substring(j - suffixLength, suffixLength) +
                             shortText.Substring(j, prefixLength);
                bestLongTextA  = longtext[..(i  - suffixLength)];
                bestLongTextB  = longtext[(i    + prefixLength)..];
                bestShortTextA = shortText[..(j - suffixLength)];
                bestShortTextB = shortText[(j   + prefixLength)..];
            }

            if (bestCommon.Length * 2 >= longtext.Length)
            {
                return (bestLongTextA, bestLongTextB, bestShortTextA, bestShortTextB, bestCommon);
            }

            return null;
        }
    }

    /// <summary>Reduce the number of edits by eliminating semantically trivial equalities.</summary>
    private static void DiffCleanupSemantic(List<DiffTuple> diffs)
    {
        var changes = false;
        // JS uses a plain array with manual length where indexes can temporarily go
        // negative (equalities[equalitiesLength++] with equalitiesLength < 0 writes a
        // "-n" property that is read back later); a Dictionary reproduces that exactly.
        var     equalities       = new Dictionary<int, int>(); // Stack of indices where equalities are found.
        var     equalitiesLength = 0;                          // Keeping our own length var is faster in JS.
        string? lastEquality     = null;
        // Always equal to diffs[equalities[equalitiesLength - 1]][1]
        var pointer = 0; // Index of current position.
        // Number of characters that changed prior to the equality.
        var lengthInsertions1 = 0;
        var lengthDeletions1  = 0;
        // Number of characters that changed after the equality.
        var lengthInsertions2 = 0;
        var lengthDeletions2  = 0;
        while (pointer < diffs.Count)
        {
            if (diffs[pointer].Op == Equal)
            {
                // Equality found.
                equalities[equalitiesLength] = pointer;
                equalitiesLength++;
                lengthInsertions1 = lengthInsertions2;
                lengthDeletions1  = lengthDeletions2;
                lengthInsertions2 = 0;
                lengthDeletions2  = 0;
                lastEquality      = diffs[pointer].Text;
            }
            else
            {
                // An insertion or deletion.
                if (diffs[pointer].Op == Insert)
                {
                    lengthInsertions2 += diffs[pointer].Text.Length;
                }
                else
                {
                    lengthDeletions2 += diffs[pointer].Text.Length;
                }

                // Eliminate an equality that is smaller or equal to the edits on both
                // sides of it.
                if (
                    lastEquality        != null                                          &&
                    lastEquality.Length <= Math.Max(lengthInsertions1, lengthDeletions1) &&
                    lastEquality.Length <= Math.Max(lengthInsertions2, lengthDeletions2)
                )
                {
                    // Duplicate record.
                    diffs.Insert(equalities[equalitiesLength - 1], new DiffTuple(Delete, lastEquality));
                    // Change second copy to insert.
                    diffs[equalities[equalitiesLength - 1] + 1].Op = Insert;
                    // Throw away the equality we just deleted.
                    equalitiesLength--;
                    // Throw away the previous equality (it needs to be reevaluated).
                    equalitiesLength--;
                    pointer           = equalitiesLength > 0 ? equalities[equalitiesLength - 1] : -1;
                    lengthInsertions1 = 0; // Reset the counters.
                    lengthDeletions1  = 0;
                    lengthInsertions2 = 0;
                    lengthDeletions2  = 0;
                    lastEquality      = null;
                    changes           = true;
                }
            }

            pointer++;
        }

        // Normalize the diff.
        if (changes)
        {
            DiffCleanupMerge(diffs, false);
        }

        DiffCleanupSemanticLossless(diffs);

        // Find any overlaps between deletions and insertions.
        // e.g: <del>abcxxx</del><ins>xxxdef</ins>
        //   -> <del>abc</del>xxx<ins>def</ins>
        // e.g: <del>xxxabc</del><ins>defxxx</ins>
        //   -> <ins>def</ins>xxx<del>abc</del>
        // Only extract an overlap if it is as big as the edit ahead or behind it.
        pointer = 1;
        while (pointer < diffs.Count)
        {
            if (diffs[pointer - 1].Op == Delete && diffs[pointer].Op == Insert)
            {
                var deletion       = diffs[pointer - 1].Text;
                var insertion      = diffs[pointer].Text;
                var overlapLength1 = DiffCommonOverlap(deletion, insertion);
                var overlapLength2 = DiffCommonOverlap(insertion, deletion);
                if (overlapLength1 >= overlapLength2)
                {
                    if (overlapLength1 >= deletion.Length / 2.0 || overlapLength1 >= insertion.Length / 2.0)
                    {
                        // Overlap found. Insert an equality and trim the surrounding edits.
                        diffs.Insert(pointer, new DiffTuple(Equal, insertion[..overlapLength1]));
                        diffs[pointer - 1].Text = deletion[..^overlapLength1];
                        diffs[pointer + 1].Text = insertion[overlapLength1..];
                        pointer++;
                    }
                }
                else
                {
                    if (overlapLength2 >= deletion.Length / 2.0 || overlapLength2 >= insertion.Length / 2.0)
                    {
                        // Reverse overlap found.
                        // Insert an equality and swap and trim the surrounding edits.
                        diffs.Insert(pointer, new DiffTuple(Equal, deletion[..overlapLength2]));
                        diffs[pointer - 1].Op   = Insert;
                        diffs[pointer - 1].Text = insertion[..^overlapLength2];
                        diffs[pointer + 1].Op   = Delete;
                        diffs[pointer + 1].Text = deletion[overlapLength2..];
                        pointer++;
                    }
                }

                pointer++;
            }

            pointer++;
        }
    }

    // JS: nonAlphaNumericRegex_ = /[^a-zA-Z0-9]/
    private static bool IsNonAlphaNumeric(char c)
    {
        return c is (< 'a' or > 'z') and (< 'A' or > 'Z') and (< '0' or > '9');
    }

    // JS: whitespaceRegex_ = /\s/ — the exact ECMAScript \s character set.
    private static bool IsJsWhitespace(char c)
    {
        return c is
            '\t'
         or '\n'
         or '\v'
         or '\f'
         or '\r'
         or ' '
         or '\u00a0'
         or '\u1680'
         or (>= '\u2000' and <= '\u200a')
         or '\u2028'
         or '\u2029'
         or '\u202f'
         or '\u205f'
         or '\u3000'
         or '\ufeff';
    }

    // JS: linebreakRegex_ = /[\r\n]/
    private static bool IsLineBreak(char c)
    {
        return c is '\r' or '\n';
    }

    // JS: blanklineEndRegex_ = /\n\r?\n$/ ($ matches at the very end in JS).
    private static bool MatchesBlankLineEnd(string s)
    {
        var n = s.Length;
        if (n < 2 || s[n - 1] != '\n')
        {
            return false;
        }

        if (s[n - 2] == '\n')
        {
            return true;
        }

        return n >= 3 && s[n - 2] == '\r' && s[n - 3] == '\n';
    }

    // JS: blanklineStartRegex_ = /^\r?\n\r?\n/
    private static bool MatchesBlankLineStart(string s)
    {
        var i = 0;
        if (i < s.Length && s[i] == '\r')
        {
            i++;
        }

        if (i >= s.Length || s[i] != '\n')
        {
            return false;
        }

        i++;
        if (i < s.Length && s[i] == '\r')
        {
            i++;
        }

        return i < s.Length && s[i] == '\n';
    }

    /// <summary>
    /// Look for single edits surrounded on both sides by equalities which can be
    /// shifted sideways to align the edit to a word boundary.
    /// e.g: The c&lt;ins&gt;at c&lt;/ins&gt;ame. -&gt; The &lt;ins&gt;cat &lt;/ins&gt;came.
    /// </summary>
    private static void DiffCleanupSemanticLossless(List<DiffTuple> diffs)
    {
        // Given two strings, compute a score representing whether the internal boundary
        // falls on logical boundaries. Scores range from 6 (best) to 0 (worst).
        static int DiffCleanupSemanticScore(string one, string two)
        {
            if (one.Length == 0 || two.Length == 0)
            {
                // Edges are the best.
                return 6;
            }

            // Each port of this function behaves slightly differently due to subtle
            // differences in each language's definition of things like 'whitespace'.
            // Since this function's purpose is largely cosmetic, the choice has been
            // made to use each language's native features rather than force total
            // conformity.
            var char1            = one[^1];
            var char2            = two[0];
            var nonAlphaNumeric1 = IsNonAlphaNumeric(char1);
            var nonAlphaNumeric2 = IsNonAlphaNumeric(char2);
            var whitespace1      = nonAlphaNumeric1 && IsJsWhitespace(char1);
            var whitespace2      = nonAlphaNumeric2 && IsJsWhitespace(char2);
            var lineBreak1       = whitespace1      && IsLineBreak(char1);
            var lineBreak2       = whitespace2      && IsLineBreak(char2);
            var blankLine1       = lineBreak1       && MatchesBlankLineEnd(one);
            var blankLine2       = lineBreak2       && MatchesBlankLineStart(two);

            if (blankLine1 || blankLine2)
            {
                // Five points for blank lines.
                return 5;
            }
            else if (lineBreak1 || lineBreak2)
            {
                // Four points for line breaks.
                return 4;
            }
            else if (nonAlphaNumeric1 && !whitespace1 && whitespace2)
            {
                // Three points for end of sentences.
                return 3;
            }
            else if (whitespace1 || whitespace2)
            {
                // Two points for whitespace.
                return 2;
            }
            else if (nonAlphaNumeric1 || nonAlphaNumeric2)
            {
                // One point for non-alphanumeric.
                return 1;
            }

            return 0;
        }

        var pointer = 1;
        // Intentionally ignore the first and last element (don't need checking).
        while (pointer < diffs.Count - 1)
        {
            if (diffs[pointer - 1].Op == Equal && diffs[pointer + 1].Op == Equal)
            {
                // This is a single edit surrounded by equalities.
                var equality1 = diffs[pointer - 1].Text;
                var edit      = diffs[pointer].Text;
                var equality2 = diffs[pointer + 1].Text;

                // First, shift the edit as far left as possible.
                var commonOffset = DiffCommonSuffix(equality1, edit);
                if (commonOffset != 0)
                {
                    var commonString = edit[^commonOffset..];
                    equality1 = equality1[..^commonOffset];
                    edit      = commonString + edit[..^commonOffset];
                    equality2 = commonString + equality2;
                }

                // Second, step character by character right, looking for the best fit.
                var bestEquality1 = equality1;
                var bestEdit = edit;
                var bestEquality2 = equality2;
                var bestScore = DiffCleanupSemanticScore(equality1, edit) + DiffCleanupSemanticScore(edit, equality2);
                while (edit.Length > 0 && equality2.Length > 0 && edit[0] == equality2[0])
                {
                    equality1 += edit[0];
                    edit      =  edit[1..] + equality2[0];
                    equality2 =  equality2[1..];
                    var score = DiffCleanupSemanticScore(equality1, edit) + DiffCleanupSemanticScore(edit, equality2);
                    // The >= encourages trailing rather than leading whitespace on edits.
                    if (score < bestScore) continue;
                    bestScore     = score;
                    bestEquality1 = equality1;
                    bestEdit      = edit;
                    bestEquality2 = equality2;
                }

                if (diffs[pointer - 1].Text != bestEquality1)
                {
                    // We have an improvement, save it back to the diff.
                    if (bestEquality1.Length > 0)
                    {
                        diffs[pointer - 1].Text = bestEquality1;
                    }
                    else
                    {
                        diffs.RemoveAt(pointer - 1);
                        pointer--;
                    }

                    diffs[pointer].Text = bestEdit;
                    if (bestEquality2.Length > 0)
                    {
                        diffs[pointer + 1].Text = bestEquality2;
                    }
                    else
                    {
                        diffs.RemoveAt(pointer + 1);
                        pointer--;
                    }
                }
            }

            pointer++;
        }
    }

    /// <summary>
    /// Reorder and merge like edit sections. Merge equalities. Any edit section can
    /// move as long as it doesn't cross an equality.
    /// </summary>
    private static void DiffCleanupMerge(List<DiffTuple> diffs, bool fixUnicode)
    {
        while (true)
        {
            diffs.Add(new DiffTuple(Equal, "")); // Add a dummy entry at the end.
            var pointer     = 0;
            var countDelete = 0;
            var countInsert = 0;
            var textDelete  = "";
            var textInsert  = "";
            while (pointer < diffs.Count)
            {
                if (pointer < diffs.Count - 1 && diffs[pointer].Text.Length == 0)
                {
                    diffs.RemoveAt(pointer);
                    continue;
                }

                var op = diffs[pointer].Op;
                switch (op)
                {
                    case Insert :
                        countInsert++;
                        textInsert += diffs[pointer].Text;
                        pointer++;
                        break;
                    case Delete :
                        countDelete++;
                        textDelete += diffs[pointer].Text;
                        pointer++;
                        break;
                    // Equal
                    default :
                    {
                        var previousEquality = pointer - countInsert - countDelete - 1;
                        if (fixUnicode)
                        {
                            // prevent splitting of unicode surrogate pairs. when fix_unicode is true,
                            // we assume that the old and new text in the diff are complete and correct
                            // unicode-encoded JS strings, but the tuple boundaries may fall between
                            // surrogate pairs. we fix this by shaving off stray surrogates from the end
                            // of the previous equality and the beginning of this equality. this may
                            // create empty equalities or a common prefix or suffix. for example, if AB
                            // and AC are emojis, `[[0, 'A'], [-1, 'BA'], [0, 'C']]` would turn into
                            // deleting 'ABAC' and inserting 'AC', and then the common suffix 'AC' will
                            // be eliminated. in this particular case, both equalities go away, we absorb
                            // any previous inequalities, and we keep scanning for the next equality
                            // before rewriting the tuples.
                            if (previousEquality >= 0 && EndsWithPairStart(diffs[previousEquality].Text))
                            {
                                var stray = diffs[previousEquality].Text[^1..];
                                diffs[previousEquality].Text = diffs[previousEquality].Text[..^1];

                                textDelete = stray + textDelete;
                                textInsert = stray + textInsert;
                                if (diffs[previousEquality].Text.Length == 0)
                                {
                                    // emptied out previous equality, so delete it and include previous delete/insert
                                    diffs.RemoveAt(previousEquality);
                                    pointer--;
                                    var k = previousEquality - 1;
                                    if (k >= 0 && k < diffs.Count && diffs[k].Op == Insert)
                                    {
                                        countInsert++;
                                        textInsert = diffs[k].Text + textInsert;
                                        k--;
                                    }

                                    if (k >= 0 && k < diffs.Count && diffs[k].Op == Delete)
                                    {
                                        countDelete++;
                                        textDelete = diffs[k].Text + textDelete;
                                        k--;
                                    }

                                    previousEquality = k;
                                }
                            }

                            if (diffs[pointer].Text.Length > 0 && StartsWithPairEnd(diffs[pointer].Text))
                            {
                                var stray = diffs[pointer].Text[..1];
                                diffs[pointer].Text =  diffs[pointer].Text[1..];
                                textDelete          += stray;
                                textInsert          += stray;
                            }
                        }

                        if (pointer < diffs.Count - 1 && diffs[pointer].Text.Length == 0)
                        {
                            // for empty equality not at end, wait for next equality
                            diffs.RemoveAt(pointer);
                            continue;
                        }

                        if (textDelete.Length > 0 || textInsert.Length > 0)
                        {
                            // note that diff_commonPrefix and diff_commonSuffix are unicode-aware
                            if (textDelete.Length > 0 && textInsert.Length > 0)
                            {
                                // Factor out any common prefixes.
                                var commonLength = DiffCommonPrefix(textInsert, textDelete);
                                if (commonLength != 0)
                                {
                                    if (previousEquality >= 0)
                                    {
                                        diffs[previousEquality].Text += textInsert[..commonLength];
                                    }
                                    else
                                    {
                                        diffs.Insert(0, new DiffTuple(Equal, textInsert[..commonLength]));
                                        pointer++;
                                    }

                                    textInsert = textInsert[commonLength..];
                                    textDelete = textDelete[commonLength..];
                                }

                                // Factor out any common suffixes.
                                commonLength = DiffCommonSuffix(textInsert, textDelete);
                                if (commonLength != 0)
                                {
                                    diffs[pointer].Text = textInsert[^commonLength..] + diffs[pointer].Text;
                                    textInsert          = textInsert[..^commonLength];
                                    textDelete          = textDelete[..^commonLength];
                                }
                            }

                            // Delete the offending records and add the merged ones.
                            var n = countInsert + countDelete;
                            switch (textDelete.Length)
                            {
                                case 0 when textInsert.Length == 0 :
                                    diffs.RemoveRange(pointer - n, n);
                                    pointer -= n;
                                    break;
                                case 0 :
                                    diffs.RemoveRange(pointer - n, n);
                                    diffs.Insert(pointer      - n, new DiffTuple(Insert, textInsert));
                                    pointer = pointer - n + 1;
                                    break;
                                default :
                                {
                                    if (textInsert.Length == 0)
                                    {
                                        diffs.RemoveRange(pointer - n, n);
                                        diffs.Insert(pointer      - n, new DiffTuple(Delete, textDelete));
                                        pointer = pointer - n + 1;
                                    }
                                    else
                                    {
                                        diffs.RemoveRange(pointer - n, n);
                                        diffs.Insert(pointer      - n, new DiffTuple(Delete, textDelete));
                                        diffs.Insert(pointer - n  + 1, new DiffTuple(Insert, textInsert));
                                        pointer = pointer - n + 2;
                                    }

                                    break;
                                }
                            }
                        }

                        if (pointer != 0 && diffs[pointer - 1].Op == Equal)
                        {
                            // Merge this equality with the previous one.
                            diffs[pointer - 1].Text += diffs[pointer].Text;
                            diffs.RemoveAt(pointer);
                        }
                        else
                        {
                            pointer++;
                        }

                        countInsert = 0;
                        countDelete = 0;
                        textDelete  = "";
                        textInsert  = "";
                        break;
                    }
                }
            }

            if (diffs.Count > 0 && diffs[^1].Text.Length == 0)
            {
                diffs.RemoveAt(diffs.Count - 1); // Remove the dummy entry at the end.
            }

            // Second pass: look for single edits surrounded on both sides by equalities
            // which can be shifted sideways to eliminate an equality.
            // e.g: A<ins>BA</ins>C -> <ins>AB</ins>AC
            var changes = false;
            pointer = 1;
            // Intentionally ignore the first and last element (don't need checking).
            while (pointer < diffs.Count - 1)
            {
                if (diffs[pointer - 1].Op == Equal && diffs[pointer + 1].Op == Equal)
                {
                    // This is a single edit surrounded by equalities.
                    if (diffs[pointer].Text.EndsWith(diffs[pointer - 1].Text, StringComparison.Ordinal))
                    {
                        // Shift the edit over the previous equality.
                        diffs[pointer].Text = diffs[pointer - 1].Text +
                                              diffs[pointer].Text[..^diffs[pointer - 1].Text.Length];
                        diffs[pointer + 1].Text = diffs[pointer - 1].Text + diffs[pointer + 1].Text;
                        diffs.RemoveAt(pointer - 1);
                        changes = true;
                    }
                    else if (diffs[pointer].Text.StartsWith(diffs[pointer + 1].Text, StringComparison.Ordinal))
                    {
                        // Shift the edit over the next equality.
                        diffs[pointer - 1].Text += diffs[pointer + 1].Text;
                        diffs[pointer].Text = diffs[pointer].Text[diffs[pointer + 1].Text.Length..] +
                                              diffs[pointer + 1].Text;
                        diffs.RemoveAt(pointer + 1);
                        changes = true;
                    }
                }

                pointer++;
            }

            // If shifts were made, the diff needs reordering and another shift sweep.
            if (changes)
            {
                continue;
            }

            break;
        }
    }

    private static bool IsSurrogatePairStart(char c)
    {
        return c >= 0xd800 && c <= 0xdbff;
    }

    private static bool IsSurrogatePairEnd(char c)
    {
        return c >= 0xdc00 && c <= 0xdfff;
    }

    private static bool StartsWithPairEnd(string str)
    {
        return str.Length > 0 && IsSurrogatePairEnd(str[0]);
    }

    private static bool EndsWithPairStart(string str)
    {
        return str.Length > 0 && IsSurrogatePairStart(str[^1]);
    }

    private static List<DiffTuple> RemoveEmptyTuples(IEnumerable<DiffTuple> tuples)
    {
        return tuples.Where(t => t.Text.Length > 0).ToList();
    }

    private static List<DiffTuple>? MakeEditSplice(string before, string oldMiddle, string newMiddle, string after)
    {
        if (EndsWithPairStart(before) || StartsWithPairEnd(after))
        {
            return null;
        }

        return RemoveEmptyTuples(
        [
            new DiffTuple(Equal, before),
            new DiffTuple(Delete, oldMiddle),
            new DiffTuple(Insert, newMiddle),
            new DiffTuple(Equal, after),
        ]);
    }

    /// <summary>JS substring(start) — clamps out-of-range start instead of throwing.</summary>
    private static string JsSubstring(string s, int start)
    {
        if (start <= 0)
        {
            return s;
        }

        return start >= s.Length ? "" : s[start..];
    }

    private static List<DiffTuple>? FindCursorEditDiff(string oldText, string newText, CursorInfo cursorPos)
    {
        // note: this runs after equality check has ruled out exact equality
        var oldRange = cursorPos.OldRange;
        var newRange = cursorPos.NewRange;

        var oldLength = oldText.Length;
        var newLength = newText.Length;

        if (oldRange == null)
        {
            return null;
        }

        switch (oldRange.Length)
        {
            case 0 when (newRange == null || newRange.Length == 0) :
            {
                // see if we have an insert or delete before or after cursor
                var oldCursor      = oldRange.Index;
                var oldBefore      = oldCursor >= oldText.Length ? oldText : oldText[..Math.Max(0, oldCursor)];
                var oldAfter       = JsSubstring(oldText, oldCursor);
                var maybeNewCursor = newRange?.Index;

                // editBefore: is this an insert or delete right before oldCursor?
                {
                    var newCursor = oldCursor + newLength - oldLength;
                    if (maybeNewCursor != null && maybeNewCursor != newCursor)
                    {
                        goto editAfter;
                    }

                    if (newCursor < 0 || newCursor > newLength)
                    {
                        goto editAfter;
                    }

                    var newBefore = newText[..newCursor];
                    var newAfter  = newText[newCursor..];
                    if (newAfter != oldAfter)
                    {
                        goto editAfter;
                    }

                    var prefixLength = Math.Min(oldCursor, newCursor);
                    var oldPrefix    = oldBefore[..prefixLength];
                    var newPrefix    = newBefore[..prefixLength];
                    if (oldPrefix != newPrefix)
                    {
                        goto editAfter;
                    }

                    var oldMiddle = oldBefore[prefixLength..];
                    var newMiddle = newBefore[prefixLength..];
                    return MakeEditSplice(oldPrefix, oldMiddle, newMiddle, oldAfter);
                }

                // editAfter: is this an insert or delete right after oldCursor?
                editAfter:
                {
                    if (maybeNewCursor != null && maybeNewCursor != oldCursor)
                    {
                        return null;
                    }

                    var cursor    = oldCursor;
                    var newBefore = cursor >= newText.Length ? newText : newText[..Math.Max(0, cursor)];
                    var newAfter  = JsSubstring(newText, cursor);
                    if (newBefore != oldBefore)
                    {
                        return null;
                    }

                    var suffixLength = Math.Min(oldLength - cursor, newLength - cursor);
                    var oldSuffix    = oldAfter[^suffixLength..];
                    var newSuffix    = newAfter[^suffixLength..];
                    if (oldSuffix != newSuffix)
                    {
                        return null;
                    }

                    var oldMiddle = oldAfter[..^suffixLength];
                    var newMiddle = newAfter[..^suffixLength];
                    return MakeEditSplice(oldBefore, oldMiddle, newMiddle, oldSuffix);
                }
            }
            case > 0 when newRange is { Length: 0 } :
            {
                // replaceRange: see if diff could be a splice of the old selection range
                // (JS slice clamps out-of-range bounds)
                var oldPrefix    = oldRange.Index >= oldLength ? oldText : oldText[..Math.Max(0, oldRange.Index)];
                var oldSuffix    = JsSubstring(oldText, oldRange.Index + oldRange.Length);
                var prefixLength = oldPrefix.Length;
                var suffixLength = oldSuffix.Length;
                if (newLength < prefixLength + suffixLength)
                {
                    return null;
                }

                var newPrefix = newText[..prefixLength];
                var newSuffix = newText[(newLength - suffixLength)..];
                if (oldPrefix != newPrefix || oldSuffix != newSuffix)
                {
                    return null;
                }

                var oldMiddle = oldText.Substring(prefixLength, oldLength - suffixLength - prefixLength);
                var newMiddle = newText.Substring(prefixLength, newLength - suffixLength - prefixLength);
                return MakeEditSplice(oldPrefix, oldMiddle, newMiddle, oldSuffix);
            }
            default :
                return null;
        }
    }
}
