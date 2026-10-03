namespace Banned.CodeDiff.Avalonia.Models;

/// <summary>Display mode of <see cref="Views.DiffView"/>.</summary>
public enum DiffViewMode
{
    /// <summary>Two-column split view: old file on the left, new file on the right.</summary>
    Split = 0,

    /// <summary>Single-column unified view: deleted lines above the added ones, dual line numbers.</summary>
    Unified = 1,
}
