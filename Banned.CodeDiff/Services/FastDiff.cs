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
///     fast-diff 的操作码。JS 使用原始数字 -1/1/0；显式赋值以保持这些取值（golden JSON 会把它们导出为整数）。<br />
///     fast-diff's op codes. JS uses the raw numbers -1/1/0; the explicit assignments
///     keep the wire values (golden JSON dumps them as ints).
/// </summary>
public enum DiffOp
{
    /// <summary>删除：文本仅在旧文本（text1）中。<br />Delete: text only in the old text (text1).</summary>
    Delete = -1,

    /// <summary>插入：文本仅在新文本（text2）中。<br />Insert: text only in the new text (text2).</summary>
    Insert = 1,

    /// <summary>相同：两段文本共有。<br />Equal: text common to both texts.</summary>
    Equal = 0
}

/// <summary>
///     一次删除、插入或保留操作;清理阶段会就地修改。<br />
///     One deletion, insertion or equality operation; cleanup mutates it in place.
/// </summary>
public sealed class DiffTuple(DiffOp op, string text)
{
    /// <summary>本元组的操作码。<br />The op code of this tuple.</summary>
    public DiffOp Op { get; set; } = op;

    /// <summary>本元组携带的文本。<br />The text carried by this tuple.</summary>
    public string Text { get; set; } = text;

    /// <summary>解构为 (Op, Text)。<br />Deconstructs into (Op, Text).</summary>
    /// <param name="op">操作码。The op code.</param>
    /// <param name="text">文本。The text.</param>
    public void Deconstruct(out DiffOp op, out string text)
    {
        op   = Op;
        text = Text;
    }

    /// <summary>返回形如 [op, "text"] 的调试字符串。<br />Returns a debug string of the form [op, "text"].</summary>
    public override string ToString()
    {
        return $"[{(int)Op}, \"{Text}\"]";
    }
}

/// <summary>
///     JS cursor_pos 的 {oldRange, newRange} 对象中单个范围的形式（起始索引 + 长度）。<br />JS: the {oldRange, newRange} cursor_pos
///     object form.
/// </summary>
/// <param name="Index">范围起始索引。Start index of the range.</param>
/// <param name="Length">范围长度。Length of the range.</param>
public sealed record CursorRange(int Index, int Length);

/// <summary>
///     JS cursor_pos 的 {oldRange, newRange} 对象形式，用于把 diff 定位到一次光标编辑；
///     oldRange 必填，newRange 可省略。<br />
///     The {oldRange, newRange} form of the JS cursor_pos object, used to locate the
///     diff at a cursor edit; oldRange is required, newRange is optional.
/// </summary>
public sealed class CursorInfo
{
    /// <summary>旧文本（text1）中的选区范围。<br />The selection range within text1 (old text).</summary>
    public CursorRange? OldRange { get; set; }

    /// <summary>新文本（text2）中的选区范围；可省略。<br />The selection range within text2 (new text); optional.</summary>
    public CursorRange? NewRange { get; set; }
}

/// <summary>
///     npm 包 fast-diff@1.3.0 的移植（单文件 diff.js）：基于 Neil Fraser 的
///     diff-match-patch，移除了 patch/match 功能与部分高级选项。<br />
///     Port of the npm package fast-diff@1.3.0 (diff.js, single file), based on
///     Neil Fraser's diff-match-patch with the patch and match functionality and
///     certain advanced options removed.
/// </summary>
public static class FastDiff
{
    /// <summary>
    ///     找出两段文本之间的差异；先剥离公共前缀与公共后缀，再对中间部分做 diff 以简化问题。<br />
    ///     Find the differences between two texts. Simplifies the problem by stripping any
    ///     common prefix or suffix off the texts before diffing.
    /// </summary>
    /// <param name="text1">旧文本。Old string to be diffed.</param>
    /// <param name="text2">新文本。New string to be diffed.</param>
    /// <param name="cursorPos">text1 中的编辑位置（JS number 形式）。Edit position in text1 (JS number form).</param>
    /// <param name="cleanup">返回前是否执行语义清理。Apply semantic cleanup before returning.</param>
    public static List<DiffTuple> Diff(string text1, string text2, int? cursorPos = null, bool cleanup = false)
    {
        var cursor = cursorPos.HasValue
            ? new CursorInfo { OldRange = new CursorRange(cursorPos.Value, 0) }
            : null;
        return DiffMain(text1, text2, cursor, cleanup, true, 0);
    }

    /// <summary>
    ///     对应 JS 的 diff(text1, text2, {oldRange, newRange}, cleanup)：以旧/新选区定位光标编辑。<br />JS: diff(text1, text2, {oldRange,
    ///     newRange}, cleanup).
    /// </summary>
    /// <param name="text1">旧文本。Old string to be diffed.</param>
    /// <param name="text2">新文本。New string to be diffed.</param>
    /// <param name="cursor">光标选区信息，用于加速定位本次编辑。Cursor selection info used to localize the edit.</param>
    /// <param name="cleanup">返回前是否执行语义清理。Apply semantic cleanup before returning.</param>
    public static List<DiffTuple> Diff(string text1, string text2, CursorInfo cursor, bool cleanup = false)
    {
        return DiffMain(text1, text2, cursor, cleanup, true, 0);
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
        // 病态输入可能无限递归,以深度护栏避免进程栈溢出。
        if (depth > 1000)
            throw new InvalidOperationException("fast-diff recursion depth exceeded on pathological input");

        if (text1 == text2)
        {
            if (text1.Length > 0) return [new DiffTuple(DiffOp.Equal, text1)];

            return [];
        }

        if (cursorPos != null)
        {
            var editDiff = FindCursorEditDiff(text1, text2, cursorPos);
            if (editDiff != null) return editDiff;
        }

        var commonLength = DiffCommonPrefix(text1, text2);
        var commonPrefix = text1[..commonLength];
        text1 = text1[commonLength..];
        text2 = text2[commonLength..];

        commonLength = DiffCommonSuffix(text1, text2);
        var commonSuffix = text1[^commonLength..];
        text1 = text1[..^commonLength];
        text2 = text2[..^commonLength];

        var diffs = DiffCompute(text1, text2, depth);

        if (commonPrefix.Length > 0) diffs.Insert(0, new DiffTuple(DiffOp.Equal, commonPrefix));

        if (commonSuffix.Length > 0) diffs.Add(new DiffTuple(DiffOp.Equal, commonSuffix));

        DiffCleanupMerge(diffs, fixUnicode);
        if (cleanup) DiffCleanupSemantic(diffs);

        return diffs;
    }

    /// <summary>
    ///     Find the differences between two texts. Assumes that the texts do not have any
    ///     common prefix or suffix.
    /// </summary>
    private static List<DiffTuple> DiffCompute(string text1, string text2, int depth)
    {
        if (text1.Length == 0)
            return [new DiffTuple(DiffOp.Insert, text2)];

        if (text2.Length == 0)
            return [new DiffTuple(DiffOp.Delete, text1)];

        var longText = text1.Length > text2.Length ? text1 : text2;
        var shorText = text1.Length > text2.Length ? text2 : text1;
        var i        = longText.IndexOf(shorText, StringComparison.Ordinal);
        if (i != -1)
        {
            var diffs = new List<DiffTuple>
            {
                new(DiffOp.Insert, longText[..i]),
                new(DiffOp.Equal, shorText),
                new(DiffOp.Insert, longText[(i + shorText.Length)..])
            };
            if (text1.Length <= text2.Length) return diffs;
            diffs[0].Op = DiffOp.Delete;
            diffs[2].Op = DiffOp.Delete;

            return diffs;
        }

        if (shorText.Length == 1)
            return [new DiffTuple(DiffOp.Delete, text1), new DiffTuple(DiffOp.Insert, text2)];

        var hm = DiffHalfMatch(text1, text2);
        if (hm == null) return DiffBisect(text1, text2, depth);
        var hmv = hm.Value;
        var (text1a, text1b, text2a, text2b, midCommon) = hmv;
        var diffsa = DiffMain(text1a, text2a, null, false, false, depth + 1);
        var diffsb = DiffMain(text1b, text2b, null, false, false, depth + 1);
        var result = new List<DiffTuple>(diffsa) { new(DiffOp.Equal, midCommon) };
        result.AddRange(diffsb);
        return result;
    }

    /// <summary>
    ///     Find the 'middle snake' of a diff, split the problem in two and return the
    ///     recursively constructed diff. See Myers 1986 paper: An O(ND) Difference
    ///     Algorithm and Its Variations.
    /// </summary>
    private static List<DiffTuple> DiffBisect(string text1, string text2, int depth)
    {
        var text1Length = text1.Length;
        var text2Length = text2.Length;
        var maxD        = (text1Length + text2Length + 1) / 2;
        var vOffset     = maxD;
        var vLength     = 2 * maxD;
        var v1          = new int[vLength];
        var v2          = new int[vLength];
        for (var x = 0; x < vLength; x++)
        {
            v1[x] = -1;
            v2[x] = -1;
        }

        v1[vOffset + 1] = 0;
        v2[vOffset + 1] = 0;
        var delta = text1Length - text2Length;
        // 奇数长度差时,正向路径与反向路径在本轮碰撞。
        var front = delta % 2 != 0;
        // 限制 k 的扫描范围,避免越出网格。
        var k1start = 0;
        var k1end   = 0;
        var k2start = 0;
        var k2end   = 0;
        for (var d = 0; d < maxD; d++)
        {
            // 正向扫描。
            for (var k1 = -d + k1start; k1 <= d - k1end; k1 += 2)
            {
                var k1Offset = vOffset + k1;
                int x1;
                if (k1 == -d || (k1 != d && v1[k1Offset - 1] < v1[k1Offset + 1]))
                    x1 = v1[k1Offset + 1];
                else
                    x1 = v1[k1Offset - 1] + 1;

                var y1 = x1 - k1;
                while (x1 < text1Length && y1 < text2Length && text1[x1] == text2[y1])
                {
                    x1++;
                    y1++;
                }

                v1[k1Offset] = x1;
                if (x1 > text1Length)
                {
                    k1end += 2;
                }
                else if (y1 > text2Length)
                {
                    k1start += 2;
                }
                else if (front)
                {
                    var k2Offset = vOffset + delta - k1;
                    if (k2Offset < 0 || k2Offset >= vLength || v2[k2Offset] == -1) continue;
                    // 反向坐标转为左上角坐标。
                    var x2 = text1Length - v2[k2Offset];
                    if (x1 >= x2)
                        return DiffBisectSplit(text1, text2, x1, y1, depth);
                }
            }

            // 反向扫描。
            for (var k2 = -d + k2start; k2 <= d - k2end; k2 += 2)
            {
                var k2Offset = vOffset + k2;
                int x2;
                if (k2 == -d || (k2 != d && v2[k2Offset - 1] < v2[k2Offset + 1]))
                    x2 = v2[k2Offset + 1];
                else
                    x2 = v2[k2Offset - 1] + 1;

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
                    k2end += 2;
                }
                else if (y2 > text2Length)
                {
                    k2start += 2;
                }
                else if (!front)
                {
                    var k1Offset = vOffset + delta - k2;
                    if (k1Offset < 0 || k1Offset >= vLength || v1[k1Offset] == -1) continue;
                    var x1 = v1[k1Offset];
                    var y1 = vOffset + x1 - k1Offset;
                    // 反向坐标转为左上角坐标。
                    x2 = text1Length - x2;
                    if (x1 >= x2)
                        return DiffBisectSplit(text1, text2, x1, y1, depth);
                }
            }
        }

        // 无公共部分,退化为整段删除后插入。
        return [new DiffTuple(DiffOp.Delete, text1), new DiffTuple(DiffOp.Insert, text2)];
    }

    /// <summary>
    ///     Given the location of the 'middle snake', split the diff in two parts and recurse.
    /// </summary>
    private static List<DiffTuple> DiffBisectSplit(string text1, string text2, int x, int y, int depth)
    {
        var text1a = text1[..x];
        var text2a = text2[..y];
        var text1b = text1[x..];
        var text2b = text2[y..];

        var diffs  = DiffMain(text1a, text2a, null, false, false, depth + 1);
        var diffsb = DiffMain(text1b, text2b, null, false, false, depth + 1);

        diffs.AddRange(diffsb);
        return diffs;
    }

    /// <summary>Determine the common prefix of two strings.</summary>
    private static int DiffCommonPrefix(string text1, string text2)
    {
        if (text1.Length == 0 || text2.Length == 0 || text1[0] != text2[0]) return 0;

        // 二分搜索公共前缀。
        var pointerMin   = 0;
        var pointerMax   = Math.Min(text1.Length, text2.Length);
        var pointerMid   = pointerMax;
        var pointerStart = 0;
        while (pointerMin < pointerMid)
        {
            if (string.CompareOrdinal(text1, pointerStart, text2, pointerStart, pointerMid - pointerStart) == 0)
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

        if (IsSurrogatePairStart(text1[pointerMid - 1])) pointerMid--;

        return pointerMid;
    }

    /// <summary>Determine if the suffix of one string is the prefix of another.</summary>
    private static int DiffCommonOverlap(string text1, string text2)
    {
        var text1Length = text1.Length;
        var text2Length = text2.Length;
        if (text1Length == 0 || text2Length == 0) return 0;

        if (text1Length > text2Length)
            text1                                 = text1[(text1Length - text2Length)..];
        else if (text1Length < text2Length) text2 = text2[..text1Length];

        var textLength = Math.Min(text1Length, text2Length);
        if (text1 == text2) return textLength;

        // 逐步扩大匹配长度。
        // 循环中 length <= textLength:匹配位置保证 found + length 不越界;整串相等已提前返回,终点不会再执行 length++。
        var best   = 0;
        var length = 1;
        while (true)
        {
            var pattern = text1[(textLength - length)..];
            var found   = text2.IndexOf(pattern, StringComparison.Ordinal);
            if (found == -1) return best;

            length += found;
            if (found != 0 && text1[^length..] != text2[..length]) continue;
            best = length;
            length++;
        }
    }

    /// <summary>Determine the common suffix of two strings.</summary>
    private static int DiffCommonSuffix(string text1, string text2)
    {
        if (text1.Length == 0 || text2.Length == 0 || text1[^1] != text2[^1]) return 0;

        // 二分搜索公共后缀。
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

        if (IsSurrogatePairEnd(text1[^pointerMid])) pointerMid--;

        return pointerMid;
    }

    /// <summary>
    ///     Do the two texts share a substring which is at least half the length of the
    ///     longer text? This speedup can produce non-minimal diffs.
    ///     Returns (prefix of text1, suffix of text1, prefix of text2, suffix of text2,
    ///     common middle) or null when there was no match.
    /// </summary>
    private static (string Text1A, string Text1B, string Text2A, string Text2B, string MidCommon)? DiffHalfMatch(
        string text1, string text2)
    {
        var longText  = text1.Length > text2.Length ? text1 : text2;
        var shortText = text1.Length > text2.Length ? text2 : text1;
        if (longText.Length < 4 || shortText.Length * 2 < longText.Length) return null; // Pointless.

        var hm1 = DiffHalfMatchI(longText, shortText, (longText.Length + 3) / 4);
        var hm2 = DiffHalfMatchI(longText, shortText, (longText.Length + 1) / 2);

        (string, string, string, string, string)? hm;
        if (hm1 == null && hm2 == null) return null;

        if (hm2 == null)
            hm = hm1;
        else if (hm1 == null)
            hm = hm2;
        else
            hm = hm1.Value.Item5.Length > hm2.Value.Item5.Length ? hm1 : hm2;

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
                return (bestLongTextA, bestLongTextB, bestShortTextA, bestShortTextB, bestCommon);

            return null;
        }
    }

    /// <summary>Reduce the number of edits by eliminating semantically trivial equalities.</summary>
    private static void DiffCleanupSemantic(List<DiffTuple> diffs)
    {
        var changes = false;
        // JS 稀疏数组会读写负索引,用字典保留该行为。
        var     equalities       = new Dictionary<int, int>(); // Stack of indices where equalities are found.
        var     equalitiesLength = 0;                          // Keeping our own length var is faster in JS.
        string? lastEquality     = null;
        // 保持为最近一个 equality 的文本。
        var pointer = 0; // Index of current position.
        var lengthInsertions1 = 0;
        var lengthDeletions1  = 0;
        var lengthInsertions2 = 0;
        var lengthDeletions2  = 0;
        while (pointer < diffs.Count)
        {
            if (diffs[pointer].Op == DiffOp.Equal)
            {
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
                if (diffs[pointer].Op == DiffOp.Insert)
                    lengthInsertions2 += diffs[pointer].Text.Length;
                else
                    lengthDeletions2 += diffs[pointer].Text.Length;

                // 删除不大于两侧编辑长度的 equality。
                if (
                    lastEquality        != null                                          &&
                    lastEquality.Length <= Math.Max(lengthInsertions1, lengthDeletions1) &&
                    lastEquality.Length <= Math.Max(lengthInsertions2, lengthDeletions2)
                )
                {
                    diffs.Insert(equalities[equalitiesLength - 1], new DiffTuple(DiffOp.Delete, lastEquality));
                    diffs[equalities[equalitiesLength - 1] + 1].Op = DiffOp.Insert;
                    equalitiesLength--;
                    // 前一个 equality 也须重新评估。
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

        if (changes) DiffCleanupMerge(diffs, false);

        DiffCleanupSemanticLossless(diffs);

        // 重叠达到任一编辑长度的一半时提取为 equality。
        pointer = 1;
        while (pointer < diffs.Count)
        {
            if (diffs[pointer - 1].Op == DiffOp.Delete && diffs[pointer].Op == DiffOp.Insert)
            {
                var deletion       = diffs[pointer - 1].Text;
                var insertion      = diffs[pointer].Text;
                var overlapLength1 = DiffCommonOverlap(deletion, insertion);
                var overlapLength2 = DiffCommonOverlap(insertion, deletion);
                if (overlapLength1 >= overlapLength2)
                {
                    // 使用浮点除法,保留 JS length / 2 的行为。
                    if (overlapLength1 >= deletion.Length / 2.0 || overlapLength1 >= insertion.Length / 2.0)
                    {
                        diffs.Insert(pointer, new DiffTuple(DiffOp.Equal, insertion[..overlapLength1]));
                        diffs[pointer - 1].Text = deletion[..^overlapLength1];
                        diffs[pointer + 1].Text = insertion[overlapLength1..];
                        pointer++;
                    }
                }
                else
                {
                    // 使用浮点除法,保留 JS length / 2 的行为。
                    if (overlapLength2 >= deletion.Length / 2.0 || overlapLength2 >= insertion.Length / 2.0)
                    {
                        diffs.Insert(pointer, new DiffTuple(DiffOp.Equal, deletion[..overlapLength2]));
                        diffs[pointer - 1].Op   = DiffOp.Insert;
                        diffs[pointer - 1].Text = insertion[..^overlapLength2];
                        diffs[pointer + 1].Op   = DiffOp.Delete;
                        diffs[pointer + 1].Text = deletion[overlapLength2..];
                        pointer++;
                    }
                }

                pointer++;
            }

            pointer++;
        }
    }

    private static bool IsNonAlphaNumeric(char c)
    {
        return c is (< 'a' or > 'z') and (< 'A' or > 'Z') and (< '0' or > '9');
    }

    // 保持 ECMAScript 的空白字符集合。
    private static bool IsJsWhitespace(char c)
    {
        return c is '\t'
                 or '\n'
                 or '\v'
                 or '\f'
                 or '\r'
                 or ' '
                 or '\u00a0'
                 or '\u1680'
                 or >= '\u2000' and <= '\u200a'
                 or '\u2028'
                 or '\u2029'
                 or '\u202f'
                 or '\u205f'
                 or '\u3000'
                 or '\ufeff';
    }

    private static bool IsLineBreak(char c)
    {
        return c is '\r' or '\n';
    }

    // JS 的 $ 仅匹配字符串末尾。
    private static bool MatchesBlankLineEnd(SegmentView s)
    {
        var n = s.Length;
        if (n < 2 || s[n - 1] != '\n') return false;

        if (s[n - 2] == '\n') return true;

        return n >= 3 && s[n - 2] == '\r' && s[n - 3] == '\n';
    }

    private static bool MatchesBlankLineStart(SegmentView s)
    {
        var i = 0;
        if (i < s.Length && s[i] == '\r') i++;

        if (i >= s.Length || s[i] != '\n') return false;

        i++;
        if (i < s.Length && s[i] == '\r') i++;

        return i < s.Length && s[i] == '\n';
    }

    /// <summary>
    ///     Look for single edits surrounded on both sides by equalities which can be
    ///     shifted sideways to align the edit to a word boundary.
    ///     e.g: The c&lt;ins&gt;at c&lt;/ins&gt;ame. -&gt; The &lt;ins&gt;cat &lt;/ins&gt;came.
    /// </summary>
    private static void DiffCleanupSemanticLossless(List<DiffTuple> diffs)
    {
        // 边界评分 0–6,优先在空行、换行和自然分隔处切分。
        static int DiffCleanupSemanticScore(SegmentView one, SegmentView two)
        {
            if (one.Length == 0 || two.Length == 0)
                return 6;

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
                return 5;

            if (lineBreak1 || lineBreak2)
                return 4;

            if (nonAlphaNumeric1 && !whitespace1 && whitespace2)
                return 3;

            if (whitespace1 || whitespace2)
                return 2;

            if (nonAlphaNumeric1 || nonAlphaNumeric2)
                return 1;

            return 0;
        }

        var pointer = 1;
        // 首尾没有完整邻接关系,不检查。
        while (pointer < diffs.Count - 1)
        {
            if (diffs[pointer - 1].Op == DiffOp.Equal && diffs[pointer + 1].Op == DiffOp.Equal)
            {
                var equality1 = diffs[pointer - 1].Text;
                var edit      = diffs[pointer].Text;
                var equality2 = diffs[pointer + 1].Text;

                // 先左移至最远位置,再向右寻找最佳边界。
                var commonOffset = DiffCommonSuffix(equality1, edit);
                if (commonOffset != 0)
                {
                    var commonString = edit[^commonOffset..];
                    equality1 = equality1[..^commonOffset];
                    edit      = commonString + edit[..^commonOffset];
                    equality2 = commonString + equality2;
                }

                // 仅记录最佳偏移,候选通过 SegmentView 评分,最终才构造字符串。
                // edit 长度固定;偏移超过其长度后,从 equality2 取固定长度滑动窗口。
                var bestOffset = 0;
                var bestScore =
                    DiffCleanupSemanticScore(new SegmentView(equality1, 0, equality1.Length, edit, 0, 0, equality2, 0,
                                                             0),
                                             new SegmentView(edit, 0, edit.Length, equality2, 0, 0, string.Empty, 0,
                                                             0)) +
                    DiffCleanupSemanticScore(new SegmentView(edit, 0, edit.Length, equality2, 0, 0, string.Empty, 0, 0),
                                             new SegmentView(equality2, 0, equality2.Length, string.Empty, 0, 0,
                                                             string.Empty, 0, 0));
                if (edit.Length > 0)
                {
                    // 空 edit 不参与移动;移出 edit 后,首字符来自 equality2 的滑动窗口。
                    var i = 0;
                    while (i                                                        < equality2.Length &&
                           (i < edit.Length ? edit[i] : equality2[i - edit.Length]) == equality2[i])
                    {
                        i++;
                        var shiftedEdit = new SegmentView(edit, Math.Min(i, edit.Length), Math.Max(0, edit.Length - i),
                                                          equality2, Math.Max(0, i - edit.Length),
                                                          Math.Min(i, edit.Length), string.Empty, 0, 0);
                        var score =
                            DiffCleanupSemanticScore(new SegmentView(equality1, 0, equality1.Length, edit, 0,
                                                                     Math.Min(i, edit.Length), equality2, 0,
                                                                     Math.Max(0, i - edit.Length)), shiftedEdit) +
                            DiffCleanupSemanticScore(shiftedEdit,
                                                     new SegmentView(equality2, i, equality2.Length - i, string.Empty,
                                                                     0, 0, string.Empty, 0, 0));
                        // 同分优先保留尾部空白。
                        if (score < bestScore) continue;
                        bestScore  = score;
                        bestOffset = i;
                    }
                }

                var editTaken     = Math.Min(bestOffset, edit.Length);
                var bestEquality1 = equality1 + edit[..editTaken] + equality2[..(bestOffset - editTaken)];
                var bestEdit = JsSubstring(edit, bestOffset) +
                               equality2[Math.Max(0, bestOffset - edit.Length)..bestOffset];
                var bestEquality2 = equality2[bestOffset..];

                if (diffs[pointer - 1].Text != bestEquality1)
                {
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
    ///     Reorder and merge like edit sections. Merge equalities. Any edit section can
    ///     move as long as it doesn't cross an equality.
    /// </summary>
    private static void DiffCleanupMerge(List<DiffTuple> diffs, bool fixUnicode)
    {
        while (true)
        {
            diffs.Add(new DiffTuple(DiffOp.Equal, "")); // Add a dummy entry at the end.
            var pointer     = 0;
            var countDelete = 0;
            var countInsert = 0;
            var deleteRun   = new RunText();
            var insertRun   = new RunText();
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
                    case DiffOp.Insert :
                        countInsert++;
                        insertRun.Append(diffs[pointer].Text);
                        pointer++;
                        break;
                    case DiffOp.Delete :
                        countDelete++;
                        deleteRun.Append(diffs[pointer].Text);
                        pointer++;
                        break;
                    default :
                    {
                        var previousEquality = pointer - countInsert - countDelete - 1;
                        if (fixUnicode)
                        {
                            // UTF-16 代理对不能被 tuple 边界拆开;将孤立代理移回编辑段,合并由此产生的空 equality。
                            if (previousEquality >= 0 && EndsWithPairStart(diffs[previousEquality].Text))
                            {
                                var stray = diffs[previousEquality].Text[^1..];
                                diffs[previousEquality].Text = diffs[previousEquality].Text[..^1];

                                deleteRun.Prepend(stray);
                                insertRun.Prepend(stray);
                                if (diffs[previousEquality].Text.Length == 0)
                                {
                                    diffs.RemoveAt(previousEquality);
                                    pointer--;
                                    var k = previousEquality - 1;
                                    if (k >= 0 && k < diffs.Count && diffs[k].Op == DiffOp.Insert)
                                    {
                                        countInsert++;
                                        insertRun.Prepend(diffs[k].Text);
                                        k--;
                                    }

                                    if (k >= 0 && k < diffs.Count && diffs[k].Op == DiffOp.Delete)
                                    {
                                        countDelete++;
                                        deleteRun.Prepend(diffs[k].Text);
                                        k--;
                                    }

                                    previousEquality = k;
                                }
                            }

                            if (diffs[pointer].Text.Length > 0 && StartsWithPairEnd(diffs[pointer].Text))
                            {
                                var stray = diffs[pointer].Text[..1];
                                diffs[pointer].Text = diffs[pointer].Text[1..];
                                deleteRun.Append(stray);
                                insertRun.Append(stray);
                            }
                        }

                        if (pointer < diffs.Count - 1 && diffs[pointer].Text.Length == 0)
                        {
                            // 空 equality 暂不改写,等待下一个边界。
                            diffs.RemoveAt(pointer);
                            continue;
                        }

                        if (deleteRun.Length > 0 || insertRun.Length > 0)
                        {
                            // 在 equality 边界一次拼接累计片段。
                            var textDelete = deleteRun.ToString();
                            var textInsert = insertRun.ToString();

                            // 公共前后缀检查会保护代理对。
                            if (textDelete.Length > 0 && textInsert.Length > 0)
                            {
                                var commonLength = DiffCommonPrefix(textInsert, textDelete);
                                if (commonLength != 0)
                                {
                                    if (previousEquality >= 0)
                                    {
                                        diffs[previousEquality].Text += textInsert[..commonLength];
                                    }
                                    else
                                    {
                                        diffs.Insert(0, new DiffTuple(DiffOp.Equal, textInsert[..commonLength]));
                                        pointer++;
                                    }

                                    textInsert = textInsert[commonLength..];
                                    textDelete = textDelete[commonLength..];
                                }

                                commonLength = DiffCommonSuffix(textInsert, textDelete);
                                if (commonLength != 0)
                                {
                                    diffs[pointer].Text = textInsert[^commonLength..] + diffs[pointer].Text;
                                    textInsert          = textInsert[..^commonLength];
                                    textDelete          = textDelete[..^commonLength];
                                }
                            }

                            var n = countInsert + countDelete;
                            switch (textDelete.Length)
                            {
                                case 0 when textInsert.Length == 0 :
                                    diffs.RemoveRange(pointer - n, n);
                                    pointer -= n;
                                    break;
                                case 0 :
                                    diffs.RemoveRange(pointer - n, n);
                                    diffs.Insert(pointer      - n, new DiffTuple(DiffOp.Insert, textInsert));
                                    pointer = pointer - n + 1;
                                    break;
                                default :
                                {
                                    if (textInsert.Length == 0)
                                    {
                                        diffs.RemoveRange(pointer - n, n);
                                        diffs.Insert(pointer      - n, new DiffTuple(DiffOp.Delete, textDelete));
                                        pointer = pointer - n + 1;
                                    }
                                    else
                                    {
                                        diffs.RemoveRange(pointer - n, n);
                                        diffs.Insert(pointer      - n, new DiffTuple(DiffOp.Delete, textDelete));
                                        diffs.Insert(pointer - n  + 1, new DiffTuple(DiffOp.Insert, textInsert));
                                        pointer = pointer - n + 2;
                                    }

                                    break;
                                }
                            }
                        }

                        if (pointer != 0 && diffs[pointer - 1].Op == DiffOp.Equal)
                        {
                            diffs[pointer - 1].Text += diffs[pointer].Text;
                            diffs.RemoveAt(pointer);
                        }
                        else
                        {
                            pointer++;
                        }

                        countInsert = 0;
                        countDelete = 0;
                        deleteRun.Clear();
                        insertRun.Clear();
                        break;
                    }
                }
            }

            if (diffs.Count > 0 && diffs[^1].Text.Length == 0)
                diffs.RemoveAt(diffs.Count - 1); // Remove the dummy entry at the end.

            // 再次移动两侧为 equality 的单编辑段,以消除可合并的 equality。
            var changes = false;
            pointer = 1;
            // 首尾没有完整邻接关系,不检查。
            while (pointer < diffs.Count - 1)
            {
                if (diffs[pointer - 1].Op == DiffOp.Equal && diffs[pointer + 1].Op == DiffOp.Equal)
                {
                    if (diffs[pointer].Text.EndsWith(diffs[pointer - 1].Text, StringComparison.Ordinal))
                    {
                        diffs[pointer].Text = diffs[pointer - 1].Text +
                                              diffs[pointer].Text[..^diffs[pointer - 1].Text.Length];
                        diffs[pointer + 1].Text = diffs[pointer - 1].Text + diffs[pointer + 1].Text;
                        diffs.RemoveAt(pointer - 1);
                        changes = true;
                    }
                    else if (diffs[pointer].Text.StartsWith(diffs[pointer + 1].Text, StringComparison.Ordinal))
                    {
                        diffs[pointer - 1].Text += diffs[pointer + 1].Text;
                        diffs[pointer].Text = diffs[pointer].Text[diffs[pointer + 1].Text.Length..] +
                                              diffs[pointer + 1].Text;
                        diffs.RemoveAt(pointer + 1);
                        changes = true;
                    }
                }

                pointer++;
            }

            // 发生移动后重新合并并扫描。
            if (changes) continue;

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
        if (EndsWithPairStart(before) || StartsWithPairEnd(after)) return null;

        return RemoveEmptyTuples([
            new DiffTuple(DiffOp.Equal, before),
            new DiffTuple(DiffOp.Delete, oldMiddle),
            new DiffTuple(DiffOp.Insert, newMiddle),
            new DiffTuple(DiffOp.Equal, after)
        ]);
    }

    /// <summary>JS substring(start) — clamps out-of-range start instead of throwing.</summary>
    private static string JsSubstring(string s, int start)
    {
        if (start <= 0) return s;

        return start >= s.Length ? "" : s[start..];
    }

    /// <summary>
    ///     JS slice(s.length - n) — the suffix slice of findCursorEditDiff's editAfter branch.
    ///     A negative start (n &gt; s.Length) clamps to 0 and returns the whole string; a start
    ///     past the end (n &lt; 0) returns the empty string. Unlike the C# range operators,
    ///     slice never throws on out-of-range bounds.
    /// </summary>
    private static string JsSliceSuffix(string s, int n)
    {
        if (n < 0) return "";

        return n >= s.Length ? s : s[^n..];
    }

    /// <summary>
    ///     JS slice(0, s.length - n) — the middle slice of findCursorEditDiff's editAfter branch.
    ///     A negative end (n &gt; s.Length) clamps to 0 and returns the empty string; an end
    ///     past the length (n &lt; 0) returns the whole string.
    /// </summary>
    private static string JsSliceWithoutSuffix(string s, int n)
    {
        if (n < 0) return s;

        return n >= s.Length ? "" : s[..^n];
    }

    private static List<DiffTuple>? FindCursorEditDiff(string oldText, string newText, CursorInfo cursorPos)
    {
        // 调用前已排除文本完全相等。
        var oldRange = cursorPos.OldRange;
        var newRange = cursorPos.NewRange;

        var oldLength = oldText.Length;
        var newLength = newText.Length;

        if (oldRange == null) return null;

        switch (oldRange.Length)
        {
            case 0 when newRange == null || newRange.Length == 0 :
            {
                var oldCursor      = oldRange.Index;
                var oldBefore      = oldCursor >= oldText.Length ? oldText : oldText[..Math.Max(0, oldCursor)];
                var oldAfter       = JsSubstring(oldText, oldCursor);
                var maybeNewCursor = newRange?.Index;

                {
                    var newCursor = oldCursor + newLength - oldLength;
                    if (maybeNewCursor != null && maybeNewCursor != newCursor) goto editAfter;

                    if (newCursor < 0 || newCursor > newLength) goto editAfter;

                    var newBefore = newText[..newCursor];
                    var newAfter  = newText[newCursor..];
                    if (newAfter != oldAfter) goto editAfter;

                    var prefixLength = Math.Min(oldCursor, newCursor);
                    var oldPrefix    = oldBefore[..prefixLength];
                    var newPrefix    = newBefore[..prefixLength];
                    if (oldPrefix != newPrefix) goto editAfter;

                    var oldMiddle = oldBefore[prefixLength..];
                    var newMiddle = newBefore[prefixLength..];
                    return MakeEditSplice(oldPrefix, oldMiddle, newMiddle, oldAfter);
                }

                editAfter:
                {
                    if (maybeNewCursor != null && maybeNewCursor != oldCursor) return null;

                    var cursor    = oldCursor;
                    var newBefore = cursor >= newText.Length ? newText : newText[..Math.Max(0, cursor)];
                    var newAfter  = JsSubstring(newText, cursor);
                    if (newBefore != oldBefore) return null;

                    // 前置相等检查保证 suffixLength 非负;负 cursor 仍可通过。
                    // 负 cursor 会使后缀长度越界,须用 JsSlice 钳制,不能改为 C# range。
                    var suffixLength = Math.Min(oldLength - cursor, newLength - cursor);
                    var oldSuffix    = JsSliceSuffix(oldAfter, suffixLength);
                    var newSuffix    = JsSliceSuffix(newAfter, suffixLength);
                    if (oldSuffix != newSuffix) return null;

                    var oldMiddle = JsSliceWithoutSuffix(oldAfter, suffixLength);
                    var newMiddle = JsSliceWithoutSuffix(newAfter, suffixLength);
                    return MakeEditSplice(oldBefore, oldMiddle, newMiddle, oldSuffix);
                }
            }
            case > 0 when newRange is { Length: 0 } :
            {
                // 替换选区时按 JS slice 语义钳制越界参数。
                var oldPrefix    = oldRange.Index >= oldLength ? oldText : oldText[..Math.Max(0, oldRange.Index)];
                var oldSuffix    = JsSubstring(oldText, oldRange.Index + oldRange.Length);
                var prefixLength = oldPrefix.Length;
                var suffixLength = oldSuffix.Length;
                if (newLength < prefixLength + suffixLength) return null;

                var newPrefix = newText[..prefixLength];
                var newSuffix = newText[(newLength - suffixLength)..];
                if (oldPrefix != newPrefix || oldSuffix != newSuffix) return null;

                var oldMiddle = oldText.Substring(prefixLength, oldLength - suffixLength - prefixLength);
                var newMiddle = newText.Substring(prefixLength, newLength - suffixLength - prefixLength);
                return MakeEditSplice(oldPrefix, oldMiddle, newMiddle, oldSuffix);
            }
            default :
                return null;
        }
    }

    /// <summary>
    ///     A read-only view over up to three contiguous string segments. diff_cleanupSemanticLossless
    ///     scores virtual concatenations (e.g. edit[i..]+equality2[..i]) that JS materializes per
    ///     shift step; the view defers that to the single slice at the winning offset. Indexing and
    ///     length follow the concatenation exactly; an empty segment (length 0) is never dereferenced.
    /// </summary>
    private readonly struct SegmentView(
        string a,
        int    aStart,
        int    aLength,
        string b,
        int    bStart,
        int    bLength,
        string c,
        int    cStart,
        int    cLength
    )
    {
        public int Length => aLength + bLength + cLength;

        public char this[int index] => index < aLength ? a[aStart + index] :
            index < aLength + bLength ? b[bStart + index - aLength] : c[cStart + index - aLength - bLength];
    }

    /// <summary>
    ///     The delete/insert texts accumulated between two equalities by DiffCleanupMerge.
    ///     JS builds them with `+=` per tuple, copying the growing string each time (quadratic
    ///     in the run length); segments defer the join to the one materialization per equality.
    /// </summary>
    private sealed class RunText
    {
        private readonly List<string> _parts = [];

        public int Length { get; private set; }

        public void Append(string text)
        {
            if (text.Length == 0) return;

            _parts.Add(text);
            Length += text.Length;
        }

        public void Prepend(string text)
        {
            if (text.Length == 0) return;

            _parts.Insert(0, text);
            Length += text.Length;
        }

        public void Clear()
        {
            _parts.Clear();
            Length = 0;
        }

        public override string ToString()
        {
            return _parts.Count switch
            {
                0 => string.Empty,
                1 => _parts[0],
                _ => string.Concat(_parts)
            };
        }
    }
}
