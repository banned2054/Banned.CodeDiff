using Banned.CodeDiff.Models;
using Banned.CodeDiff.Services;
using Banned.CodeDiff.Utils;

namespace Banned.CodeDiff.Avalonia.Services;

/// <summary>
/// Port of packages/core/src/multiSelect/manager.ts — the multi-select state machine. The JS class
/// binds container-level mouse events and resolves the DOM contract inline; here the owning
/// <see cref="Views.DiffView"/> translates pointer input into these calls after resolving the
/// DOM contract through <see cref="DiffSelectionDom"/>, so this class is pure state.
///
/// Deviations from the JS original (intentional, see Docs/CHANGELOG.md):
/// - the <c>scopeToHunk</c> hook is not ported (the upstream default is the identity function);
/// - the 16 ms <c>debounceUpdateVirtual</c> around the diffFile subscribe is a DOM batching
///   optimization — the owner recomputes the selection visual synchronously instead;
/// - <c>updateContainer</c>/<c>updateDiffFile</c>/<c>updateOptions</c>/<c>destroy</c> collapse to
///   the owner gating the feature: this object lives exactly as long as the view's selection
///   feature is enabled.
/// </summary>
internal sealed class DiffSelection
{
    private MultiSelectState _state = MultiSelectState.Empty;

    private MultiSelectPreselectedLines _preselectedLines = MultiSelectPreselectedLines.Empty;

    /// <summary>JS: options.onSelectionChange — fires with the current range (or <c>null</c> on
    /// clear) and the state copy.</summary>
    public event Action<MultiSelectRange?, MultiSelectState>? SelectionChanged;

    /// <summary>JS: options.onSelectionComplete — fires with the result, or <c>null</c> when the
    /// release happened without a range.</summary>
    public event Action<MultiSelectResult?>? SelectionCompleted;

    /// <summary>JS: getState — the current state (records are immutable, so no copy is needed).</summary>
    public MultiSelectState GetState() => _state;

    /// <summary>JS: the private #preselectedLines field (read by the visual pass).</summary>
    public MultiSelectPreselectedLines PreselectedLines => _preselectedLines;

    /// <summary>Port of #handleMouseDown_Split minus the DOM resolution.</summary>
    public void HandlePointerPressed_Split(SplitSide side, int lineNumber)
    {
        _state = new MultiSelectState(IsSelecting: true, StartInfo: new MultiSelectStartInfo(lineNumber, side),
                                      CurrentRange: _state.CurrentRange);

        var range = new MultiSelectRange(side, lineNumber, lineNumber);

        // JS applies options.scopeToHunk here — not ported (identity upstream).
        _state = _state with { CurrentRange = range };

        // JS: #updateVisual(); onSelectionChange(range, { ...state }) — the owner applies the
        // visual through the event.
        SelectionChanged?.Invoke(range, _state);
    }

    /// <summary>Port of #handleMouseDown_Unified minus the DOM resolution: the line number is the
    /// new number when present, else the old one; the side follows which number exists.</summary>
    public void HandlePointerPressed_Unified((int? Old, int? New) lineNumbers)
    {
        var lineNumber = lineNumbers.New ?? lineNumbers.Old;

        if (lineNumber == null)
        {
            return;
        }

        var side = lineNumbers.New != null ? SplitSide.New : SplitSide.Old;

        _state = new MultiSelectState(IsSelecting: true, StartInfo: new MultiSelectStartInfo(lineNumber.Value, side),
                                      CurrentRange: _state.CurrentRange);

        var range = new MultiSelectRange(side, lineNumber.Value, lineNumber.Value);

        _state = _state with { CurrentRange = range };

        SelectionChanged?.Invoke(range, _state);
    }

    /// <summary>Port of #handleMouseOver_Split minus the DOM resolution: the range always stays on
    /// the start side; the hovered line number may come from either side's number cell.</summary>
    public void HandlePointerMoved_Split(int lineNumber)
    {
        if (!_state.IsSelecting || _state.StartInfo == null)
        {
            return;
        }

        var range = new MultiSelectRange(_state.StartInfo.Side, _state.StartInfo.LineNumber, lineNumber);

        _state = _state with { CurrentRange = range };

        SelectionChanged?.Invoke(range, _state);
    }

    /// <summary>Port of #handleMouseOver_Unified minus the DOM resolution: the tracked line number
    /// is the one on the start side — rows without a number there (upstream <c>undefined</c>) do
    /// not extend the range.</summary>
    public void HandlePointerMoved_Unified((int? Old, int? New) lineNumbers)
    {
        if (!_state.IsSelecting || _state.StartInfo == null)
        {
            return;
        }

        var lineNumber = _state.StartInfo.Side == SplitSide.Old ? lineNumbers.Old : lineNumbers.New;

        if (lineNumber == null)
        {
            return;
        }

        var range = new MultiSelectRange(_state.StartInfo.Side, _state.StartInfo.LineNumber, lineNumber.Value);

        _state = _state with { CurrentRange = range };

        SelectionChanged?.Invoke(range, _state);
    }

    /// <summary>Port of #handleMouseUp — the upstream document-level mouseup: normalizes the
    /// range, ends the selection, and reports the result.</summary>
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

    /// <summary>Port of getSelectionResult — the normalized range plus the line data from
    /// DiffFile for the current mode.</summary>
    public MultiSelectResult? GetSelectionResult(DiffFile? diffFile, bool isUnifiedMode)
    {
        var currentRange = _state.CurrentRange;

        if (currentRange == null || diffFile == null)
        {
            return null;
        }

        var range = MultiSelectData.NormalizeRange(currentRange);
        var lines = isUnifiedMode
            ? MultiSelectData.GetSelectedLinesFromDiffFile_Unified(diffFile, range)
            : MultiSelectData.GetSelectedLinesFromDiffFile_Split(diffFile, range);

        return new MultiSelectResult(range, lines);
    }

    /// <summary>Port of setPreselectedLines (JS also runs #updateVisual — the owner does).</summary>
    public void SetPreselectedLines(MultiSelectPreselectedLines lines)
    {
        _preselectedLines = lines;
    }

    /// <summary>Port of clearSelection — resets the state and notifies. The JS original keeps
    /// #preselectedLines (the comment-anchored channel); this port persists completed selections
    /// through that same channel, so the owning view clears it as well (see DiffView.ClearSelection).</summary>
    public void ClearSelection()
    {
        ResetState();

        // JS: #updateVisual(); onSelectionChange(null, { ...state })
        SelectionChanged?.Invoke(null, _state);
    }

    /// <summary>Port of #resetState.</summary>
    public void ResetState()
    {
        _state = MultiSelectState.Empty;
    }
}
