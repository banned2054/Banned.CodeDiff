using Banned.CodeDiff.Models;

namespace Banned.CodeDiff.Avalonia.Models;

/// <summary>
///     <see cref="Views.DiffView.SelectionChanged" /> 事件的载荷 — 对应上游管理器的
///     onSelectionChange(range, state) 回调对;选区被清除时范围为 <c>null</c>。<br />
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

    /// <summary>获取当前选区;选区被清除后为 <c>null</c>。<br />Gets the current selection range, or <c>null</c> after a clear.</summary>
    public MultiSelectRange? Range { get; }

    /// <summary>获取变更发生时的选区状态。<br />Gets the selection state at the time of the change.</summary>
    public MultiSelectState State { get; }
}
