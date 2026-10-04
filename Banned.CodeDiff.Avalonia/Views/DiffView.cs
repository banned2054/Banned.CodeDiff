using System.Windows.Input;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Media;
using Banned.CodeDiff.Avalonia.Models;
using Banned.CodeDiff.Avalonia.Utils;
using Banned.CodeDiff.Models;
using Banned.CodeDiff.Services;

namespace Banned.CodeDiff.Avalonia.Views;

/// <summary>
/// Renders a <see cref="T:Banned.CodeDiff.Services.DiffFile"/> as a read-only, GitHub-style diff
/// view with line-level add/delete background colors — either split (two columns, default) or
/// unified (single column with dual line numbers, deleted lines above the added ones). Assign a
/// <see cref="DiffFile"/> (built or not — <c>Init</c> and the <c>Build*DiffLines</c> calls are
/// idempotent and invoked on demand) and the view keeps itself in sync through the model's
/// <c>Updated</c> event.
/// </summary>
public sealed class DiffView : TemplatedControl
{
    /// <summary>Minimum width of a line-number column, matching the upstream aside width.</summary>
    private const double NumberColumnMinWidth = 40;

    /// <summary>Horizontal padding of a line-number column (10px on each side upstream).</summary>
    private const double NumberColumnPadding = 20;

    /// <summary>Approximate advance width of one monospace digit relative to the font size.</summary>
    private const double MonospaceCharWidthRatio = 0.62;

    /// <summary>Identifies the <see cref="DiffFile"/> dependency property.</summary>
    public static readonly StyledProperty<DiffFile?> DiffFileProperty =
        AvaloniaProperty.Register<DiffView, DiffFile?>(nameof(DiffFile));

    /// <summary>Identifies the <see cref="ViewMode"/> dependency property.</summary>
    public static readonly StyledProperty<DiffViewMode> ViewModeProperty =
        AvaloniaProperty.Register<DiffView, DiffViewMode>(nameof(ViewMode));

    /// <summary>Identifies the <see cref="SyntaxHighlight"/> dependency property.</summary>
    public static readonly StyledProperty<bool> SyntaxHighlightProperty =
        AvaloniaProperty.Register<DiffView, bool>(nameof(SyntaxHighlight), true);

    /// <summary>Identifies the <see cref="Highlighter"/> dependency property.</summary>
    public static readonly StyledProperty<IDiffHighlighter?> HighlighterProperty =
        AvaloniaProperty.Register<DiffView, IDiffHighlighter?>(nameof(Highlighter));

    /// <summary>Identifies the <see cref="Rows"/> direct property.</summary>
    public static readonly DirectProperty<DiffView, IReadOnlyList<DiffRow>> RowsProperty =
        AvaloniaProperty.RegisterDirect<DiffView, IReadOnlyList<DiffRow>>(nameof(Rows), o => o.Rows);

    /// <summary>Identifies the <see cref="NumberColumnWidth"/> direct property.</summary>
    public static readonly DirectProperty<DiffView, double> NumberColumnWidthProperty =
        AvaloniaProperty.RegisterDirect<DiffView, double>(nameof(NumberColumnWidth), o => o.NumberColumnWidth);

    private IReadOnlyList<DiffRow> _rows              = [];
    private double                 _numberColumnWidth = NumberColumnMinWidth;
    private bool                   _rebuilding;
    private ScrollViewer?          _scrollViewer;
    private ItemsControl?          _items;
    private Vector?                _pendingExpandOffset;

    /// <summary>Initializes a new instance of the <see cref="DiffView"/> class.</summary>
    public DiffView()
    {
        // SetCurrentValue keeps these overridable by styles and inherited values.
        SetCurrentValue(FontFamilyProperty, new FontFamily("Menlo, Consolas, monospace"));
        SetCurrentValue(FontSizeProperty, 14.0);

        ExpandHunkUpCommand   = new HunkExpandCommand(this, HunkExpandDirection.Up);
        ExpandHunkDownCommand = new HunkExpandCommand(this, HunkExpandDirection.Down);
        ExpandHunkAllCommand  = new HunkExpandCommand(this, HunkExpandDirection.All);

        // Brushes are baked into the rows; switch palette by rebuilding on theme changes.
        ActualThemeVariantChanged += (_, _) => RebuildRows();
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

    /// <summary>Gets or sets whether the model runs syntax highlighting
    /// (<c>DiffFile.InitSyntax</c>) before rendering; the built-in TextMate engine
    /// colors lines when the language is registered.</summary>
    public bool SyntaxHighlight
    {
        get => GetValue(SyntaxHighlightProperty);
        set => SetValue(SyntaxHighlightProperty, value);
    }

    /// <summary>Gets or sets the syntax engine passed to <c>InitSyntax</c>; <c>null</c> uses
    /// the core library's built-in TextMate engine (upstream: registerHighlighter).</summary>
    public IDiffHighlighter? Highlighter
    {
        get => GetValue(HighlighterProperty);
        set => SetValue(HighlighterProperty, value);
    }

    /// <summary>Gets the flat row list currently rendered (content rows and hunk placeholders).</summary>
    public IReadOnlyList<DiffRow> Rows => _rows;

    /// <summary>Gets the resolved line-number column width for the current rows and font size.</summary>
    public double NumberColumnWidth => _numberColumnWidth;

    /// <summary>Gets the command that expands a hunk row up by the compose length (40 lines);
    /// the command parameter is the <see cref="DiffSplitHunkRow"/> or <see cref="DiffUnifiedHunkRow"/>.</summary>
    public ICommand ExpandHunkUpCommand { get; }

    /// <summary>Gets the command that expands a hunk row down by the compose length (40 lines);
    /// the command parameter is the <see cref="DiffSplitHunkRow"/> or <see cref="DiffUnifiedHunkRow"/>.</summary>
    public ICommand ExpandHunkDownCommand { get; }

    /// <summary>Gets the command that fully expands a hunk row; the command parameter is the
    /// <see cref="DiffSplitHunkRow"/> or <see cref="DiffUnifiedHunkRow"/>.</summary>
    public ICommand ExpandHunkAllCommand { get; }

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
            if (change.OldValue is DiffFile oldFile)
            {
                oldFile.Updated -= OnFileUpdated;
            }

            if (change.NewValue is DiffFile newFile)
            {
                newFile.Updated += OnFileUpdated;
            }

            RebuildRows();
        }
        else if (change.Property == ViewModeProperty || change.Property == SyntaxHighlightProperty ||
                 change.Property == HighlighterProperty)
        {
            RebuildRows();
        }
        else if (change.Property == FontSizeProperty)
        {
            UpdateNumberColumnWidth();
        }
    }

    private void OnFileUpdated()
    {
        // Updated also fires from the Build*DiffLines calls inside RebuildRows; the flag breaks the re-entry.
        if (!_rebuilding)
        {
            RebuildRows();
        }
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

                if (SyntaxHighlight)
                {
                    file.InitSyntax(Highlighter);
                }

                rows = ViewMode switch
                {
                    DiffViewMode.Unified => BuildUnified(file),
                    _                    => BuildSplit(file),
                };
            }

            SetAndRaise(RowsProperty, ref _rows, rows);
            UpdateNumberColumnWidth();
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
        {
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
        }

        var width = Math.Max(NumberColumnMinWidth,
                             Math.Ceiling(digits * FontSize * MonospaceCharWidthRatio) + NumberColumnPadding);

        SetAndRaise(NumberColumnWidthProperty, ref _numberColumnWidth, width);
    }

    /// <summary>
    /// Captures the scroll state needed to keep the viewport anchored across the row rebuild an
    /// expansion triggers: the clicked row's flat index, the scroll offset, and the realized
    /// heights of the clicked placeholder and of a neighboring content row (expansions insert
    /// only content rows). Returns <c>null</c> when the row or its containers cannot be resolved.
    /// </summary>
    private ExpandAnchor? CaptureExpandAnchor(DiffRow row)
    {
        if (_scrollViewer == null || _items == null)
        {
            return null;
        }

        var index = IndexOfRow(row);

        if (index < 0)
        {
            return null;
        }

        if (_items.ContainerFromItem(row) is not { Bounds.Height: > 0 } placeholderContainer)
        {
            return null;
        }

        var contentRowHeight = FindRealizedContentRowHeight(index);

        if (contentRowHeight <= 0)
        {
            return null;
        }

        return new ExpandAnchor(_scrollViewer.Offset.Y, contentRowHeight, placeholderContainer.Bounds.Height,
                                index, _rows.Count);
    }

    /// <summary>
    /// Re-anchors the viewport after an expansion rebuilt the rows, mirroring the browser scroll
    /// anchoring the upstream web views rely on: the clicked hunk row — or, when the expansion
    /// removes it, the row that followed it — keeps its pre-click viewport position.
    /// </summary>
    private void ApplyExpandAnchor(ExpandAnchor anchor, HunkExpandDirection direction, int hunkIndex)
    {
        if (_scrollViewer == null || _rows.Count == anchor.OldCount)
        {
            return; // nothing was revealed
        }

        int insertedAbove;
        bool placeholderReplaced;

        if (direction == HunkExpandDirection.Up)
        {
            // Rows above the placeholder never move: a surviving placeholder keeps its flat
            // index (no scroll delta); when the whole hidden range is revealed the placeholder
            // disappears and the revealed rows land where it was, above the row after it.
            placeholderReplaced = anchor.OldIndex >= _rows.Count ||
                                  _rows[anchor.OldIndex] is not (DiffSplitHunkRow or DiffUnifiedHunkRow);
            insertedAbove = placeholderReplaced ? _rows.Count - anchor.OldCount + 1 : 0;
        }
        else if (direction == HunkExpandDirection.Down)
        {
            // Down reveals rows above the placeholder, whose hunk key is unchanged — it moves
            // down by the inserted count. The trailing strip disappears instead and appends its
            // rows below everything visible.
            var movedTo = FindHunkRowIndex(hunkIndex);

            placeholderReplaced = false;
            insertedAbove = movedTo > anchor.OldIndex ? movedTo - anchor.OldIndex : 0;
        }
        else
        {
            // All removes the placeholder and reveals the whole range above the row after it.
            placeholderReplaced = true;
            insertedAbove = _rows.Count - anchor.OldCount + 1;
        }

        if (insertedAbove <= 0 && !placeholderReplaced)
        {
            return;
        }

        // A replaced placeholder contributes its own height back to the content above the anchor.
        var delta = insertedAbove * anchor.ContentRowHeight -
                    (placeholderReplaced ? anchor.PlaceholderHeight : 0);

        if (Math.Abs(delta) < 0.01)
        {
            return;
        }

        // ScrollViewer.Offset is coerced against the current extent, which still reflects the old
        // rows until the next layout pass — defer the adjustment to the rebuild's extent change,
        // then restore the captured offset plus the inserted height.
        _pendingExpandOffset = new Vector(_scrollViewer.Offset.X, anchor.OffsetY + delta);
        _scrollViewer.ScrollChanged -= OnScrollChanged;
        _scrollViewer.ScrollChanged += OnScrollChanged;
    }

    private void OnScrollChanged(object? sender, ScrollChangedEventArgs e)
    {
        if (_pendingExpandOffset is not { } target || sender is not ScrollViewer scroller)
        {
            return;
        }

        _pendingExpandOffset = null;
        scroller.ScrollChanged -= OnScrollChanged;
        scroller.Offset = target;
    }

    private int IndexOfRow(DiffRow row)
    {
        for (var index = 0; index < _rows.Count; index++)
        {
            if (ReferenceEquals(_rows[index], row))
            {
                return index;
            }
        }

        return -1;
    }

    /// <summary>Height of the nearest realized content row around the clicked placeholder — the
    /// expansion inserts only content rows of that same template.</summary>
    private double FindRealizedContentRowHeight(int anchorIndex)
    {
        var maxDistance = Math.Max(anchorIndex, _rows.Count - 1 - anchorIndex);

        for (var distance = 1; distance <= maxDistance; distance++)
        {
            if (TryGetContentRowHeight(anchorIndex - distance, out var height) ||
                TryGetContentRowHeight(anchorIndex + distance, out height))
            {
                return height;
            }
        }

        return 0;

        bool TryGetContentRowHeight(int index, out double height)
        {
            height = 0;

            if (index < 0 || index >= _rows.Count ||
                _rows[index] is not (DiffSplitContentRow or DiffUnifiedContentRow))
            {
                return false;
            }

            if (_items?.ContainerFromItem(_rows[index]) is not { Bounds.Height: > 0 } container)
            {
                return false;
            }

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
                _                          => int.MinValue,
            };

            if (key == hunkIndex)
            {
                return index;
            }
        }

        return -1;
    }

    /// <summary>Relays a hunk-row expand click to the model's expand API for the active view mode
    /// and re-anchors the viewport after the rebuild shifts the clicked row.</summary>
    private sealed class HunkExpandCommand(DiffView owner, HunkExpandDirection direction) : ICommand
    {
        // Availability is encoded in the row's button visibility, so there is no per-row state to
        // invalidate; the empty handlers keep command sources subscribed without overhead.
        public event EventHandler? CanExecuteChanged
        {
            add { }
            remove { }
        }

        public bool CanExecute(object? parameter) =>
            owner.DiffFile?.GetExpandEnabled() == true && parameter is DiffSplitHunkRow or DiffUnifiedHunkRow;

        public void Execute(object? parameter)
        {
            if (parameter is not (DiffSplitHunkRow or DiffUnifiedHunkRow))
            {
                return;
            }

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

            if (anchor is { } captured)
            {
                owner.ApplyExpandAnchor(captured, direction, hunkIndex);
            }
        }
    }

    /// <summary>Pre-expansion scroll state used to anchor the viewport across a row rebuild.</summary>
    private readonly record struct ExpandAnchor(double OffsetY, double ContentRowHeight, double PlaceholderHeight,
                                                int OldIndex, int OldCount);
}
