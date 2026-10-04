using Banned.CodeDiff.Models;

namespace Banned.CodeDiff.Avalonia.Models;

/// <summary>
///     <see cref="Views.DiffView.SelectionCompleted" /> 事件的载荷 — 对应上游管理器的
///     onSelectionComplete(result) 回调;松开时没有选区则结果为 <c>null</c>。<br />
///     Payload of <see cref="Views.DiffView.SelectionCompleted" /> — the upstream manager's
///     onSelectionComplete(result); the result is <c>null</c> when the release happened without a range.
/// </summary>
public sealed class DiffSelectionCompletedEventArgs : EventArgs
{
    internal DiffSelectionCompletedEventArgs(MultiSelectResult? result)
    {
        Result = result;
    }

    /// <summary>
    ///     获取已完成的选区(归一化范围及行数据);没有选区时为 <c>null</c>。<br />
    ///     Gets the completed selection (normalized range plus the line data), or <c>null</c>.
    /// </summary>
    public MultiSelectResult? Result { get; }
}
