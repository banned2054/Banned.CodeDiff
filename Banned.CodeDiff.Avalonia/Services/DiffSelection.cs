using Banned.CodeDiff.Models;
using Banned.CodeDiff.Services;
using Banned.CodeDiff.Utils;

namespace Banned.CodeDiff.Avalonia.Services;

/// <summary>
///     多选状态机,移植自 multiSelect/manager.ts;指针命中与视觉更新由 <see cref="Views.DiffView" /> 负责。<br />
///     Selection state machine ported from multiSelect/manager.ts; <see cref="Views.DiffView" /> handles pointer targets and visual updates.
/// </summary>
internal sealed class DiffSelection
{
    private MultiSelectState _state = MultiSelectState.Empty;

    /// <summary>
    ///     对应 JS 的私有字段 #preselectedLines(供视觉处理阶段读取)。<br />JS: the private #preselectedLines field (read by the visual
    ///     pass).
    /// </summary>
    public MultiSelectPreselectedLines PreselectedLines { get; private set; } = MultiSelectPreselectedLines.Empty;

    /// <summary>
    ///     对应 options.onSelectionChange —— 携带当前范围(清除时为 <c>null</c>)与状态副本触发。<br />
    ///     JS: options.onSelectionChange — fires with the current range (or <c>null</c> on
    ///     clear) and the state copy.
    /// </summary>
    public event Action<MultiSelectRange?, MultiSelectState>? SelectionChanged;

    /// <summary>
    ///     对应 options.onSelectionComplete —— 携带结果触发;松开时没有范围则为 <c>null</c>。<br />
    ///     JS: options.onSelectionComplete — fires with the result, or <c>null</c> when the
    ///     release happened without a range.
    /// </summary>
    public event Action<MultiSelectResult?>? SelectionCompleted;

    /// <summary>
    ///     对应 JS 的 getState —— 当前状态(record 不可变,无需复制)。<br />JS: getState — the current state (records are immutable, so no
    ///     copy is needed).
    /// </summary>
    public MultiSelectState GetState()
    {
        return _state;
    }

    /// <summary>#handleMouseDown_Split 的移植,不含 DOM 解析。<br />Port of #handleMouseDown_Split minus the DOM resolution.</summary>
    public void HandlePointerPressed_Split(SplitSide side, int lineNumber)
    {
        _state = new MultiSelectState(true, new MultiSelectStartInfo(lineNumber, side),
                                      _state.CurrentRange);

        var range = new MultiSelectRange(side, lineNumber, lineNumber);

        _state = _state with { CurrentRange = range };

        // 视觉更新由所有者处理。
        SelectionChanged?.Invoke(range, _state);
    }

    /// <summary>
    ///     统一视图按下时优先使用新行号,不存在时使用旧行号。<br />
    ///     Unified press prefers the new line number, falling back to the old number.
    /// </summary>
    public void HandlePointerPressed_Unified((int? Old, int? New) lineNumbers)
    {
        var lineNumber = lineNumbers.New ?? lineNumbers.Old;

        if (lineNumber == null) return;

        var side = lineNumbers.New != null ? SplitSide.New : SplitSide.Old;

        _state = new MultiSelectState(true, new MultiSelectStartInfo(lineNumber.Value, side),
                                      _state.CurrentRange);

        var range = new MultiSelectRange(side, lineNumber.Value, lineNumber.Value);

        _state = _state with { CurrentRange = range };

        SelectionChanged?.Invoke(range, _state);
    }

    /// <summary>
    ///     #handleMouseOver_Split 的移植,不含 DOM 解析:范围始终保持在起始侧;悬停的行号
    ///     可来自任一侧的行号单元格。<br />
    ///     Port of #handleMouseOver_Split minus the DOM resolution: the range always stays on
    ///     the start side; the hovered line number may come from either side's number cell.
    /// </summary>
    public void HandlePointerMoved_Split(int lineNumber)
    {
        if (!_state.IsSelecting || _state.StartInfo == null) return;

        var range = new MultiSelectRange(_state.StartInfo.Side, _state.StartInfo.LineNumber, lineNumber);

        _state = _state with { CurrentRange = range };

        SelectionChanged?.Invoke(range, _state);
    }

    /// <summary>
    ///     统一视图拖选沿起始侧延伸;该侧无行号时忽略。<br />
    ///     Unified drag follows the starting side; rows without a number on that side are ignored.
    /// </summary>
    public void HandlePointerMoved_Unified((int? Old, int? New) lineNumbers)
    {
        if (!_state.IsSelecting || _state.StartInfo == null) return;

        var lineNumber = _state.StartInfo.Side == SplitSide.Old ? lineNumbers.Old : lineNumbers.New;

        if (lineNumber == null) return;

        var range = new MultiSelectRange(_state.StartInfo.Side, _state.StartInfo.LineNumber, lineNumber.Value);

        _state = _state with { CurrentRange = range };

        SelectionChanged?.Invoke(range, _state);
    }

    /// <summary>
    ///     #handleMouseUp 的移植——上游 document 级的 mouseup:归一化范围、结束选区并上报结果。<br />
    ///     Port of #handleMouseUp — the upstream document-level mouseup: normalizes the
    ///     range, ends the selection, and reports the result.
    /// </summary>
    public void HandlePointerReleased(DiffFile? diffFile, bool isUnifiedMode)
    {
        if (!_state.IsSelecting || _state.CurrentRange == null)
        {
            ResetState();
            return;
        }

        var normalizedRange = MultiSelectData.NormalizeRange(_state.CurrentRange);

        _state = _state with { CurrentRange = normalizedRange, IsSelecting = false };

        var result = GetSelectionResult(diffFile, isUnifiedMode);

        SelectionCompleted?.Invoke(result);
    }

    /// <summary>
    ///     getSelectionResult 的移植——归一化范围,加上 DiffFile 中当前模式下的行数据。<br />
    ///     Port of getSelectionResult — the normalized range plus the line data from
    ///     DiffFile for the current mode.
    /// </summary>
    public MultiSelectResult? GetSelectionResult(DiffFile? diffFile, bool isUnifiedMode)
    {
        var currentRange = _state.CurrentRange;

        if (currentRange == null || diffFile == null) return null;

        var range = MultiSelectData.NormalizeRange(currentRange);
        var lines = isUnifiedMode
            ? MultiSelectData.GetSelectedLinesFromDiffFile_Unified(diffFile, range)
            : MultiSelectData.GetSelectedLinesFromDiffFile_Split(diffFile, range);

        return new MultiSelectResult(range, lines);
    }

    /// <summary>
    ///     setPreselectedLines 的移植(JS 还会执行 #updateVisual——由所有者负责)。<br />Port of setPreselectedLines (JS also runs
    ///     #updateVisual — the owner does).
    /// </summary>
    public void SetPreselectedLines(MultiSelectPreselectedLines lines)
    {
        PreselectedLines = lines;
    }

    /// <summary>
    ///     重置交互状态并通知;预选行由所有者另行清除。<br />
    ///     Resets interaction state and notifies; the owner clears preselected lines separately.
    /// </summary>
    public void ClearSelection()
    {
        ResetState();

        SelectionChanged?.Invoke(null, _state);
    }

    /// <summary>#resetState 的移植。<br />Port of #resetState.</summary>
    public void ResetState()
    {
        _state = MultiSelectState.Empty;
    }
}
