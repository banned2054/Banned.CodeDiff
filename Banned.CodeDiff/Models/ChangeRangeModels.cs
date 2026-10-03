using Banned.CodeDiff.Utils;

namespace Banned.CodeDiff.Models;

/// <summary>Port of change-range.ts IRange["range"] — a {location, length} pair.</summary>
public readonly record struct TextRange(int Location, int Length);

/// <summary>Port of change-range.ts IRange (relativeChanges result).</summary>
public sealed class LineRange
{
    public TextRange      Range         { get; set; }
    public bool?          HasLineChange { get; set; }
    public NewLineSymbol? NewLineSymbol { get; set; }
}

/// <summary>Port of change-range.ts DiffRange["range"] element.</summary>
public sealed record DiffItem(int Type, string Str, int StartIndex, int EndIndex, int Length);

/// <summary>Port of change-range.ts DiffRange (diffChanges result).</summary>
public sealed class DiffRange
{
    public IReadOnlyList<DiffItem> Range         { get; set; } = [];
    public bool?                   HasLineChange { get; set; }
    public NewLineSymbol?          NewLineSymbol { get; set; }
}
