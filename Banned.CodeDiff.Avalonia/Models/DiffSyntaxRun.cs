using Avalonia.Media;

namespace Banned.CodeDiff.Avalonia.Models;

/// <summary>
///     One syntax-colored text segment of a diff content line: the range within the
///     displayed (newline-trimmed) line text and its resolved foreground brush.
/// </summary>
public sealed record DiffSyntaxRun(int Start, int Length, IBrush Foreground);
