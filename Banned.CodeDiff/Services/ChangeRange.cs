using Banned.CodeDiff.Models;
using Banned.CodeDiff.Utils;

namespace Banned.CodeDiff.Services;

/// <summary>Port of packages/core/src/parse/change-range.ts.</summary>
public static class ChangeRange
{
    private static int _maxLengthToIgnoreLineDiff = 1000;

    /// <summary>Change the maximum length of a line to ignore line diff.</summary>
    public static void ChangeMaxLengthToIgnoreLineDiff(int length)
    {
        _maxLengthToIgnoreLineDiff = length;
    }

    public static void ResetMaxLengthToIgnoreLineDiff()
    {
        _maxLengthToIgnoreLineDiff = 1000;
    }

    public static int GetMaxLengthToIgnoreLineDiff()
    {
        return _maxLengthToIgnoreLineDiff;
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
            if (stringA[startA + length] != stringB[startB + length])
            {
                break;
            }

            length += stride;
        }

        return Math.Abs(length);
    }

    private static bool IsInValidString(string s)
    {
        return s.Trim().Length == 0 || s.Length >= _maxLengthToIgnoreLineDiff;
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

        if (aSymbol == bSymbol && !hasNewLineChanged)
        {
            return (null, stringA, null, stringB);
        }

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

    // TODO maybe could use the original content line.  fixed
    /// <summary>Get the changed ranges in the strings, relative to each other.</summary>
    public static (LineRange AddRange, LineRange DelRange) RelativeChanges(DiffLine addition, DiffLine deletion)
    {
        var stringA = addition.Text;

        var stringB = deletion.Text;

        var (addSymbol, addString, delSymbol, delString) = CheckNewLineSymbolChange(addition, deletion);

        if (addString == delString && addSymbol.HasValue && delSymbol.HasValue)
        {
            return (
                new LineRange
                {
                    Range = new TextRange
                    {
                        Location = addString.Length,
                        Length   = stringA.Length - addString.Length,
                    },
                    HasLineChange = true,
                    NewLineSymbol = addSymbol,
                },
                new LineRange
                {
                    Range = new TextRange
                    {
                        Location = delString.Length,
                        Length   = stringB.Length - delString.Length,
                    },
                    HasLineChange = true,
                    NewLineSymbol = delSymbol,
                }
            );
        }

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
        addRange = new TextRange(Location : addRange.Location + prefixLength, Length : addRange.Length - prefixLength);

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
                  > 0,
            },
            new LineRange
            {
                Range = delRange,
                HasLineChange =
                    (delString[..delRange.Location] + delString[(delRange.Location + delRange.Length)..]).Trim()
                   .Length
                  > 0,
            }
        );
    }

    public static (DiffRange AddRange, DiffRange DelRange) DiffChanges(DiffLine addition, DiffLine deletion)
    {
        var (addSymbol, addString, delSymbol, delString) = CheckNewLineSymbolChange(addition, deletion);

        if (IsInValidString(addString) || IsInValidString(delString))
        {
            return (
                new DiffRange { Range = [], HasLineChange = addSymbol.HasValue, NewLineSymbol = addSymbol },
                new DiffRange { Range = [], HasLineChange = delSymbol.HasValue, NewLineSymbol = delSymbol }
            );
        }

        var diffRange = FastDiff.Diff(delString, addString, 0, true);

        var aStart = 0;
        var bStart = 0;

        var aRange = new List<DiffItem>();
        foreach (var item in diffRange.Where(item => item.Op != FastDiff.Delete))
        {
            aRange.Add(new DiffItem(item.Op, item.Text, aStart, aStart + item.Text.Length - 1, item.Text.Length));
            aStart += item.Text.Length;
        }

        var bRange = new List<DiffItem>();
        foreach (var item in diffRange.Where(item => item.Op != FastDiff.Insert))
        {
            bRange.Add(new DiffItem(item.Op, item.Text, bStart, bStart + item.Text.Length - 1, item.Text.Length));
            bStart += item.Text.Length;
        }

        var hasLineChange = aRange.Any(i => i.Type == FastDiff.Equal && i.Str.Trim().Length > 0);

        return (new DiffRange { Range = aRange, HasLineChange = hasLineChange, NewLineSymbol = addSymbol },
                new DiffRange { Range = bRange, HasLineChange = hasLineChange, NewLineSymbol = delSymbol });
    }
}