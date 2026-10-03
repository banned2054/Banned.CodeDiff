using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Media;
using Avalonia.Media.TextFormatting;
using Banned.CodeDiff.Avalonia.Models;

namespace Banned.CodeDiff.Avalonia.Views;

/// <summary>
/// Renders one diff line and paints word-level highlight rectangles behind the changed ranges.
/// Avalonia text runs expose no per-run background, so the highlight is custom-drawn: the whole
/// line is laid out once with <see cref="TextLayout"/> and
/// <see cref="TextLayout.HitTestTextPosition"/> resolves each range boundary to an x coordinate.
/// Single line, no wrap (wrap mode is M6 scope).
/// </summary>
public sealed class DiffSegmentText : Control
{
    /// <summary>Corner radius of the highlight rectangles, approximating the upstream 0.2em.</summary>
    private const float HighlightCornerRadius = 2f;

    /// <summary>Identifies the <see cref="Text"/> dependency property.</summary>
    public static readonly StyledProperty<string?> TextProperty =
        AvaloniaProperty.Register<DiffSegmentText, string?>(nameof(Text));

    /// <summary>Identifies the <see cref="Highlights"/> dependency property.</summary>
    public static readonly StyledProperty<IReadOnlyList<DiffHighlight>> HighlightsProperty =
        AvaloniaProperty.Register<DiffSegmentText, IReadOnlyList<DiffHighlight>>(nameof(Highlights));

    /// <summary>Identifies the <see cref="HighlightBrush"/> dependency property.</summary>
    public static readonly StyledProperty<IBrush?> HighlightBrushProperty =
        AvaloniaProperty.Register<DiffSegmentText, IBrush?>(nameof(HighlightBrush));

    /// <summary>Identifies the <see cref="FontFamily"/> dependency property.</summary>
    public static readonly StyledProperty<FontFamily> FontFamilyProperty =
        TextElement.FontFamilyProperty.AddOwner<DiffSegmentText>();

    /// <summary>Identifies the <see cref="FontSize"/> dependency property.</summary>
    public static readonly StyledProperty<double> FontSizeProperty =
        TextElement.FontSizeProperty.AddOwner<DiffSegmentText>();

    /// <summary>Identifies the <see cref="Foreground"/> dependency property.</summary>
    public static readonly StyledProperty<IBrush?> ForegroundProperty =
        TextElement.ForegroundProperty.AddOwner<DiffSegmentText>();

    private TextLayout? _layout;

    static DiffSegmentText()
    {
        AffectsMeasure<DiffSegmentText>(TextProperty, FontFamilyProperty, FontSizeProperty);
        AffectsRender<DiffSegmentText>(HighlightsProperty, HighlightBrushProperty, ForegroundProperty);
    }

    /// <summary>Gets or sets the line text to render.</summary>
    public string? Text
    {
        get => GetValue(TextProperty);
        set => SetValue(TextProperty, value);
    }

    /// <summary>Gets or sets the word-level highlight ranges within <see cref="Text"/>.</summary>
    public IReadOnlyList<DiffHighlight> Highlights
    {
        get => GetValue(HighlightsProperty);
        set => SetValue(HighlightsProperty, value);
    }

    /// <summary>Gets or sets the brush used to paint the highlight rectangles.</summary>
    public IBrush? HighlightBrush
    {
        get => GetValue(HighlightBrushProperty);
        set => SetValue(HighlightBrushProperty, value);
    }

    /// <summary>Gets or sets the font family used to render the text.</summary>
    public FontFamily FontFamily
    {
        get => GetValue(FontFamilyProperty);
        set => SetValue(FontFamilyProperty, value);
    }

    /// <summary>Gets or sets the font size used to render the text.</summary>
    public double FontSize
    {
        get => GetValue(FontSizeProperty);
        set => SetValue(FontSizeProperty, value);
    }

    /// <summary>Gets or sets the brush used to render the text.</summary>
    public IBrush? Foreground
    {
        get => GetValue(ForegroundProperty);
        set => SetValue(ForegroundProperty, value);
    }

    /// <summary>Computes the highlight rectangles for the given ranges within a text layout.</summary>
    internal static IEnumerable<Rect> ComputeHighlightRects(
        TextLayout layout, int textLength, IReadOnlyList<DiffHighlight> highlights)
    {
        var height = GetLayoutHeight(layout);

        foreach (var highlight in highlights)
        {
            var start = Math.Clamp(highlight.Start, 0, textLength);
            var end   = Math.Clamp(highlight.Start + highlight.Length, start, textLength);

            if (end == start)
            {
                continue;
            }

            var x0 = layout.HitTestTextPosition(start).X;
            var x1 = layout.HitTestTextPosition(end).X;

            if (x1 <= x0)
            {
                continue;
            }

            yield return new Rect(x0, 0, x1 - x0, height);
        }
    }

    /// <inheritdoc />
    protected override Size MeasureOverride(Size availableSize)
    {
        var layout = GetLayout();
        var width  = layout.TextLines.Select(line => line.WidthIncludingTrailingWhitespace).Prepend(0.0).Max();

        return new Size(width, GetLayoutHeight(layout));
    }

    /// <inheritdoc />
    public override void Render(DrawingContext context)
    {
        var layout = GetLayout();

        if (string.IsNullOrEmpty(Text))
        {
            return;
        }

        var highlightBrush = HighlightBrush;

        if (highlightBrush != null && Highlights.Count > 0)
        {
            foreach (var rect in ComputeHighlightRects(layout, Text!.Length, Highlights))
            {
                context.FillRectangle(highlightBrush, rect, HighlightCornerRadius);
            }
        }

        layout.Draw(context, new Point(0, 0));
    }

    private static double GetLayoutHeight(TextLayout layout)
    {
        return layout.TextLines.Sum(line => line.Height);
    }

    private TextLayout GetLayout()
    {
        if (_layout != null)
        {
            return _layout;
        }

        _layout = new TextLayout(Text ?? string.Empty, new Typeface(FontFamily), FontSize, Foreground ?? Brushes.Black);

        return _layout;
    }

    /// <inheritdoc />
    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);

        // The cached layout depends on text, font, and foreground properties; highlight ranges
        // and brushes only affect rendering.
        if (change.Property == TextProperty       ||
            change.Property == FontFamilyProperty ||
            change.Property == FontSizeProperty   ||
            change.Property == ForegroundProperty)
        {
            _layout = null;
        }
    }
}
