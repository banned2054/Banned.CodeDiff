using Banned.CodeDiff.Models;
using Banned.CodeDiff.Services;

namespace Banned.CodeDiff.Utils;

/// <summary>Port of packages/core/src/diff-file-utils.ts.</summary>
public static class DiffFileUtils
{
    public static List<DiffSplitLineItem> GetSplitLines(DiffFile diffFile)
    {
        var splitLineLength = diffFile.SplitLineLength;

        var splitLines = new List<DiffSplitLineItem>();

        foreach (var index in DiffTool.NumIterator(splitLineLength, i => i))
        {
            splitLines.Add(new DiffSplitLineItem(DiffFileLineType.Hunk, index, index + 1));

            splitLines.Add(new DiffSplitLineItem(DiffFileLineType.Content, index, index + 1));

            splitLines.Add(new DiffSplitLineItem(DiffFileLineType.Widget, index, index + 1));

            splitLines.Add(new DiffSplitLineItem(DiffFileLineType.Extend, index, index + 1));
        }

        return splitLines;
    }

    public static List<DiffSplitContentLineItem> GetSplitContentLines(DiffFile diffFile)
    {
        var splitLineLength = diffFile.SplitLineLength;

        return (from index in DiffTool.NumIterator(splitLineLength, i => i)
                let splitLeftLine = diffFile.GetSplitLeftLine(index)
                let splitRightLine = diffFile.GetSplitRightLine(index)
                where splitLeftLine?.IsHidden != true && splitRightLine?.IsHidden != true
                select new DiffSplitContentLineItem(DiffFileLineType.Content, index, index + 1, splitLeftLine,
                                                    splitRightLine)).ToList();
    }

    public static List<DiffUnifiedLineItem> GetUnifiedLines(DiffFile diffFile)
    {
        var unifiedLineLength = diffFile.UnifiedLineLength;

        var unifiedLines = new List<DiffUnifiedLineItem>();

        foreach (var index in DiffTool.NumIterator(unifiedLineLength, i => i))
        {
            unifiedLines.Add(new DiffUnifiedLineItem(DiffFileLineType.Hunk, index, index + 1));

            unifiedLines.Add(new DiffUnifiedLineItem(DiffFileLineType.Content, index, index + 1));

            unifiedLines.Add(new DiffUnifiedLineItem(DiffFileLineType.Widget, index, index + 1));

            unifiedLines.Add(new DiffUnifiedLineItem(DiffFileLineType.Extend, index, index + 1));
        }

        return unifiedLines;
    }

    public static List<DiffUnifiedContentLineItem> GetUnifiedContentLine(DiffFile diffFile)
    {
        var unifiedLineLength = diffFile.UnifiedLineLength;

        return (from index in DiffTool.NumIterator(unifiedLineLength, i => i)
                let unifiedLine = diffFile.GetUnifiedLine(index)
                where unifiedLine?.IsHidden != true
                select new DiffUnifiedContentLineItem(DiffFileLineType.Content, index, index + 1, unifiedLine))
           .ToList();
    }

    public static (bool Split, bool Unified) CheckCurrentLineIsHidden(DiffFile diffFile, int lineNumber, SplitSide side)
    {
        var splitLine = diffFile.GetSplitLineByLineNumber(lineNumber, side);

        var unifiedLine = diffFile.GetUnifiedLineByLineNumber(lineNumber, side);

        return (splitLine == null || splitLine.IsHidden, unifiedLine == null || unifiedLine.IsHidden);
    }
}
