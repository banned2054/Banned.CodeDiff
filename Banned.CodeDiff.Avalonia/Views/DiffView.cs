using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Input.Platform;
using Avalonia.Media;
using Banned.CodeDiff.Avalonia.Models;
using Banned.CodeDiff.Avalonia.Services;
using Banned.CodeDiff.Avalonia.Utils;
using Banned.CodeDiff.Models;
using Banned.CodeDiff.Services;
using Banned.CodeDiff.Utils;
using System.Windows.Input;

namespace Banned.CodeDiff.Avalonia.Views;

/// <summary>
///     Renders a <see cref="T:Banned.CodeDiff.Services.DiffFile" /> as a read-only, GitHub-style diff
///     view with line-level add/delete background colors — either split (two columns, default) or
///     unified (single column with dual line numbers, deleted lines above the added ones). Assign a
///     <see cref="DiffFile" /> (built or not — <c>Init</c> and the <c>Build*DiffLines</c> calls are
///     idempotent and invoked on demand) and the view keeps itself in sync through the model's
///     <c>Updated</c> event.
/// </summary>
public sealed class DiffView : TemplatedControl
{
    /// <summary>Minimum width of a line-number column, matching the upstream aside width.</summary>
    private const double NumberColumnMinWidth = 40;

    /// <summary>Horizontal padding of a line-number column (10px on each side upstream).</summary>
    private const double NumberColumnPadding = 20;

    /// <summary>Approximate advance width of one monospace digit relative to the font size.</summary>
    private const double MonospaceCharWidthRatio = 0.62;

    /// <summary>Identifies the <see cref="DiffFile" /> dependency property.</summary>
    public static readonly StyledProperty<DiffFile?> DiffFileProperty =
        AvaloniaProperty.Register<DiffView, DiffFile?>(nameof(DiffFile));

    /// <summary>Identifies the <see cref="ViewMode" /> dependency property.</summary>
    public static readonly StyledProperty<DiffViewMode> ViewModeProperty =
        AvaloniaProperty.Register<DiffView, DiffViewMode>(nameof(ViewMode));

    /// <summary>Identifies the <see cref="SyntaxHighlight" /> dependency property.</summary>
    public static readonly StyledProperty<bool> SyntaxHighlightProperty =
        AvaloniaProperty.Register<DiffView, bool>(nameof(SyntaxHighlight), true);

    /// <summary>Identifies the <see cref="IsSelectionEnabled" /> dependency property.</summary>
    public static readonly StyledProperty<bool> IsSelectionEnabledProperty =
        AvaloniaProperty.Register<DiffView, bool>(nameof(IsSelectionEnabled));

    /// <summary>Identifies the <see cref="Wrap" /> dependency property.</summary>
    public static readonly StyledProperty<bool> WrapProperty =
        AvaloniaProperty.Register<DiffView, bool>(nameof(Wrap));

    /// <summary>Identifies the <see cref="Highlighter" /> dependency property.</summary>
    public static readonly StyledProperty<IDiffHighlighter?> HighlighterProperty =
        AvaloniaProperty.Register<DiffView, IDiffHighlighter?>(nameof(Highlighter));

    /// <summary>Identifies the <see cref="Rows" /> direct property.</summary>
    public static readonly DirectProperty<DiffView, IReadOnlyList<DiffRow>> RowsProperty =
        AvaloniaProperty.RegisterDirect<DiffView, IReadOnlyList<DiffRow>>(nameof(Rows), o => o.Rows);

    /// <summary>Identifies the <see cref="NumberColumnWidth" /> direct property.</summary>
    public static readonly DirectProperty<DiffView, double> NumberColumnWidthProperty =
        AvaloniaProperty.RegisterDirect<DiffView, double>(nameof(NumberColumnWidth), o => o.NumberColumnWidth);

    /// <summary>The three copy commands, kept for CanExecuteChanged invalidation.</summary>
    private readonly List<DiffCopyCommand> _copyCommands;

    /// <summary>
    ///     Rows/cells currently flagged selected — cleared first on every visual pass
    ///     (upstream removes the CSS class from every row before re-applying ranges).
    /// </summary>
    private readonly List<DiffSplitCellModel> _selectedSplitCells = [];

    private readonly List<DiffUnifiedContentRow> _selectedUnifiedRows = [];

    /// <summary>The multi-select state machine (upstream multiSelect/manager.ts).</summary>
    private readonly DiffSelection _selection = new();

    private ItemsControl? _items;
    private double        _numberColumnWidth = NumberColumnMinWidth;
    private Vector?       _pendingExpandOffset;
    private bool          _rebuilding;

    private IReadOnlyList<DiffRow> _rows = [];
    private ScrollViewer?          _scrollViewer;

    /// <summary>
    ///     LineIndex → content row map for the current <see cref="Rows" /> (built lazily,
    ///     dropped on every rebuild): the split visual pass used to scan all rows per selected line
    ///     (O(lines × rows) per pointer move); the map keeps it O(1) per line.
    /// </summary>
    private Dictionary<int, DiffSplitContentRow>? _splitRowsByLineIndex;

    /// <summary>Initializes a new instance of the <see cref="DiffView" /> class.</summary>
    public DiffView()
    {
        // SetCurrentValue keeps these overridable by styles and inherited values.
        SetCurrentValue(FontFamilyProperty, new FontFamily("Menlo, Consolas, monospace"));
        SetCurrentValue(FontSizeProperty, 14.0);

        ExpandHunkUpCommand   = new HunkExpandCommand(this, HunkExpandDirection.Up);
        ExpandHunkDownCommand = new HunkExpandCommand(this, HunkExpandDirection.Down);
        ExpandHunkAllCommand  = new HunkExpandCommand(this, HunkExpandDirection.All);

        var copySelection = new DiffCopyCommand(this, static v => v.CanCopySelection(),
                                                static v => v.CopySelectionAsync());
        var copyOldFile = new DiffCopyCommand(this, static v => v.DiffFile?.OldFileRaw != null,
                                              static v => v.CopyOldFileAsync());
        var copyNewFile = new DiffCopyCommand(this, static v => v.DiffFile?.NewFileRaw != null,
                                              static v => v.CopyNewFileAsync());

        CopySelectionCommand = copySelection;
        CopyOldFileCommand   = copyOldFile;
        CopyNewFileCommand   = copyNewFile;

        _copyCommands = [copySelection, copyOldFile, copyNewFile];

        // Brushes are baked into the rows; switch palette by rebuilding on theme changes.
        ActualThemeVariantChanged += (_, _) => RebuildRows();

        _selection.SelectionChanged   += OnSelectionChanged;
        _selection.SelectionCompleted += OnSelectionCompleted;
    }

    /// <summary>Gets or sets the diff model to render. <c>null</c> clears the view.</summary>
    public DiffFile? DiffFile
    {
        get => GetValue(DiffFileProperty);
        set => SetValue(DiffFileProperty, value);
    }

    /// <summary>Gets or sets the display mode (split or unified).</summary>
    public DiffViewMode ViewMode
    {
        get => GetValue(ViewModeProperty);
        set => SetValue(ViewModeProperty, value);
    }

    /// <summary>
    ///     Gets or sets whether the model runs syntax highlighting
    ///     (<c>DiffFile.InitSyntax</c>) before rendering; the built-in TextMate engine
    ///     colors lines when the language is registered.
    /// </summary>
    public bool SyntaxHighlight
    {
        get => GetValue(SyntaxHighlightProperty);
        set => SetValue(SyntaxHighlightProperty, value);
    }

    /// <summary>
    ///     Gets or sets the syntax engine passed to <c>InitSyntax</c>; <c>null</c> uses
    ///     the core library's built-in TextMate engine (upstream: registerHighlighter).
    /// </summary>
    public IDiffHighlighter? Highlighter
    {
        get => GetValue(HighlighterProperty);
        set => SetValue(HighlighterProperty, value);
    }

    /// <summary>
    ///     Gets or sets a value indicating whether long lines wrap at the view width (the upstream
    ///     diffViewWrap prop: white-space pre-wrap, word breaks at the line edge) instead of
    ///     rendering one horizontally clipped line. Defaults to <c>false</c> — the upstream wrappers
    ///     default it to on, but an opt-in keeps existing hosts' row layout (one line tall)
    ///     unchanged. Split rows keep both sides the height of the taller one; line-number columns
    ///     never wrap.
    /// </summary>
    public bool Wrap
    {
        get => GetValue(WrapProperty);
        set => SetValue(WrapProperty, value);
    }

    /// <summary>
    ///     Gets or sets whether dragging over line-number cells selects line ranges (the upstream
    ///     multiSelect feature). Defaults to <c>false</c> — the upstream wrappers default it to on,
    ///     but an opt-in keeps existing hosts' pointer behavior unchanged.
    /// </summary>
    public bool IsSelectionEnabled
    {
        get => GetValue(IsSelectionEnabledProperty);
        set => SetValue(IsSelectionEnabledProperty, value);
    }

    /// <summary>Gets the flat row list currently rendered (content rows and hunk placeholders).</summary>
    public IReadOnlyList<DiffRow> Rows => _rows;

    /// <summary>Gets the resolved line-number column width for the current rows and font size.</summary>
    public double NumberColumnWidth => _numberColumnWidth;

    /// <summary>
    ///     Gets the command that expands a hunk row up by the compose length (40 lines);
    ///     the command parameter is the <see cref="DiffSplitHunkRow" /> or <see cref="DiffUnifiedHunkRow" />.
    /// </summary>
    public ICommand ExpandHunkUpCommand { get; }

    /// <summary>
    ///     Gets the command that expands a hunk row down by the compose length (40 lines);
    ///     the command parameter is the <see cref="DiffSplitHunkRow" /> or <see cref="DiffUnifiedHunkRow" />.
    /// </summary>
    public ICommand ExpandHunkDownCommand { get; }

    /// <summary>
    ///     Gets the command that fully expands a hunk row; the command parameter is the
    ///     <see cref="DiffSplitHunkRow" /> or <see cref="DiffUnifiedHunkRow" />.
    /// </summary>
    public ICommand ExpandHunkAllCommand { get; }

    /// <summary>
    ///     Gets the command that copies the current selection's plain text
    ///     (<see cref="CopySelectionAsync" />). Native port addition — the upstream library has no
    ///     copy feature. CanExecute is false without a selection or when every selected line is
    ///     hidden behind a collapsed hunk; no keyboard shortcut is built in (hosts bind their own to
    ///     avoid clashing with the host's bindings).
    /// </summary>
    public ICommand CopySelectionCommand { get; }

    /// <summary>
    ///     Gets the command that copies the whole old-side file content
    ///     (<see cref="CopyOldFileAsync" />); independent of any selection. Native port addition.
    /// </summary>
    public ICommand CopyOldFileCommand { get; }

    /// <summary>
    ///     Gets the command that copies the whole new-side file content
    ///     (<see cref="CopyNewFileAsync" />); independent of any selection. Native port addition.
    /// </summary>
    public ICommand CopyNewFileCommand { get; }

    /// <summary>
    ///     Occurs while a selection drag moves and when the selection is cleared — the upstream
    ///     manager's onSelectionChange. The range is <c>null</c> on clear.
    /// </summary>
    public event EventHandler<DiffSelectionChangedEventArgs>? SelectionChanged;

    /// <summary>
    ///     Occurs when a selection drag is released — the upstream manager's onSelectionComplete
    ///     surfaced through the React wrapper's onMultiSelectComplete. The result is <c>null</c>
    ///     when the release happened without a range.
    /// </summary>
    public event EventHandler<DiffSelectionCompletedEventArgs>? SelectionCompleted;

    /// <summary>
    ///     Gets the current selection result (normalized range plus line data) — during a drag it
    ///     reflects the live range; after a release it keeps returning the completed range until the
    ///     next interaction clears it (upstream manager semantics). Returns <c>null</c> without a range.
    /// </summary>
    public MultiSelectResult? GetSelectionResult()
    {
        return _selection.GetSelectionResult(DiffFile, ViewMode == DiffViewMode.Unified);
    }

    /// <summary>Gets the current selection state (upstream manager getState).</summary>
    public MultiSelectState GetSelectionState()
    {
        return _selection.GetState();
    }

    /// <summary>
    ///     Clears the selection: the interactive state and the persisted completion highlight.
    ///     Upstream difference: the JS manager's clearSelection keeps #preselectedLines (the
    ///     comment-anchored channel this port also uses to persist completed selections), so a clear
    ///     that kept them would never remove the highlight — both channels are cleared here.
    /// </summary>
    public void ClearSelection()
    {
        _selection.ClearSelection();
        _selection.SetPreselectedLines(MultiSelectPreselectedLines.Empty);
        ApplySelectionVisual();
    }

    /// <summary>
    ///     Sets preselected lines (e.g. from existing annotations). Each side's list merges into one
    ///     big min/max range — the upstream-known semantics (visual.ts changePreselectedLinesToLineRange):
    ///     a scattered list highlights everything between its min and max.
    /// </summary>
    public void SetPreselectedLines(IReadOnlyList<int>? oldLines = null, IReadOnlyList<int>? newLines = null)
    {
        _selection.SetPreselectedLines(new MultiSelectPreselectedLines(oldLines, newLines));
        ApplySelectionVisual();
    }

    // ---- copy (native port feature — the upstream library has no copy counterpart) ----

    /// <summary>
    ///     Copies the current selection as plain text to the clipboard: one output line per selected
    ///     line, hidden lines skipped and trailing newlines trimmed
    ///     (<see cref="MultiSelectData.GetSelectedTextFromResult" /> — the copy matches what the view
    ///     shows). A silent no-op returning <c>false</c> without a visible selection (or without a
    ///     clipboard, i.e. detached from a TopLevel).
    /// </summary>
    public async Task<bool> CopySelectionAsync()
    {
        var text = MultiSelectData.GetSelectedTextFromResult(GetSelectionResult());

        if (text.Length == 0) return false;

        return await SetClipboardTextAsync(text);
    }

    /// <summary>
    ///     Copies the whole old-side file content (<c>DiffFile.OldFileRaw</c>) to the
    ///     clipboard, its trailing newline kept as-is. Returns <c>false</c> — without touching the
    ///     clipboard — when the model has no old-side content.
    /// </summary>
    public Task<bool> CopyOldFileAsync()
    {
        return CopyFileContentAsync(DiffFile?.OldFileRaw);
    }

    /// <summary>
    ///     Copies the whole new-side file content (<c>DiffFile.NewFileRaw</c>) to the
    ///     clipboard, its trailing newline kept as-is. Returns <c>false</c> — without touching the
    ///     clipboard — when the model has no new-side content.
    /// </summary>
    public Task<bool> CopyNewFileAsync()
    {
        return CopyFileContentAsync(DiffFile?.NewFileRaw);
    }

    private async Task<bool> CopyFileContentAsync(string? content)
    {
        if (content == null) return false;

        return await SetClipboardTextAsync(content);
    }

    private async Task<bool> SetClipboardTextAsync(string text)
    {
        var clipboard = TopLevel.GetTopLevel(this)?.Clipboard;

        if (clipboard == null) return false;

        await clipboard.SetTextAsync(text);

        return true;
    }

    /// <summary>
    ///     Lightweight equivalent of <c>GetSelectionResult()?.Lines.Any(l => !l.IsHide) == true</c>:
    ///     the result is non-null exactly when a current range and the model exist, a member line is
    ///     collected exactly when the by-line-number lookup finds it (a found line's number is never
    ///     null — it matched the query), and its <c>IsHide</c> is the found item's <c>IsHidden</c>
    ///     (<see cref="DiffFileUtils.CheckCurrentLineIsHidden" /> re-looks-up the same item). Early
    ///     exits on the first visible line instead of materializing the whole result.
    /// </summary>
    private bool CanCopySelection()
    {
        var file  = DiffFile;
        var range = _selection.GetState().CurrentRange;

        if (file == null || range == null) return false;

        var normalized = MultiSelectData.NormalizeRange(range);

        if (ViewMode == DiffViewMode.Unified)
        {
            for (var lineNum = normalized.StartLineNumber; lineNum <= normalized.EndLineNumber; lineNum++)
                if (file.GetUnifiedLineByLineNumber(lineNum, normalized.Side) is { IsHidden: false })
                    return true;

            return false;
        }

        for (var lineNum = normalized.StartLineNumber; lineNum <= normalized.EndLineNumber; lineNum++)
            if (file.GetSplitLineByLineNumber(lineNum, normalized.Side) is { IsHidden: false })
                return true;

        return false;
    }

    /// <summary>
    ///     Invalidates the copy commands after a selection or model change (the file
    ///     commands track the model, the selection command tracks the current selection result).
    /// </summary>
    private void RaiseCopyCommandsCanExecuteChanged()
    {
        foreach (var command in _copyCommands) command.RaiseCanExecuteChanged();
    }

    /// <inheritdoc />
    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);
        _scrollViewer = e.NameScope.Find<ScrollViewer>("PART_ScrollViewer");
        _items        = e.NameScope.Find<ItemsControl>("PART_Items");
    }

    /// <inheritdoc />
    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);

        if (change.Property == DiffFileProperty)
        {
            if (change.OldValue is DiffFile oldFile) oldFile.Updated -= OnFileUpdated;

            if (change.NewValue is DiffFile newFile) newFile.Updated += OnFileUpdated;

            RebuildRows();
        }
        else if (change.Property == ViewModeProperty || change.Property == SyntaxHighlightProperty ||
                 change.Property == HighlighterProperty)
        {
            if (change.Property == ViewModeProperty)
                // Upstream wrapper (DiffViewWithMultiSelect.tsx): a mode change drops the
                // persisted preselected lines (updateMultiResult(undefined)); the manager state
                // itself survives.
                _selection.SetPreselectedLines(MultiSelectPreselectedLines.Empty);

            RebuildRows();
        }
        else if (change.Property == IsSelectionEnabledProperty && !change.GetNewValue<bool>())
        {
            // Upstream wrapper destroys the manager when disabled — destroy() clears the
            // selection; here the pointer routing is simply gated by the flag as well.
            ClearSelection();
        }
        else if (change.Property == FontSizeProperty)
        {
            UpdateNumberColumnWidth();
        }
    }

    private void OnFileUpdated()
    {
        // Updated also fires from the Build*DiffLines calls inside RebuildRows; the flag breaks the re-entry.
        if (!_rebuilding) RebuildRows();
    }

    private void RebuildRows()
    {
        _rebuilding = true;
        try
        {
            var file = DiffFile;
            var rows = (IReadOnlyList<DiffRow>)[];

            if (file != null)
            {
                // Upstream keeps raw init and syntax init separable (the vue component only runs
                // initSyntax while syntax highlighting is enabled); DiffFile.Init runs both.
                file.InitRaw();

                if (SyntaxHighlight) file.InitSyntax(Highlighter);

                rows = ViewMode switch
                {
                    DiffViewMode.Unified => BuildUnified(file),
                    _                    => BuildSplit(file)
                };
            }

            SetAndRaise(RowsProperty, ref _rows, rows);
            _splitRowsByLineIndex = null;
            UpdateNumberColumnWidth();

            // Upstream manager subscribes diffFile changes and re-runs the selection visual (its
            // 16 ms debounce is a DOM batching optimization — applied synchronously here): rows
            // are fresh objects, so hidden lines stay unhighlighted while remaining in the range
            // and revealed lines pick the highlight up after an expand.
            ApplySelectionVisual();

            // Expands/collapses flip IsHide on selection members; the model change also covers
            // the file commands' availability.
            RaiseCopyCommandsCanExecuteChanged();
        }
        finally
        {
            _rebuilding = false;
        }
    }

    private IReadOnlyList<DiffRow> BuildSplit(DiffFile file)
    {
        file.BuildSplitDiffLines();
        return DiffSplitRowBuilder.Build(file, ActualThemeVariant);
    }

    private IReadOnlyList<DiffRow> BuildUnified(DiffFile file)
    {
        file.BuildUnifiedDiffLines();
        return DiffUnifiedRowBuilder.Build(file, ActualThemeVariant);
    }

    private void UpdateNumberColumnWidth()
    {
        var digits = 0;

        foreach (var row in _rows)
            switch (row)
            {
                case DiffSplitContentRow split :
                    digits = Math.Max(digits,
                                      Math.Max(split.Left.Number?.Length ?? 0, split.Right.Number?.Length ?? 0));
                    break;
                case DiffUnifiedContentRow unified :
                    digits = Math.Max(digits, Math.Max(unified.OldNumber?.Length ?? 0, unified.NewNumber?.Length ?? 0));
                    break;
            }

        var width = Math.Max(NumberColumnMinWidth,
                             Math.Ceiling(digits * FontSize * MonospaceCharWidthRatio) + NumberColumnPadding);

        SetAndRaise(NumberColumnWidthProperty, ref _numberColumnWidth, width);
    }

    // ---- multi-select (upstream multiSelect/manager.ts + react/DiffViewWithMultiSelect.tsx) ----

    /// <inheritdoc />
    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);

        if (!IsSelectionEnabled) return;

        var hit = HitTestRowVisual(e);

        if (hit == null) return;

        if (ViewMode == DiffViewMode.Unified)
        {
            var lineNumbers = DiffSelectionDom.GetLineNumbersFromElement_Unified(hit);

            if (lineNumbers == null) return;

            _selection.HandlePointerPressed_Unified(lineNumbers.Value);
        }
        else
        {
            // A press must start in a line-number cell — the upstream start rule is wider (it
            // also accepts the "+" add-widget button, which this port does not render).
            var numberHolder = DiffSelectionDom.GetNumberHolderElement_Split(hit, true);

            if (numberHolder == null) return;

            var line = DiffSelectionDom.GetLineNumberFromElement_Split(numberHolder);

            if (line == null) return;

            var side = DiffSelectionDom.GetSideFromElement_Split(numberHolder);

            if (side == null) return;

            _selection.HandlePointerPressed_Split(side.Value, line.Value);
        }

        // The upstream release listener is a document-level mouseup; capturing the pointer
        // guarantees the release reaches this control wherever the pointer ends up.
        if (_selection.GetState().IsSelecting) e.Pointer.Capture(this);
    }

    /// <inheritdoc />
    protected override void OnPointerMoved(PointerEventArgs e)
    {
        base.OnPointerMoved(e);

        if (!IsSelectionEnabled || !_selection.GetState().IsSelecting) return;

        var hit = HitTestRowVisual(e);

        if (hit == null) return;

        if (ViewMode == DiffViewMode.Unified)
        {
            var lineNumbers = DiffSelectionDom.GetLineNumbersFromElement_Unified(hit);

            if (lineNumbers != null) _selection.HandlePointerMoved_Unified(lineNumbers.Value);
        }
        else
        {
            // While dragging, content cells resolve to their side's number cell (upstream
            // getNumberHolderElement_Split with inMouseDown=false) — hovering the line content
            // extends the selection too.
            var numberHolder = DiffSelectionDom.GetNumberHolderElement_Split(hit, false);
            var line         = DiffSelectionDom.GetLineNumberFromElement_Split(numberHolder);

            if (line != null) _selection.HandlePointerMoved_Split(line.Value);
        }
    }

    /// <inheritdoc />
    protected override void OnPointerReleased(PointerReleasedEventArgs e)
    {
        base.OnPointerReleased(e);

        if (!IsSelectionEnabled || !_selection.GetState().IsSelecting) return;

        // The document-level mouseup equivalent — the capture routes the release here.
        _selection.HandlePointerReleased(DiffFile, ViewMode == DiffViewMode.Unified);

        e.Pointer.Capture(null);
    }

    /// <inheritdoc />
    protected override void OnPointerCaptureLost(PointerCaptureLostEventArgs e)
    {
        base.OnPointerCaptureLost(e);

        // Losing the capture mid-drag (window deactivation, …) is the closest analog of the
        // upstream document mouseup — finish the selection instead of leaving it stuck.
        if (IsSelectionEnabled && _selection.GetState().IsSelecting)
            _selection.HandlePointerReleased(DiffFile, ViewMode == DiffViewMode.Unified);
    }

    private Visual? HitTestRowVisual(PointerEventArgs e)
    {
        return _items?.InputHitTest(e.GetPosition(_items)) as Visual;
    }

    /// <summary>
    ///     Wrapper behavior around the manager's onSelectionChange (DiffViewWithMultiSelect.tsx): a
    ///     new drag drops the previously completed selection — persisted through the preselected
    ///     channel here. The upstream <c>.diff-multi-selecting</c> CSS class only disables native
    ///     text selection in the DOM; this control renders no selectable text, so there is nothing
    ///     to disable.
    /// </summary>
    private void OnSelectionChanged(MultiSelectRange? range, MultiSelectState state)
    {
        if (state.IsSelecting && !_selection.PreselectedLines.IsEmpty)
            _selection.SetPreselectedLines(MultiSelectPreselectedLines.Empty);

        ApplySelectionVisual();

        RaiseCopyCommandsCanExecuteChanged();

        SelectionChanged?.Invoke(this, new DiffSelectionChangedEventArgs(range, state));
    }

    /// <summary>
    ///     Wrapper behavior around the manager's onSelectionComplete: a completed selection with
    ///     lines persists as its side's preselected min/max range, keeping the highlight after the
    ///     release (the upstream-known big-range merge of setPreselectedLines); an empty result
    ///     drops it.
    /// </summary>
    private void OnSelectionCompleted(MultiSelectResult? result)
    {
        if (result is { Lines.Count: > 0 } completed)
        {
            var numbers = new[] { completed.Range.StartLineNumber, completed.Range.EndLineNumber };

            _selection.SetPreselectedLines(completed.Range.Side == SplitSide.Old
                                               ? new MultiSelectPreselectedLines(numbers)
                                               : new MultiSelectPreselectedLines(New : numbers));
        }
        else
        {
            _selection.SetPreselectedLines(MultiSelectPreselectedLines.Empty);
        }

        ApplySelectionVisual();

        RaiseCopyCommandsCanExecuteChanged();

        SelectionCompleted?.Invoke(this, new DiffSelectionCompletedEventArgs(result));
    }

    /// <summary>
    ///     Port of multiSelect/visual.ts updateSelectionVisual_Split/_Unified minus the DOM: the
    ///     preselected ranges plus the current range (all normalized) flag the covered row models.
    ///     Hidden lines keep their membership in the data layer but produce no visible row, so they
    ///     stay unhighlighted until an expansion reveals them.
    /// </summary>
    private void ApplySelectionVisual()
    {
        foreach (var cell in _selectedSplitCells) cell.IsSelected = false;

        _selectedSplitCells.Clear();

        foreach (var row in _selectedUnifiedRows) row.IsSelected = false;

        _selectedUnifiedRows.Clear();

        var file = DiffFile;

        if (file == null) return;

        var preselectedRanges = MultiSelectData.ChangePreselectedLinesToLineRange(_selection.PreselectedLines);
        var currentRange      = _selection.GetState().CurrentRange;

        var allRanges = new List<MultiSelectRange>(preselectedRanges);

        if (currentRange != null) allRanges.Add(currentRange);

        var normalizedRanges = allRanges.Select(MultiSelectData.NormalizeRange).ToList();

        if (ViewMode == DiffViewMode.Unified)
            ApplyUnifiedSelection(normalizedRanges);
        else
            ApplySplitSelection(file, normalizedRanges);
    }

    /// <summary>
    ///     Port of visual.ts addClassForSplitRange: selected lines flag their row's cells —
    ///     context lines flag both sides, everything else only the range's side.
    /// </summary>
    private void ApplySplitSelection(DiffFile file, List<MultiSelectRange> allRanges)
    {
        // One pass over the rows instead of a scan per selected line; LineIndex is unique (one
        // content row per split index), so FirstOrDefault == the map hit, misses == the scan's null.
        if (_splitRowsByLineIndex == null)
        {
            _splitRowsByLineIndex = new Dictionary<int, DiffSplitContentRow>(_rows.Count);

            foreach (var row in _rows)
                if (row is DiffSplitContentRow content)
                    _splitRowsByLineIndex[content.LineIndex] = content;
        }

        foreach (var range in allRanges)
        {
            var rangeLines = MultiSelectData.GetSelectedLinesFromDiffFile_Split(file, range);

            foreach (var item in rangeLines.Where(item => !item.IsHide && item.Index != 0))
            {
                if (!_splitRowsByLineIndex.TryGetValue(item.Index, out var row)) continue;

                if (item.IsContext)
                {
                    SelectCell(row.Left);
                    SelectCell(row.Right);
                }
                else
                {
                    SelectCell(range.Side == SplitSide.Old ? row.Left : row.Right);
                }
            }
        }
    }

    /// <summary>
    ///     Port of visual.ts updateSelectionVisual_Unified's matching loop: a row is
    ///     selected when its old (or new) number falls inside an old-side (or new-side) range.
    /// </summary>
    private void ApplyUnifiedSelection(List<MultiSelectRange> allRanges)
    {
        foreach (var row in _rows.OfType<DiffUnifiedContentRow>())
        {
            var selected =
                allRanges.Any(range =>
                                  (range.Side == SplitSide.Old         && row.OldLineNumber is { } rowLineOld &&
                                   rowLineOld >= range.StartLineNumber && rowLineOld <= range.EndLineNumber) ||
                                  (range.Side == SplitSide.New         && row.NewLineNumber is { } rowLineNew &&
                                   rowLineNew >= range.StartLineNumber && rowLineNew <= range.EndLineNumber));

            if (!selected) continue;
            row.IsSelected = true;
            _selectedUnifiedRows.Add(row);
        }
    }

    private void SelectCell(DiffSplitCellModel cell)
    {
        cell.IsSelected = true;
        _selectedSplitCells.Add(cell);
    }

    /// <summary>
    ///     Captures the scroll state needed to keep the viewport anchored across the row rebuild an
    ///     expansion triggers: the clicked row's flat index, the scroll offset, and the realized
    ///     heights of the clicked placeholder and of a neighboring content row (expansions insert
    ///     only content rows). Returns <c>null</c> when the row or its containers cannot be resolved.
    /// </summary>
    private ExpandAnchor? CaptureExpandAnchor(DiffRow row)
    {
        if (_scrollViewer == null || _items == null) return null;

        var index = IndexOfRow(row);

        if (index < 0) return null;

        if (_items.ContainerFromItem(row) is not { Bounds.Height: > 0 } placeholderContainer) return null;

        var contentRowHeight = FindRealizedContentRowHeight(index);

        if (contentRowHeight <= 0) return null;

        return new ExpandAnchor(_scrollViewer.Offset.Y, contentRowHeight, placeholderContainer.Bounds.Height,
                                index, _rows.Count);
    }

    /// <summary>
    ///     Re-anchors the viewport after an expansion rebuilt the rows, mirroring the browser scroll
    ///     anchoring the upstream web views rely on: the clicked hunk row — or, when the expansion
    ///     removes it, the row that followed it — keeps its pre-click viewport position.
    /// </summary>
    private void ApplyExpandAnchor(ExpandAnchor anchor, HunkExpandDirection direction, int hunkIndex)
    {
        if (_scrollViewer == null || _rows.Count == anchor.OldCount) return; // nothing was revealed

        int  insertedAbove;
        bool placeholderReplaced;

        switch (direction)
        {
            case HunkExpandDirection.Up :
                // Rows above the placeholder never move: a surviving placeholder keeps its flat
                // index (no scroll delta); when the whole hidden range is revealed the placeholder
                // disappears and the revealed rows land where it was, above the row after it.
                placeholderReplaced = anchor.OldIndex >= _rows.Count ||
                                      _rows[anchor.OldIndex] is not (DiffSplitHunkRow or DiffUnifiedHunkRow);
                insertedAbove = placeholderReplaced ? _rows.Count - anchor.OldCount + 1 : 0;
                break;
            case HunkExpandDirection.Down :
            {
                // Down reveals rows above the placeholder, whose hunk key is unchanged — it moves
                // down by the inserted count. The trailing strip disappears instead and appends its
                // rows below everything visible.
                var movedTo = FindHunkRowIndex(hunkIndex);

                placeholderReplaced = false;
                insertedAbove       = movedTo > anchor.OldIndex ? movedTo - anchor.OldIndex : 0;
                break;
            }
            default :
                // All removes the placeholder and reveals the whole range above the row after it.
                placeholderReplaced = true;
                insertedAbove       = _rows.Count - anchor.OldCount + 1;
                break;
        }

        if (insertedAbove <= 0 && !placeholderReplaced) return;

        // A replaced placeholder contributes its own height back to the content above the anchor.
        var delta = insertedAbove * anchor.ContentRowHeight -
                    (placeholderReplaced ? anchor.PlaceholderHeight : 0);

        if (Math.Abs(delta) < 0.01) return;

        // ScrollViewer.Offset is coerced against the current extent, which still reflects the old
        // rows until the next layout pass — defer the adjustment to the rebuild's extent change,
        // then restore the captured offset plus the inserted height.
        _pendingExpandOffset        =  new Vector(_scrollViewer.Offset.X, anchor.OffsetY + delta);
        _scrollViewer.ScrollChanged -= OnScrollChanged;
        _scrollViewer.ScrollChanged += OnScrollChanged;
    }

    private void OnScrollChanged(object? sender, ScrollChangedEventArgs e)
    {
        if (_pendingExpandOffset is not { } target || sender is not ScrollViewer scroller) return;

        _pendingExpandOffset   =  null;
        scroller.ScrollChanged -= OnScrollChanged;
        scroller.Offset        =  target;
    }

    private int IndexOfRow(DiffRow row)
    {
        for (var index = 0; index < _rows.Count; index++)
            if (ReferenceEquals(_rows[index], row))
                return index;

        return -1;
    }

    /// <summary>
    ///     Height of the nearest realized content row around the clicked placeholder — the
    ///     expansion inserts only content rows of that same template.
    /// </summary>
    private double FindRealizedContentRowHeight(int anchorIndex)
    {
        var maxDistance = Math.Max(anchorIndex, _rows.Count - 1 - anchorIndex);

        for (var distance = 1; distance <= maxDistance; distance++)
            if (TryGetContentRowHeight(anchorIndex - distance, out var height) ||
                TryGetContentRowHeight(anchorIndex + distance, out height))
                return height;

        return 0;

        bool TryGetContentRowHeight(int index, out double height)
        {
            height = 0;

            if (index < 0 || index >= _rows.Count ||
                _rows[index] is not (DiffSplitContentRow or DiffUnifiedContentRow))
                return false;

            if (_items?.ContainerFromItem(_rows[index]) is not { Bounds.Height: > 0 } container) return false;

            height = container.Bounds.Height;

            return true;
        }
    }

    private int FindHunkRowIndex(int hunkIndex)
    {
        for (var index = 0; index < _rows.Count; index++)
        {
            var key = _rows[index] switch
            {
                DiffSplitHunkRow split     => split.HunkIndex,
                DiffUnifiedHunkRow unified => unified.HunkIndex,
                _                          => int.MinValue
            };

            if (key == hunkIndex) return index;
        }

        return -1;
    }

    /// <summary>
    ///     Relays a hunk-row expand click to the model's expand API for the active view mode
    ///     and re-anchors the viewport after the rebuild shifts the clicked row.
    /// </summary>
    private sealed class HunkExpandCommand(DiffView owner, HunkExpandDirection direction) : ICommand
    {
        // Availability is encoded in the row's button visibility, so there is no per-row state to
        // invalidate; the empty handlers keep command sources subscribed without overhead.
        public event EventHandler? CanExecuteChanged
        {
            add { }
            remove { }
        }

        public bool CanExecute(object? parameter)
        {
            return owner.DiffFile?.IsExpandEnabled == true && parameter is DiffSplitHunkRow or DiffUnifiedHunkRow;
        }

        public void Execute(object? parameter)
        {
            if (parameter is not (DiffSplitHunkRow or DiffUnifiedHunkRow)) return;

            var row = (DiffRow)parameter;
            var hunkIndex = parameter is DiffSplitHunkRow split
                ? split.HunkIndex
                : ((DiffUnifiedHunkRow)parameter).HunkIndex;
            var anchor = owner.CaptureExpandAnchor(row);

            switch (parameter)
            {
                case DiffSplitHunkRow splitRow :
                    owner.DiffFile?.OnSplitHunkExpand(direction, splitRow.HunkIndex);
                    break;
                case DiffUnifiedHunkRow unifiedRow :
                    owner.DiffFile?.OnUnifiedHunkExpand(direction, unifiedRow.HunkIndex);
                    break;
            }

            if (anchor is { } captured) owner.ApplyExpandAnchor(captured, direction, hunkIndex);
        }
    }

    /// <summary>
    ///     Relays one of the view's copy operations. Unlike <see cref="HunkExpandCommand" />, whose
    ///     availability is static per row, copy availability follows the selection and the model, so
    ///     the owner raises <see cref="RaiseCanExecuteChanged" /> when those change.
    /// </summary>
    private sealed class DiffCopyCommand(
        DiffView             owner,
        Func<DiffView, bool> canExecute,
        Func<DiffView, Task> executeAsync) : ICommand
    {
        private EventHandler? _canExecuteChanged;

        public event EventHandler? CanExecuteChanged
        {
            add => _canExecuteChanged += value;
            remove => _canExecuteChanged -= value;
        }

        public bool CanExecute(object? parameter)
        {
            return canExecute(owner);
        }

        /// <summary>
        ///     ICommand.Execute is synchronous by contract; the clipboard write runs
        ///     fire-and-forget, the standard async-command pattern.
        /// </summary>
        public async void Execute(object? parameter)
        {
            await executeAsync(owner);
        }

        public void RaiseCanExecuteChanged()
        {
            _canExecuteChanged?.Invoke(owner, EventArgs.Empty);
        }
    }

    /// <summary>Pre-expansion scroll state used to anchor the viewport across a row rebuild.</summary>
    private readonly record struct ExpandAnchor(
        double OffsetY,
        double ContentRowHeight,
        double PlaceholderHeight,
        int    OldIndex,
        int    OldCount);
}
