using Banned.CodeDiff.Models;
using Banned.CodeDiff.Services;

namespace Banned.CodeDiff.Utils;

/// <summary>
///     packages/core/src/diff-file-utils.ts 的移植:构建渲染层行项列表的无状态纯辅助方法。<br />
///     Port of packages/core/src/diff-file-utils.ts: stateless pure helpers that build the
///     render-layer line item lists.
/// </summary>
public static class DiffFileUtils
{
    /// <summary>
    ///     生成分栏视图的行项列表:按行 index 顺序,每行依次产生 hunk / content / widget / extend
    ///     四项,lineNumber 为 index + 1。<br />
    ///     Builds the split-view line item list: in line index order, each line contributes one
    ///     hunk / content / widget / extend item, with lineNumber = index + 1.
    /// </summary>
    /// <param name="diffFile">源 diff 文件。The source diff file.</param>
    /// <returns>每行 4 项、按视图行序排列的列表。The list with 4 items per line, in view-line order.</returns>
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

    /// <summary>
    ///     生成分栏视图的 content 行项列表,任一侧被隐藏(折叠)的行会被跳过。<br />
    ///     Builds the split-view content line item list; lines hidden (collapsed) on either side are skipped.
    /// </summary>
    /// <param name="diffFile">源 diff 文件。The source diff file.</param>
    /// <returns>可见 content 行项,每项携带左右两侧的行数据。The visible content items, each carrying both sides' line data.</returns>
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

    /// <summary>
    ///     生成统一视图的行项列表:按行 index 顺序,每行依次产生 hunk / content / widget / extend
    ///     四项,lineNumber 为 index + 1。<br />
    ///     Builds the unified-view line item list: in line index order, each line contributes one
    ///     hunk / content / widget / extend item, with lineNumber = index + 1.
    /// </summary>
    /// <param name="diffFile">源 diff 文件。The source diff file.</param>
    /// <returns>每行 4 项、按视图行序排列的列表。The list with 4 items per line, in view-line order.</returns>
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

    /// <summary>
    ///     生成统一视图的 content 行项列表,被隐藏(折叠)的行会被跳过。<br />
    ///     Builds the unified-view content line item list; hidden (collapsed) lines are skipped.
    /// </summary>
    /// <param name="diffFile">源 diff 文件。The source diff file.</param>
    /// <returns>可见 content 行项,每项携带统一视图行数据。The visible content items, each carrying the unified line data.</returns>
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

    /// <summary>
    ///     检查指定行号在分栏与统一视图下是否被隐藏(行不存在或 IsHidden)。<br />
    ///     Checks whether the given line number is hidden in the split and unified views (missing line or IsHidden).
    /// </summary>
    /// <param name="diffFile">源 diff 文件。The source diff file.</param>
    /// <param name="lineNumber">文件行号(1 起始)。The file line number (1-based).</param>
    /// <param name="side">旧侧/新侧。The old/new side.</param>
    /// <returns>(分栏视图是否隐藏, 统一视图是否隐藏)。(Whether hidden in the split view, whether hidden in the unified view.)</returns>
    public static (bool Split, bool Unified) CheckCurrentLineIsHidden(DiffFile diffFile, int lineNumber, SplitSide side)
    {
        var splitLine = diffFile.GetSplitLineByLineNumber(lineNumber, side);

        var unifiedLine = diffFile.GetUnifiedLineByLineNumber(lineNumber, side);

        return (splitLine == null || splitLine.IsHidden, unifiedLine == null || unifiedLine.IsHidden);
    }
}
