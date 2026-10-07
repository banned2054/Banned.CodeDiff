namespace Banned.CodeDiff.Models;

/// <summary>
///     hunk 头信息,移植自 diff-file.ts;省略的行数以 <c>null</c> 表示 JS 的 NaN。<br />
///     Hunk header data ported from diff-file.ts; omitted counts use null to represent JS NaN.
/// </summary>
public sealed class HunkInfo
{
    /// <summary>旧侧起始行号(1 基)。<br />Old-side start line number (1-based).</summary>
    public int OldStartIndex { get; set; }

    /// <summary>
    ///     旧侧行数;hunk 头省略计数时为 <c>null</c>。<br />
    ///     Old-side line count; null when omitted from the hunk header.
    /// </summary>
    public int? OldLength { get; set; }

    /// <summary>新侧起始行号(1 基)。<br />New-side start line number (1-based).</summary>
    public int NewStartIndex { get; set; }

    /// <summary>
    ///     新侧行数;hunk 头省略计数时为 <c>null</c>。<br />
    ///     New-side line count; null when omitted from the hunk header.
    /// </summary>
    public int? NewLength { get; set; }

    /// <summary>
    ///     折叠前 OldStartIndex 的快照(collapse/restore 用)。<br />Snapshot of OldStartIndex before collapsing (used by
    ///     collapse/restore).
    /// </summary>
    public int OldStartIndexSnapshot { get; set; }

    /// <summary>
    ///     折叠前 OldLength 的快照(collapse/restore 用)。<br />Snapshot of OldLength before collapsing (used by
    ///     collapse/restore).
    /// </summary>
    public int? OldLengthSnapshot { get; set; }

    /// <summary>
    ///     折叠前 NewStartIndex 的快照(collapse/restore 用)。<br />Snapshot of NewStartIndex before collapsing (used by
    ///     collapse/restore).
    /// </summary>
    public int NewStartIndexSnapshot { get; set; }

    /// <summary>
    ///     折叠前 NewLength 的快照(collapse/restore 用)。<br />Snapshot of NewLength before collapsing (used by
    ///     collapse/restore).
    /// </summary>
    public int? NewLengthSnapshot { get; set; }
}

/// <summary>
///     diff-file.ts 类型 HunkLineInfo 的移植。splitInfo/unifiedInfo 是 JS 的交叉类型
///     HunkLineInfo &amp; HunkInfo,因此本类同时携带两组字段(以及折叠/恢复用的快照字段)。<br />
///     Port of diff-file.ts type HunkLineInfo. splitInfo/unifiedInfo are the JS
///     intersection type HunkLineInfo &amp; HunkInfo, so this class carries both groups of
///     fields (plus their snapshot counterparts used by collapse/restore).
/// </summary>
public sealed class HunkLineInfo
{
    /// <summary>
    ///     折叠区间的起始索引(0 基,基于分栏/统一行列表)。<br />Start index of the collapsed range (0-based, into the split/unified line
    ///     list).
    /// </summary>
    public int StartHiddenIndex { get; set; }

    /// <summary>折叠区间的结束索引(0 基,含)。<br />End index of the collapsed range (0-based, inclusive).</summary>
    public int EndHiddenIndex { get; set; }

    /// <summary>
    ///     折叠后 hunk 头行展示的文本;JS 原版在占位场景可为空字符串。<br />Text shown on the collapsed hunk header row; may be empty as a
    ///     placeholder in the JS original.
    /// </summary>
    public string? PlainText { get; set; }

    /// <summary>
    ///     折叠前 StartHiddenIndex 的快照(collapse/restore 用)。<br />Snapshot of StartHiddenIndex before collapsing (used by
    ///     collapse/restore).
    /// </summary>
    public int StartHiddenIndexSnapshot { get; set; }

    /// <summary>
    ///     折叠前 EndHiddenIndex 的快照(collapse/restore 用)。<br />Snapshot of EndHiddenIndex before collapsing (used by
    ///     collapse/restore).
    /// </summary>
    public int EndHiddenIndexSnapshot { get; set; }

    /// <summary>
    ///     折叠前 PlainText 的快照(collapse/restore 用)。<br />Snapshot of PlainText before collapsing (used by
    ///     collapse/restore).
    /// </summary>
    public string? PlainTextSnapshot { get; set; }

    /// <summary>
    ///     旧侧起始行号(1 基);来自 hunkInfo 组字段,<c>null</c> 表示未填充。<br />Old-side start line number (1-based), from the hunkInfo
    ///     group of fields; <c>null</c> when not populated.
    /// </summary>
    public int? OldStartIndex { get; set; }

    /// <summary>
    ///     旧侧行数;hunk 头省略计数或未填充时为 <c>null</c>。<br />Old-side line count; <c>null</c> when the hunk header omits it or the
    ///     field is not populated.
    /// </summary>
    public int? OldLength { get; set; }

    /// <summary>
    ///     新侧起始行号(1 基);来自 hunkInfo 组字段,<c>null</c> 表示未填充。<br />New-side start line number (1-based), from the hunkInfo
    ///     group of fields; <c>null</c> when not populated.
    /// </summary>
    public int? NewStartIndex { get; set; }

    /// <summary>
    ///     新侧行数;hunk 头省略计数或未填充时为 <c>null</c>。<br />New-side line count; <c>null</c> when the hunk header omits it or the
    ///     field is not populated.
    /// </summary>
    public int? NewLength { get; set; }

    /// <summary>
    ///     折叠前 OldStartIndex 的快照(collapse/restore 用)。<br />Snapshot of OldStartIndex before collapsing (used by
    ///     collapse/restore).
    /// </summary>
    public int? OldStartIndexSnapshot { get; set; }

    /// <summary>
    ///     折叠前 OldLength 的快照(collapse/restore 用)。<br />Snapshot of OldLength before collapsing (used by
    ///     collapse/restore).
    /// </summary>
    public int? OldLengthSnapshot { get; set; }

    /// <summary>
    ///     折叠前 NewStartIndex 的快照(collapse/restore 用)。<br />Snapshot of NewStartIndex before collapsing (used by
    ///     collapse/restore).
    /// </summary>
    public int? NewStartIndexSnapshot { get; set; }

    /// <summary>
    ///     折叠前 NewLength 的快照(collapse/restore 用)。<br />Snapshot of NewLength before collapsing (used by
    ///     collapse/restore).
    /// </summary>
    public int? NewLengthSnapshot { get; set; }

    /// <summary>
    ///     从 HunkInfo 复制 hunk 信息字段并填充折叠区间信息。<br />
    ///     JS: { ...hunkInfo, startHiddenIndex, endHiddenIndex, plainText, _startHiddenIndex, _endHiddenIndex, _plainText
    ///     }
    /// </summary>
    public static HunkLineInfo FromHunkInfo(
        HunkInfo hunkInfo, int startHiddenIndex, int endHiddenIndex, string? plainText
    )
    {
        return new HunkLineInfo
        {
            StartHiddenIndex         = startHiddenIndex,
            EndHiddenIndex           = endHiddenIndex,
            PlainText                = plainText,
            StartHiddenIndexSnapshot = startHiddenIndex,
            EndHiddenIndexSnapshot   = endHiddenIndex,
            PlainTextSnapshot        = plainText,
            OldStartIndex            = hunkInfo.OldStartIndex,
            OldLength                = hunkInfo.OldLength,
            NewStartIndex            = hunkInfo.NewStartIndex,
            NewLength                = hunkInfo.NewLength,
            OldStartIndexSnapshot    = hunkInfo.OldStartIndexSnapshot,
            OldLengthSnapshot        = hunkInfo.OldLengthSnapshot,
            NewStartIndexSnapshot    = hunkInfo.NewStartIndexSnapshot,
            NewLengthSnapshot        = hunkInfo.NewLengthSnapshot
        };
    }

    /// <summary>按字段复制当前项并应用给定的覆盖值。<br />JS: { ...current.splitInfo, startHiddenIndex } (field-wise copy with overrides).</summary>
    public HunkLineInfo With(
        int? startHiddenIndex = null, int? endHiddenIndex = null, string? plainText = null, bool clearPlainText = false)
    {
        return new HunkLineInfo
        {
            StartHiddenIndex         = startHiddenIndex ?? StartHiddenIndex,
            EndHiddenIndex           = endHiddenIndex   ?? EndHiddenIndex,
            PlainText                = clearPlainText ? "" : plainText ?? PlainText,
            StartHiddenIndexSnapshot = StartHiddenIndexSnapshot,
            EndHiddenIndexSnapshot   = EndHiddenIndexSnapshot,
            PlainTextSnapshot        = PlainTextSnapshot,
            OldStartIndex            = OldStartIndex,
            OldLength                = OldLength,
            NewStartIndex            = NewStartIndex,
            NewLength                = NewLength,
            OldStartIndexSnapshot    = OldStartIndexSnapshot,
            OldLengthSnapshot        = OldLengthSnapshot,
            NewStartIndexSnapshot    = NewStartIndexSnapshot,
            NewLengthSnapshot        = NewLengthSnapshot
        };
    }

    /// <summary>
    ///     合并当前项与给定 HunkInfo(展开 "all" 场景)。<br />JS: { ...current.splitInfo, ...current.hunkInfo, plainText: current.text,
    ///     startHiddenIndex } (expand "all").
    /// </summary>
    public HunkLineInfo WithHunkInfo(
        HunkInfo hunkInfo, int? startHiddenIndex = null, string? plainText = null, bool clearPlainText = false)
    {
        return new HunkLineInfo
        {
            StartHiddenIndex         = startHiddenIndex ?? StartHiddenIndex,
            EndHiddenIndex           = EndHiddenIndex,
            PlainText                = clearPlainText ? "" : plainText ?? PlainText,
            StartHiddenIndexSnapshot = StartHiddenIndexSnapshot,
            EndHiddenIndexSnapshot   = EndHiddenIndexSnapshot,
            PlainTextSnapshot        = PlainTextSnapshot,
            OldStartIndex            = hunkInfo.OldStartIndex,
            OldLength                = hunkInfo.OldLength,
            NewStartIndex            = hunkInfo.NewStartIndex,
            NewLength                = hunkInfo.NewLength,
            OldStartIndexSnapshot    = OldStartIndexSnapshot,
            OldLengthSnapshot        = OldLengthSnapshot,
            NewStartIndexSnapshot    = NewStartIndexSnapshot,
            NewLengthSnapshot        = NewLengthSnapshot
        };
    }

    /// <summary>
    ///     按 JS 模板插值渲染可能缺失的计数:值为 <c>null</c> 时输出 "NaN"(对应 "@@ -1 +1 @@" 解析出的计数)。<br />JS template interpolation of a
    ///     possibly-NaN count ("@@ -1 +1 @@" parsed count).
    /// </summary>
    public static string RenderCount(int? value)
    {
        return value?.ToString() ?? "NaN";
    }

    /// <summary>
    ///     用快照字段恢复折叠前的各字段值。<br />JS: { ...item.splitInfo, oldStartIndex: item.splitInfo._oldStartIndex, ... } (collapse
    ///     restore).
    /// </summary>
    public HunkLineInfo RestoreOriginal()
    {
        return new HunkLineInfo
        {
            StartHiddenIndex         = StartHiddenIndexSnapshot,
            EndHiddenIndex           = EndHiddenIndexSnapshot,
            PlainText                = PlainTextSnapshot,
            StartHiddenIndexSnapshot = StartHiddenIndexSnapshot,
            EndHiddenIndexSnapshot   = EndHiddenIndexSnapshot,
            PlainTextSnapshot        = PlainTextSnapshot,
            OldStartIndex            = OldStartIndexSnapshot,
            OldLength                = OldLengthSnapshot,
            NewStartIndex            = NewStartIndexSnapshot,
            NewLength                = NewLengthSnapshot,
            OldStartIndexSnapshot    = OldStartIndexSnapshot,
            OldLengthSnapshot        = OldLengthSnapshot,
            NewStartIndexSnapshot    = NewStartIndexSnapshot,
            NewLengthSnapshot        = NewLengthSnapshot
        };
    }
}

/// <summary>diff-file.ts 接口 SplitLineItem 的移植。<br />Port of diff-file.ts interface SplitLineItem.</summary>
public sealed class SplitLineItem
{
    /// <summary>
    ///     分栏本侧行号(1 基);JS 可选字段,可能为 <c>null</c>。<br />Split-view line number on this side (1-based); optional in JS and
    ///     may be <c>null</c>.
    /// </summary>
    public int? LineNumber { get; set; }

    /// <summary>行文本。<br />The line text.</summary>
    public string? Value { get; set; }

    /// <summary>关联的 <see cref="DiffLine" />。<br />The associated <see cref="DiffLine" />.</summary>
    public DiffLine? Diff { get; set; }

    /// <summary>该行是否被折叠隐藏。<br />Whether the line is hidden behind a collapsed hunk.</summary>
    public bool IsHidden { get; set; }

    /// <summary>折叠前 IsHidden 的快照。<br />Snapshot of IsHidden before collapsing.</summary>
    public bool IsHiddenSnapshot { get; set; }

    /// <summary>返回按字段浅拷贝的新实例。<br />Returns a field-wise shallow copy of this item.</summary>
    public SplitLineItem Clone()
    {
        return (SplitLineItem)MemberwiseClone();
    }
}

/// <summary>diff-file.ts 接口 UnifiedLineItem 的移植。<br />Port of diff-file.ts interface UnifiedLineItem.</summary>
public sealed class UnifiedLineItem
{
    /// <summary>
    ///     旧侧行号(1 基);该行在旧侧无对应时为 <c>null</c>。<br />Old-side line number (1-based); <c>null</c> when the line has no
    ///     old-side counterpart.
    /// </summary>
    public int? OldLineNumber { get; set; }

    /// <summary>
    ///     新侧行号(1 基);该行在新侧无对应时为 <c>null</c>。<br />New-side line number (1-based); <c>null</c> when the line has no
    ///     new-side counterpart.
    /// </summary>
    public int? NewLineNumber { get; set; }

    /// <summary>行文本。<br />The line text.</summary>
    public string? Value { get; set; }

    /// <summary>关联的 <see cref="DiffLine" />。<br />The associated <see cref="DiffLine" />.</summary>
    public DiffLine? Diff { get; set; }

    /// <summary>该行是否被折叠隐藏。<br />Whether the line is hidden behind a collapsed hunk.</summary>
    public bool IsHidden { get; set; }

    /// <summary>折叠前 IsHidden 的快照。<br />Snapshot of IsHidden before collapsing.</summary>
    public bool IsHiddenSnapshot { get; set; }

    /// <summary>返回按字段浅拷贝的新实例。<br />Returns a field-wise shallow copy of this item.</summary>
    public UnifiedLineItem Clone()
    {
        return (UnifiedLineItem)MemberwiseClone();
    }
}

/// <summary>diff-file-utils.ts 枚举 SplitSide 的移植。<br />Port of diff-file-utils.ts enum SplitSide.</summary>
public enum SplitSide
{
    /// <summary>旧侧。<br />The old side.</summary>
    Old = 1,

    /// <summary>新侧。<br />The new side.</summary>
    New = 2
}
