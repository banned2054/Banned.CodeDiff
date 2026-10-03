namespace Banned.CodeDiff.Avalonia.Models;

/// <summary>A word-level highlight range within a line's text: [Start, Start + Length).</summary>
public readonly record struct DiffHighlight(int Start, int Length);
