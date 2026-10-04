using Banned.CodeDiff.Models;
using Banned.CodeDiff.Services;
using Banned.CodeDiff.Utils;

namespace Banned.CodeDiff.Avalonia.Services;

/// <summary>
///     multiSelect/manager.ts 的移植——多选状态机。JS 版在容器上绑定鼠标事件并内联解析
///     DOM 契约;此处由持有它的 <see cref="Views.DiffView" /> 先经 <see cref="DiffSelectionDom" />
///     解析 DOM 契约,再把指针输入翻译为对它的调用,因此本类只承载纯状态。
///     与 JS 原版的差异(有意为之,见 Docs/CHANGELOG.md):
///     - 未移植 <c>scopeToHunk</c> 钩子(上游默认即恒等函数);
///     - diffFile 订阅外围 16 ms 的 <c>debounceUpdateVirtual</c> 是一种 DOM 批处理优化——
///     改由所有者在事件中同步重算选区视觉;
///     - <c>updateContainer</c>/<c>updateDiffFile</c>/<c>updateOptions</c>/<c>destroy</c> 归结为
///     所有者对该功能的启用门控:本对象的生命周期与视图多选功能的启用期严格一致。<br />
///     Port of packages/core/src/multiSelect/manager.ts — the multi-select state machine. The JS class
///     binds container-level mouse events and resolves the DOM contract inline; here the owning
///     <see cref="Views.DiffView" /> translates pointer input into these calls after resolving the
///     DOM contract through <see cref="DiffSelectionDom" />, so this class is pure state.
///     Deviations from the JS original (intentional, see Docs/CHANGELOG.md):
///     - the <c>scopeToHunk</c> hook is not ported (the upstream default is the identity function);
///     - the 16 ms <c>debounceUpdateVirtual</c> around the diffFile subscribe is a DOM batching
///     optimization — the owner recomputes the selection visual synchronously instead;
///     - <c>updateContainer</c>/<c>updateDiffFile</c>/<c>updateOptions</c>/<c>destroy</c> collapse to
///     the owner gating the feature: this object lives exactly as long as the view's selection
///     feature is enabled.
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

        // JS applies options.scopeToHunk here — not ported (identity upstream).
        _state = _state with { CurrentRange = range };

        // JS: #updateVisual(); onSelectionChange(range, { ...state }) — the owner applies the
        // visual through the event.
        SelectionChanged?.Invoke(range, _state);
    }

    /// <summary>
    ///     #handleMouseDown_Unified 的移植,不含 DOM 解析:行号优先取新行号,缺省时取旧行号;
    ///     侧别随存在的那一个行号而定。<br />
    ///     Port of #handleMouseDown_Unified minus the DOM resolution: the line number is the
    ///     new number when present, else the old one; the side follows which number exists.
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
    ///     #handleMouseOver_Unified 的移植,不含 DOM 解析:跟踪的是起始侧的行号——起始侧
    ///     没有行号的行(上游 <c>undefined</c>)不延伸范围。<br />
    ///     Port of #handleMouseOver_Unified minus the DOM resolution: the tracked line number
    ///     is the one on the start side — rows without a number there (upstream <c>undefined</c>) do
    ///     not extend the range.
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
    ///     clearSelection 的移植——重置状态并通知。JS 原版会保留 #preselectedLines(承载评论
    ///     锚点的通道);本移植经由同一通道持久化已完成的选区,因此所有者视图会将其一并清除
    ///     (见 DiffView.ClearSelection)。<br />
    ///     Port of clearSelection — resets the state and notifies. The JS original keeps
    ///     #preselectedLines (the comment-anchored channel); this port persists completed selections
    ///     through that same channel, so the owning view clears it as well (see DiffView.ClearSelection).
    /// </summary>
    public void ClearSelection()
    {
        ResetState();

        // JS: #updateVisual(); onSelectionChange(null, { ...state })
        SelectionChanged?.Invoke(null, _state);
    }

    /// <summary>#resetState 的移植。<br />Port of #resetState.</summary>
    public void ResetState()
    {
        _state = MultiSelectState.Empty;
    }
}
