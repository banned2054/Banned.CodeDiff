namespace Banned.CodeDiff.Models;

// Port of packages/core/src/multiSelect/types.ts.
//
// Deviations from the JS original (intentional):
// - The JS side type is the string union "old" | "new" (MultiSelectSide); the C# port reuses the
//   SplitSide enum already used by the rest of the core (data.ts converts between the two anyway).
// - The JS LineRange interface is renamed to MultiSelectRange: "LineRange" is already taken by the
//   change-range.ts port in ChangeRangeModels.cs.

/// <summary>types.ts 接口 LineRange 的移植(已改名,见文件头注释)。<br />Port of types.ts interface LineRange (renamed, see file header).</summary>
/// <param name="Side">选区所在侧(旧侧/新侧)。<br />The side of the range (old / new).</param>
/// <param name="StartLineNumber">起始行号(1 基,含)。<br />Start line number (1-based, inclusive).</param>
/// <param name="EndLineNumber">结束行号(1 基,含)。<br />End line number (1-based, inclusive).</param>
public sealed record MultiSelectRange(SplitSide Side, int StartLineNumber, int EndLineNumber);

/// <summary>
///     types.ts 接口 SelectedLine 的移植。JS 的可选字段在此成为普通成员:数据层
///     (multiSelect/data.ts)总是赋值,实践中不会出现 `undefined`。<br />
///     Port of types.ts interface SelectedLine. The JS optional fields become plain members: the data
///     layer (multiSelect/data.ts) always assigns them, so `undefined` never occurs in practice.
/// </summary>
/// <param name="Index">
///     该行在所在侧分栏/统一模型中的位置(1 基)(JS 原版为 <c>getSplitLineIndexByLineNumber + 1</c>)。<br />
///     1-based position of the line in its side's split/unified model
///     (<c>getSplitLineIndexByLineNumber + 1</c> in the JS original).
/// </param>
/// <param name="LineNumber">该行的行号(1 基)。<br />The line number (1-based).</param>
/// <param name="Value">
///     该行的原始文本(<c>SplitLineItem.value</c> / <c>UnifiedLineItem.value</c>)。<br />The raw line value (
///     <c>SplitLineItem.value</c> / <c>UnifiedLineItem.value</c>).
/// </param>
/// <param name="IsHide">该行当前是否被折叠的 hunk 遮挡。<br />Whether the line is currently hidden behind a collapsed hunk.</param>
/// <param name="IsDelete">
///     是否为删除行:<c>diff?.type === DiffLineType.Delete</c>。<br /><c>diff?.type === DiffLineType.Delete</c>
///     .
/// </param>
/// <param name="IsAdd">是否为新增行:<c>diff?.type === DiffLineType.Add</c>。<br /><c>diff?.type === DiffLineType.Add</c>.</param>
/// <param name="IsContext">
///     <c>diff?.type === DiffLineType.Context || diff?.type === undefined</c> —— 缺失 DiffLine
///     (hunk 展开揭示的原始行)按上下文行处理,复刻 JS 的怪癖。<br />
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

/// <summary>types.ts 接口 MultiSelectResult 的移植。<br />Port of types.ts interface MultiSelectResult.</summary>
/// <param name="Range">选中的行号区间。<br />The selected line range.</param>
/// <param name="Lines">选中的行列表。<br />The selected lines.</param>
public sealed record MultiSelectResult(MultiSelectRange Range, IReadOnlyList<SelectedLine> Lines);

/// <summary>
///     types.ts 接口 MultiSelectState 的移植——内联的 <c>{ lineNumber, side } | null</c>
///     startInfo 形态改为 <see cref="MultiSelectStartInfo" />。<br />
///     Port of types.ts interface MultiSelectState — the inline
///     <c>{ lineNumber, side } | null</c> startInfo shape becomes <see cref="MultiSelectStartInfo" />.
/// </summary>
/// <param name="IsSelecting">是否正在进行选择。<br />Whether a selection is in progress.</param>
/// <param name="StartInfo">选区起点;<c>null</c> 表示尚未开始。<br />The selection start; <c>null</c> when not started.</param>
/// <param name="CurrentRange">当前拖拽出的选区;<c>null</c> 表示尚未确定。<br />The range being dragged out; <c>null</c> until determined.</param>
public sealed record MultiSelectState(bool IsSelecting, MultiSelectStartInfo? StartInfo, MultiSelectRange? CurrentRange)
{
    /// <summary>
    ///     JS 的初始 <c>{ isSelecting: false, startInfo: null, currentRange: null }</c>。<br />JS: the initial
    ///     <c>{ isSelecting: false, startInfo: null, currentRange: null }</c>.
    /// </summary>
    public static MultiSelectState Empty { get; } = new(false, null, null);
}

/// <summary>
///     types.ts 内联 startInfo 形态 <c>{ lineNumber: number; side: MultiSelectSide }</c> 的移植。<br />Port of types.ts
///     inline startInfo shape <c>{ lineNumber: number; side: MultiSelectSide }</c>.
/// </summary>
/// <param name="LineNumber">起点行号(1 基)。<br />Start line number (1-based).</param>
/// <param name="Side">起点所在侧。<br />The side of the start.</param>
public sealed record MultiSelectStartInfo(int LineNumber, SplitSide Side);

/// <summary>
///     types.ts / manager.ts 预选行形态 <c>{ old?: number[]; new?: number[] }</c> 的移植
///     (setPreselectedLines 参数,visual.ts PreselectedLineType)。<br />
///     Port of types.ts / manager.ts preselected-lines shape <c>{ old?: number[]; new?: number[] }</c>
///     (setPreselectedLines parameter, visual.ts PreselectedLineType).
/// </summary>
/// <param name="Old">
///     旧侧的预选行号列表(1 基);<c>null</c> 或空表示无。<br />Preselected old-side line numbers (1-based); <c>null</c> or
///     empty means none.
/// </param>
/// <param name="New">
///     新侧的预选行号列表(1 基);<c>null</c> 或空表示无。<br />Preselected new-side line numbers (1-based); <c>null</c> or
///     empty means none.
/// </param>
public sealed record MultiSelectPreselectedLines(IReadOnlyList<int>? Old = null, IReadOnlyList<int>? New = null)
{
    /// <summary>空预选集(两侧均无预选行)。<br />The empty preselection (no preselected lines on either side).</summary>
    public static MultiSelectPreselectedLines Empty { get; } = new();

    /// <summary>两侧均无预选行时为 <c>true</c>。<br />Whether neither side has preselected lines.</summary>
    public bool IsEmpty => (Old == null || Old.Count == 0) && (New == null || New.Count == 0);
}
