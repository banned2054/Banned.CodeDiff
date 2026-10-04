namespace Banned.CodeDiff.Models;

/// <summary>
///     Port of diff-file.ts type HunkInfo. Start indexes are always present in a
///     parser-valid hunk header ("@@ -(\d+)(?:,(\d+))? +... @@"); the counts may be
///     omitted ("@@ -1 +1 @@"), which is Number(undefined) = NaN in JS — represented
///     here as null.
/// </summary>
public sealed class HunkInfo
{
    public int  OldStartIndex         { get; set; }
    public int? OldLength             { get; set; }
    public int  NewStartIndex         { get; set; }
    public int? NewLength             { get; set; }
    public int  OldStartIndexSnapshot { get; set; }
    public int? OldLengthSnapshot     { get; set; }
    public int  NewStartIndexSnapshot { get; set; }
    public int? NewLengthSnapshot     { get; set; }
}

/// <summary>
///     Port of diff-file.ts type HunkLineInfo. splitInfo/unifiedInfo are the JS
///     intersection type HunkLineInfo &amp; HunkInfo, so this class carries both groups of
///     fields (plus their snapshot counterparts used by collapse/restore).
/// </summary>
public sealed class HunkLineInfo
{
    public int     StartHiddenIndex         { get; set; }
    public int     EndHiddenIndex           { get; set; }
    public string? PlainText                { get; set; }
    public int     StartHiddenIndexSnapshot { get; set; }
    public int     EndHiddenIndexSnapshot   { get; set; }
    public string? PlainTextSnapshot        { get; set; }
    public int?    OldStartIndex            { get; set; }
    public int?    OldLength                { get; set; }
    public int?    NewStartIndex            { get; set; }
    public int?    NewLength                { get; set; }
    public int?    OldStartIndexSnapshot    { get; set; }
    public int?    OldLengthSnapshot        { get; set; }
    public int?    NewStartIndexSnapshot    { get; set; }
    public int?    NewLengthSnapshot        { get; set; }

    /// <summary>
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

    /// <summary>JS: { ...current.splitInfo, startHiddenIndex } (field-wise copy with overrides).</summary>
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

    /// <summary>JS: { ...current.splitInfo, ...current.hunkInfo, plainText: current.text, startHiddenIndex } (expand "all").</summary>
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

    /// <summary>JS template interpolation of a possibly-NaN count ("@@ -1 +1 @@" parsed count).</summary>
    public static string RenderCount(int? value)
    {
        return value?.ToString() ?? "NaN";
    }

    /// <summary>JS: { ...item.splitInfo, oldStartIndex: item.splitInfo._oldStartIndex, ... } (collapse restore).</summary>
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

/// <summary>Port of diff-file.ts interface SplitLineItem.</summary>
public sealed class SplitLineItem
{
    public int?      LineNumber       { get; set; }
    public string?   Value            { get; set; }
    public DiffLine? Diff             { get; set; }
    public bool      IsHidden         { get; set; }
    public bool      IsHiddenSnapshot { get; set; }

    public SplitLineItem Clone()
    {
        return (SplitLineItem)MemberwiseClone();
    }
}

/// <summary>Port of diff-file.ts interface UnifiedLineItem.</summary>
public sealed class UnifiedLineItem
{
    public int?      OldLineNumber    { get; set; }
    public int?      NewLineNumber    { get; set; }
    public string?   Value            { get; set; }
    public DiffLine? Diff             { get; set; }
    public bool      IsHidden         { get; set; }
    public bool      IsHiddenSnapshot { get; set; }

    public UnifiedLineItem Clone()
    {
        return (UnifiedLineItem)MemberwiseClone();
    }
}

/// <summary>Port of diff-file-utils.ts enum SplitSide.</summary>
public enum SplitSide
{
    Old = 1,
    New = 2
}
