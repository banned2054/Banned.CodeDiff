using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Media;
using Avalonia.Media.TextFormatting;
using Banned.CodeDiff.Avalonia.Models;

namespace Banned.CodeDiff.Avalonia.Views;

/// <summary>
///     Renders one diff line: syntax-colored text segments plus word-level highlight
///     rectangles behind the changed ranges. Avalonia text runs expose no per-run
///     background, so the highlight is custom-drawn: the whole line is laid out once
///     with <see cref="TextLayout" /> and <see cref="TextLayout.HitTestTextPosition" />
///     resolves each range boundary to x/y coordinates. Syntax coloring draws each
///     <see cref="DiffSyntaxRun" /> as its own small layout, positioned at the
///     coordinates the whole-line layout reports for the run start (monospaced diff
///     text keeps segments aligned). With <see cref="Wrap" /> off the layout is one
///     line measuring the full text width (upstream white-space: pre); with it on the
///     layout wraps at the measured width (upstream diffViewWrap: pre-wrap), and a
///     range or run crossing a line break is drawn per text line, like browser inline
///     boxes fragmenting across lines.
/// </summary>
public sealed class DiffSegmentText : Control
{
    /// <summary>Corner radius of the highlight rectangles, approximating the upstream 0.2em.</summary>
    private const float HighlightCornerRadius = 2f;

    /// <summary>Identifies the <see cref="Text" /> dependency property.</summary>
    public static readonly StyledProperty<string?> TextProperty =
        AvaloniaProperty.Register<DiffSegmentText, string?>(nameof(Text));

    /// <summary>Identifies the <see cref="Wrap" /> dependency property.</summary>
    public static readonly StyledProperty<bool> WrapProperty =
        AvaloniaProperty.Register<DiffSegmentText, bool>(nameof(Wrap));

    /// <summary>Identifies the <see cref="Highlights" /> dependency property.</summary>
    public static readonly StyledProperty<IReadOnlyList<DiffHighlight>> HighlightsProperty =
        AvaloniaProperty.Register<DiffSegmentText, IReadOnlyList<DiffHighlight>>(nameof(Highlights));

    /// <summary>Identifies the <see cref="SyntaxRuns" /> dependency property.</summary>
    public static readonly StyledProperty<IReadOnlyList<DiffSyntaxRun>?> SyntaxRunsProperty =
        AvaloniaProperty.Register<DiffSegmentText, IReadOnlyList<DiffSyntaxRun>?>(nameof(SyntaxRuns));

    /// <summary>Identifies the <see cref="HighlightBrush" /> dependency property.</summary>
    public static readonly StyledProperty<IBrush?> HighlightBrushProperty =
        AvaloniaProperty.Register<DiffSegmentText, IBrush?>(nameof(HighlightBrush));

    /// <summary>Identifies the <see cref="FontFamily" /> dependency property.</summary>
    public static readonly StyledProperty<FontFamily> FontFamilyProperty =
        TextElement.FontFamilyProperty.AddOwner<DiffSegmentText>();

    /// <summary>Identifies the <see cref="FontSize" /> dependency property.</summary>
    public static readonly StyledProperty<double> FontSizeProperty =
        TextElement.FontSizeProperty.AddOwner<DiffSegmentText>();

    /// <summary>Identifies the <see cref="Foreground" /> dependency property.</summary>
    public static readonly StyledProperty<IBrush?> ForegroundProperty =
        TextElement.ForegroundProperty.AddOwner<DiffSegmentText>();

    /// <summary>The width the cached layout was built for — wrap layouts are width-sensitive.</summary>
    private double _layoutWidth = double.NaN;

    private IReadOnlyList<(TextLayout Layout, double X, double Y)>? _syntaxLayouts;

    static DiffSegmentText()
    {
        AffectsMeasure<DiffSegmentText>(TextProperty, FontFamilyProperty, FontSizeProperty, WrapProperty);
        AffectsRender<DiffSegmentText>(HighlightsProperty, SyntaxRunsProperty, HighlightBrushProperty,
                                       ForegroundProperty);
    }

    /// <summary>Gets or sets the line text to render.</summary>
    public string? Text
    {
        get => GetValue(TextProperty);
        set => SetValue(TextProperty, value);
    }

    /// <summary>
    ///     Gets or sets a value indicating whether the text wraps at the measured width,
    ///     growing the control height over several text lines (upstream diffViewWrap), instead of
    ///     rendering one full-width line. Words break at line edges when they do not fit.
    /// </summary>
    public bool Wrap
    {
        get => GetValue(WrapProperty);
        set => SetValue(WrapProperty, value);
    }

    /// <summary>Gets or sets the word-level highlight ranges within <see cref="Text" />.</summary>
    public IReadOnlyList<DiffHighlight> Highlights
    {
        get => GetValue(HighlightsProperty);
        set => SetValue(HighlightsProperty, value);
    }

    /// <summary>
    ///     Gets or sets the syntax-colored segments within <see cref="Text" />;
    ///     <c>null</c> renders the whole line with <see cref="Foreground" />.
    /// </summary>
    public IReadOnlyList<DiffSyntaxRun>? SyntaxRuns
    {
        get => GetValue(SyntaxRunsProperty);
        set => SetValue(SyntaxRunsProperty, value);
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

    /// <summary>
    ///     Number of text lines of the layout built for the latest measure pass — a
    ///     wrap-mode probe for the headless tests (1 unless the text wrapped).
    /// </summary>
    internal int TextLineCount => CurrentLayout?.TextLines.Count ?? 0;

    /// <summary>
    ///     The layout built for the latest measure pass — a wrap-mode probe for the
    ///     headless tests.
    /// </summary>
    internal TextLayout? CurrentLayout { get; private set; }

    /// <summary>
    ///     Computes the highlight rectangles for the given ranges within a text layout.
    ///     A range crossing a line break (wrapped layout) fragments into one rectangle per text
    ///     line — the first runs to its line's end, middle lines cover their full width, and the
    ///     last starts at the line's left edge, like a browser inline box background.
    /// </summary>
    internal static IEnumerable<Rect> ComputeHighlightRects(
        TextLayout layout, int textLength, IReadOnlyList<DiffHighlight> highlights)
    {
        var lines    = layout.TextLines;
        var lineTops = new double[lines.Count];

        for (var i = 1; i < lines.Count; i++) lineTops[i] = lineTops[i - 1] + lines[i - 1].Height;

        foreach (var highlight in highlights)
        {
            var start = Math.Clamp(highlight.Start, 0, textLength);
            var end   = Math.Clamp(highlight.Start + highlight.Length, start, textLength);

            if (end == start) continue;

            var firstLine = LineOfPosition(lines, start);
            var lastLine  = LineOfPosition(lines, end);
            var x0        = layout.HitTestTextPosition(start).X;

            if (firstLine == lastLine)
            {
                var x1 = layout.HitTestTextPosition(end).X;

                if (x1 <= x0) continue;

                yield return new Rect(x0, lineTops[firstLine], x1 - x0, lines[firstLine].Height);

                continue;
            }

            yield return new Rect(x0, lineTops[firstLine],
                                  lines[firstLine].WidthIncludingTrailingWhitespace - x0, lines[firstLine].Height);

            for (var line = firstLine + 1; line < lastLine; line++)
                yield return new Rect(0, lineTops[line],
                                      lines[line].WidthIncludingTrailingWhitespace, lines[line].Height);

            var lastX = layout.HitTestTextPosition(end).X;

            if (lastX > 0) yield return new Rect(0, lineTops[lastLine], lastX, lines[lastLine].Height);
        }
    }

    /// <summary>
    ///     Index of the text line containing the text position — the last line whose first
    ///     character index precedes it (the end-of-text position belongs to the last line).
    /// </summary>
    private static int LineOfPosition(IReadOnlyList<TextLine> lines, int position)
    {
        var result = 0;

        for (var i = 1; i < lines.Count; i++)
        {
            if (lines[i].FirstTextSourceIndex > position) break;

            result = i;
        }

        return result;
    }

    /// <inheritdoc />
    protected override Size MeasureOverride(Size availableSize)
    {
        var layout = GetLayout(availableSize.Width);

        // Hand-rolled Select/Prepend/Max: same result (0 for an empty line list), no enumerator
        // and closure allocations on this per-row hot path.
        var width = layout.TextLines.Select(line => line.WidthIncludingTrailingWhitespace).Prepend(0.0).Max();

        return new Size(width, GetLayoutHeight(layout));
    }

    /// <inheritdoc />
    public override void Render(DrawingContext context)
    {
        var layout = CurrentLayout ?? GetLayout(double.PositiveInfinity);

        if (string.IsNullOrEmpty(Text)) return;

        var highlightBrush = HighlightBrush;

        if (highlightBrush != null && Highlights.Count > 0)
            foreach (var rect in ComputeHighlightRects(layout, Text!.Length, Highlights))
                context.FillRectangle(highlightBrush, rect, HighlightCornerRadius);

        var syntaxLayouts = GetSyntaxLayouts(layout);

        if (syntaxLayouts == null)
        {
            layout.Draw(context, new Point(0, 0));

            return;
        }

        foreach (var (runLayout, x, y) in syntaxLayouts) runLayout.Draw(context, new Point(x, y));
    }

    private static double GetLayoutHeight(TextLayout layout)
    {
        return layout.TextLines.Sum(line => line.Height);
    }

    /// <summary>
    ///     The whole-line layout, cached per width: nowrap builds a single unconstrained line
    ///     (upstream white-space: pre); wrap builds a layout constrained to the measure width
    ///     (upstream pre-wrap), rebuilt whenever the width changes.
    /// </summary>
    private TextLayout GetLayout(double constraintWidth)
    {
        var wrapWidth = Wrap && double.IsFinite(constraintWidth) && constraintWidth > 0
            ? constraintWidth
            : double.PositiveInfinity;

        if (CurrentLayout != null && wrapWidth == _layoutWidth) return CurrentLayout;

        CurrentLayout = wrapWidth == double.PositiveInfinity
            ? new TextLayout(Text ?? string.Empty, new Typeface(FontFamily), FontSize, Foreground ?? Brushes.Black)
            : new TextLayout(Text ?? string.Empty, new Typeface(FontFamily), FontSize, Foreground ?? Brushes.Black,
                             textWrapping : TextWrapping.Wrap, maxWidth : wrapWidth);
        _layoutWidth   = wrapWidth;
        _syntaxLayouts = null;

        return CurrentLayout;
    }

    /// <summary>
    ///     Builds one small layout per syntax segment, offset to the coordinates the whole-line
    ///     layout reports for the segment start. Gaps between runs (plain segments) fall back to the
    ///     whole-line layout's default foreground, so they are drawn as part of the nearest
    ///     default-colored run — plain text is prepended to the first run's start and appended
    ///     after the last run's end via the fallback layout pass. A segment crossing a line break
    ///     (wrapped layout) is split per text line and each piece placed at its own coordinates.
    /// </summary>
    internal IReadOnlyList<(TextLayout Layout, double X, double Y)>? GetSyntaxLayouts(TextLayout layout)
    {
        if (_syntaxLayouts != null) return _syntaxLayouts;

        var runs = SyntaxRuns;

        if (runs is not { Count: > 0 }) return null;

        var text     = Text ?? string.Empty;
        var typeface = new Typeface(FontFamily);
        var plain    = Foreground ?? Brushes.Black;

        var layouts = new List<(TextLayout, double, double)>(runs.Count + 2);

        // Plain text before the first colored run.
        if (runs[0].Start > 0) AddSegmentLayouts(layouts, layout, typeface, text, 0, runs[0].Start, FontSize, plain);

        var previousEnd = 0;

        foreach (var run in runs)
        {
            var start = Math.Clamp(run.Start, 0, text.Length);
            var end   = Math.Clamp(run.Start + run.Length, start, text.Length);

            // A gap between runs renders with the default foreground.
            if (start > previousEnd)
                AddSegmentLayouts(layouts, layout, typeface, text, previousEnd, start, FontSize, plain);

            if (end > start) AddSegmentLayouts(layouts, layout, typeface, text, start, end, FontSize, run.Foreground);

            previousEnd = Math.Max(previousEnd, end);
        }

        // Plain text after the last colored run.
        if (previousEnd < text.Length)
            AddSegmentLayouts(layouts, layout, typeface, text, previousEnd, text.Length, FontSize, plain);

        _syntaxLayouts = layouts;

        return _syntaxLayouts;
    }

    /// <summary>
    ///     Adds the layouts for one foreground segment between <paramref name="start" /> and
    ///     <paramref name="end" />: the whole-line layout reports where each piece begins, so a
    ///     nowrap layout yields a single piece while a wrapped one yields one per text line.
    /// </summary>
    private static void AddSegmentLayouts(List<(TextLayout Layout, double X, double Y)> layouts,
                                          TextLayout layout, Typeface typeface, string text, int start, int end,
                                          double fontSize, IBrush foreground)
    {
        end = Math.Clamp(end, start, text.Length);

        var lines = layout.TextLines;
        var index = LineOfPosition(lines, start);

        for (var position = start; position < end; index++)
        {
            if (index >= lines.Count) break;

            var line     = lines[index];
            var pieceEnd = Math.Min(end, line.FirstTextSourceIndex + line.Length);

            if (pieceEnd > position)
            {
                var location = layout.HitTestTextPosition(position);

                layouts.Add((new TextLayout(text[position..pieceEnd], typeface, fontSize, foreground),
                             location.X, location.Y));
                position = pieceEnd;
            }
            else
            {
                // A zero-length line cannot advance the position by itself.
                position++;
            }
        }
    }

    /// <inheritdoc />
    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);

        // The cached layouts depend on text, font, foreground, wrap mode, and the run structure;
        // highlight ranges and brushes only affect rendering.
        if (change.Property != TextProperty       &&
            change.Property != FontFamilyProperty &&
            change.Property != FontSizeProperty   &&
            change.Property != ForegroundProperty &&
            change.Property != WrapProperty       &&
            change.Property != SyntaxRunsProperty) return;
        CurrentLayout  = null;
        _layoutWidth   = double.NaN;
        _syntaxLayouts = null;
    }
}
