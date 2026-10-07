using Banned.CodeDiff.Models;
using Banned.CodeDiff.Utils;

namespace Banned.CodeDiff.Services;

/// <summary>packages/core/src/parse/change-range.ts 的移植。<br />Port of packages/core/src/parse/change-range.ts.</summary>
public static class ChangeRange
{
    /// <summary>忽略行内 diff 的最大行长上限，超过该长度的行不做行内比较（对应 JS 的 getMaxLengthToIgnoreLineDiff）。<br />JS: getMaxLengthToIgnoreLineDiff.</summary>
    public static int MaxLengthToIgnoreLineDiff { get; private set; } = 1000;

    /// <summary>更改忽略行内 diff 的最大行长。<br />Change the maximum length of a line to ignore line diff.</summary>
    /// <param name="length">新的最大行长。New maximum line length.</param>
    public static void ChangeMaxLengthToIgnoreLineDiff(int length)
    {
        MaxLengthToIgnoreLineDiff = length;
    }

    /// <summary>
    ///     将忽略行内 diff 的最大行长重置为默认值 1000。<br />Resets the maximum line length for ignoring line diff back to the default
    ///     value of 1000.
    /// </summary>
    public static void ResetMaxLengthToIgnoreLineDiff()
    {
        MaxLengthToIgnoreLineDiff = 1000;
    }

    /// <summary>Get the maximum position in the range.</summary>
    private static int RangeMax(TextRange range)
    {
        return range.Location + range.Length;
    }

    /// <summary>Get the length of the common substring between the two strings.</summary>
    private static int CommonLength(string stringA, TextRange rangeA, string stringB, TextRange rangeB, bool reverse)
    {
        var max    = Math.Min(rangeA.Length, rangeB.Length);
        var startA = reverse ? RangeMax(rangeA) - 1 : rangeA.Location;
        var startB = reverse ? RangeMax(rangeB) - 1 : rangeB.Location;
        var stride = reverse ? -1 : 1;

        var length = 0;
        while (Math.Abs(length) < max)
        {
            if (stringA[startA + length] != stringB[startB + length]) break;

            length += stride;
        }

        return Math.Abs(length);
    }

    private static bool IsInValidString(string s)
    {
        return s.Trim().Length == 0 || s.Length >= MaxLengthToIgnoreLineDiff;
    }

    private static (NewLineSymbol? AddSymbol, string AddString, NewLineSymbol? DelSymbol, string DelString)
        CheckNewLineSymbolChange(DiffLine addition, DiffLine deletion)
    {
        var stringA = addition.Text;

        var stringB = deletion.Text;

        var aEndStr = JsSliceFromEnd(stringA, 2);

        var bEndStr = JsSliceFromEnd(stringB, 2);

        var aSymbol =
            aEndStr == "\r\n"
                ? NewLineSymbol.CRLF
                : aEndStr.EndsWith("\r")
                    ? NewLineSymbol.CR
                    : aEndStr.EndsWith("\n")
                        ? NewLineSymbol.LF
                        : NewLineSymbol.NULL;

        var bSymbol =
            bEndStr == "\r\n"
                ? NewLineSymbol.CRLF
                : bEndStr.EndsWith("\r")
                    ? NewLineSymbol.CR
                    : bEndStr.EndsWith("\n")
                        ? NewLineSymbol.LF
                        : NewLineSymbol.NULL;

        var hasNewLineChanged = addition.NoTrailingNewLine != deletion.NoTrailingNewLine;

        if (aSymbol == bSymbol && !hasNewLineChanged) return (null, stringA, null, stringB);

        return (
            hasNewLineChanged
                ? addition.NoTrailingNewLine
                    ? NewLineSymbol.NEWLINE
                    : NewLineSymbol.NORMAL
                : aSymbol,
            aSymbol switch
            {
                NewLineSymbol.CRLF => stringA.Length >= 2 ? stringA[..^2] : "",
                NewLineSymbol.CR   => stringA.Length >= 1 ? stringA[..^1] : "",
                NewLineSymbol.LF   => stringA.Length >= 1 ? stringA[..^1] : "",
                _                  => stringA
            },
            hasNewLineChanged
                ? deletion.NoTrailingNewLine
                    ? NewLineSymbol.NEWLINE
                    : NewLineSymbol.NORMAL
                : bSymbol,
            bSymbol switch
            {
                NewLineSymbol.CRLF => stringB.Length >= 2 ? stringB[..^2] : "",
                NewLineSymbol.CR   => stringB.Length >= 1 ? stringB[..^1] : "",
                NewLineSymbol.LF   => stringB.Length >= 1 ? stringB[..^1] : "",
                _                  => stringB
            }
        );
    }

    /// <summary>JS: str.slice(-n) semantics (whole string when shorter than n).</summary>
    private static string JsSliceFromEnd(string s, int n)
    {
        return n <= 0 ? "" : s.Length <= n ? s : s[^n..];
    }

    /// <summary>获取两个字符串相互之间的变更范围。<br />Get the changed ranges in the strings, relative to each other.</summary>
    /// <param name="addition">新增行。The added line.</param>
    /// <param name="deletion">删除行。The deleted line.</param>
    /// <returns>新增侧与删除侧的变更范围。The change ranges for the addition side and the deletion side.</returns>
    public static (LineRange AddRange, LineRange DelRange) RelativeChanges(DiffLine addition, DiffLine deletion)
    {
        var stringA = addition.Text;

        var stringB = deletion.Text;

        var (addSymbol, addString, delSymbol, delString) = CheckNewLineSymbolChange(addition, deletion);

        if (addString == delString && addSymbol.HasValue && delSymbol.HasValue)
            return (
                new LineRange
                {
                    Range = new TextRange
                    {
                        Location = addString.Length,
                        Length   = stringA.Length - addString.Length
                    },
                    HasLineChange = true,
                    NewLineSymbol = addSymbol
                },
                new LineRange
                {
                    Range = new TextRange
                    {
                        Location = delString.Length,
                        Length   = stringB.Length - delString.Length
                    },
                    HasLineChange = true,
                    NewLineSymbol = delSymbol
                }
            );

        var delRange = new TextRange(0, delString.Length);
        var addRange = new TextRange(0, addString.Length);

        if (IsInValidString(stringA) || IsInValidString(stringB))
        {
            addRange = addRange with { Length = 0 };
            delRange = delRange with { Length = 0 };

            return (
                new LineRange { Range = addRange },
                new LineRange { Range = delRange }
            );
        }

        var prefixLength = CommonLength(delString, delRange, addString, addRange, false);

        delRange = delRange with
        {
            Location = delRange.Location + prefixLength, Length = delRange.Length - prefixLength
        };
        addRange = new TextRange(addRange.Location + prefixLength, addRange.Length - prefixLength);

        var suffixLength = CommonLength(delString, delRange, addString, addRange, true);

        delRange = delRange with { Length = delRange.Length - suffixLength };

        addRange = addRange with { Length = addRange.Length - suffixLength };

        return (
            new LineRange
            {
                Range = addRange,
                HasLineChange =
                    (addString[..addRange.Location] + addString[(addRange.Location + addRange.Length)..]).Trim()
                   .Length
                  > 0
            },
            new LineRange
            {
                Range = delRange,
                HasLineChange =
                    (delString[..delRange.Location] + delString[(delRange.Location + delRange.Length)..]).Trim()
                   .Length
                  > 0
            }
        );
    }

    /// <summary>
    ///     基于 fast-diff 计算一对（新增行，删除行）的行内 diff 文本段，返回两侧各自的范围。<br />Computes the in-line diff segments for an (addition,
    ///     deletion) pair with fast-diff and returns the ranges for both sides.
    /// </summary>
    /// <param name="addition">新增行。The added line.</param>
    /// <param name="deletion">删除行。The deleted line.</param>
    /// <returns>新增侧与删除侧的 diff 文本段范围。The diff segment ranges for the addition side and the deletion side.</returns>
    public static (DiffRange AddRange, DiffRange DelRange) DiffChanges(DiffLine addition, DiffLine deletion)
    {
        var (addSymbol, addString, delSymbol, delString) = CheckNewLineSymbolChange(addition, deletion);

        if (IsInValidString(addString) || IsInValidString(delString))
            return (
                new DiffRange { Range = [], HasLineChange = addSymbol.HasValue, NewLineSymbol = addSymbol },
                new DiffRange { Range = [], HasLineChange = delSymbol.HasValue, NewLineSymbol = delSymbol }
            );

        var diffRange = FastDiff.Diff(delString, addString, 0, true);

        var aStart = 0;
        var bStart = 0;

        var aRange = new List<DiffItem>();
        var bRange = new List<DiffItem>();

        var hasLineChange = false;

        // a/b 偏移与 HasLineChange 各自独立累计。
        foreach (var item in diffRange)
        {
            if (item.Op != DiffOp.Delete)
            {
                aRange.Add(new DiffItem(item.Op, item.Text, aStart, aStart + item.Text.Length - 1, item.Text.Length));
                aStart += item.Text.Length;

                if (!hasLineChange && item.Op == DiffOp.Equal && item.Text.Trim().Length > 0) hasLineChange = true;
            }

            if (item.Op == DiffOp.Insert) continue;
            bRange.Add(new DiffItem(item.Op, item.Text, bStart, bStart + item.Text.Length - 1, item.Text.Length));
            bStart += item.Text.Length;
        }

        return (new DiffRange { Range = aRange, HasLineChange = hasLineChange, NewLineSymbol = addSymbol },
                new DiffRange { Range = bRange, HasLineChange = hasLineChange, NewLineSymbol = delSymbol });
    }
}
