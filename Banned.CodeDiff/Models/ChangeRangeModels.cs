using Banned.CodeDiff.Services;
using Banned.CodeDiff.Utils;

namespace Banned.CodeDiff.Models;

/// <summary>
///     change-range.ts IRange["range"] 的移植:一个 {location, length} 数对。<br />Port of change-range.ts IRange["range"] — a
///     {location, length} pair.
/// </summary>
/// <param name="Location">范围的起始位置。<br />The starting location of the range.</param>
/// <param name="Length">范围的长度。<br />The length of the range.</param>
public readonly record struct TextRange(int Location, int Length);

/// <summary>change-range.ts IRange(relativeChanges 结果)的移植。<br />Port of change-range.ts IRange (relativeChanges result).</summary>
public sealed class LineRange
{
    /// <summary>
    ///     本行相对对侧行的变化子串范围(起始位置 + 长度)。<br />The changed substring of this line relative to its counterpart (location +
    ///     length).
    /// </summary>
    public TextRange Range { get; set; }

    /// <summary>
    ///     去掉变化子串后本行是否仍有非空白内容;<c>null</c> 对应 JS 中未赋值的可选字段。<br />Whether the line still has non-whitespace content outside
    ///     the changed substring; <c>null</c> mirrors the absent optional field in JS.
    /// </summary>
    public bool? HasLineChange { get; set; }

    /// <summary>
    ///     检测到的行尾换行符差异;两侧一致时为 <c>null</c>(JS 未赋值)。<br />The detected end-of-line newline-symbol change; <c>null</c> when
    ///     both sides agree (absent in JS).
    /// </summary>
    public NewLineSymbol? NewLineSymbol { get; set; }
}

/// <summary>
///     change-range.ts DiffRange["range"] 元素的移植;<c>Type</c> 为 fast-diff 操作码。<br />Port of change-range.ts
///     DiffRange["range"] element; <c>Type</c> is the fast-diff op code.
/// </summary>
/// <param name="Type">fast-diff 操作码。<br />The fast-diff op code.</param>
/// <param name="Str">文本段内容。<br />The segment text.</param>
/// <param name="StartIndex">文本段在本侧行文本中的起始索引。<br />Start index within the side's line text.</param>
/// <param name="EndIndex">文本段的结束索引(含)。<br />End index of the segment (inclusive).</param>
/// <param name="Length">文本段长度。<br />Segment length.</param>
public sealed record DiffItem(DiffOp Type, string Str, int StartIndex, int EndIndex, int Length);

/// <summary>change-range.ts DiffRange(diffChanges 结果)的移植。<br />Port of change-range.ts DiffRange (diffChanges result).</summary>
public sealed class DiffRange
{
    /// <summary>本侧行的 fast-diff 文本段列表。<br />The fast-diff segments of this side's line.</summary>
    public IReadOnlyList<DiffItem> Range { get; set; } = [];

    /// <summary>
    ///     本行与对侧是否存在非空白的公共文本段(JS 原版按 add 侧判定);<c>null</c> 对应 JS 中未赋值的可选字段。<br />Whether the line shares non-whitespace
    ///     common segments with its counterpart (JS judges on the add side); <c>null</c> mirrors the absent optional field in
    ///     JS.
    /// </summary>
    public bool? HasLineChange { get; set; }

    /// <summary>
    ///     检测到的行尾换行符差异;两侧一致时为 <c>null</c>(JS 未赋值)。<br />The detected end-of-line newline-symbol change; <c>null</c> when
    ///     both sides agree (absent in JS).
    /// </summary>
    public NewLineSymbol? NewLineSymbol { get; set; }
}
