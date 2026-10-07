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
///     将 <see cref="T:Banned.CodeDiff.Services.DiffFile" /> 渲染为只读的 GitHub 风格 diff
///     视图,行级新增/删除带背景色——分栏(两列,默认)或统一(单列、双行号,删除行位于
///     新增行之上)两种模式。赋给它一个 <see cref="DiffFile" />(无论是否已构建——
///     <c>Init</c> 与各 <c>Build*DiffLines</c> 调用都是幂等的、按需调用),视图便会通过模型
///     的 <c>Updated</c> 事件保持自身同步。<br />
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

        // Brushes are baked into the rows; switch palette by rebuilding on theme changes.
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
    ///     获取或设置渲染前模型是否执行语法高亮(<c>DiffFile.InitSyntax</c>);语言已注册时
    ///     内置 TextMate 引擎会为各行着色。<br />
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
    ///     获取或设置传给 <c>InitSyntax</c> 的语法高亮器;<c>null</c> 使用核心库内置的
    ///     TextMate 引擎(上游:registerHighlighter)。<br />
    ///     Gets or sets the syntax engine passed to <c>InitSyntax</c>; <c>null</c> uses
    ///     the core library's built-in TextMate engine (upstream: registerHighlighter).
    /// </summary>
    public IDiffHighlighter? Highlighter
    {
        get => GetValue(HighlighterProperty);
        set => SetValue(HighlighterProperty, value);
    }

    /// <summary>
    ///     获取或设置一个值,指示长行是否在视图宽度处自动换行(对应上游 diffViewWrap prop:
    ///     white-space pre-wrap,行边缘断词),而不是渲染一行被水平裁剪的文本。默认为
    ///     <c>false</c>——上游包装器默认开启,这里做成显式启用,让既有宿主的行布局(单行高)
    ///     保持不变。分栏行两侧保持与较高一侧同高;行号列永不换行。<br />
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
    ///     获取或设置一个值,指示统一视图是否把旧/新双行号列合并为单列显示:删除行显示旧行号,
    ///     上下文行、新增行与展开出的上下文行显示新行号。默认为 <c>false</c>(GitHub 式旧|新
    ///     双列)。仅 <see cref="DiffViewMode.Unified" /> 生效,分栏模式不受影响;运行时切换通过
    ///     样式选择器立即生效,不重建行。<br />
    ///     Gets or sets whether the unified view merges the old/new line-number columns into one:
    ///     deleted lines show the old number, while context, added, and revealed lines show the
    ///     new number. Defaults to <c>false</c> (the GitHub-style old|new pair). Only effective in
    ///     <see cref="DiffViewMode.Unified" /> — split view is unaffected; toggling at runtime
    ///     applies immediately through style selectors without rebuilding rows.
    /// </summary>
    public bool UseSingleLineNumberColumn
    {
        get => GetValue(UseSingleLineNumberColumnProperty);
        set => SetValue(UseSingleLineNumberColumnProperty, value);
    }

    /// <summary>
    ///     获取或设置在行号单元格上拖拽时是否按行区间创建选区(上游的 multiSelect 功能)。
    ///     默认为 <c>false</c>——上游包装器默认开启,这里做成显式启用,让既有宿主的指针行为
    ///     保持不变。<br />
    ///     Gets or sets whether dragging over line-number cells selects line ranges (the upstream
    ///     multiSelect feature). Defaults to <c>false</c> — the upstream wrappers default it to on,
    ///     but an opt-in keeps existing hosts' pointer behavior unchanged.
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
    ///     获取或设置评论锚点携带的文件身份(如仓库相对路径);由宿主提供,经
    ///     <see cref="GetCommentAnchor" /> 流入锚点,视图自身不解析文件名。评论呈现按该身份
    ///     过滤——只有 <see cref="DiffCommentAnchor.FilePath" /> 与之一致的评论会渲染,切换
    ///     文件不会把评论串到其他文件的同号行上。默认为 <c>null</c>。<br />
    ///     Gets or sets the file identity carried by comment anchors (e.g. a repo-relative path);
    ///     supplied by the host and flowed into <see cref="GetCommentAnchor" /> — the view never
    ///     parses file names itself. Comment rendering filters on it: only comments whose
    ///     <see cref="DiffCommentAnchor.FilePath" /> matches render, so switching files never leaks
    ///     comments onto same-numbered lines of another file. Defaults to <c>null</c>.
    /// </summary>
    public string? FilePath
    {
        get => GetValue(FilePathProperty);
        set => SetValue(FilePathProperty, value);
    }

    /// <summary>
    ///     获取或设置宿主提供的行内评论;每个锚点渲染一张卡片(位于其范围内最后一个可见行
    ///     之后),锚点行获得与多选状态独立的持久高亮。评论按文件身份隔离——仅
    ///     <see cref="DiffCommentAnchor.FilePath" /> 与 <see cref="FilePath" /> 一致的评论参与
    ///     渲染。集合内容变化不会自动刷新——重新赋值该属性(换成新集合实例)后视图重建行。
    ///     默认为 <c>null</c>(无评论)。<br />
    ///     Gets or sets the host-provided inline comments; each anchor renders one card (after the
    ///     last visible line of its range) and its lines get a persistent highlight independent of
    ///     the multi-select state. Comments are file-scoped — only those whose
    ///     <see cref="DiffCommentAnchor.FilePath" /> matches <see cref="FilePath" /> render.
    ///     Mutating the collection in place does not refresh — reassign the property (a new
    ///     collection instance) and the view rebuilds its rows. Defaults to <c>null</c>
    ///     (no comments).
    /// </summary>
    public IReadOnlyList<DiffComment>? Comments
    {
        get => GetValue(CommentsProperty);
        set => SetValue(CommentsProperty, value);
    }

    /// <summary>
    ///     获取或设置统一的语义配色覆盖(<see cref="DiffPalette" />);<c>null</c> 保持内置
    ///     上游配色。赋值即重建行。<br />
    ///     Gets or sets the unified semantic color overrides (<see cref="DiffPalette" />);
    ///     <c>null</c> keeps the built-in upstream colors. Assigning rebuilds the rows.
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
    ///     获取复制当前选区纯文本的命令(<see cref="CopySelectionAsync" />)。本机移植新增——
    ///     上游库没有复制功能。没有选区、或所有被选行都隐藏在折叠的 hunk 之内时 CanExecute 为
    ///     false;不内置键盘快捷键(宿主自行绑定,以免与宿主的快捷键冲突)。<br />
    ///     Gets the command that copies the current selection's plain text
    ///     (<see cref="CopySelectionAsync" />). Native port addition — the upstream library has no
    ///     copy feature. CanExecute is false without a selection or when every selected line is
    ///     hidden behind a collapsed hunk; no keyboard shortcut is built in (hosts bind their own to
    ///     avoid clashing with the host's bindings).
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
    ///     获取「添加评论」入口命令——显式触发评论流程的唯一通道:CanExecute 跟随当前选区
    ///     (与复制命令同条件),执行时经 <see cref="GetCommentAnchor" /> 推导锚点并引发
    ///     <see cref="CommentRequested" />;评论编辑器由宿主提供。普通选区完成本身不会引发
    ///     事件。宿主可把任意按钮绑定到该命令。<br />
    ///     Gets the "add comment" entry command — the single explicit channel into the comment
    ///     flow: CanExecute follows the current selection (same conditions as the copy commands),
    ///     and executing derives the anchor through <see cref="GetCommentAnchor" /> and raises
    ///     <see cref="CommentRequested" />; the comment editor itself is host business. Completing
    ///     an ordinary selection never raises the event by itself. Hosts bind any button of theirs
    ///     to this command.
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
    ///     在选区拖拽释放时发生——对应上游管理器的 onSelectionComplete,经 React 包装器的
    ///     onMultiSelectComplete 呈现。释放时没有区间则结果为 <c>null</c>。<br />
    ///     Occurs when a selection drag is released — the upstream manager's onSelectionComplete
    ///     surfaced through the React wrapper's onMultiSelectComplete. The result is <c>null</c>
    ///     when the release happened without a range.
    /// </summary>
    public event EventHandler<DiffSelectionCompletedEventArgs>? SelectionCompleted;

    /// <summary>
    ///     在宿主通过 <see cref="BeginCommentCommand" /> 显式发起评论时发生,携带从当前选区
    ///     推导的行范围锚点(含 <see cref="FilePath" />);评论的编辑、提交与持久化由宿主
    ///     负责。<br />
    ///     Occurs when the host explicitly initiates a comment through
    ///     <see cref="BeginCommentCommand" />, carrying the line-range anchor derived from the
    ///     current selection (including <see cref="FilePath" />); editing, submitting, and
    ///     persisting the comment are the host's responsibility.
    /// </summary>
    public event EventHandler<DiffCommentRequestedEventArgs>? CommentRequested;

    /// <summary>
    ///     获取当前选区结果(规范化区间加行数据)——拖拽过程中反映实时区间;释放之后持续
    ///     返回已完成的区间,直到下一次交互将其清除(上游管理器语义)。没有区间时返回
    ///     <c>null</c>。<br />
    ///     Gets the current selection result (normalized range plus line data) — during a drag it
    ///     reflects the live range; after a release it keeps returning the completed range until the
    ///     next interaction clears it (upstream manager semantics). Returns <c>null</c> without a range.
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
    ///     从当前选区推导行范围评论锚点:侧别与起止行号来自选区(归一化区间;删除行自然锚定
    ///     旧侧、新增行锚定新侧、上下文行保留所选侧),文件身份取 <see cref="FilePath" />。
    ///     没有选区或模型时返回 <c>null</c>。<br />
    ///     Derives a line-range comment anchor from the current selection: the side and start/end
    ///     numbers come from the (normalized) selection range — deleted lines naturally anchor the
    ///     old side, added lines the new one, context lines keep the selected side — and the file
    ///     identity comes from <see cref="FilePath" />. Returns <c>null</c> without a selection or
    ///     model.
    /// </summary>
    public DiffCommentAnchor? GetCommentAnchor()
    {
        var result = GetSelectionResult();

        if (result == null) return null;

        return new DiffCommentAnchor(FilePath, result.Range.Side, result.Range.StartLineNumber,
                                     result.Range.EndLineNumber);
    }

    /// <summary>
    ///     清除选区:交互状态与持久化的完成态高亮一并清除。与上游的差异:JS 管理器的
    ///     clearSelection 会保留 #preselectedLines(本移植同样用它持久化已完成的选区,即
    ///     评论锚定通道),若清除时保留它,高亮将永远不会消失——因此这里两个通道都清除。<br />
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
    ///     设置预选行(例如来自既有批注)。每侧的行列表会合并成一个覆盖 min/max 的大区间——
    ///     上游已知语义(visual.ts changePreselectedLinesToLineRange):离散的列表会高亮其
    ///     最小值与最大值之间的所有行。<br />
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
    ///     将当前选区以纯文本复制到剪贴板:每个被选行对应一行输出,隐藏行跳过、行尾换行符
    ///     去除(<see cref="MultiSelectData.GetSelectedTextFromResult" />——复制内容与视图所示
    ///     一致)。没有可见选区(或没有剪贴板,即未附加到 TopLevel)时静默返回 <c>false</c>。<br />
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
                // Upstream keeps raw init and syntax init separable (the vue component only runs
                // initSyntax while syntax highlighting is enabled); DiffFile.Init runs both.
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

        RaiseCommandsCanExecuteChanged();

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

        RaiseCommandsCanExecuteChanged();

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

        // Context lines flag both sides (upstream addClassForSplitRange); changes flag only the
        // range's side.
        ApplySplitRanges(file, normalizedRanges, true, SelectCell);
        ApplyUnifiedRanges(normalizedRanges, row =>
        {
            row.IsSelected = true;
            _selectedUnifiedRows.Add(row);
        });
    }

    /// <summary>
    ///     The comment-anchor channel of the visual pass — structurally the same range-matching as
    ///     the selection above, but a separate flag (<see cref="DiffSplitCellModel.IsCommented" /> /
    ///     <see cref="DiffUnifiedContentRow.IsCommented" />) with its own tracking lists, so a new
    ///     drag replacing the selection never clears the comment highlight. Context lines keep the
    ///     anchor's side only (the split card sits on that side).
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
    ///     Port of visual.ts addClassForSplitRange: matched lines flag their row's cells — with
    ///     <paramref name="contextFlagsBothSides" />, context lines flag both sides (selection
    ///     semantics); otherwise only the range's side (comment semantics — the card sits on that
    ///     side).
    /// </summary>
    private void ApplySplitRanges(DiffFile file, List<MultiSelectRange> allRanges, bool contextFlagsBothSides,
                                  Action<DiffSplitCellModel> apply)
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
    ///     Inserts one comment card row per anchor right after the last visible line of its range —
    ///     the Avalonia equivalent of the upstream comment widget mounting under a line. Anchors are
    ///     stable (side + line numbers), so view-mode switches and hunk expands/collapses re-derive
    ///     the position on every rebuild; an anchor with no visible line renders no card until an
    ///     expansion reveals its lines. Comments are file-scoped: only anchors whose
    ///     <see cref="DiffCommentAnchor.FilePath" /> matches <see cref="FilePath" /> render, so
    ///     comments never leak onto same-numbered lines of another file.
    /// </summary>
    private IReadOnlyList<DiffRow> InsertCommentRows(IReadOnlyList<DiffRow> rows)
    {
        var comments = Comments;

        if (comments == null || comments.Count == 0) return rows;

        var filePath = FilePath;

        var fileComments = comments.Where(comment => comment.Anchor.FilePath == filePath).ToList();

        if (fileComments.Count == 0) return rows;

        // One card per anchor, groups kept in first-seen order; anchors normalize their ends so a
        // reversed host range still positions deterministically.
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

        // Flat row index each anchor's card goes after — the last row inside the anchor's range.
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
    ///     State-driven commands implement this so the owner can batch-invalidate their
    ///     CanExecute after a selection or model change.
    /// </summary>
    private interface IStateCommand : ICommand
    {
        void RaiseCanExecuteChanged();
    }

    /// <summary>
    ///     Relays the explicit "add comment" entry: availability follows the current selection
    ///     (same conditions as copy), execution derives the anchor from the selection and surfaces
    ///     it through <see cref="DiffView.CommentRequested" /> — the comment editor itself stays
    ///     host business. Completing an ordinary selection never raises the event on its own.
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
    ///     Relays one of the view's copy operations. Unlike <see cref="HunkExpandCommand" />, whose
    ///     availability is static per row, copy availability follows the selection and the model, so
    ///     the owner raises <see cref="RaiseCanExecuteChanged" /> when those change.
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
