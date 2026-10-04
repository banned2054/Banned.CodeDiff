using Banned.CodeDiff.Models;

namespace Banned.CodeDiff.Avalonia.Models;

/// <summary>
///     Payload of <see cref="Views.DiffView.SelectionChanged" /> — the upstream manager's
///     onSelectionChange(range, state) pair; the range is <c>null</c> when the selection was cleared.
/// </summary>
public sealed class DiffSelectionChangedEventArgs : EventArgs
{
    internal DiffSelectionChangedEventArgs(MultiSelectRange? range, MultiSelectState state)
    {
        Range = range;
        State = state;
    }

    /// <summary>Gets the current selection range, or <c>null</c> after a clear.</summary>
    public MultiSelectRange? Range { get; }

    /// <summary>Gets the selection state at the time of the change.</summary>
    public MultiSelectState State { get; }
}
