using Banned.CodeDiff.Models;

namespace Banned.CodeDiff.Avalonia.Models;

/// <summary>
///     Payload of <see cref="Views.DiffView.SelectionCompleted" /> — the upstream manager's
///     onSelectionComplete(result); the result is <c>null</c> when the release happened without a range.
/// </summary>
public sealed class DiffSelectionCompletedEventArgs : EventArgs
{
    internal DiffSelectionCompletedEventArgs(MultiSelectResult? result)
    {
        Result = result;
    }

    /// <summary>Gets the completed selection (normalized range plus the line data), or <c>null</c>.</summary>
    public MultiSelectResult? Result { get; }
}
