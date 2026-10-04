using Banned.CodeDiff.Models;
using Banned.CodeDiff.Services;

namespace Banned.CodeDiff.Utils;

/// <summary>Port of packages/core/src/diff-file-utils.ts.</summary>
public static class DiffFileUtils
{
    public static List<DiffSplitLineItem> GetSplitLines(DiffFile diffFile)
    {
        var splitLineLength = diffFile.SplitLineLength;

        var splitLines = new List<DiffSplitLineItem>(splitLineLength * 4);

        for (var index = 0; index < splitLineLength; index++)
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

        var splitContentLines = new List<DiffSplitContentLineItem>(splitLineLength);

        for (var index = 0; index < splitLineLength; index++)
        {
            var splitLeftLine  = diffFile.GetSplitLeftLine(index);
            var splitRightLine = diffFile.GetSplitRightLine(index);

            if (splitLeftLine?.IsHidden == true || splitRightLine?.IsHidden == true) continue;

            splitContentLines.Add(new DiffSplitContentLineItem(DiffFileLineType.Content, index, index + 1,
                                                               splitLeftLine, splitRightLine));
        }

        return splitContentLines;
    }

    public static List<DiffUnifiedLineItem> GetUnifiedLines(DiffFile diffFile)
    {
        var unifiedLineLength = diffFile.UnifiedLineLength;

        var unifiedLines = new List<DiffUnifiedLineItem>(unifiedLineLength * 4);

        for (var index = 0; index < unifiedLineLength; index++)
        {
            unifiedLines.Add(new DiffUnifiedLineItem(DiffFileLineType.Hunk, index, index    + 1));
            unifiedLines.Add(new DiffUnifiedLineItem(DiffFileLineType.Content, index, index + 1));
            unifiedLines.Add(new DiffUnifiedLineItem(DiffFileLineType.Widget, index, index  + 1));
            unifiedLines.Add(new DiffUnifiedLineItem(DiffFileLineType.Extend, index, index  + 1));
        }

        return unifiedLines;
    }

    public static List<DiffUnifiedContentLineItem> GetUnifiedContentLine(DiffFile diffFile)
    {
        var unifiedLineLength = diffFile.UnifiedLineLength;

        var unifiedContentLines = new List<DiffUnifiedContentLineItem>(unifiedLineLength);

        for (var index = 0; index < unifiedLineLength; index++)
        {
            var unifiedLine = diffFile.GetUnifiedLine(index);

            if (unifiedLine?.IsHidden == true) continue;

            unifiedContentLines.Add(new DiffUnifiedContentLineItem(DiffFileLineType.Content, index, index + 1,
                                                                   unifiedLine));
        }

        return unifiedContentLines;
    }

    public static (bool Split, bool Unified) CheckCurrentLineIsHidden(DiffFile diffFile, int lineNumber, SplitSide side)
    {
        var splitLine = diffFile.GetSplitLineByLineNumber(lineNumber, side);

        var unifiedLine = diffFile.GetUnifiedLineByLineNumber(lineNumber, side);

        return (splitLine == null || splitLine.IsHidden, unifiedLine == null || unifiedLine.IsHidden);
    }
}
