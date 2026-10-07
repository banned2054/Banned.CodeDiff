namespace Banned.CodeDiff.Models;

/// <summary>表示 diff 中一行的类别。<br />Indicate what a line in the diff represents.</summary>
public enum DiffLineType
{
    /// <summary>上下文行(未变更)。<br />Context line (unchanged).</summary>
    Context = 0,

    /// <summary>新增行。<br />Added line.</summary>
    Add = 1,

    /// <summary>删除行。<br />Deleted line.</summary>
    Delete = 2,

    /// <summary>hunk 头行。<br />Hunk header line.</summary>
    Hunk = 3
}

/// <summary>raw-diff.ts IRawDiff 的移植。<br />Port of raw-diff.ts IRawDiff.</summary>
public sealed class RawDiff
{
    /// <summary>
    ///     diff 头部的纯文本内容:从 diff 开头到第一个 hunk 头之前的全部文本,不含末尾换行符。<br />
    ///     The plain text contents of the diff header. This contains everything from the
    ///     start of the diff up until the first hunk header starts. Note that this does
    ///     not include a trailing newline.
    /// </summary>
    public string Header { get; init; } = "";

    /// <summary>
    ///     diff 正文的纯文本内容:从 diff 头之后到最后一个字符,不含末尾换行符,
    ///     也不含 "no newline at end of file" 标记(见 <see cref="DiffLine.NoTrailingNewLine" />)。<br />
    ///     The plain text contents of the diff. This contains everything after the diff
    ///     header until the last character in the diff. Does not include a trailing newline
    ///     nor 'no newline at end of file' comments (see <see cref="DiffLine.NoTrailingNewLine" />).
    /// </summary>
    public string Contents { get; init; } = "";

    /// <summary>
    ///     diff 中的每个 hunk,含起止位置、行与行状态信息。<br />Each hunk in the diff with information about start, and end positions,
    ///     lines and line statuses.
    /// </summary>
    public IReadOnlyList<DiffHunk> Hunks { get; init; } = [];

    /// <summary>
    ///     unified diff 是否表明因某一版本为二进制而无法比较内容。<br />
    ///     Whether or not the unified diff indicates that the contents could not be diffed due to one of the versions
    ///     being binary.
    /// </summary>
    public bool IsBinary { get; init; }

    /// <summary>diff 中的最大行号。<br />The largest line number in the diff.</summary>
    public int MaxLineNumber { get; init; }

    /// <summary>diff 是否包含不可见的双向(bidi)字符。<br />Whether or not the diff has invisible bidi characters.</summary>
    public bool HasHiddenBidiChars { get; init; }
}

/// <summary>raw-diff.ts DiffHunkExpansionType 的移植。<br />Port of raw-diff.ts DiffHunkExpansionType.</summary>
public enum DiffHunkExpansionType
{
    /// <summary>hunk 头不可展开。<br />The hunk header cannot be expanded at all.</summary>
    None = 0,

    /// <summary>
    ///     只能向上展开的 hunk 头;仅第一个 hunk 可以只向上展开。<br />
    ///     The hunk header can be expanded up exclusively. Only the first hunk can be
    ///     expanded up exclusively.
    /// </summary>
    Up = 1,

    /// <summary>
    ///     只能向下展开的 hunk 头;仅最后一个 hunk(只有一行的占位 hunk)可以只向下展开。<br />
    ///     The hunk header can be expanded down exclusively. Only the last hunk (if it's
    ///     the dummy hunk with only one line) can be expanded down exclusively.
    /// </summary>
    Down = 2,

    /// <summary>hunk 头可向上、向下展开。<br />The hunk header can be expanded both up and down.</summary>
    Both = 3,

    /// <summary>
    ///     表示较短间隔的 hunk 头,展开后将与上一个 hunk 合并。<br />
    ///     The hunk header represents a short gap that, when expanded, will result in
    ///     merging this hunk and the hunk above.
    /// </summary>
    Short = 4
}

/// <summary>diff hunk 起止位置的详情。<br />Details about the start and end of a diff hunk.</summary>
public sealed class DiffHunkHeader(int oldStartLine, int oldLineCount, int newStartLine, int newLineCount)
{
    /// <summary>本 hunk 在旧(原始)文件中的起始行。<br />The line in the old (or original) file where this diff hunk starts.</summary>
    public int OldStartLine { get; } = oldStartLine;

    /// <summary>本 hunk 覆盖的旧(原始)文件行数。<br />The number of lines in the old (or original) file that this diff hunk covers</summary>
    public int OldLineCount { get; } = oldLineCount;

    /// <summary>本 hunk 在新文件中的起始行。<br />The line in the new file where this diff hunk starts.</summary>
    public int NewStartLine { get; } = newStartLine;

    /// <summary>本 hunk 覆盖的新文件行数。<br />The number of lines in the new file that this diff hunk covers.</summary>
    public int NewLineCount { get; } = newLineCount;

    /// <summary>
    ///     渲染 "@@ -旧起始,旧行数 +新起始,新行数 @@" 形式的 hunk 头文本。<br />Renders the "@@ -oldStart,oldCount +newStart,newCount @@" hunk
    ///     header text.
    /// </summary>
    public string ToDiffLineRepresentation()
    {
        return $"@@ -{OldStartLine},{OldLineCount} +{NewStartLine},{NewLineCount} @@";
    }

    // 保留上游比较行为:重复比较 oldStartLine,不比较 newLineCount。
    /// <summary>判断与另一个 hunk 头是否相等。<br />Determines equality with another hunk header.</summary>
    /// <remarks>
    ///     行为与 JS 原版逐行一致:OldStartLine 被比较了两次,且从不比较 NewLineCount。<br />Behavior is kept identical to the JS original:
    ///     OldStartLine is compared twice and NewLineCount is never compared.
    /// </remarks>
    public bool Equals(DiffHunkHeader other)
    {
        return OldStartLine == other.OldStartLine &&
               OldLineCount == other.OldLineCount &&
               NewStartLine == other.NewStartLine &&
               OldStartLine == other.OldStartLine;
    }
}

/// <summary>每个 diff 由若干 hunk 组成。<br />Each diff is made up of a number of hunks.</summary>
public sealed class DiffHunk(
    DiffHunkHeader          header,
    IReadOnlyList<DiffLine> lines,
    int                     unifiedDiffStart,
    int                     unifiedDiffEnd,
    DiffHunkExpansionType   expansionType)
{
    /// <summary>来自 hunk 头的行起始与补丁长度信息。<br />The details from the diff hunk header about the line start and patch length.</summary>
    public DiffHunkHeader Header { get; } = header;

    /// <summary>该 diff 区段的内容——上下文行与变更行。<br />The contents - context and changes - of the diff section.</summary>
    public IReadOnlyList<DiffLine> Lines { get; } = lines;

    /// <summary>本 hunk 在整个文件 diff 中的起始位置。<br />The diff hunk's start position in the overall file diff.</summary>
    public int UnifiedDiffStart { get; } = unifiedDiffStart;

    /// <summary>本 hunk 在整个文件 diff 中的结束位置。<br />The diff hunk's end position in the overall file diff.</summary>
    public int UnifiedDiffEnd { get; } = unifiedDiffEnd;

    /// <summary>
    ///     hunk 头的可展开方式(见 <see cref="DiffHunkExpansionType" />)。<br />How the hunk header can be expanded (see
    ///     <see cref="DiffHunkExpansionType" />).
    /// </summary>
    public DiffHunkExpansionType ExpansionType { get; } = expansionType;

    /// <summary>逐字段比较两个 hunk(含逐行比较)。<br />Field-wise comparison of two hunks (including line-by-line comparison).</summary>
    public bool Equals(DiffHunk other)
    {
        if (ReferenceEquals(this, other)) return true;

        return Header.Equals(other.Header)                &&
               UnifiedDiffStart == other.UnifiedDiffStart &&
               UnifiedDiffEnd   == other.UnifiedDiffEnd   &&
               ExpansionType    == other.ExpansionType    &&
               Lines.Count      == other.Lines.Count      &&
               Lines.Zip(other.Lines, (xLine, o) => xLine.Equals(o)).All(x => x);
    }
}

/// <summary>记录 diff 中每行相关细节。<br />Track details related to each line in the diff.</summary>
// 解析、分栏和统一模型共享同一行实例,因此将 DiffLineItem / DiffHunkItem 状态合并于此。
public class DiffLine(
    string       text,
    DiffLineType type,
    int?         originalLineNumber,
    int?         oldLineNumber,
    int?         newLineNumber,
    bool         noTrailingNewLine = false)
{
    /// <summary>行文本,可能带行尾换行符(\r\n 等)。<br />The line text; may include a trailing newline (\r\n etc.).</summary>
    public string Text { get; } = text;

    /// <summary>行类型(上下文 / 新增 / 删除 / hunk 头)。<br />The line type (context / add / delete / hunk header).</summary>
    public DiffLineType Type { get; } = type;

    /// <summary>
    ///     该行在原始 diff 补丁中的行号(展开之前);因展开操作新增的行则为 <c>null</c>。<br />
    ///     Line number in the original diff patch (before expanding it), or null if it was
    ///     added as part of a diff expansion action.
    /// </summary>
    public int? OriginalLineNumber { get; } = originalLineNumber;

    /// <summary>
    ///     旧侧行号(1 基);该行在旧侧无对应时为 <c>null</c>。<br />Old-side line number (1-based); <c>null</c> when the line has no
    ///     old-side counterpart.
    /// </summary>
    public int? OldLineNumber { get; } = oldLineNumber;

    /// <summary>
    ///     新侧行号(1 基);该行在新侧无对应时为 <c>null</c>。<br />New-side line number (1-based); <c>null</c> when the line has no
    ///     new-side counterpart.
    /// </summary>
    public int? NewLineNumber { get; } = newLineNumber;

    /// <summary>该行是否带 "no newline at end of file" 标记。<br />Whether the line carries the "no newline at end of file" marker.</summary>
    public bool NoTrailingNewLine { get; } = noTrailingNewLine;

    // ---- word-level diff results (change-range.ts), filled by getDiffRange ----

    /// <summary>
    ///     词级相对变化范围(change-range.ts relativeChanges 的结果,由 GetDiffRange 填充);<c>null</c> 表示尚未计算。<br />Word-level relative
    ///     change range (result of change-range.ts relativeChanges, filled by GetDiffRange); <c>null</c> until computed.
    /// </summary>
    public LineRange? Changes { get; set; }

    /// <summary>JS 字段:diffChanges(基于 fast-diff 的本行文本段范围)。<br />JS field: diffChanges (fast-diff based ranges of this line).</summary>
    public DiffRange? DiffChanges { get; set; }

    /// <summary>
    ///     JS 字段:_diffChanges。保存对侧行的范围(新增行上是删除范围,反之亦然);由基于 fast-diff
    ///     的语法模板构建器消费。<br />
    ///     JS field: _diffChanges. Holds the *opposite* line's range (deletion range on an
    ///     addition and vice versa); consumed by the fast-diff syntax template builder.
    /// </summary>
    public DiffRange? InternalDiffChanges { get; set; }

    // ---- DiffFile line model state (diff-file.ts DiffLineItem / DiffHunkItem) ----

    /// <summary>
    ///     JS 字段:DiffLineItem.index。该行被组装进 diffLines 之前为 <c>null</c>。<br />JS field: DiffLineItem.index. Null until the
    ///     line is composed into diffLines.
    /// </summary>
    public int? Index { get; set; }

    /// <summary>
    ///     JS 字段:DiffLineItem.prevHunkLine。设置在 hunk 头之后的第一个上下文行上。<br />JS field: DiffLineItem.prevHunkLine. Set on the
    ///     first context line after a hunk header.
    /// </summary>
    public DiffLine? PrevHunkLine { get; set; }

    /// <summary>
    ///     JS 字段:DiffHunkItem.isFirst。是否为所在 hunk 的首行;<c>null</c> 表示尚未赋值。<br />JS field: DiffHunkItem.isFirst. Whether the
    ///     line is the first line of its hunk; <c>null</c> until assigned.
    /// </summary>
    public bool? IsFirst { get; set; }

    /// <summary>
    ///     JS 字段:DiffHunkItem.isLast。是否为所在 hunk 的末行;<c>null</c> 表示尚未赋值。<br />JS field: DiffHunkItem.isLast. Whether the
    ///     line is the last line of its hunk; <c>null</c> until assigned.
    /// </summary>
    public bool? IsLast { get; set; }

    /// <summary>
    ///     JS 字段:DiffHunkItem.hunkInfo。hunk 头信息;<c>null</c> 表示非 hunk 头行或尚未赋值。<br />JS field: DiffHunkItem.hunkInfo. The
    ///     hunk header info; <c>null</c> when not a hunk header line or not assigned yet.
    /// </summary>
    public HunkInfo? HunkInfo { get; set; }

    /// <summary>
    ///     JS 字段:DiffHunkItem.splitInfo。分栏视图的折叠区间信息;仅折叠 hunk 的头行有值。<br />JS field: DiffHunkItem.splitInfo.
    ///     Collapsed-range info for the split view; set only on header lines of collapsed hunks.
    /// </summary>
    public HunkLineInfo? SplitInfo { get; set; }

    /// <summary>
    ///     JS 字段:DiffHunkItem.unifiedInfo。统一视图的折叠区间信息;仅折叠 hunk 的头行有值。<br />JS field: DiffHunkItem.unifiedInfo.
    ///     Collapsed-range info for the unified view; set only on header lines of collapsed hunks.
    /// </summary>
    public HunkLineInfo? UnifiedInfo { get; set; }

    /// <summary>
    ///     返回仅替换 NoTrailingNewLine 的新实例,其余字段保留。<br />Returns a new instance with only NoTrailingNewLine replaced; all
    ///     other fields are kept.
    /// </summary>
    public DiffLine WithNoTrailingNewLine(bool noTrailingNewLine)
    {
        return new DiffLine(Text, Type, OriginalLineNumber, OldLineNumber, NewLineNumber, noTrailingNewLine);
    }

    /// <summary>该行是否为计入变更的行(新增或删除)。<br />Whether the line counts as a change line (add or delete).</summary>
    public bool IsIncludeableLine()
    {
        return Type is DiffLineType.Add or DiffLineType.Delete;
    }

    /// <summary>逐字段比较两行是否相等。<br />Field-wise equality check against another line.</summary>
    public bool Equals(DiffLine? other)
    {
        return other is not null                              &&
               Text               == other.Text               &&
               Type               == other.Type               &&
               OriginalLineNumber == other.OriginalLineNumber &&
               OldLineNumber      == other.OldLineNumber      &&
               NewLineNumber      == other.NewLineNumber      &&
               NoTrailingNewLine  == other.NoTrailingNewLine;
    }

    /// <summary>以给定文本克隆当前行(其余字段保留)。<br />Clones this line with the given text (all other fields are kept).</summary>
    public DiffLine Clone(string text)
    {
        return new DiffLine(text, Type, OriginalLineNumber, OldLineNumber, NewLineNumber, NoTrailingNewLine);
    }
}

/// <summary><see cref="DiffLine" /> 的扩展方法。<br />Extension methods for <see cref="DiffLine" />.</summary>
public static class DiffLineExtensions
{
    /// <summary>parse/diff-line.ts checkDiffLineIncludeChange 的移植。<br />Port of parse/diff-line.ts checkDiffLineIncludeChange.</summary>
    public static bool CheckDiffLineIncludeChange(this DiffLine? diffLine)
    {
        return diffLine?.Type is DiffLineType.Add or DiffLineType.Delete;
    }
}
