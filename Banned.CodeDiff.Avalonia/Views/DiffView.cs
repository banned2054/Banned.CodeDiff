using Avalonia;
using Avalonia.Controls.Primitives;
using Avalonia.Media;
using Banned.CodeDiff.Avalonia.Models;
using Banned.CodeDiff.Avalonia.Utils;
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

    /// <summary>Identifies the <see cref="Rows"/> direct property.</summary>
    public static readonly DirectProperty<DiffView, IReadOnlyList<DiffRow>> RowsProperty =
        AvaloniaProperty.RegisterDirect<DiffView, IReadOnlyList<DiffRow>>(nameof(Rows), o => o.Rows);

    /// <summary>Identifies the <see cref="NumberColumnWidth"/> direct property.</summary>
    public static readonly DirectProperty<DiffView, double> NumberColumnWidthProperty =
        AvaloniaProperty.RegisterDirect<DiffView, double>(nameof(NumberColumnWidth), o => o.NumberColumnWidth);

    private IReadOnlyList<DiffRow> _rows              = [];
    private double                 _numberColumnWidth = NumberColumnMinWidth;
    private bool                   _rebuilding;

    /// <summary>Initializes a new instance of the <see cref="DiffView"/> class.</summary>
    public DiffView()
    {
        // SetCurrentValue keeps these overridable by styles and inherited values.
        SetCurrentValue(FontFamilyProperty, new FontFamily("Menlo, Consolas, monospace"));
        SetCurrentValue(FontSizeProperty, 14.0);

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

    /// <summary>Gets the flat row list currently rendered (content rows and hunk placeholders).</summary>
    public IReadOnlyList<DiffRow> Rows => _rows;

    /// <summary>Gets the resolved line-number column width for the current rows and font size.</summary>
    public double NumberColumnWidth => _numberColumnWidth;

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
        else if (change.Property == ViewModeProperty)
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
                file.Init();

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
                case DiffSplitContentRow split:
                    digits = Math.Max(digits,
                                      Math.Max(split.Left.Number?.Length ?? 0, split.Right.Number?.Length ?? 0));
                    break;
                case DiffUnifiedContentRow unified:
                    digits = Math.Max(digits,
                                      Math.Max(unified.OldNumber?.Length ?? 0, unified.NewNumber?.Length ?? 0));
                    break;
            }
        }

        var width = Math.Max(NumberColumnMinWidth,
                             Math.Ceiling(digits * FontSize * MonospaceCharWidthRatio) + NumberColumnPadding);

        SetAndRaise(NumberColumnWidthProperty, ref _numberColumnWidth, width);
    }
}
