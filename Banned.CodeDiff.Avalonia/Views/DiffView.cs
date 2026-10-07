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
///     只读 GitHub 风格 diff 控件,支持分栏与统一视图。按需初始化模型,并订阅 <see cref="DiffFile" /> 的更新。<br />
///     Read-only GitHub-style split/unified diff control; initializes the model as needed and subscribes to its updates.
/// </summary>
public sealed class DiffView : TemplatedControl
{
    /// <summary>Minimum width of a line-number column, matching the upstream aside width.</summary>
    private const double NumberColumnMinWidth = 40;

    /// <summary>Horizontal padding of a line-number column (10px on each side upstream).</summary>
    private const double NumberColumnPadding = 20;

    /// <summary>Approximate advance width of one monospace digit relative to the font size.</summary>
    private const double MonospaceCharWidthRatio = 0.62;

    /// <summary>
    ///     Viewport-sized probe scrolls an unrealized expansion anchor may receive before the
    ///     tune gives up (see OnAnchorTuneLayout) — generous enough for several tall comment
    ///     cards, bounded so a pathological expansion cannot scroll forever.
    /// </summary>
    private const int MaxAnchorTuneProbes = 24;

    /// <summary>标识 <see cref="DiffFile" /> 依赖属性。<br />Identifies the <see cref="DiffFile" /> dependency property.</summary>
    public static readonly StyledProperty<DiffFile?> DiffFileProperty =
        AvaloniaProperty.Register<DiffView, DiffFile?>(nameof(DiffFile));

    /// <summary>标识 <see cref="ViewMode" /> 依赖属性。<br />Identifies the <see cref="ViewMode" /> dependency property.</summary>
    public static readonly StyledProperty<DiffViewMode> ViewModeProperty =
        AvaloniaProperty.Register<DiffView, DiffViewMode>(nameof(ViewMode));

    /// <summary>
    ///     标识 <see cref="SyntaxHighlight" /> 依赖属性。<br />Identifies the <see cref="SyntaxHighlight" /> dependency
    ///     property.
    /// </summary>
    public static readonly StyledProperty<bool> SyntaxHighlightProperty =
        AvaloniaProperty.Register<DiffView, bool>(nameof(SyntaxHighlight), true);

    /// <summary>
    ///     标识 <see cref="IsSelectionEnabled" /> 依赖属性。<br />Identifies the <see cref="IsSelectionEnabled" /> dependency
    ///     property.
    /// </summary>
    public static readonly StyledProperty<bool> IsSelectionEnabledProperty =
        AvaloniaProperty.Register<DiffView, bool>(nameof(IsSelectionEnabled));

    /// <summary>标识 <see cref="Wrap" /> 依赖属性。<br />Identifies the <see cref="Wrap" /> dependency property.</summary>
    public static readonly StyledProperty<bool> WrapProperty =
        AvaloniaProperty.Register<DiffView, bool>(nameof(Wrap));

    /// <summary>
    ///     标识 <see cref="UseSingleLineNumberColumn" /> 依赖属性。<br />Identifies the
    ///     <see cref="UseSingleLineNumberColumn" /> dependency property.
    /// </summary>
    public static readonly StyledProperty<bool> UseSingleLineNumberColumnProperty =
        AvaloniaProperty.Register<DiffView, bool>(nameof(UseSingleLineNumberColumn));

    /// <summary>标识 <see cref="FilePath" /> 依赖属性。<br />Identifies the <see cref="FilePath" /> dependency property.</summary>
    public static readonly StyledProperty<string?> FilePathProperty =
        AvaloniaProperty.Register<DiffView, string?>(nameof(FilePath));

    /// <summary>标识 <see cref="Comments" /> 依赖属性。<br />Identifies the <see cref="Comments" /> dependency property.</summary>
    public static readonly StyledProperty<IReadOnlyList<DiffComment>?> CommentsProperty =
        AvaloniaProperty.Register<DiffView, IReadOnlyList<DiffComment>?>(nameof(Comments));

    /// <summary>标识 <see cref="Palette" /> 依赖属性。<br />Identifies the <see cref="Palette" /> dependency property.</summary>
    public static readonly StyledProperty<DiffPalette?> PaletteProperty =
        AvaloniaProperty.Register<DiffView, DiffPalette?>(nameof(Palette));

    /// <summary>标识 <see cref="ThemePreset" /> 依赖属性。<br />Identifies the <see cref="ThemePreset" /> dependency property.</summary>
    public static readonly StyledProperty<DiffThemePreset?> ThemePresetProperty =
        AvaloniaProperty.Register<DiffView, DiffThemePreset?>(nameof(ThemePreset));

    /// <summary>标识 <see cref="DiffPreset" /> 依赖属性。<br />Identifies the <see cref="DiffPreset" /> dependency property.</summary>
    public static readonly StyledProperty<DiffThemePreset?> DiffPresetProperty =
        AvaloniaProperty.Register<DiffView, DiffThemePreset?>(nameof(DiffPreset));

    /// <summary>标识 <see cref="SyntaxPreset" /> 依赖属性。<br />Identifies the <see cref="SyntaxPreset" /> dependency property.</summary>
    public static readonly StyledProperty<DiffThemePreset?> SyntaxPresetProperty =
        AvaloniaProperty.Register<DiffView, DiffThemePreset?>(nameof(SyntaxPreset));

    /// <summary>标识 <see cref="SyntaxOverrides" /> 依赖属性。<br />Identifies the <see cref="SyntaxOverrides" /> dependency property.</summary>
    public static readonly StyledProperty<DiffSyntaxOverrides?> SyntaxOverridesProperty =
        AvaloniaProperty.Register<DiffView, DiffSyntaxOverrides?>(nameof(SyntaxOverrides));

    /// <summary>标识 <see cref="Highlighter" /> 依赖属性。<br />Identifies the <see cref="Highlighter" /> dependency property.</summary>
    public static readonly StyledProperty<IDiffHighlighter?> HighlighterProperty =
        AvaloniaProperty.Register<DiffView, IDiffHighlighter?>(nameof(Highlighter));

    /// <summary>标识 <see cref="Rows" /> 直接属性。<br />Identifies the <see cref="Rows" /> direct property.</summary>
    public static readonly DirectProperty<DiffView, IReadOnlyList<DiffRow>> RowsProperty =
        AvaloniaProperty.RegisterDirect<DiffView, IReadOnlyList<DiffRow>>(nameof(Rows), o => o.Rows);

    /// <summary>
    ///     标识 <see cref="NumberColumnWidth" /> 直接属性。<br />Identifies the <see cref="NumberColumnWidth" /> direct
    ///     property.
    /// </summary>
    public static readonly DirectProperty<DiffView, double> NumberColumnWidthProperty =
        AvaloniaProperty.RegisterDirect<DiffView, double>(nameof(NumberColumnWidth), o => o.NumberColumnWidth);

    /// <summary>The commands whose CanExecute follows the selection/model state.</summary>
    private readonly List<IStateCommand> _commands;

    /// <summary>
    ///     Rows/cells currently flagged selected — cleared first on every visual pass
    ///     (upstream removes the CSS class from every row before re-applying ranges).
    /// </summary>
    private readonly List<DiffSplitCellModel> _selectedSplitCells = [];

    private readonly List<DiffUnifiedContentRow> _selectedUnifiedRows = [];

    /// <summary>Cells currently flagged commented — the comment-anchor channel, independent of the selection.</summary>
    private readonly List<DiffSplitCellModel> _commentedSplitCells = [];

    private readonly List<DiffUnifiedContentRow> _commentedUnifiedRows = [];

    /// <summary>The multi-select state machine (upstream multiSelect/manager.ts).</summary>
    private readonly DiffSelection _selection = new();

    private ItemsControl? _items;

    private double  _numberColumnWidth = NumberColumnMinWidth;
    private Vector? _pendingExpandOffset;
    private int     _anchorTuneLayouts;
    private int     _anchorTuneApplies;
    private int     _anchorTuneProbes;
    private double  _anchorTuneLastTop;
    private bool    _anchorTuneApplied;
    private bool    _rebuilding;

    /// <summary>Pending post-layout correction of an expansion anchor (see OnAnchorTuneLayout).</summary>
    private ExpandAnchorTune? _pendingAnchorTune;

    private IReadOnlyList<DiffRow> _rows = [];
    private ScrollViewer?          _scrollViewer;

    /// <summary>
    ///     当前行的懒加载索引;重建行时清空。
    /// </summary>
    private Dictionary<int, DiffSplitContentRow>? _splitRowsByLineIndex;

    /// <summary>初始化 <see cref="DiffView" /> 类的新实例。<br />Initializes a new instance of the <see cref="DiffView" /> class.</summary>
    public DiffView()
    {
        // 保留样式和继承值对默认字体的覆盖。
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

        var beginComment = new DiffBeginCommentCommand(this);

        BeginCommentCommand = beginComment;

        _commands = [copySelection, copyOldFile, copyNewFile, beginComment];

        // 行模型持有画刷,主题变化须重建。
        ActualThemeVariantChanged += (_, _) => RebuildRows();

        _selection.SelectionChanged   += OnSelectionChanged;
        _selection.SelectionCompleted += OnSelectionCompleted;
    }

    /// <summary>获取或设置要渲染的 diff 模型。<c>null</c> 会清空视图。<br />Gets or sets the diff model to render. <c>null</c> clears the view.</summary>
    public DiffFile? DiffFile
    {
        get => GetValue(DiffFileProperty);
        set => SetValue(DiffFileProperty, value);
    }

    /// <summary>获取或设置显示模式(分栏或统一)。<br />Gets or sets the display mode (split or unified).</summary>
    public DiffViewMode ViewMode
    {
        get => GetValue(ViewModeProperty);
        set => SetValue(ViewModeProperty, value);
    }

    /// <summary>
    ///     是否启用语法高亮。<br />
    ///     Whether syntax highlighting is enabled.
    /// </summary>
    public bool SyntaxHighlight
    {
        get => GetValue(SyntaxHighlightProperty);
        set => SetValue(SyntaxHighlightProperty, value);
    }

    /// <summary>
    ///     语法高亮器;<c>null</c> 使用内置 TextMate 引擎。<br />
    ///     Syntax highlighter; null uses the built-in TextMate engine.
    /// </summary>
    public IDiffHighlighter? Highlighter
    {
        get => GetValue(HighlighterProperty);
        set => SetValue(HighlighterProperty, value);
    }

    /// <summary>
    ///     长行是否自动换行,默认 <c>false</c>;行号不换行,分栏两侧等高。<br />
    ///     Whether long lines wrap; default false. Line numbers never wrap and split cells share a row height.
    /// </summary>
    public bool Wrap
    {
        get => GetValue(WrapProperty);
        set => SetValue(WrapProperty, value);
    }

    /// <summary>
    ///     统一视图是否合并行号列,默认 <c>false</c>;删除行用旧号,其他行用新号。切换不重建行。<br />
    ///     Whether unified line numbers share one column; default false. Deleted lines use old numbers, others use new numbers; switching does not rebuild rows.
    /// </summary>
    public bool UseSingleLineNumberColumn
    {
        get => GetValue(UseSingleLineNumberColumnProperty);
        set => SetValue(UseSingleLineNumberColumnProperty, value);
    }

    /// <summary>
    ///     是否允许从行号格拖选行范围,默认 <c>false</c>。<br />
    ///     Whether line-number cells allow drag selection; default false.
    /// </summary>
    public bool IsSelectionEnabled
    {
        get => GetValue(IsSelectionEnabledProperty);
        set => SetValue(IsSelectionEnabledProperty, value);
    }

    /// <summary>
    ///     获取当前渲染的扁平行列表(内容行、hunk 占位行与评论卡片行)。<br />Gets the flat row list currently rendered (content rows, hunk
    ///     placeholders, and comment card rows).
    /// </summary>
    public IReadOnlyList<DiffRow> Rows => _rows;

    /// <summary>
    ///     宿主提供的文件身份,默认 <c>null</c>;仅呈现锚点身份匹配的评论。<br />
    ///     Host-provided file identity, default null; only comments with matching anchor identities render.
    /// </summary>
    public string? FilePath
    {
        get => GetValue(FilePathProperty);
        set => SetValue(FilePathProperty, value);
    }

    /// <summary>
    ///     同文件的宿主评论,每个锚点在最后可见行后呈现一张卡片并独立高亮。默认 <c>null</c>;变更后须赋新集合刷新。<br />
    ///     Host comments for this file; each anchor renders a card after its last visible line with independent highlighting. Default null; assign a new collection to refresh changes.
    /// </summary>
    public IReadOnlyList<DiffComment>? Comments
    {
        get => GetValue(CommentsProperty);
        set => SetValue(CommentsProperty, value);
    }

    /// <summary>
    ///     宿主 Diff 配色覆盖,优先于预设;<c>null</c> 使用预设值。赋值重建行。<br />
    ///     Host diff color overrides, taking precedence over presets; null uses preset values. Assignment rebuilds rows.
    /// </summary>
    public DiffPalette? Palette
    {
        get => GetValue(PaletteProperty);
        set => SetValue(PaletteProperty, value);
    }

    /// <summary>
    ///     组合 Diff/语法预设,默认 GitHub。优先级:宿主覆盖 → 独立预设 → 组合预设 → GitHub;赋值重建行。<br />
    ///     Combined diff/syntax preset, default GitHub. Priority: host overrides, independent presets, combined preset, GitHub; assignment rebuilds rows.
    /// </summary>
    public DiffThemePreset? ThemePreset
    {
        get => GetValue(ThemePresetProperty);
        set => SetValue(ThemePresetProperty, value);
    }

    /// <summary>
    ///     独立 Diff 预设,覆盖组合预设的 Diff 配色;不影响语法。赋值重建行。<br />
    ///     Independent diff preset, overriding the combined diff palette without changing syntax; assignment rebuilds rows.
    /// </summary>
    public DiffThemePreset? DiffPreset
    {
        get => GetValue(DiffPresetProperty);
        set => SetValue(DiffPresetProperty, value);
    }

    /// <summary>
    ///     独立语法预设,覆盖组合预设且不重新分词;缺少浅色变体时回退 GitHub 浅色。赋值重建行。<br />
    ///     Independent syntax preset, overriding the combined theme without retokenizing; missing light variants use GitHub light. Assignment rebuilds rows.
    /// </summary>
    public DiffThemePreset? SyntaxPreset
    {
        get => GetValue(SyntaxPresetProperty);
        set => SetValue(SyntaxPresetProperty, value);
    }

    /// <summary>
    ///     宿主语法 scope 与默认前景覆盖,切换预设时保留。赋值重建行。<br />
    ///     Host syntax scope and default-foreground overrides, retained across preset changes; assignment rebuilds rows.
    /// </summary>
    public DiffSyntaxOverrides? SyntaxOverrides
    {
        get => GetValue(SyntaxOverridesProperty);
        set => SetValue(SyntaxOverridesProperty, value);
    }

    /// <summary>获取按当前行与字号解析出的行号列宽度。<br />Gets the resolved line-number column width for the current rows and font size.</summary>
    public double NumberColumnWidth => _numberColumnWidth;

    /// <summary>
    ///     获取将 hunk 行向上展开一个 compose 长度(40 行)的命令;命令参数为
    ///     <see cref="DiffSplitHunkRow" /> 或 <see cref="DiffUnifiedHunkRow" />。<br />
    ///     Gets the command that expands a hunk row up by the compose length (40 lines);
    ///     the command parameter is the <see cref="DiffSplitHunkRow" /> or <see cref="DiffUnifiedHunkRow" />.
    /// </summary>
    public ICommand ExpandHunkUpCommand { get; }

    /// <summary>
    ///     获取将 hunk 行向下展开一个 compose 长度(40 行)的命令;命令参数为
    ///     <see cref="DiffSplitHunkRow" /> 或 <see cref="DiffUnifiedHunkRow" />。<br />
    ///     Gets the command that expands a hunk row down by the compose length (40 lines);
    ///     the command parameter is the <see cref="DiffSplitHunkRow" /> or <see cref="DiffUnifiedHunkRow" />.
    /// </summary>
    public ICommand ExpandHunkDownCommand { get; }

    /// <summary>
    ///     获取完全展开一个 hunk 行的命令;命令参数为
    ///     <see cref="DiffSplitHunkRow" /> 或 <see cref="DiffUnifiedHunkRow" />。<br />
    ///     Gets the command that fully expands a hunk row; the command parameter is the
    ///     <see cref="DiffSplitHunkRow" /> or <see cref="DiffUnifiedHunkRow" />.
    /// </summary>
    public ICommand ExpandHunkAllCommand { get; }

    /// <summary>
    ///     复制可见选区的命令;无可见选中行时不可执行,快捷键由宿主绑定。<br />
    ///     Copies visible selected text; disabled without visible selected lines. Keyboard shortcuts are host-defined.
    /// </summary>
    public ICommand CopySelectionCommand { get; }

    /// <summary>
    ///     获取复制整个旧侧文件内容的命令(<see cref="CopyOldFileAsync" />);与任何选区无关。
    ///     本机移植新增。<br />
    ///     Gets the command that copies the whole old-side file content
    ///     (<see cref="CopyOldFileAsync" />); independent of any selection. Native port addition.
    /// </summary>
    public ICommand CopyOldFileCommand { get; }

    /// <summary>
    ///     获取复制整个新侧文件内容的命令(<see cref="CopyNewFileAsync" />);与任何选区无关。
    ///     本机移植新增。<br />
    ///     Gets the command that copies the whole new-side file content
    ///     (<see cref="CopyNewFileAsync" />); independent of any selection. Native port addition.
    /// </summary>
    public ICommand CopyNewFileCommand { get; }

    /// <summary>
    ///     从可见选区发起评论并引发 <see cref="CommentRequested" />;编辑器由宿主提供,普通拖选不触发评论。<br />
    ///     Starts a comment from the visible selection and raises <see cref="CommentRequested" />; the host supplies the editor. Ordinary selection does not start comments.
    /// </summary>
    public ICommand BeginCommentCommand { get; }

    /// <summary>
    ///     在选区拖拽移动过程中以及选区被清空时发生——对应上游管理器的 onSelectionChange。
    ///     清空时区间为 <c>null</c>。<br />
    ///     Occurs while a selection drag moves and when the selection is cleared — the upstream
    ///     manager's onSelectionChange. The range is <c>null</c> on clear.
    /// </summary>
    public event EventHandler<DiffSelectionChangedEventArgs>? SelectionChanged;

    /// <summary>
    ///     拖选释放时发生;无区间时结果为 <c>null</c>。<br />
    ///     Raised on selection release; the result is null without a range.
    /// </summary>
    public event EventHandler<DiffSelectionCompletedEventArgs>? SelectionCompleted;

    /// <summary>
    ///     显式发起评论时发生;宿主负责编辑、提交及持久化。<br />
    ///     Raised when a comment is explicitly requested; editing, submission and persistence belong to the host.
    /// </summary>
    public event EventHandler<DiffCommentRequestedEventArgs>? CommentRequested;

    /// <summary>
    ///     返回当前或已完成的选区结果,直到下次交互清除;无区间时为 <c>null</c>。<br />
    ///     Returns the current or completed selection until the next interaction clears it; null without a range.
    /// </summary>
    public MultiSelectResult? GetSelectionResult()
    {
        return _selection.GetSelectionResult(DiffFile, ViewMode == DiffViewMode.Unified);
    }

    /// <summary>获取当前选区状态(上游管理器的 getState)。<br />Gets the current selection state (upstream manager getState).</summary>
    public MultiSelectState GetSelectionState()
    {
        return _selection.GetState();
    }

    /// <summary>
    ///     按 <see cref="FilePath" /> 与当前选区生成规范化评论锚点;无模型或选区时返回 <c>null</c>。<br />
    ///     Builds a normalized comment anchor from <see cref="FilePath" /> and the selection; returns null without a model or range.
    /// </summary>
    public DiffCommentAnchor? GetCommentAnchor()
    {
        var result = GetSelectionResult();

        if (result == null) return null;

        return new DiffCommentAnchor(FilePath, result.Range.Side, result.Range.StartLineNumber,
                                     result.Range.EndLineNumber);
    }

    /// <summary>
    ///     清除当前选区和持久化的完成态高亮。<br />
    ///     Clears the active selection and persisted completed-selection highlighting.
    /// </summary>
    public void ClearSelection()
    {
        _selection.ClearSelection();
        _selection.SetPreselectedLines(MultiSelectPreselectedLines.Empty);
        ApplySelectionVisual();
    }

    /// <summary>
    ///     设置预选行;每侧按 min/max 合并,包含离散行号之间的所有行。<br />
    ///     Sets preselected lines; each side becomes an inclusive min/max range covering intervening lines.
    /// </summary>
    public void SetPreselectedLines(IReadOnlyList<int>? oldLines = null, IReadOnlyList<int>? newLines = null)
    {
        _selection.SetPreselectedLines(new MultiSelectPreselectedLines(oldLines, newLines));
        ApplySelectionVisual();
    }

    // ---- copy (native port feature — the upstream library has no copy counterpart) ----

    /// <summary>
    ///     复制可见选区纯文本;无可见选区或剪贴板时返回 <c>false</c>。<br />
    ///     Copies visible selected text; returns false without a visible selection or clipboard.
    /// </summary>
    public async Task<bool> CopySelectionAsync()
    {
        var text = MultiSelectData.GetSelectedTextFromResult(GetSelectionResult());

        if (text.Length == 0) return false;

        return await SetClipboardTextAsync(text);
    }

    /// <summary>
    ///     将整个旧侧文件内容(<c>DiffFile.OldFileRaw</c>)复制到剪贴板,末尾换行符原样保留。
    ///     模型没有旧侧内容时返回 <c>false</c>,且不触碰剪贴板。<br />
    ///     Copies the whole old-side file content (<c>DiffFile.OldFileRaw</c>) to the
    ///     clipboard, its trailing newline kept as-is. Returns <c>false</c> — without touching the
    ///     clipboard — when the model has no old-side content.
    /// </summary>
    public Task<bool> CopyOldFileAsync()
    {
        return CopyFileContentAsync(DiffFile?.OldFileRaw);
    }

    /// <summary>
    ///     将整个新侧文件内容(<c>DiffFile.NewFileRaw</c>)复制到剪贴板,末尾换行符原样保留。
    ///     模型没有新侧内容时返回 <c>false</c>,且不触碰剪贴板。<br />
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
    ///     检查选区是否含可见行,避免构造完整选择结果。
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
    ///     Invalidates the state-driven commands after a selection or model change (the file
    ///     commands track the model, the selection command and the comment command track the
    ///     current selection result).
    /// </summary>
    private void RaiseCommandsCanExecuteChanged()
    {
        foreach (var command in _commands) command.RaiseCanExecuteChanged();
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
                // 切换模式清除预选行,保留交互状态,与上游一致。
                _selection.SetPreselectedLines(MultiSelectPreselectedLines.Empty);

            RebuildRows();
        }
        else if (change.Property == IsSelectionEnabledProperty && !change.GetNewValue<bool>())
        {
            // 禁用多选时清除选区并关闭指针处理。
            ClearSelection();
        }
        else if (change.Property == CommentsProperty   || change.Property == PaletteProperty      ||
                 change.Property == FilePathProperty   || change.Property == ThemePresetProperty  ||
                 change.Property == DiffPresetProperty || change.Property == SyntaxPresetProperty ||
                 change.Property == SyntaxOverridesProperty)
        {
            // 评论、文件身份和配色均影响行模型,须重建。
            RebuildRows();
        }
        else if (change.Property == FontSizeProperty)
        {
            UpdateNumberColumnWidth();
        }
    }

    private void OnFileUpdated()
    {
        // 构建行也会触发 Updated,须防止重入。
        if (!_rebuilding) RebuildRows();
    }

    private void RebuildRows()
    {
        _rebuilding = true;

        // 旧锚点指向即将替换的行。
        DropPendingAnchorTune();

        try
        {
            var file = DiffFile;
            var rows = (IReadOnlyList<DiffRow>)[];

            if (file != null)
            {
                // 原文与语法分开初始化,禁用高亮时不分词。
                file.InitRaw();

                if (SyntaxHighlight) file.InitSyntax(Highlighter);

                rows = ViewMode switch
                {
                    DiffViewMode.Unified => BuildUnified(file),
                    _                    => BuildSplit(file)
                };

                rows = InsertCommentRows(rows);
            }

            SetAndRaise(RowsProperty, ref _rows, rows);
            _splitRowsByLineIndex = null;
            UpdateNumberColumnWidth();
            ApplyCanvasBackground();

            // 新行实例须重新应用选区;隐藏行展开后恢复高亮。
            ApplySelectionVisual();

            // 评论按稳定行号重新定位,不改变选区状态。
            ApplyCommentVisual();

            // 展开状态影响命令可用性。
            RaiseCommandsCanExecuteChanged();
        }
        finally
        {
            _rebuilding = false;
        }
    }

    private IReadOnlyList<DiffRow> BuildSplit(DiffFile file)
    {
        file.BuildSplitDiffLines();
        return DiffSplitRowBuilder.Build(file, CreateThemeContext());
    }

    private IReadOnlyList<DiffRow> BuildUnified(DiffFile file)
    {
        file.BuildUnifiedDiffLines();
        return DiffUnifiedRowBuilder.Build(file, CreateThemeContext());
    }

    /// <summary>
    ///     当前控件实例的配色解析输入(变体 + 宿主覆盖 + 预设选择)。所有层都来自本实例的
    ///     属性——不同 <see cref="DiffView" /> 实例互不影响。<br />
    ///     This control instance's color-resolution input (variant + host overrides + preset
    ///     choices). Every layer comes from this instance's properties — separate
    ///     <see cref="DiffView" /> instances never affect each other.
    /// </summary>
    private DiffThemeContext CreateThemeContext() =>
        new(ActualThemeVariant, Palette, ThemePreset, DiffPreset, SyntaxPreset, SyntaxOverrides);


    /// <summary>
    ///     应用画布配色;撤销时恢复宿主背景,不覆盖宿主后续设置。
    /// </summary>
    private void ApplyCanvasBackground()
    {
        var canvas = DiffBrushes.Get(ActualThemeVariant, Palette, ThemePreset, DiffPreset).CanvasBackground;

        if (canvas != null)
        {
            // 记录宿主最新背景,清除画布配色时恢复它。
            if (!_canvasApplied || !ReferenceEquals(Background, _appliedCanvasBrush))
                _backgroundBeforeCanvas = Background;

            _appliedCanvasBrush = canvas;
            _canvasApplied      = true;
            SetCurrentValue(BackgroundProperty, canvas);
        }
        else if (_canvasApplied)
        {
            _canvasApplied = false;

            // 仅撤销本控件设置的画布,保留宿主后续赋值。
            if (ReferenceEquals(Background, _appliedCanvasBrush))
                SetCurrentValue(BackgroundProperty, _backgroundBeforeCanvas);
        }
    }

    private bool    _canvasApplied;
    private IBrush? _appliedCanvasBrush;
    private IBrush? _backgroundBeforeCanvas;

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
            // 拖选必须从行号格开始。
            var numberHolder = DiffSelectionDom.GetNumberHolderElement_Split(hit, true);

            if (numberHolder == null) return;

            var line = DiffSelectionDom.GetLineNumberFromElement_Split(numberHolder);

            if (line == null) return;

            var side = DiffSelectionDom.GetSideFromElement_Split(numberHolder);

            if (side == null) return;

            _selection.HandlePointerPressed_Split(side.Value, line.Value);
        }

        // 捕获指针,确保控件外释放也能结束拖选。
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
            // 拖选时悬停内容格也可延伸范围。
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

        _selection.HandlePointerReleased(DiffFile, ViewMode == DiffViewMode.Unified);

        e.Pointer.Capture(null);
    }

    /// <inheritdoc />
    protected override void OnPointerCaptureLost(PointerCaptureLostEventArgs e)
    {
        base.OnPointerCaptureLost(e);

        // 丢失捕获也须结束拖选,避免状态卡住。
        if (IsSelectionEnabled && _selection.GetState().IsSelecting)
            _selection.HandlePointerReleased(DiffFile, ViewMode == DiffViewMode.Unified);
    }

    private Visual? HitTestRowVisual(PointerEventArgs e)
    {
        return _items?.InputHitTest(e.GetPosition(_items)) as Visual;
    }

    /// <summary>
    ///     新拖选清除上次完成态高亮。
    /// </summary>
    private void OnSelectionChanged(MultiSelectRange? range, MultiSelectState state)
    {
        if (state.IsSelecting && !_selection.PreselectedLines.IsEmpty)
            _selection.SetPreselectedLines(MultiSelectPreselectedLines.Empty);

        ApplySelectionVisual();

        RaiseCommandsCanExecuteChanged();

        SelectionChanged?.Invoke(this, new DiffSelectionChangedEventArgs(range, state));
    }

    /// <summary>
    ///     将完成选区保存为预选范围,空结果清除高亮。
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

        RaiseCommandsCanExecuteChanged();

        SelectionCompleted?.Invoke(this, new DiffSelectionCompletedEventArgs(result));
    }

    /// <summary>
    ///     按当前与预选范围更新行高亮;隐藏行展开后重新匹配。
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

        // 上下文选区覆盖两侧,变更仅覆盖所选侧。
        ApplySplitRanges(file, normalizedRanges, true, SelectCell);
        ApplyUnifiedRanges(normalizedRanges, row =>
        {
            row.IsSelected = true;
            _selectedUnifiedRows.Add(row);
        });
    }

    /// <summary>
    ///     独立更新评论高亮,不受新拖选影响;分栏上下文仅标记锚点侧。
    /// </summary>
    private void ApplyCommentVisual()
    {
        foreach (var cell in _commentedSplitCells) cell.IsCommented = false;

        _commentedSplitCells.Clear();

        foreach (var row in _commentedUnifiedRows) row.IsCommented = false;

        _commentedUnifiedRows.Clear();

        var comments = Comments;
        var file     = DiffFile;

        if (comments == null || comments.Count == 0 || file == null) return;

        // 仅高亮当前文件的评论。
        var filePath = FilePath;

        var ranges = comments.Where(comment => comment.Anchor.FilePath == filePath)
                             .Select(comment =>
                                         MultiSelectData.NormalizeRange(new MultiSelectRange(comment.Anchor.Side,
                                                                                 comment.Anchor.StartLineNumber,
                                                                                 comment.Anchor.EndLineNumber)))
                             .ToList();

        ApplySplitRanges(file, ranges, false, cell =>
        {
            cell.IsCommented = true;
            _commentedSplitCells.Add(cell);
        });
        ApplyUnifiedRanges(ranges, row =>
        {
            row.IsCommented = true;
            _commentedUnifiedRows.Add(row);
        });
    }

    /// <summary>
    ///     匹配分栏范围;选区上下文标记两侧,评论仅标记锚点侧。
    /// </summary>
    private void ApplySplitRanges(DiffFile file, List<MultiSelectRange> allRanges, bool contextFlagsBothSides,
                                  Action<DiffSplitCellModel> apply)
    {
        // 每个 LineIndex 仅对应一个内容行,可按索引查找。
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

                if (contextFlagsBothSides && item.IsContext)
                {
                    apply(row.Left);
                    apply(row.Right);
                }
                else
                {
                    apply(range.Side == SplitSide.Old ? row.Left : row.Right);
                }
            }
        }
    }

    /// <summary>
    ///     Port of visual.ts updateSelectionVisual_Unified's matching loop: a row matches when its
    ///     old (or new) number falls inside an old-side (or new-side) range.
    /// </summary>
    private void ApplyUnifiedRanges(List<MultiSelectRange> allRanges, Action<DiffUnifiedContentRow> apply)
    {
        foreach (var row in _rows.OfType<DiffUnifiedContentRow>())
        {
            var matched =
                allRanges.Any(range =>
                                  (range.Side == SplitSide.Old         && row.OldLineNumber is { } rowLineOld &&
                                   rowLineOld >= range.StartLineNumber && rowLineOld <= range.EndLineNumber) ||
                                  (range.Side == SplitSide.New         && row.NewLineNumber is { } rowLineNew &&
                                   rowLineNew >= range.StartLineNumber && rowLineNew <= range.EndLineNumber));

            if (matched) apply(row);
        }
    }

    /// <summary>
    ///     同文件的每个锚点在范围内最后可见行后插入评论卡片;无可见行则不显示。
    /// </summary>
    private IReadOnlyList<DiffRow> InsertCommentRows(IReadOnlyList<DiffRow> rows)
    {
        var comments = Comments;

        if (comments == null || comments.Count == 0) return rows;

        var filePath = FilePath;

        var fileComments = comments.Where(comment => comment.Anchor.FilePath == filePath).ToList();

        if (fileComments.Count == 0) return rows;

        // 同锚点合并为一张卡片,保持首次出现顺序。
        var byAnchor = new Dictionary<DiffCommentAnchor, List<DiffComment>>();
        var order    = new List<DiffCommentAnchor>();

        foreach (var comment in fileComments)
        {
            var anchor = comment.Anchor.Normalize();

            if (!byAnchor.TryGetValue(anchor, out var group))
            {
                group            = [];
                byAnchor[anchor] = group;
                order.Add(anchor);
            }

            group.Add(comment);
        }

        var insertAfter = new Dictionary<int, List<DiffCommentAnchor>>();

        foreach (var anchor in order)
        {
            var last = LastRowIndexOfAnchor(rows, anchor);

            if (last < 0) continue;

            if (!insertAfter.TryGetValue(last, out var anchors))
            {
                anchors           = [];
                insertAfter[last] = anchors;
            }

            anchors.Add(anchor);
        }

        if (insertAfter.Count == 0) return rows;

        var brushes = DiffBrushes.Get(ActualThemeVariant, Palette, ThemePreset, DiffPreset);
        var result  = new List<DiffRow>(rows.Count + insertAfter.Count);

        for (var index = 0; index < rows.Count; index++)
        {
            result.Add(rows[index]);

            if (!insertAfter.TryGetValue(index, out var anchors)) continue;
            foreach (var anchor in anchors)
                result.Add(CreateCommentRow(anchor, byAnchor[anchor], brushes));
        }

        return result;
    }

    private static int LastRowIndexOfAnchor(IReadOnlyList<DiffRow> rows, DiffCommentAnchor anchor)
    {
        var last = -1;

        for (var index = 0; index < rows.Count; index++)
        {
            var row = rows[index];

            if (row is DiffSplitContentRow split)
            {
                var number = anchor.Side == SplitSide.Old ? split.Left.Number : split.Right.Number;

                if (Matches(number)) last = index;
            }
            else if (row is DiffUnifiedContentRow unified)
            {
                var number = anchor.Side == SplitSide.Old ? unified.OldLineNumber : unified.NewLineNumber;

                if (number is int value && value >= anchor.StartLineNumber && value <= anchor.EndLineNumber)
                    last = index;
            }
        }

        return last;

        bool Matches(string? number)
        {
            return number != null                   && int.TryParse(number, out var value) &&
                   value  >= anchor.StartLineNumber && value <= anchor.EndLineNumber;
        }
    }

    private DiffCommentRow CreateCommentRow(DiffCommentAnchor anchor, List<DiffComment> comments, DiffBrushSet brushes)
    {
        if (ViewMode == DiffViewMode.Unified) return new DiffUnifiedCommentRow(anchor, comments, brushes);

        return new DiffSplitCommentRow(anchor, comments, anchor.Side, brushes);
    }

    private void SelectCell(DiffSplitCellModel cell)
    {
        cell.IsSelected = true;
        _selectedSplitCells.Add(cell);
    }

    /// <summary>
    ///     记录展开前的偏移、候选锚点位置和邻行高度;无法定位时返回 <c>null</c>。
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
                                index, _rows.Count, ContentTopOf(placeholderContainer),
                                ContentTopOf(ContainerOfRow(index + 1)));
    }

    private Visual? ContainerOfRow(int index)
    {
        return index >= 0 && index < _rows.Count ? _items?.ContainerFromItem(_rows[index]) : null;
    }

    /// <summary>
    ///     Content-space (offset-independent) top of a realized row container — measuring in the
    ///     items' coordinates keeps the anchor math valid whatever the scroll offset is.
    /// </summary>
    private double? ContentTopOf(Visual? container)
    {
        var matrix = container?.TransformToVisual(_items!);

        return matrix?.Transform(new Point()).Y;
    }

    /// <summary>
    ///     展开后保持占位行的视口位置;占位行移除时改用其后一行。
    /// </summary>
    private void ApplyExpandAnchor(ExpandAnchor anchor, HunkExpandDirection direction, int hunkIndex)
    {
        if (_scrollViewer == null || _rows.Count == anchor.OldCount) return; // nothing was revealed

        int  insertedAbove;
        bool placeholderReplaced;

        switch (direction)
        {
            case HunkExpandDirection.Up :
                // 向上展开不移动占位行;占位行消失时,新增行落在其后一行之前。
                placeholderReplaced = anchor.OldIndex >= _rows.Count ||
                                      _rows[anchor.OldIndex] is not (DiffSplitHunkRow or DiffUnifiedHunkRow);
                insertedAbove = placeholderReplaced ? _rows.Count - anchor.OldCount + 1 : 0;
                break;
            case HunkExpandDirection.Down :
            {
                // 向下展开在占位行前插入行;末尾展开条消失并在末尾追加行。
                var movedTo = FindHunkRowIndex(hunkIndex);

                placeholderReplaced = false;
                insertedAbove       = movedTo > anchor.OldIndex ? movedTo - anchor.OldIndex : 0;
                break;
            }
            default :
                placeholderReplaced = true;
                insertedAbove       = _rows.Count - anchor.OldCount + 1;
                break;
        }

        if (insertedAbove <= 0 && !placeholderReplaced) return;

        // 占位行保留时以它为锚点,移除时以原后一行为锚点。
        var postAnchorIndex = placeholderReplaced
            ? anchor.OldIndex + (_rows.Count - anchor.OldCount + 1)
            : FindHunkRowIndex(hunkIndex);
        var preTop = placeholderReplaced ? anchor.AfterContentTop : anchor.AnchorContentTop;

        if (postAnchorIndex >= 0 && postAnchorIndex < _rows.Count && preTop != null)
        {
            // 先估算偏移使锚点进入虚拟化窗口,布局后再按真实高度校正。
            _pendingExpandOffset = new Vector(_scrollViewer.Offset.X,
                                              anchor.OffsetY + insertedAbove * anchor.ContentRowHeight -
                                              (placeholderReplaced ? anchor.PlaceholderHeight : 0));
            _pendingAnchorTune    =  new ExpandAnchorTune(anchor.OffsetY, preTop.Value, _rows[postAnchorIndex]);
            _anchorTuneLayouts    =  0;
            _anchorTuneApplies    =  0;
            _anchorTuneProbes     =  0;
            _anchorTuneApplied    =  false;
            _items!.LayoutUpdated -= OnAnchorTuneLayout;
            _items.LayoutUpdated  += OnAnchorTuneLayout;
        }
        else
        {
            // 无法测量锚点时使用估算高度,扣除已移除的占位行。
            var delta = insertedAbove * anchor.ContentRowHeight -
                        (placeholderReplaced ? anchor.PlaceholderHeight : 0);

            if (Math.Abs(delta) < 0.01) return;

            _pendingExpandOffset = new Vector(_scrollViewer.Offset.X, anchor.OffsetY + delta);
        }

        // Offset 会按旧 extent 钳制,须等重建后的 extent 更新再调整。
        _scrollViewer.ScrollChanged -= OnScrollChanged;
        _scrollViewer.ScrollChanged += OnScrollChanged;
    }

    /// <summary>
    ///     布局后按实际行高校正锚点,直到测量稳定。
    /// </summary>
    private void OnAnchorTuneLayout(object? sender, EventArgs e)
    {
        if (_pendingAnchorTune is not { } tune || _items == null || _scrollViewer == null) return;

        if (_items.ContainerFromItem(tune.Row) is not Visual container)
        {
            // 真实行高可能将锚点挤出虚拟化窗口,向其滚动一屏后继续校正。
            if (++_anchorTuneLayouts <= 3) return;

            if (!ProbeAnchorIntoView(tune)) DropPendingAnchorTune();

            return;
        }

        var top = ContentTopOf(container);

        if (top == null)
        {
            DropPendingAnchorTune();

            return;
        }

        if (_anchorTuneApplied && Math.Abs(top.Value - _anchorTuneLastTop) < 0.01)
        {
            DropPendingAnchorTune();

            return;
        }

        if (_anchorTuneApplies >= 4)
        {
            DropPendingAnchorTune();

            return;
        }

        _anchorTuneLastTop = top.Value;
        _anchorTuneApplied = true;
        _anchorTuneApplies++;
        _anchorTuneLayouts = 0;

        _scrollViewer.Offset = new Vector(_scrollViewer.Offset.X, tune.OffsetY + top.Value - tune.PreTop);
    }

    /// <summary>
    ///     向未实现的锚点滚动一屏以触发虚拟化;无探测方向或超出预算时返回 <c>false</c>。
    /// </summary>
    private bool ProbeAnchorIntoView(ExpandAnchorTune tune)
    {
        if (_scrollViewer == null || _items == null || _scrollViewer.Viewport.Height <= 0) return false;

        if (_anchorTuneProbes >= MaxAnchorTuneProbes) return false;

        var anchorIndex = IndexOfRow(tune.Row);

        if (anchorIndex < 0) return false;

        var neighbor = FindNearestRealizedRowIndex(anchorIndex);

        if (neighbor < 0) return false;

        _anchorTuneProbes++;
        _anchorTuneLayouts = 0;
        _scrollViewer.Offset = new Vector(_scrollViewer.Offset.X,
                                          _scrollViewer.Offset.Y +
                                          (neighbor < anchorIndex ? _scrollViewer.Viewport.Height
                                                                  : -_scrollViewer.Viewport.Height));

        return true;
    }

    /// <summary>Index of the row nearest to <paramref name="index" /> with a realized container.</summary>
    private int FindNearestRealizedRowIndex(int index)
    {
        var maxDistance = Math.Max(index, _rows.Count - 1 - index);

        for (var distance = 1; distance <= maxDistance; distance++)
        {
            if (IsRealized(index - distance)) return index - distance;
            if (IsRealized(index + distance)) return index + distance;
        }

        return -1;

        bool IsRealized(int rowIndex)
        {
            return rowIndex >= 0 && rowIndex < _rows.Count &&
                   _items?.ContainerFromItem(_rows[rowIndex]) is { Bounds.Height: > 0 };
        }
    }

    private void DropPendingAnchorTune()
    {
        if (_pendingAnchorTune == null) return;

        _pendingAnchorTune    =  null;
        _items?.LayoutUpdated -= OnAnchorTuneLayout;
        _anchorTuneApplied    =  false;
        _anchorTuneApplies    =  0;
        _anchorTuneProbes     =  0;
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
        // 可用性由行按钮显隐决定,无需单独刷新命令状态。
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
    ///     State-driven commands implement this so the owner can batch-invalidate their
    ///     CanExecute after a selection or model change.
    /// </summary>
    private interface IStateCommand : ICommand
    {
        void RaiseCanExecuteChanged();
    }

    /// <summary>
    ///     从可见选区发起评论请求,由宿主处理。
    /// </summary>
    private sealed class DiffBeginCommentCommand(DiffView owner) : IStateCommand
    {
        private EventHandler? _canExecuteChanged;

        public event EventHandler? CanExecuteChanged
        {
            add => _canExecuteChanged += value;
            remove => _canExecuteChanged -= value;
        }

        public bool CanExecute(object? parameter)
        {
            // 隐藏选区不能发起评论,条件与复制一致。
            return owner.CanCopySelection();
        }

        public void Execute(object? parameter)
        {
            var anchor = owner.GetCommentAnchor();

            if (anchor != null) owner.CommentRequested?.Invoke(owner, new DiffCommentRequestedEventArgs(anchor));
        }

        public void RaiseCanExecuteChanged()
        {
            _canExecuteChanged?.Invoke(owner, EventArgs.Empty);
        }
    }

    /// <summary>
    ///     转发复制操作;选区或模型变化时刷新可用状态。
    /// </summary>
    private sealed class DiffCopyCommand(
        DiffView             owner,
        Func<DiffView, bool> canExecute,
        Func<DiffView, Task> executeAsync) : IStateCommand
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
        double  OffsetY,
        double  ContentRowHeight,
        double  PlaceholderHeight,
        int     OldIndex,
        int     OldCount,
        double? AnchorContentTop,
        double? AfterContentTop);

    /// <summary>
    ///     The exact post-layout re-anchoring input: the pre-expand offset, the anchor row's
    ///     pre-expand content-space top, and the row that inherits the anchor position.
    /// </summary>
    private readonly record struct ExpandAnchorTune(double OffsetY, double PreTop, DiffRow Row);
}
