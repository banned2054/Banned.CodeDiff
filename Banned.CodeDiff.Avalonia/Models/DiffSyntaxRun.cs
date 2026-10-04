using Avalonia.Media;

namespace Banned.CodeDiff.Avalonia.Models;

/// <summary>
///     diff 内容行中的一个语法着色区间:显示文本(去除换行符后)中的范围及其
///     解析出的前景画刷。<br />
///     One syntax-colored text segment of a diff content line: the range within the
///     displayed (newline-trimmed) line text and its resolved foreground brush.
/// </summary>
/// <param name="Start">区间在行文本中的起始偏移(从 0 开始)。Zero-based start offset within the line text.</param>
/// <param name="Length">区间的长度。Length of the segment.</param>
/// <param name="Foreground">区间的前景画刷。Foreground brush of the segment.</param>
public sealed record DiffSyntaxRun(int Start, int Length, IBrush Foreground);
