namespace Banned.CodeDiff.Models;

/// <summary>indicate what a line in the diff represents</summary>
public enum DiffLineType
{
    Context = 0,
    Add     = 1,
    Delete  = 2,
    Hunk    = 3,
}

/// <summary>Port of raw-diff.ts IRawDiff.</summary>
public sealed class RawDiff
{
    /// <summary>
    /// The plain text contents of the diff header. This contains everything from the
    /// start of the diff up until the first hunk header starts. Note that this does
    /// not include a trailing newline.
    /// </summary>
    public string Header { get; init; } = "";

    /// <summary>
    /// The plain text contents of the diff. This contains everything after the diff
    /// header until the last character in the diff. Does not include a trailing newline
    /// nor 'no newline at end of file' comments (see <see cref="DiffLine.NoTrailingNewLine"/>).
    /// </summary>
    public string Contents { get; init; } = "";

    /// <summary>Each hunk in the diff with information about start, and end positions, lines and line statuses.</summary>
    public IReadOnlyList<DiffHunk> Hunks { get; init; } = [];

    /// <summary>Whether or not the unified diff indicates that the contents could not be diffed due to one of the versions being binary.</summary>
    public bool IsBinary { get; init; }

    /// <summary>The largest line number in the diff</summary>
    public int MaxLineNumber { get; init; }

    /// <summary>Whether or not the diff has invisible bidi characters</summary>
    public bool HasHiddenBidiChars { get; init; }
}

/// <summary>Port of raw-diff.ts DiffHunkExpansionType.</summary>
public enum DiffHunkExpansionType
{
    /// <summary>The hunk header cannot be expanded at all.</summary>
    None = 0,

    /// <summary>
    /// The hunk header can be expanded up exclusively. Only the first hunk can be
    /// expanded up exclusively.
    /// </summary>
    Up = 1,

    /// <summary>
    /// The hunk header can be expanded down exclusively. Only the last hunk (if it's
    /// the dummy hunk with only one line) can be expanded down exclusively.
    /// </summary>
    Down = 2,

    /// <summary>The hunk header can be expanded both up and down.</summary>
    Both = 3,

    /// <summary>
    /// The hunk header represents a short gap that, when expanded, will result in
    /// merging this hunk and the hunk above.
    /// </summary>
    Short = 4,
}

/// <summary>details about the start and end of a diff hunk</summary>
public sealed class DiffHunkHeader(int oldStartLine, int oldLineCount, int newStartLine, int newLineCount)
{
    /// <summary>The line in the old (or original) file where this diff hunk starts.</summary>
    public int OldStartLine { get; } = oldStartLine;

    /// <summary>The number of lines in the old (or original) file that this diff hunk covers</summary>
    public int OldLineCount { get; } = oldLineCount;

    /// <summary>The line in the new file where this diff hunk starts.</summary>
    public int NewStartLine { get; } = newStartLine;

    /// <summary>The number of lines in the new file that this diff hunk covers.</summary>
    public int NewLineCount { get; } = newLineCount;

    public string ToDiffLineRepresentation()
    {
        return $"@@ -{OldStartLine},{OldLineCount} +{NewStartLine},{NewLineCount} @@";
    }

    public bool Equals(DiffHunkHeader other)
    {
        // NOTE: kept identical to the JS original, which compares oldStartLine twice
        // and never compares newLineCount.
        return OldStartLine == other.OldStartLine &&
               OldLineCount == other.OldLineCount &&
               NewStartLine == other.NewStartLine &&
               OldStartLine == other.OldStartLine;
    }
}

/// <summary>each diff is made up of a number of hunks</summary>
public sealed class DiffHunk(
    DiffHunkHeader          header,
    IReadOnlyList<DiffLine> lines,
    int                     unifiedDiffStart,
    int                     unifiedDiffEnd,
    DiffHunkExpansionType   expansionType)
{
    /// <summary>The details from the diff hunk header about the line start and patch length.</summary>
    public DiffHunkHeader Header { get; } = header;

    /// <summary>The contents - context and changes - of the diff section.</summary>
    public IReadOnlyList<DiffLine> Lines { get; } = lines;

    /// <summary>The diff hunk's start position in the overall file diff.</summary>
    public int UnifiedDiffStart { get; } = unifiedDiffStart;

    /// <summary>The diff hunk's end position in the overall file diff.</summary>
    public int UnifiedDiffEnd { get; } = unifiedDiffEnd;

    public DiffHunkExpansionType ExpansionType { get; } = expansionType;

    public bool Equals(DiffHunk other)
    {
        if (ReferenceEquals(this, other))
        {
            return true;
        }

        return Header.Equals(other.Header)                &&
               UnifiedDiffStart == other.UnifiedDiffStart &&
               UnifiedDiffEnd   == other.UnifiedDiffEnd   &&
               ExpansionType    == other.ExpansionType    &&
               Lines.Count      == other.Lines.Count      &&
               Lines.Zip(other.Lines, (xLine, o) => xLine.Equals(o)).All(x => x);
    }
}

/// <summary>track details related to each line in the diff</summary>
//
// The DiffLineItem / DiffHunkItem fields from diff-file.ts (index, prevHunkLine,
// isFirst, isLast, hunkInfo, splitInfo, unifiedInfo) are flattened onto this class:
// the JS implementation mutates one shared object identity (the DiffLine created by
// the parser is the same object used as a DiffLineItem/DiffHunkItem and referenced
// from split/unified models), so C# keeps the same instances instead of wrapping.
public class DiffLine(
    string       text,
    DiffLineType type,
    int?         originalLineNumber,
    int?         oldLineNumber,
    int?         newLineNumber,
    bool         noTrailingNewLine = false)
{
    public string Text { get; } = text;

    public DiffLineType Type { get; } = type;

    /// <summary>
    /// Line number in the original diff patch (before expanding it), or null if it was
    /// added as part of a diff expansion action.
    /// </summary>
    public int? OriginalLineNumber { get; } = originalLineNumber;

    public int? OldLineNumber { get; } = oldLineNumber;

    public int? NewLineNumber { get; } = newLineNumber;

    public bool NoTrailingNewLine { get; } = noTrailingNewLine;

    // ---- word-level diff results (change-range.ts), filled by getDiffRange ----

    public LineRange? Changes { get; set; }

    /// <summary>JS field: diffChanges (fast-diff based ranges of this line).</summary>
    public DiffRange? DiffChanges { get; set; }

    /// <summary>
    /// JS field: _diffChanges. Holds the *opposite* line's range (deletion range on an
    /// addition and vice versa); consumed by the fast-diff syntax template builder.
    /// </summary>
    public DiffRange? InternalDiffChanges { get; set; }

    // ---- DiffFile line model state (diff-file.ts DiffLineItem / DiffHunkItem) ----

    /// <summary>JS field: DiffLineItem.index. Null until the line is composed into diffLines.</summary>
    public int? Index { get; set; }

    /// <summary>JS field: DiffLineItem.prevHunkLine. Set on the first context line after a hunk header.</summary>
    public DiffLine? PrevHunkLine { get; set; }

    /// <summary>JS field: DiffHunkItem.isFirst.</summary>
    public bool? IsFirst { get; set; }

    /// <summary>JS field: DiffHunkItem.isLast.</summary>
    public bool? IsLast { get; set; }

    /// <summary>JS field: DiffHunkItem.hunkInfo.</summary>
    public HunkInfo? HunkInfo { get; set; }

    /// <summary>JS field: DiffHunkItem.splitInfo.</summary>
    public HunkLineInfo? SplitInfo { get; set; }

    /// <summary>JS field: DiffHunkItem.unifiedInfo.</summary>
    public HunkLineInfo? UnifiedInfo { get; set; }

    public DiffLine WithNoTrailingNewLine(bool noTrailingNewLine)
    {
        return new DiffLine(Text, Type, OriginalLineNumber, OldLineNumber, NewLineNumber, noTrailingNewLine);
    }

    public bool IsIncludeableLine()
    {
        return Type is DiffLineType.Add or DiffLineType.Delete;
    }

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

    public DiffLine Clone(string text)
    {
        return new DiffLine(text, Type, OriginalLineNumber, OldLineNumber, NewLineNumber, NoTrailingNewLine);
    }
}

public static class DiffLineExtensions
{
    /// <summary>Port of parse/diff-line.ts checkDiffLineIncludeChange.</summary>
    public static bool CheckDiffLineIncludeChange(this DiffLine? diffLine) =>
        diffLine?.Type is DiffLineType.Add or DiffLineType.Delete;
}
