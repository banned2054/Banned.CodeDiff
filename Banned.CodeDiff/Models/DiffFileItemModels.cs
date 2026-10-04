namespace Banned.CodeDiff.Models;

/// <summary>diff-file-utils.ts 枚举 DiffFileLineType 的移植。<br />Port of diff-file-utils.ts enum DiffFileLineType.</summary>
public enum DiffFileLineType
{
    /// <summary>hunk 头部行。<br />The hunk header row.</summary>
    Hunk = 1,

    /// <summary>内容行。<br />The content row.</summary>
    Content = 2,

    /// <summary>挂载 widget(如评论)的行。<br />The widget row (mount point for widgets).</summary>
    Widget = 3,

    /// <summary>展开控制行。<br />The expansion row.</summary>
    Extend = 4
}

/// <summary>diff-file-utils.ts DiffSplitContentLineItem 的移植。<br />Port of diff-file-utils.ts DiffSplitContentLineItem.</summary>
/// <param name="Type">
///     行类别;内容行为 <see cref="DiffFileLineType.Content" />。<br />The line type;
///     <see cref="DiffFileLineType.Content" /> for content lines.
/// </param>
/// <param name="Index">在分栏行列表中的下标(0 基)。<br />Index into the split line list (0-based).</param>
/// <param name="LineNumber">展示行号(1 基,即 index + 1)。<br />Display line number (1-based, index + 1).</param>
/// <param name="Left">左侧行;可为 <c>null</c>。<br />The left side line; may be <c>null</c>.</param>
/// <param name="Right">右侧行;可为 <c>null</c>。<br />The right side line; may be <c>null</c>.</param>
public sealed record DiffSplitContentLineItem(
    DiffFileLineType Type,
    int              Index,
    int              LineNumber,
    SplitLineItem?   Left,
    SplitLineItem?   Right
);

/// <summary>diff-file-utils.ts DiffUnifiedContentLineItem 的移植。<br />Port of diff-file-utils.ts DiffUnifiedContentLineItem.</summary>
/// <param name="Type">
///     行类别;内容行为 <see cref="DiffFileLineType.Content" />。<br />The line type;
///     <see cref="DiffFileLineType.Content" /> for content lines.
/// </param>
/// <param name="Index">在统一行列表中的下标(0 基)。<br />Index into the unified line list (0-based).</param>
/// <param name="LineNumber">展示行号(1 基,即 index + 1)。<br />Display line number (1-based, index + 1).</param>
/// <param name="UnifiedLine">统一视图的行;可为 <c>null</c>。<br />The unified view line; may be <c>null</c>.</param>
public sealed record DiffUnifiedContentLineItem(
    DiffFileLineType Type,
    int              Index,
    int              LineNumber,
    UnifiedLineItem? UnifiedLine
);

/// <summary>diff-file-utils.ts DiffSplitLineItem 的移植。<br />Port of diff-file-utils.ts DiffSplitLineItem.</summary>
/// <param name="Type">行类别。<br />The line type.</param>
/// <param name="Index">在分栏行列表中的下标(0 基)。<br />Index into the split line list (0-based).</param>
/// <param name="LineNumber">展示行号(1 基,即 index + 1)。<br />Display line number (1-based, index + 1).</param>
public sealed record DiffSplitLineItem(DiffFileLineType Type, int Index, int LineNumber);

/// <summary>diff-file-utils.ts DiffUnifiedLineItem 的移植。<br />Port of diff-file-utils.ts DiffUnifiedLineItem.</summary>
/// <param name="Type">行类别。<br />The line type.</param>
/// <param name="Index">在统一行列表中的下标(0 基)。<br />Index into the unified line list (0-based).</param>
/// <param name="LineNumber">展示行号(1 基,即 index + 1)。<br />Display line number (1-based, index + 1).</param>
public sealed record DiffUnifiedLineItem(DiffFileLineType Type, int Index, int LineNumber);
