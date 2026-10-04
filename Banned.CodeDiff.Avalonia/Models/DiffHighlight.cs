namespace Banned.CodeDiff.Avalonia.Models;

/// <summary>
///     行内文本中的一个词级高亮范围:[Start, Start + Length)。<br />
///     A word-level highlight range within a line's text: [Start, Start + Length).
/// </summary>
/// <param name="Start">范围的起始偏移(从 0 开始)。Zero-based start offset of the range.</param>
/// <param name="Length">范围的长度。Length of the range.</param>
public readonly record struct DiffHighlight(int Start, int Length);
