namespace Banned.CodeDiff.Models;

/// <summary>Port of diff-file-utils.ts enum DiffFileLineType.</summary>
public enum DiffFileLineType
{
    Hunk    = 1,
    Content = 2,
    Widget  = 3,
    Extend  = 4
}

/// <summary>Port of diff-file-utils.ts DiffSplitContentLineItem.</summary>
public sealed record DiffSplitContentLineItem(
    DiffFileLineType Type,
    int              Index,
    int              LineNumber,
    SplitLineItem?   Left,
    SplitLineItem?   Right
);

/// <summary>Port of diff-file-utils.ts DiffUnifiedContentLineItem.</summary>
public sealed record DiffUnifiedContentLineItem(
    DiffFileLineType Type,
    int              Index,
    int              LineNumber,
    UnifiedLineItem? UnifiedLine
);

/// <summary>Port of diff-file-utils.ts DiffSplitLineItem.</summary>
public sealed record DiffSplitLineItem(DiffFileLineType Type, int Index, int LineNumber);

/// <summary>Port of diff-file-utils.ts DiffUnifiedLineItem.</summary>
public sealed record DiffUnifiedLineItem(DiffFileLineType Type, int Index, int LineNumber);
