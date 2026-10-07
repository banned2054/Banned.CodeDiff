using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Media;
using Avalonia.Media.TextFormatting;
using Banned.CodeDiff.Avalonia.Models;

namespace Banned.CodeDiff.Avalonia.Views;

/// <summary>
///     渲染 diff 行的语法颜色与词级高亮,支持自动换行。<br />
///     Renders syntax colors and word-level highlights for a diff line, with optional wrapping.
/// </summary>
public sealed class DiffSegmentText : Control
{
    /// <summary>Corner radius of the highlight rectangles, approximating the upstream 0.2em.</summary>
    private const float HighlightCornerRadius = 2f;

    /// <summary>标识 <see cref="Text" /> 依赖属性。<br />Identifies the <see cref="Text" /> dependency property.</summary>
    public static readonly StyledProperty<string?> TextProperty =
        AvaloniaProperty.Register<DiffSegmentText, string?>(nameof(Text));

    /// <summary>标识 <see cref="Wrap" /> 依赖属性。<br />Identifies the <see cref="Wrap" /> dependency property.</summary>
    public static readonly StyledProperty<bool> WrapProperty =
        AvaloniaProperty.Register<DiffSegmentText, bool>(nameof(Wrap));

    /// <summary>标识 <see cref="Highlights" /> 依赖属性。<br />Identifies the <see cref="Highlights" /> dependency property.</summary>
    public static readonly StyledProperty<IReadOnlyList<DiffHighlight>> HighlightsProperty =
        AvaloniaProperty.Register<DiffSegmentText, IReadOnlyList<DiffHighlight>>(nameof(Highlights));

    /// <summary>标识 <see cref="SyntaxRuns" /> 依赖属性。<br />Identifies the <see cref="SyntaxRuns" /> dependency property.</summary>
    public static readonly StyledProperty<IReadOnlyList<DiffSyntaxRun>?> SyntaxRunsProperty =
        AvaloniaProperty.Register<DiffSegmentText, IReadOnlyList<DiffSyntaxRun>?>(nameof(SyntaxRuns));

    /// <summary>标识 <see cref="HighlightBrush" /> 依赖属性。<br />Identifies the <see cref="HighlightBrush" /> dependency property.</summary>
    public static readonly StyledProperty<IBrush?> HighlightBrushProperty =
        AvaloniaProperty.Register<DiffSegmentText, IBrush?>(nameof(HighlightBrush));

    /// <summary>标识 <see cref="FontFamily" /> 依赖属性。<br />Identifies the <see cref="FontFamily" /> dependency property.</summary>
    public static readonly StyledProperty<FontFamily> FontFamilyProperty =
        TextElement.FontFamilyProperty.AddOwner<DiffSegmentText>();

    /// <summary>标识 <see cref="FontSize" /> 依赖属性。<br />Identifies the <see cref="FontSize" /> dependency property.</summary>
    public static readonly StyledProperty<double> FontSizeProperty =
        TextElement.FontSizeProperty.AddOwner<DiffSegmentText>();

    /// <summary>标识 <see cref="Foreground" /> 依赖属性。<br />Identifies the <see cref="Foreground" /> dependency property.</summary>
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

    /// <summary>获取或设置要渲染的行文本。<br />Gets or sets the line text to render.</summary>
    public string? Text
    {
        get => GetValue(TextProperty);
        set => SetValue(TextProperty, value);
    }

    /// <summary>
    ///     是否按可用宽度换行并增加高度;过长单词在行边缘断开。<br />
    ///     Whether text wraps to the available width and grows in height; long words break at line edges.
    /// </summary>
    public bool Wrap
    {
        get => GetValue(WrapProperty);
        set => SetValue(WrapProperty, value);
    }

    /// <summary>
    ///     获取或设置 <see cref="Text" /> 内的词级高亮区间。<br />Gets or sets the word-level highlight ranges within
    ///     <see cref="Text" />.
    /// </summary>
    public IReadOnlyList<DiffHighlight> Highlights
    {
        get => GetValue(HighlightsProperty);
        set => SetValue(HighlightsProperty, value);
    }

    /// <summary>
    ///     获取或设置 <see cref="Text" /> 内的语法着色文本段;<c>null</c> 时整行以
    ///     <see cref="Foreground" /> 渲染。<br />
    ///     Gets or sets the syntax-colored segments within <see cref="Text" />;
    ///     <c>null</c> renders the whole line with <see cref="Foreground" />.
    /// </summary>
    public IReadOnlyList<DiffSyntaxRun>? SyntaxRuns
    {
        get => GetValue(SyntaxRunsProperty);
        set => SetValue(SyntaxRunsProperty, value);
    }

    /// <summary>获取或设置用于绘制高亮矩形的画刷。<br />Gets or sets the brush used to paint the highlight rectangles.</summary>
    public IBrush? HighlightBrush
    {
        get => GetValue(HighlightBrushProperty);
        set => SetValue(HighlightBrushProperty, value);
    }

    /// <summary>获取或设置渲染文本所用的字体系列。<br />Gets or sets the font family used to render the text.</summary>
    public FontFamily FontFamily
    {
        get => GetValue(FontFamilyProperty);
        set => SetValue(FontFamilyProperty, value);
    }

    /// <summary>获取或设置渲染文本所用的字号。<br />Gets or sets the font size used to render the text.</summary>
    public double FontSize
    {
        get => GetValue(FontSizeProperty);
        set => SetValue(FontSizeProperty, value);
    }

    /// <summary>获取或设置渲染文本所用的画刷。<br />Gets or sets the brush used to render the text.</summary>
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
    ///     计算词级高亮矩形;跨行区间按文本行拆分。
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

        var width = 0.0;

        foreach (var line in layout.TextLines)
        {
            var lineWidth = line.WidthIncludingTrailingWhitespace;

            if (lineWidth > width) width = lineWidth;
        }

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
        const float tolerance = 0.0000001f;
        if (CurrentLayout != null && Math.Abs(wrapWidth - _layoutWidth) < tolerance) return CurrentLayout;

        CurrentLayout = double.IsPositiveInfinity(wrapWidth)
            ? new TextLayout(Text ?? string.Empty, new Typeface(FontFamily), FontSize, Foreground ?? Brushes.Black)
            : new TextLayout(Text ?? string.Empty, new Typeface(FontFamily), FontSize, Foreground ?? Brushes.Black,
                             textWrapping : TextWrapping.Wrap, maxWidth : wrapWidth);
        _layoutWidth   = wrapWidth;
        _syntaxLayouts = null;

        return CurrentLayout;
    }

    /// <summary>
    ///     按文本行生成着色布局,空隙使用默认前景色。
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

        if (runs[0].Start > 0) AddSegmentLayouts(layouts, layout, typeface, text, 0, runs[0].Start, FontSize, plain);

        var previousEnd = 0;

        foreach (var run in runs)
        {
            var start = Math.Clamp(run.Start, 0, text.Length);
            var end   = Math.Clamp(run.Start + run.Length, start, text.Length);

            if (start > previousEnd)
                AddSegmentLayouts(layouts, layout, typeface, text, previousEnd, start, FontSize, plain);

            if (end > start) AddSegmentLayouts(layouts, layout, typeface, text, start, end, FontSize, run.Foreground);

            previousEnd = Math.Max(previousEnd, end);
        }

        if (previousEnd < text.Length)
            AddSegmentLayouts(layouts, layout, typeface, text, previousEnd, text.Length, FontSize, plain);

        _syntaxLayouts = layouts;

        return _syntaxLayouts;
    }

    /// <summary>
    ///     将一个前景色区间按文本行拆分,并定位到整行布局的坐标。
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
                // 空文本行不能推进位置。
                position++;
            }
        }
    }

    /// <inheritdoc />
    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);

        // 高亮范围和画刷仅影响绘制,无需重建文本布局。
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
