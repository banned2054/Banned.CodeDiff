namespace Banned.CodeDiff.Models;

// Port of packages/core/src/multiSelect/types.ts.
//
// Deviations from the JS original (intentional):
// - The JS side type is the string union "old" | "new" (MultiSelectSide); the C# port reuses the
//   SplitSide enum already used by the rest of the core (data.ts converts between the two anyway).
// - The JS LineRange interface is renamed to MultiSelectRange: "LineRange" is already taken by the
//   change-range.ts port in ChangeRangeModels.cs.

/// <summary>Port of types.ts interface LineRange (renamed, see file header).</summary>
public sealed record MultiSelectRange(SplitSide Side, int StartLineNumber, int EndLineNumber);

/// <summary>
///     Port of types.ts interface SelectedLine. The JS optional fields become plain members: the data
///     layer (multiSelect/data.ts) always assigns them, so `undefined` never occurs in practice.
/// </summary>
/// <param name="Index">
///     1-based position of the line in its side's split/unified model
///     (<c>getSplitLineIndexByLineNumber + 1</c> in the JS original).
/// </param>
/// <param name="Value">The raw line value (<c>SplitLineItem.value</c> / <c>UnifiedLineItem.value</c>).</param>
/// <param name="IsHide">Whether the line is currently hidden behind a collapsed hunk.</param>
/// <param name="IsDelete"><c>diff?.type === DiffLineType.Delete</c>.</param>
/// <param name="IsAdd"><c>diff?.type === DiffLineType.Add</c>.</param>
/// <param name="IsContext">
///     <c>diff?.type === DiffLineType.Context || diff?.type === undefined</c> — a missing DiffLine
///     (raw line revealed by hunk expansion) counts as context, mirroring the JS quirk.
/// </param>
public sealed record SelectedLine(
    int     Index,
    int     LineNumber,
    string? Value,
    bool    IsHide,
    bool    IsDelete,
    bool    IsAdd,
    bool    IsContext);

/// <summary>Port of types.ts interface MultiSelectResult.</summary>
public sealed record MultiSelectResult(MultiSelectRange Range, IReadOnlyList<SelectedLine> Lines);

/// <summary>
///     Port of types.ts interface MultiSelectState — the inline
///     <c>{ lineNumber, side } | null</c> startInfo shape becomes <see cref="MultiSelectStartInfo" />.
/// </summary>
public sealed record MultiSelectState(bool IsSelecting, MultiSelectStartInfo? StartInfo, MultiSelectRange? CurrentRange)
{
    /// <summary>JS: the initial <c>{ isSelecting: false, startInfo: null, currentRange: null }</c>.</summary>
    public static MultiSelectState Empty { get; } = new(false, null, null);
}

/// <summary>Port of types.ts inline startInfo shape <c>{ lineNumber: number; side: MultiSelectSide }</c>.</summary>
public sealed record MultiSelectStartInfo(int LineNumber, SplitSide Side);

/// <summary>
///     Port of types.ts / manager.ts preselected-lines shape <c>{ old?: number[]; new?: number[] }</c>
///     (setPreselectedLines parameter, visual.ts PreselectedLineType).
/// </summary>
public sealed record MultiSelectPreselectedLines(IReadOnlyList<int>? Old = null, IReadOnlyList<int>? New = null)
{
    public static MultiSelectPreselectedLines Empty { get; } = new();

    public bool IsEmpty => (Old == null || Old.Count == 0) && (New == null || New.Count == 0);
}
