using Banned.CodeDiff.Models;
using System.Text;

namespace Banned.CodeDiff.Services;

/// <summary>
///     diff 文件模型,移植自 packages/core/src/diff-file.ts。初始化后构建 split/unified 行模型,通过 <see cref="Updated" /> 通知变化;各阶段幂等。<br />
///     Diff model ported from packages/core/src/diff-file.ts. Initialize, then build split/unified rows; stages are idempotent and changes raise <see cref="Updated" />.
/// </summary>
public sealed class DiffFile
{
    /// <summary>JS module-level composeLen.</summary>
    private static int _composeLen = 40;

    private readonly bool _composeByRange = false;

    private readonly List<SplitLineItem> _splitLeftLines  = [];
    private readonly List<SplitLineItem> _splitRightLines = [];

    private readonly List<UnifiedLineItem> _unifiedLines = [];

    private List<DiffLine>? _diffLines;
    private List<RawDiff>?  _diffListResults;

    private bool _hasBuildSplit;
    private bool _hasBuildUnified;
    private bool _hasInitRaw;
    private bool _hasInitSyntax;

    private Dictionary<int, DiffLine>?   _newFileDiffLines;
    private Dictionary<int, string>?     _newFileLines;
    private Dictionary<int, bool>?       _newFilePlaceholderLines;
    private SourceFile?                  _newFileResult;
    private Dictionary<int, SyntaxLine>? _newFileSyntaxLines;
    private Dictionary<int, DiffLine>?   _oldFileDiffLines;
    private Dictionary<int, string>?     _oldFileLines;
    private Dictionary<int, bool>?       _oldFilePlaceholderLines;

    private SourceFile? _oldFileResult;

    private Dictionary<int, SyntaxLine>? _oldFileSyntaxLines;
    private Dictionary<int, DiffLine>?   _splitHunksLines;

    // 行号在构建后不再变化,展开仅改变可见性,索引可持续复用;空侧不入索引。
    private Dictionary<int, int>?      _splitLeftLineNumberIndex;
    private Dictionary<int, int>?      _splitRightLineNumberIndex;
    private Dictionary<int, DiffLine>? _unifiedHunksLines;
    private Dictionary<int, int>?      _unifiedNewLineNumberIndex;
    private Dictionary<int, int>?      _unifiedOldLineNumberIndex;

    /// <summary>
    ///     创建 DiffFile；此时不做任何解析，解析延迟到 <see cref="InitRaw" /> / <see cref="Init" /> 中进行。<br />
    ///     Create a DiffFile; nothing is parsed here, parsing is deferred to <see cref="InitRaw" /> / <see cref="Init" />.
    /// </summary>
    /// <param name="oldFileName">旧侧文件名，同时作为旧侧语言的回退推断来源。Old side file name, also a fallback source for the old side language.</param>
    /// <param name="oldFileContent">旧侧文件内容；仅提供 diff 时可为空。Old side file content; may be empty when only a diff is provided.</param>
    /// <param name="newFileName">新侧文件名，同时作为新侧语言的回退推断来源。New side file name, also a fallback source for the new side language.</param>
    /// <param name="newFileContent">新侧文件内容；仅提供 diff 时可为空。New side file content; may be empty when only a diff is provided.</param>
    /// <param name="diffList">
    ///     统一 diff 文本列表，每个元素为一段完整 diff；内部会去重。List of unified diff texts, one complete diff per element;
    ///     deduplicated internally.
    /// </param>
    /// <param name="oldFileLang">旧侧语言提示，优先于基于文件名的推断。Old side language hint, taking priority over file-name based detection.</param>
    /// <param name="newFileLang">新侧语言提示，优先于基于文件名的推断。New side language hint, taking priority over file-name based detection.</param>
    public DiffFile(
        string                oldFileName,
        string                oldFileContent,
        string                newFileName,
        string                newFileContent,
        IReadOnlyList<string> diffList,
        string?               oldFileLang = null,
        string?               newFileLang = null
    )
    {
        var diffListDedup = diffList.Distinct().ToList();

        OldFileName = oldFileName;

        NewFileName = newFileName;

        DiffList = diffListDedup;

        var oldLangSource = FirstNonEmpty(oldFileLang, oldFileName, newFileLang, newFileName);
        var newLangSource = FirstNonEmpty(newFileLang, newFileName, oldFileLang, oldFileName);
        OldFileLang = oldLangSource.Length > 0 ? DiffTool.GetLang(oldLangSource) : "txt";
        NewFileLang = newLangSource.Length > 0 ? DiffTool.GetLang(newLangSource) : "txt";

        OldFileContent = oldFileContent;

        NewFileContent = newFileContent;
    }

    /// <summary>当前全局展开步长（每次向上/向下展开显示的行数，默认 40）。<br />JS: getCurrentComposeLength.</summary>
    public static int CurrentComposeLength => _composeLen;

    /// <summary>旧侧文件名。<br />Old side file name.</summary>
    public string OldFileName { get; }

    /// <summary>
    ///     旧侧文件内容；构造时仅提供 diff 时，由 <see cref="InitRaw" /> 依据 diff 反推合成回填。<br />Old side file content; backfilled by
    ///     <see cref="InitRaw" /> from the diff when only a diff was provided.
    /// </summary>
    public string OldFileContent { get; private set; }

    /// <summary>
    ///     旧侧语言标识：按 oldFileLang → oldFileName → newFileLang → newFileName 顺序取第一个非空者交给 <see cref="DiffTool.GetLang" />
    ///     解析，无来源时为 "txt"。<br />Old side language: the first non-empty of oldFileLang → oldFileName → newFileLang →
    ///     newFileName is resolved via <see cref="DiffTool.GetLang" />, falling back to "txt" without a source.
    /// </summary>
    public string OldFileLang { get; }

    /// <summary>新侧文件名。<br />New side file name.</summary>
    public string NewFileName { get; }

    /// <summary>
    ///     新侧文件内容；构造时仅提供 diff 时，由 <see cref="InitRaw" /> 依据 diff 反推合成回填。<br />New side file content; backfilled by
    ///     <see cref="InitRaw" /> from the diff when only a diff was provided.
    /// </summary>
    public string NewFileContent { get; private set; }

    /// <summary>
    ///     新侧语言标识：按 newFileLang → newFileName → oldFileLang → oldFileName 顺序取第一个非空者交给 <see cref="DiffTool.GetLang" />
    ///     解析，无来源时为 "txt"。<br />New side language: the first non-empty of newFileLang → newFileName → oldFileLang →
    ///     oldFileName is resolved via <see cref="DiffTool.GetLang" />, falling back to "txt" without a source.
    /// </summary>
    public string NewFileLang { get; }

    /// <summary>
    ///     构造时传入的统一 diff 文本列表（已去重，保持首次出现顺序）。<br />The unified diff texts passed at construction (deduplicated,
    ///     first-occurrence order kept).
    /// </summary>
    public IReadOnlyList<string> DiffList { get; }

    /// <summary>diff 覆盖的最大文件行号（新旧两侧行号取较大者）。<br />The largest file line number covered by the diff (max of both sides).</summary>
    public int DiffLineLength { get; private set; }

    /// <summary>
    ///     分栏视图总行数（<see cref="SplitRightLines" /> 的长度）。<br />Total line count of the split view (length of
    ///     <see cref="SplitRightLines" />).
    /// </summary>
    public int SplitLineLength { get; private set; }

    /// <summary>
    ///     统一视图总行数（<see cref="UnifiedLines" /> 的长度）。<br />Total line count of the unified view (length of
    ///     <see cref="UnifiedLines" />).
    /// </summary>
    public int UnifiedLineLength { get; private set; }

    /// <summary>文件总行数（两侧原始内容行数的较大者）。<br />Total file line count (larger of both sides).</summary>
    public int FileLineLength { get; private set; }

    /// <summary>新增行总数。<br />Total number of added lines.</summary>
    public int AdditionLength { get; private set; }

    /// <summary>删除行总数。<br />Total number of deleted lines.</summary>
    public int DeletionLength { get; private set; }

    /// <summary>
    ///     构建行模型时是否存在被折叠的行（置位后不随展开操作复位）。<br />Whether any collapsed lines existed when the line models were built (never
    ///     reset by expansion).
    /// </summary>
    public bool HasSomeLineCollapsed { get; private set; }

    /// <summary>模型更新计数（<see cref="NotifyAll" /> 的调用次数）。<br />Model update counter (number of <see cref="NotifyAll" /> calls).</summary>
    public int UpdateCount { get; private set; }

    /// <summary>当前主题名；未经 <see cref="InitTheme" /> 设置时为 <c>null</c>（InitTheme 会兜底为 "light"）。<br />JS: _getTheme.</summary>
    public string? Theme { get; private set; }

    /// <summary>当前生效的语法高亮器名称（由语法初始化流程同步）。<br />JS: _getHighlighterName.</summary>
    public string? HighlighterName { get; private set; }

    /// <summary>当前生效的语法高亮器类型（由语法初始化流程同步）。<br />JS: _getHighlighterType.</summary>
    public HighlighterType? HighlighterType { get; private set; }


    // ---- expansion ----

    /// <summary>展开/折叠操作是否可用；纯 diff 渲染（文件内容完全由 diff 合成）时不可用。<br />JS: getExpandEnabled.</summary>
    public bool IsExpandEnabled => !IsPureDiffRender && !_composeByRange;

    /// <summary>
    ///     分栏视图是否已全部展开（由 <see cref="OnAllExpand" /> 置位、<see cref="OnAllCollapse" /> 复位）。<br />Whether the split view has
    ///     been fully expanded (set by <see cref="OnAllExpand" />, cleared by <see cref="OnAllCollapse" />).
    /// </summary>
    public bool HasExpandSplitAll { get; private set; }

    /// <summary>
    ///     统一视图是否已全部展开（由 <see cref="OnAllExpand" /> 置位、<see cref="OnAllCollapse" /> 复位）。<br />Whether the unified view
    ///     has been fully expanded (set by <see cref="OnAllExpand" />, cleared by <see cref="OnAllCollapse" />).
    /// </summary>
    public bool HasExpandUnifiedAll { get; private set; }

    // ---- misc accessors ----

    /// <summary>
    ///     旧侧文件经 transform 处理后的原始内容（<see cref="InitRaw" /> 之前为 <c>null</c>）；
    ///     区别于输入属性 <see cref="OldFileContent" />。<br />
    ///     JS: getOldFileContent — the transform-processed raw of the old file
    ///     (<c>null</c> before <see cref="InitRaw" />). Distinct from the
    ///     <see cref="OldFileContent" /> input property.
    /// </summary>
    public string? OldFileRaw => _oldFileResult?.Raw;

    /// <summary>
    ///     新侧文件经 transform 处理后的原始内容（<see cref="InitRaw" /> 之前为 <c>null</c>）；
    ///     区别于输入属性 <see cref="NewFileContent" />。<br />
    ///     JS: getNewFileContent — the transform-processed raw of the new file
    ///     (<c>null</c> before <see cref="InitRaw" />). Distinct from the
    ///     <see cref="NewFileContent" /> input property.
    /// </summary>
    public string? NewFileRaw => _newFileResult?.Raw;

    /// <summary>是否为「纯 diff 渲染」：两侧文件内容均未提供、文件内容完全由 diff 反推合成；此时禁用展开。<br />JS: _getIsPureDiffRender.</summary>
    public bool IsPureDiffRender { get; private set; }

    /// <summary>
    ///     分栏视图左（旧）侧的行模型；需先调用 <see cref="BuildSplitDiffLines" />，缺失的半行以空占位项补位。<br />Line models of the split view's left
    ///     (old) side; call <see cref="BuildSplitDiffLines" /> first, missing half-rows are padded with empty placeholder
    ///     items.
    /// </summary>
    public IReadOnlyList<SplitLineItem> SplitLeftLines => _splitLeftLines;

    /// <summary>
    ///     分栏视图右（新）侧的行模型；需先调用 <see cref="BuildSplitDiffLines" />，缺失的半行以空占位项补位。<br />Line models of the split view's right
    ///     (new) side; call <see cref="BuildSplitDiffLines" /> first, missing half-rows are padded with empty placeholder
    ///     items.
    /// </summary>
    public IReadOnlyList<SplitLineItem> SplitRightLines => _splitRightLines;

    /// <summary>
    ///     统一视图的行模型；需先调用 <see cref="BuildUnifiedDiffLines" />。<br />Line models of the unified view; call
    ///     <see cref="BuildUnifiedDiffLines" /> first.
    /// </summary>
    public IReadOnlyList<UnifiedLineItem> UnifiedLines => _unifiedLines;

    /// <summary>
    ///     分栏视图中作为折叠区锚点的 hunk 行索引集合（即 <see cref="GetSplitHunkLine" /> 的索引参数）；行模型未构建时为空。<br />Indexes of the hunk anchor
    ///     rows in the split view (the index arguments of <see cref="GetSplitHunkLine" />); empty before the line models are
    ///     built.
    /// </summary>
    public IReadOnlyCollection<int> SplitHunkLineIndexes => _splitHunksLines?.Keys.ToArray() ?? [];

    /// <summary>
    ///     统一视图中作为折叠区锚点的 hunk 行索引集合（即 <see cref="GetUnifiedHunkLine" /> 的索引参数）；行模型未构建时为空。<br />Indexes of the hunk anchor
    ///     rows in the unified view (the index arguments of <see cref="GetUnifiedHunkLine" />); empty before the line models
    ///     are built.
    /// </summary>
    public IReadOnlyCollection<int> UnifiedHunkLineIndexes => _unifiedHunksLines?.Keys.ToArray() ?? [];

    /// <summary>
    ///     修改全局默认展开步长（模块级状态，影响所有实例的向上/向下展开；默认 40）。<br />Changes the global default expand step (module-level state
    ///     affecting every instance's up/down expansion; default 40).
    /// </summary>
    /// <param name="compose">新的步长（行数）。New step length in lines.</param>
    public static void ChangeDefaultComposeLength(int compose)
    {
        _composeLen = compose;
    }

    /// <summary>将全局默认展开步长重置为 40。<br />Resets the global default expand step to 40.</summary>
    public static void ResetDefaultComposeLength()
    {
        _composeLen = 40;
    }

    /// <summary>模型更新通知事件；渲染层监听它以刷新视图。<br />JS: subscribe/notifyAll — the render layer listens for model changes.</summary>
    public event Action? Updated;

    private static string FirstNonEmpty(params string?[] values)
    {
        foreach (var v in values)
            if (!string.IsNullOrEmpty(v))
                return v;

        return "";
    }

    // ---- JS: Number() emulation for hunk header counts ----

    /// <summary>Number(undefined) = NaN → null; Number("") = 0; digits → value.</summary>
    private static int? JsNumber(string? s)
    {
        if (s == null) return null;

        if (s.Length == 0) return 0;

        return int.TryParse(s, out var v) ? v : null;
    }

    // ---- file composition ----

    private void DoFile()
    {
        if (string.IsNullOrEmpty(OldFileContent) && string.IsNullOrEmpty(NewFileContent)) return;

        if (!string.IsNullOrEmpty(OldFileContent))
            _oldFileResult = new SourceFile(OldFileContent, OldFileLang, OldFileName);

        if (!string.IsNullOrEmpty(NewFileContent))
            _newFileResult = new SourceFile(NewFileContent, NewFileLang, NewFileName);
    }

    private void ComposeRaw()
    {
        _oldFileResult?.DoRaw();

        _oldFileLines = _oldFileResult?.RawFile;

        _newFileResult?.DoRaw();

        _newFileLines = _newFileResult?.RawFile;

        FileLineLength = Math.Max(FileLineLength,
                                  Math.Max(_oldFileResult?.MaxLineNumber ?? 0, _newFileResult?.MaxLineNumber ?? 0));
    }

    private void DoDiff()
    {
        if (DiffList == null) return;

        _diffListResults = DiffList.Select(s => DiffParser.Shared.Parse(s)).ToList();
    }

    private void ComposeDiff()
    {
        if (_diffListResults == null || _diffListResults.Count == 0) return;

        _diffLines = [];

        AdditionLength = 0;

        DeletionLength = 0;

        var tmp = new List<DiffLine>();

        // GetDiffRange 不保留列表引用,每组清空后可复用。
        var additions = new List<DiffLine>();

        var deletions = new List<DiffLine>();

        foreach (var item in _diffListResults)
        {
            var hunks = item.Hunks;
            foreach (var hunk in hunks)
            {
                additions.Clear();
                deletions.Clear();
                foreach (var line in hunk.Lines)
                {
                    switch (line.Type)
                    {
                        case DiffLineType.Add :
                            additions.Add(line);
                            AdditionLength++;
                            break;
                        case DiffLineType.Delete :
                            deletions.Add(line);
                            DeletionLength++;
                            break;
                        default :
                            DiffTool.GetDiffRange(additions, deletions, GetNewRawLine, GetOldRawLine);
                            additions.Clear();
                            deletions.Clear();
                            break;
                    }

                    tmp.Add(line);
                }

                DiffTool.GetDiffRange(additions, deletions, GetNewRawLine, GetOldRawLine);
            }
        }

        DiffLine? prevHunkLine = null;

        for (var index = 0; index < tmp.Count; index++)
        {
            var i = tmp[index];

            i.Index = index;

            i.IsFirst = index == 0;

            switch (i.Type)
            {
                case DiffLineType.Hunk :
                {
                    var segs        = i.Text.Split("@@");
                    var numInfoSeg  = segs.Length > 1 ? segs[1] : null;
                    var numParts    = (numInfoSeg ?? "").Split(' ', StringSplitOptions.RemoveEmptyEntries);
                    var oldNumInfo  = numParts.Length > 0 ? numParts[0] : "";
                    var newNumInfo  = numParts.Length > 1 ? numParts[1] : "";
                    var oldNumParts = oldNumInfo.Split(',');
                    var newNumParts = newNumInfo.Split(',');
                    // 解析器保证起始索引为数字,这里的 ?? 0 不会生效。
                    var oldStartIndex = -JsNumber(oldNumParts[0]) ?? 0;
                    var oldLength     = oldNumParts.Length > 1 ? JsNumber(oldNumParts[1]) : null;
                    var newStartIndex = JsNumber(newNumParts[0]) ?? 0;
                    var newLength     = newNumParts.Length > 1 ? JsNumber(newNumParts[1]) : null;
                    i.HunkInfo = new HunkInfo
                    {
                        OldStartIndex         = oldStartIndex,
                        OldLength             = oldLength,
                        NewStartIndex         = newStartIndex,
                        NewLength             = newLength,
                        OldStartIndexSnapshot = oldStartIndex,
                        OldLengthSnapshot     = oldLength,
                        NewStartIndexSnapshot = newStartIndex,
                        NewLengthSnapshot     = newLength
                    };

                    prevHunkLine = i;
                    break;
                }
                case DiffLineType.Context :
                {
                    if (prevHunkLine != null)
                    {
                        i.PrevHunkLine = prevHunkLine;
                        prevHunkLine   = null;
                    }

                    break;
                }
                default :
                    prevHunkLine = null;
                    break;
            }

            _diffLines.Add(i);
        }

        _oldFileDiffLines = new Dictionary<int, DiffLine>();

        _newFileDiffLines = new Dictionary<int, DiffLine>();

        foreach (var item in _diffLines)
        {
            if (item.OldLineNumber is { } ol)
            {
                DiffLineLength = Math.Max(DiffLineLength, ol);

                _oldFileDiffLines[ol] = item;
            }

            if (item.NewLineNumber is not { } nl) continue;
            DiffLineLength = Math.Max(DiffLineLength, nl);

            _newFileDiffLines[nl] = item;
        }
    }

    private void ComposeFile()
    {
        if (!string.IsNullOrEmpty(OldFileContent) && !string.IsNullOrEmpty(NewFileContent)) return;

        var oldFilePlaceholderLines = new Dictionary<int, bool>();

        var newFilePlaceholderLines = new Dictionary<int, bool>();

        if (string.IsNullOrEmpty(OldFileContent) && string.IsNullOrEmpty(NewFileContent))
        {
            var newLineNumber    = 1;
            var oldLineNumber    = 1;
            var oldContent       = new StringBuilder();
            var newContent       = new StringBuilder();
            var hasSymbolChanged = false;
            while (oldLineNumber <= DiffLineLength || newLineNumber <= DiffLineLength)
            {
                var oldIndex    = oldLineNumber++;
                var newIndex    = newLineNumber++;
                var oldDiffLine = GetOldDiffLine(oldIndex);
                var newDiffLine = GetNewDiffLine(newIndex);
                if (oldDiffLine != null)
                {
                    oldContent.Append(oldDiffLine.Text);
                }
                else
                {
                    oldContent.Append('\n');
                    oldFilePlaceholderLines[oldIndex] = true;
                }

                if (newDiffLine != null)
                {
                    newContent.Append(newDiffLine.Text);
                }
                else
                {
                    newContent.Append('\n');
                    newFilePlaceholderLines[newIndex] = true;
                }

                if (!hasSymbolChanged && oldDiffLine != null && newDiffLine != null)
                    hasSymbolChanged =
                        hasSymbolChanged || oldDiffLine.NoTrailingNewLine != newDiffLine.NoTrailingNewLine;
            }

            var oldFileContent = oldContent.ToString();
            var newFileContent = newContent.ToString();

            if (!hasSymbolChanged && oldFileContent == newFileContent)
                return;

            OldFileContent           = oldFileContent;
            NewFileContent           = newFileContent;
            _oldFileResult           = new SourceFile(OldFileContent, OldFileLang, OldFileName);
            _newFileResult           = new SourceFile(NewFileContent, NewFileLang, NewFileName);
            _oldFilePlaceholderLines = oldFilePlaceholderLines;
            _newFilePlaceholderLines = newFilePlaceholderLines;
            IsPureDiffRender = true;
        }
        else if (_oldFileResult != null)
        {
            var newLineNumber    = 1;
            var oldLineNumber    = 1;
            var newContent       = new StringBuilder();
            var hasSymbolChanged = false;
            while (oldLineNumber <= _oldFileResult.MaxLineNumber)
            {
                var newDiffLine = GetNewDiffLine(newLineNumber++);
                var oldDiffLine = GetOldDiffLine(oldLineNumber);
                if (newDiffLine != null)
                {
                    newContent.Append(newDiffLine.Text);
                    oldLineNumber = newDiffLine.OldLineNumber is { } ol ? ol + 1 : oldLineNumber;
                }
                else
                {
                    if (oldDiffLine == null) newContent.Append(GetOldRawLine(oldLineNumber) ?? "");

                    oldLineNumber++;
                }

                if (!hasSymbolChanged && newDiffLine != null && oldDiffLine != null)
                    hasSymbolChanged =
                        hasSymbolChanged || newDiffLine.NoTrailingNewLine != oldDiffLine.NoTrailingNewLine;
            }

            var newFileContent = newContent.ToString();

            if (!hasSymbolChanged && newFileContent == OldFileContent) return;

            NewFileContent = newFileContent;
            _newFileResult = new SourceFile(NewFileContent, NewFileLang, NewFileName);
        }
        else if (_newFileResult != null)
        {
            var oldLineNumber    = 1;
            var newLineNumber    = 1;
            var oldContent       = new StringBuilder();
            var hasSymbolChanged = false;
            while (newLineNumber <= _newFileResult.MaxLineNumber)
            {
                var oldDiffLine = GetOldDiffLine(oldLineNumber++);
                var newDiffLine = GetNewDiffLine(newLineNumber);
                if (oldDiffLine != null)
                {
                    oldContent.Append(oldDiffLine.Text);
                    newLineNumber = oldDiffLine.NewLineNumber is { } nl ? nl + 1 : newLineNumber;
                }
                else
                {
                    if (newDiffLine == null) oldContent.Append(GetNewRawLine(newLineNumber) ?? "");

                    newLineNumber++;
                }

                if (!hasSymbolChanged && newDiffLine != null && oldDiffLine != null)
                    hasSymbolChanged =
                        hasSymbolChanged || newDiffLine.NoTrailingNewLine != oldDiffLine.NoTrailingNewLine;
            }

            var oldFileContent = oldContent.ToString();

            if (!hasSymbolChanged && oldFileContent == NewFileContent) return;

            OldFileContent = oldFileContent;
            _oldFileResult = new SourceFile(OldFileContent, OldFileLang, OldFileName);
        }

        ComposeRaw();
    }

    private DiffLine? GetOldDiffLine(int? lineNumber)
    {
        if (lineNumber is not { } ln || ln == 0) return null;

        return _oldFileDiffLines != null && _oldFileDiffLines.TryGetValue(ln, out var l) ? l : null;
    }

    private DiffLine? GetNewDiffLine(int? lineNumber)
    {
        if (lineNumber is not { } ln || ln == 0) return null;

        return _newFileDiffLines != null && _newFileDiffLines.TryGetValue(ln, out var l) ? l : null;
    }

    private string? GetOldRawLine(int lineNumber)
    {
        return _oldFileLines != null && _oldFileLines.TryGetValue(lineNumber, out var l) ? l : null;
    }

    private string? GetNewRawLine(int lineNumber)
    {
        return _newFileLines != null && _newFileLines.TryGetValue(lineNumber, out var l) ? l : null;
    }

    /// <summary>
    ///     initRaw 的移植（包含 initRaw 时机的 #syncSyntax 调用）；幂等，仅首次调用生效。<br />Port of initRaw (plus the initRaw-time #syncSyntax
    ///     call).
    /// </summary>
    public void InitRaw()
    {
        if (_hasInitRaw) return;

        DoFile();
        ComposeRaw();
        DoDiff();
        ComposeDiff();
        ComposeFile();
        SyncSyntax();
        _hasInitRaw = true;
    }

    // ---- syntax (initSyntax) ----

    /// <summary>initTheme 的移植：theme ?? 现有值 ?? "light"，传入 null 时保留当前主题。<br />Port of initTheme: theme ?? existing ?? "light".</summary>
    /// <param name="theme">主题名；传 null 表示保留现有值。Theme name; null keeps the current value.</param>
    public void InitTheme(string? theme)
    {
        Theme = theme ?? Theme ?? "light";
    }

    /// <summary>
    ///     initSyntax({ registerHighlighter }) 的移植；幂等——已初始化且高亮器（名称与类型）未变化时直接复用既有结果。<br />Port of initSyntax({
    ///     registerHighlighter }).
    /// </summary>
    /// <param name="registerHighlighter">
    ///     要注册的语法高亮器；缺省时使用 <see cref="DiffHighlighters.Default" />。The syntax highlighter to
    ///     register; defaults to <see cref="DiffHighlighters.Default" />.
    /// </param>
    public void InitSyntax(IDiffHighlighter? registerHighlighter = null)
    {
        if (_hasInitSyntax && (registerHighlighter == null ||
                               (registerHighlighter.Name == HighlighterName &&
                                registerHighlighter.Type == HighlighterType)))
        {
            _newFileSyntaxLines = _newFileResult?.SyntaxFile;

            _oldFileSyntaxLines = _oldFileResult?.SyntaxFile;

            return;
        }

        DoSyntax(registerHighlighter);

        ComposeDiff();

        _hasInitSyntax = true;
    }

    private void DoSyntax(IDiffHighlighter? registerHighlighter)
    {

        ComposeSyntax(registerHighlighter);

        SyncSyntax();
    }

    private void ComposeSyntax(IDiffHighlighter? registerHighlighter)
    {
        _oldFileResult?.DoSyntax(registerHighlighter, Theme);

        _oldFileSyntaxLines = _oldFileResult?.SyntaxFile;

        _newFileResult?.DoSyntax(registerHighlighter, Theme);

        _newFileSyntaxLines = _newFileResult?.SyntaxFile;
    }

    private void SyncSyntax()
    {
        HighlighterName = !string.IsNullOrEmpty(_oldFileResult?.HighlighterName) ? _oldFileResult!.HighlighterName :
            !string.IsNullOrEmpty(_newFileResult?.HighlighterName) ? _newFileResult!.HighlighterName : HighlighterName;

        HighlighterType = _oldFileResult?.HighlighterType ?? _newFileResult?.HighlighterType ?? HighlighterType;

        if (!string.IsNullOrEmpty(_oldFileResult?.HighlighterName)) _oldFileSyntaxLines = _oldFileResult!.SyntaxFile;

        if (!string.IsNullOrEmpty(_newFileResult?.HighlighterName)) _newFileSyntaxLines = _newFileResult!.SyntaxFile;
    }

    /// <summary>
    ///     getOldSyntaxLine 的移植：取旧侧指定文件行号的语法高亮文本段，无则 <c>null</c>。<br />Port of getOldSyntaxLine — syntax spans of the old
    ///     file line, or <c>null</c>.
    /// </summary>
    /// <param name="lineNumber">旧侧文件行号。Old side file line number.</param>
    public SyntaxLine? GetOldSyntaxLine(int lineNumber)
    {
        return _oldFileSyntaxLines != null && _oldFileSyntaxLines.TryGetValue(lineNumber, out var line) ? line : null;
    }

    /// <summary>
    ///     getNewSyntaxLine 的移植：取新侧指定文件行号的语法高亮文本段，无则 <c>null</c>。<br />Port of getNewSyntaxLine — syntax spans of the new
    ///     file line, or <c>null</c>.
    /// </summary>
    /// <param name="lineNumber">新侧文件行号。New side file line number.</param>
    public SyntaxLine? GetNewSyntaxLine(int lineNumber)
    {
        return _newFileSyntaxLines != null && _newFileSyntaxLines.TryGetValue(lineNumber, out var line) ? line : null;
    }

    /// <summary>
    ///     一步式初始化：依次调用 <see cref="InitRaw" /> 与 <see cref="InitSyntax" />。<br />One-shot initialization: calls
    ///     <see cref="InitRaw" /> then <see cref="InitSyntax" />.
    /// </summary>
    public void Init()
    {
        InitRaw();
        InitSyntax();
    }

    // ---- split / unified line models ----

    /// <summary>
    ///     构建分栏（split）视图的行模型；幂等，完成后通过 <see cref="Updated" /> 通知。<br />Builds the line models of the split view;
    ///     idempotent, notifies via <see cref="Updated" /> when done.
    /// </summary>
    public void BuildSplitDiffLines()
    {
        if (_hasBuildSplit) return;

        var  oldFileLineNumber    = 1;
        var  newFileLineNumber    = 1;
        var  prevIsHidden         = true;
        int? hideStart            = null; // JS: Infinity
        var  maxOldFileLineNumber = _oldFileResult?.MaxLineNumber ?? 0;
        var  maxNewFileLineNumber = _newFileResult?.MaxLineNumber ?? 0;

        while (oldFileLineNumber <= maxOldFileLineNumber || newFileLineNumber <= maxNewFileLineNumber)
        {
            var oldDiffLine      = GetOldDiffLine(oldFileLineNumber);
            var newDiffLine      = GetNewDiffLine(newFileLineNumber);
            var oldRawLine       = GetOldRawLine(oldFileLineNumber);
            var newRawLine       = GetNewRawLine(newFileLineNumber);
            var oldLineHasChange = oldDiffLine != null && oldDiffLine.IsIncludeableLine();
            var newLineHasChange = newDiffLine != null && newDiffLine.IsIncludeableLine();
            var len              = _splitRightLines.Count;
            var isHidden         = oldDiffLine == null && newDiffLine == null;

            if (oldDiffLine != null && newDiffLine == null)
                switch (oldDiffLine.NewLineNumber)
                {
                    case { } onl when onl > newFileLineNumber :
                        newFileLineNumber++;
                        continue;
                    case null :
                        newFileLineNumber++;
                        break;
                }

            if (newDiffLine != null && oldDiffLine == null)
                switch (newDiffLine.OldLineNumber)
                {
                    case { } nol when nol > oldFileLineNumber :
                        oldFileLineNumber++;
                        continue;
                    case null :
                        oldFileLineNumber++;
                        break;
                }

            if (oldDiffLine == null && string.IsNullOrEmpty(oldRawLine) && newDiffLine == null &&
                string.IsNullOrEmpty(newRawLine))
                break;

            if (oldDiffLine == null && newDiffLine == null)
            {
                if (_oldFilePlaceholderLines != null                                     &&
                    _oldFilePlaceholderLines.TryGetValue(oldFileLineNumber, out var oph) && oph &&
                    _newFilePlaceholderLines != null                                     &&
                    _newFilePlaceholderLines.TryGetValue(newFileLineNumber, out var nph) && nph)
                {
                    oldFileLineNumber++;
                    newFileLineNumber++;
                    continue;
                }

                if (string.IsNullOrEmpty(oldRawLine) && _newFilePlaceholderLines != null &&
                    _newFilePlaceholderLines.TryGetValue(newFileLineNumber, out var nph2) && nph2)
                {
                    newFileLineNumber++;
                    continue;
                }

                if (string.IsNullOrEmpty(newRawLine) && _oldFilePlaceholderLines != null &&
                    _oldFilePlaceholderLines.TryGetValue(oldFileLineNumber, out var oph2) && oph2)
                {
                    oldFileLineNumber++;
                    continue;
                }
            }

            switch (oldLineHasChange)
            {
                case true when newLineHasChange :
                case false when !newLineHasChange :
                    _splitLeftLines.Add(new SplitLineItem
                    {
                        LineNumber = oldFileLineNumber++, Value = oldRawLine, Diff = oldDiffLine, IsHidden = isHidden,
                        IsHiddenSnapshot = isHidden
                    });
                    _splitRightLines.Add(new SplitLineItem
                    {
                        LineNumber = newFileLineNumber++, Value = newRawLine, Diff = newDiffLine, IsHidden = isHidden,
                        IsHiddenSnapshot = isHidden
                    });
                    break;
                case true :
                    _splitLeftLines.Add(new SplitLineItem
                    {
                        LineNumber = oldFileLineNumber++, Value = oldRawLine, Diff = oldDiffLine, IsHidden = isHidden,
                        IsHiddenSnapshot = isHidden
                    });
                    _splitRightLines.Add(new SplitLineItem());
                    break;
                default :
                {
                    if (newLineHasChange)
                    {
                        _splitLeftLines.Add(new SplitLineItem());
                        _splitRightLines.Add(new SplitLineItem
                        {
                            LineNumber = newFileLineNumber++, Value = newRawLine, Diff = newDiffLine,
                            IsHidden   = isHidden, IsHiddenSnapshot = isHidden
                        });
                    }

                    break;
                }
            }

            if (!prevIsHidden && isHidden) hideStart = len;

            if (isHidden) HasSomeLineCollapsed = true;

            prevIsHidden = isHidden;

            var linePrevHunk = oldDiffLine?.PrevHunkLine ?? newDiffLine?.PrevHunkLine;
            if (oldDiffLine?.PrevHunkLine == null && newDiffLine?.PrevHunkLine == null) continue;
            if (linePrevHunk == null) continue;
            if (linePrevHunk.IsFirst == true)
            {
                linePrevHunk.SplitInfo = HunkLineInfo.FromHunkInfo(linePrevHunk.HunkInfo!, 0,
                                                                   linePrevHunk.HunkInfo!.NewStartIndex - 1,
                                                                   linePrevHunk.Text);
                hideStart = null;
            }
            else if (hideStart != null)
            {
                linePrevHunk.SplitInfo =
                    HunkLineInfo.FromHunkInfo(linePrevHunk.HunkInfo!, hideStart.Value, len, linePrevHunk.Text);
                hideStart = null;
            }

            _splitHunksLines      ??= new Dictionary<int, DiffLine>();
            _splitHunksLines[len] =   linePrevHunk;
        }

        if (hideStart != null)
        {
            var lastDiff = new DiffLine("", DiffLineType.Hunk, null, null, null)
            {
                IsLast = true,
                SplitInfo = new HunkLineInfo
                {
                    StartHiddenIndex         = hideStart.Value,
                    EndHiddenIndex           = _splitRightLines.Count,
                    StartHiddenIndexSnapshot = hideStart.Value,
                    EndHiddenIndexSnapshot   = _splitRightLines.Count,

                    PlainText     = "",
                    OldStartIndex = 0,
                    NewStartIndex = 0,
                    OldLength     = 0,
                    NewLength     = 0
                }
            };
            _splitHunksLines                         ??= new Dictionary<int, DiffLine>();
            _splitHunksLines[_splitRightLines.Count] =   lastDiff;
        }

        SplitLineLength = _splitRightLines.Count;

        _splitLeftLineNumberIndex  = BuildSplitLineNumberIndex(_splitLeftLines);
        _splitRightLineNumberIndex = BuildSplitLineNumberIndex(_splitRightLines);

        _hasBuildSplit = true;

        NotifyAll();
    }

    /// <summary>
    ///     构建统一（unified）视图的行模型；幂等，完成后通过 <see cref="Updated" /> 通知。<br />Builds the line models of the unified view;
    ///     idempotent, notifies via <see cref="Updated" /> when done.
    /// </summary>
    public void BuildUnifiedDiffLines()
    {
        if (_hasBuildUnified) return;

        var  oldFileLineNumber    = 1;
        var  newFileLineNumber    = 1;
        var  prevIsHidden         = true;
        int? hideStart            = null; // JS: Infinity
        var  maxOldFileLineNumber = _oldFileResult?.MaxLineNumber ?? 0;
        var  maxNewFileLineNumber = _newFileResult?.MaxLineNumber ?? 0;

        while (oldFileLineNumber <= maxOldFileLineNumber || newFileLineNumber <= maxNewFileLineNumber)
        {
            var oldRawLine       = GetOldRawLine(oldFileLineNumber);
            var oldDiffLine      = GetOldDiffLine(oldFileLineNumber);
            var newRawLine       = GetNewRawLine(newFileLineNumber);
            var newDiffLine      = GetNewDiffLine(newFileLineNumber);
            var oldLineHasChange = oldDiffLine != null && oldDiffLine.IsIncludeableLine();
            var newLineHasChange = newDiffLine != null && newDiffLine.IsIncludeableLine();
            var len              = _unifiedLines.Count;
            var isHidden         = oldDiffLine == null && newDiffLine == null;

            if (oldDiffLine != null && newDiffLine == null)
                switch (oldDiffLine.NewLineNumber)
                {
                    case { } onl when onl > newFileLineNumber :
                        newFileLineNumber++;
                        continue;
                    case null :
                        newFileLineNumber++;
                        break;
                }

            if (newDiffLine != null && oldDiffLine == null)
                switch (newDiffLine.OldLineNumber)
                {
                    case { } nol when nol > oldFileLineNumber :
                        oldFileLineNumber++;
                        continue;
                    case null :
                        oldFileLineNumber++;
                        break;
                }

            if (string.IsNullOrEmpty(oldRawLine) &&
                string.IsNullOrEmpty(newRawLine) &&
                newDiffLine == null              &&
                oldDiffLine == null)
                break;

            if (oldDiffLine == null && newDiffLine == null)
            {
                if (_oldFilePlaceholderLines != null                                     &&
                    _oldFilePlaceholderLines.TryGetValue(oldFileLineNumber, out var oph) && oph &&
                    _newFilePlaceholderLines != null                                     &&
                    _newFilePlaceholderLines.TryGetValue(newFileLineNumber, out var nph) && nph)
                {
                    oldFileLineNumber++;
                    newFileLineNumber++;
                    continue;
                }

                if (string.IsNullOrEmpty(oldRawLine) && _newFilePlaceholderLines != null &&
                    _newFilePlaceholderLines.TryGetValue(newFileLineNumber, out var nph2) && nph2)
                {
                    newFileLineNumber++;
                    continue;
                }

                if (string.IsNullOrEmpty(newRawLine) && _oldFilePlaceholderLines != null &&
                    _oldFilePlaceholderLines.TryGetValue(oldFileLineNumber, out var oph2) && oph2)
                {
                    oldFileLineNumber++;
                    continue;
                }
            }

            switch (oldLineHasChange)
            {
                case false when !newLineHasChange :
                    _unifiedLines.Add(new UnifiedLineItem
                    {
                        OldLineNumber    = oldFileLineNumber++,
                        NewLineNumber    = newFileLineNumber++,
                        Value            = newRawLine,
                        Diff             = newDiffLine,
                        IsHidden         = isHidden,
                        IsHiddenSnapshot = isHidden
                    });
                    break;
                case true :
                    _unifiedLines.Add(new UnifiedLineItem
                    {
                        OldLineNumber    = oldFileLineNumber++,
                        Value            = oldRawLine,
                        Diff             = oldDiffLine,
                        IsHidden         = isHidden,
                        IsHiddenSnapshot = isHidden
                    });
                    break;
                default :
                {
                    if (newLineHasChange)
                        _unifiedLines.Add(new UnifiedLineItem
                        {
                            NewLineNumber    = newFileLineNumber++,
                            Value            = newRawLine,
                            Diff             = newDiffLine,
                            IsHidden         = isHidden,
                            IsHiddenSnapshot = isHidden
                        });

                    break;
                }
            }

            if (!prevIsHidden && isHidden) hideStart = len;

            if (isHidden) HasSomeLineCollapsed = true;

            prevIsHidden = isHidden;

            if (oldDiffLine?.PrevHunkLine == null && newDiffLine?.PrevHunkLine == null) continue;
            var linePrevHunk = oldDiffLine?.PrevHunkLine ?? newDiffLine?.PrevHunkLine;
            if (linePrevHunk == null) continue;
            if (linePrevHunk.IsFirst == true)
            {
                linePrevHunk.UnifiedInfo = HunkLineInfo.FromHunkInfo(linePrevHunk.HunkInfo!, 0,
                                                                     linePrevHunk.HunkInfo!.NewStartIndex - 1,
                                                                     linePrevHunk.Text);
                hideStart = null;
            }
            else if (hideStart != null)
            {
                linePrevHunk.UnifiedInfo =
                    HunkLineInfo.FromHunkInfo(linePrevHunk.HunkInfo!, hideStart.Value, len, linePrevHunk.Text);
                hideStart = null;
            }

            _unifiedHunksLines      ??= new Dictionary<int, DiffLine>();
            _unifiedHunksLines[len] =   linePrevHunk;
        }

        if (hideStart != null)
        {
            var lastDiff = new DiffLine("", DiffLineType.Hunk, null, null, null)
            {
                IsLast = true,
                UnifiedInfo = new HunkLineInfo
                {
                    StartHiddenIndex         = hideStart.Value,
                    EndHiddenIndex           = _unifiedLines.Count,
                    StartHiddenIndexSnapshot = hideStart.Value,
                    EndHiddenIndexSnapshot   = _unifiedLines.Count,

                    PlainText     = "",
                    OldStartIndex = 0,
                    NewStartIndex = 0,
                    OldLength     = 0,
                    NewLength     = 0
                }
            };
            _unifiedHunksLines                      ??= new Dictionary<int, DiffLine>();
            _unifiedHunksLines[_unifiedLines.Count] =   lastDiff;
        }

        UnifiedLineLength = _unifiedLines.Count;

        _unifiedOldLineNumberIndex = BuildUnifiedLineNumberIndex(_unifiedLines, SplitSide.Old);
        _unifiedNewLineNumberIndex = BuildUnifiedLineNumberIndex(_unifiedLines, SplitSide.New);

        _hasBuildUnified = true;

        NotifyAll();
    }

    // ---- split accessors ----

    /// <summary>
    ///     按索引取分栏左（旧）侧行模型；越界返回 <c>null</c>。<br />Returns the split left (old) side line model at the index, or
    ///     <c>null</c> when out of range.
    /// </summary>
    /// <param name="index"><see cref="SplitLeftLines" /> 中的下标。Index into <see cref="SplitLeftLines" />.</param>
    public SplitLineItem? GetSplitLeftLine(int index)
    {
        return index >= 0 && index < _splitLeftLines.Count ? _splitLeftLines[index] : null;
    }


    /// <summary>
    ///     按索引取分栏右（新）侧行模型；越界返回 <c>null</c>。<br />Returns the split right (new) side line model at the index, or
    ///     <c>null</c> when out of range.
    /// </summary>
    /// <param name="index"><see cref="SplitRightLines" /> 中的下标。Index into <see cref="SplitRightLines" />.</param>
    public SplitLineItem? GetSplitRightLine(int index)
    {
        return index >= 0 && index < _splitRightLines.Count ? _splitRightLines[index] : null;
    }


    /// <summary>
    ///     按文件行号取分栏行模型（旧侧查左列、新侧查右列）；未命中返回 <c>null</c>。<br />Returns the split line model by file line number (old side →
    ///     left column, new side → right column), or <c>null</c> on a miss.
    /// </summary>
    /// <param name="lineNumber">文件行号。File line number.</param>
    /// <param name="side">旧侧或新侧。Old or new side.</param>
    public SplitLineItem? GetSplitLineByLineNumber(int lineNumber, SplitSide side)
    {
        return side == SplitSide.Old
            ? GetByLineNumber(_splitLeftLines, _splitLeftLineNumberIndex, lineNumber)
            : GetByLineNumber(_splitRightLines, _splitRightLineNumberIndex, lineNumber);
    }


    /// <summary>
    ///     按文件行号取分栏行模型在列表中的下标；未命中返回 -1。<br />Returns the list index of the split line model by file line number, or -1 on
    ///     a miss.
    /// </summary>
    /// <param name="lineNumber">文件行号。File line number.</param>
    /// <param name="side">旧侧或新侧。Old or new side.</param>
    public int GetSplitLineIndexByLineNumber(int lineNumber, SplitSide side)
    {
        return side == SplitSide.Old
            ? GetIndexByLineNumber(_splitLeftLineNumberIndex, lineNumber)
            : GetIndexByLineNumber(_splitRightLineNumberIndex, lineNumber);
    }

    /// <summary>
    ///     Builds the line-number → index map backing the split lookups (see the field
    ///     remarks for why it never needs rebuilding).
    /// </summary>
    private static Dictionary<int, int> BuildSplitLineNumberIndex(List<SplitLineItem> lines)
    {
        var index = new Dictionary<int, int>(lines.Count);

        for (var i = 0; i < lines.Count; i++)
            if (lines[i].LineNumber is { } n)
                index[n] = i;

        return index;
    }

    /// <summary>Builds the old/new line-number → index map backing the unified lookups.</summary>
    private static Dictionary<int, int> BuildUnifiedLineNumberIndex(List<UnifiedLineItem> lines, SplitSide side)
    {
        var index = new Dictionary<int, int>(lines.Count);

        for (var i = 0; i < lines.Count; i++)
        {
            var n = side == SplitSide.Old ? lines[i].OldLineNumber : lines[i].NewLineNumber;

            if (n != null) index[n.Value] = i;
        }

        return index;
    }

    /// <summary>
    ///     O(1) equivalent of <c>lines.FirstOrDefault(i => i.LineNumber == lineNumber)</c>
    ///     — a miss (or a not-yet-built model) returns <c>null</c> like the scan did.
    /// </summary>
    private static T? GetByLineNumber<T>(List<T> lines, Dictionary<int, int>? index, int lineNumber) where T : class
    {
        return index != null && index.TryGetValue(lineNumber, out var i) ? lines[i] : null;
    }

    /// <summary>
    ///     O(1) equivalent of <c>lines.FindIndex(i => i.LineNumber == lineNumber)</c>
    ///     — a miss (or a not-yet-built model) returns <c>-1</c> like the scan did.
    /// </summary>
    private static int GetIndexByLineNumber(Dictionary<int, int>? index, int lineNumber)
    {
        return index != null && index.TryGetValue(lineNumber, out var i) ? i : -1;
    }

    /// <summary>
    ///     取分栏折叠区的 hunk 锚点行（索引来自 <see cref="SplitHunkLineIndexes" />）；无则 <c>null</c>。<br />Returns the hunk anchor line
    ///     of a split collapsed region (an index from <see cref="SplitHunkLineIndexes" />), or <c>null</c>.
    /// </summary>
    /// <param name="index">折叠区锚点索引。Collapsed-region anchor index.</param>
    public DiffLine? GetSplitHunkLine(int index)
    {
        return _splitHunksLines != null && _splitHunksLines.TryGetValue(index, out var h) ? h : null;
    }

    // ---- unified accessors ----
    /// <summary>
    ///     按索引取统一视图行模型；越界返回 <c>null</c>。<br />Returns the unified line model at the index, or <c>null</c> when out of
    ///     range.
    /// </summary>
    /// <param name="index"><see cref="UnifiedLines" /> 中的下标。Index into <see cref="UnifiedLines" />.</param>
    public UnifiedLineItem? GetUnifiedLine(int index)
    {
        return index >= 0 && index < _unifiedLines.Count ? _unifiedLines[index] : null;
    }

    /// <summary>
    ///     按文件行号取统一视图行模型；未命中返回 <c>null</c>。<br />Returns the unified line model by file line number, or <c>null</c> on a
    ///     miss.
    /// </summary>
    /// <param name="lineNumber">文件行号。File line number.</param>
    /// <param name="side">旧侧或新侧。Old or new side.</param>
    public UnifiedLineItem? GetUnifiedLineByLineNumber(int lineNumber, SplitSide side)
    {
        return side == SplitSide.Old
            ? GetByLineNumber(_unifiedLines, _unifiedOldLineNumberIndex, lineNumber)
            : GetByLineNumber(_unifiedLines, _unifiedNewLineNumberIndex, lineNumber);
    }

    /// <summary>
    ///     按文件行号取统一视图行模型在列表中的下标；未命中返回 -1。<br />Returns the list index of the unified line model by file line number, or
    ///     -1 on a miss.
    /// </summary>
    /// <param name="lineNumber">文件行号。File line number.</param>
    /// <param name="side">旧侧或新侧。Old or new side.</param>
    public int GetUnifiedLineIndexByLineNumber(int lineNumber, SplitSide side)
    {
        return side == SplitSide.Old
            ? GetIndexByLineNumber(_unifiedOldLineNumberIndex, lineNumber)
            : GetIndexByLineNumber(_unifiedNewLineNumberIndex, lineNumber);
    }

    /// <summary>
    ///     取统一视图折叠区的 hunk 锚点行（索引来自 <see cref="UnifiedHunkLineIndexes" />）；无则 <c>null</c>。<br />Returns the hunk anchor
    ///     line of a unified collapsed region (an index from <see cref="UnifiedHunkLineIndexes" />), or <c>null</c>.
    /// </summary>
    /// <param name="index">折叠区锚点索引。Collapsed-region anchor index.</param>
    public DiffLine? GetUnifiedHunkLine(int index)
    {
        return _unifiedHunksLines != null && _unifiedHunksLines.TryGetValue(index, out var h) ? h : null;
    }

    private void UnhideSplitRange(int start, int end)
    {
        for (var i = start; i < end; i++)
        {
            if (i >= 0 && i < _splitLeftLines.Count && _splitLeftLines[i].IsHidden) _splitLeftLines[i].IsHidden = false;

            if (i >= 0 && i < _splitRightLines.Count && _splitRightLines[i].IsHidden)
                _splitRightLines[i].IsHidden = false;
        }
    }

    private void UnhideUnifiedRange(int start, int end)
    {
        for (var i = start; i < end; i++)
            if (i >= 0 && i < _unifiedLines.Count && _unifiedLines[i].IsHidden)
                _unifiedLines[i].IsHidden = false;
    }

    /// <summary>
    ///     展开分栏视图的一个折叠区；最后一个 hunk 不支持向上展开（静默忽略），向上展开后锚点会迁移到新的 EndHiddenIndex 键。<br />Expands a collapsed region of the
    ///     split view; the last hunk cannot expand up (silently ignored), and upward moves re-key the anchor to its new
    ///     EndHiddenIndex.
    /// </summary>
    /// <param name="dir">展开方向。Expand direction.</param>
    /// <param name="index">
    ///     折叠区锚点索引（<see cref="SplitHunkLineIndexes" /> 之一）。Collapsed-region anchor index (one of
    ///     <see cref="SplitHunkLineIndexes" />).
    /// </param>
    /// <param name="needTrigger">
    ///     是否触发 <see cref="Updated" />；批量操作时可传 false。Whether to raise <see cref="Updated" />; pass false
    ///     for batch operations.
    /// </param>
    public void OnSplitHunkExpand(HunkExpandDirection dir, int index, bool needTrigger = true)
    {
        if (!IsExpandEnabled) return;

        if (_splitHunksLines == null || !_splitHunksLines.TryGetValue(index, out var current)) return;

        if (current.SplitInfo == null) return;

        var info = current.SplitInfo;
        switch (dir)
        {
            case HunkExpandDirection.All :
            {
                UnhideSplitRange(info.StartHiddenIndex, info.EndHiddenIndex);
                current.SplitInfo =
                    info.WithHunkInfo(current.HunkInfo ?? new HunkInfo(), info.EndHiddenIndex, current.Text);
                break;
            }
            case HunkExpandDirection.Down :
            {
                UnhideSplitRange(info.StartHiddenIndex, info.StartHiddenIndex + _composeLen);
                if (current.IsLast == true)
                    current.SplitInfo = info.With(info.StartHiddenIndex + _composeLen);
                else
                    current.SplitInfo = info.With(info.StartHiddenIndex + _composeLen,
                                                  plainText :
                                                  $"@@ -{HunkLineInfo.RenderCount(info.OldStartIndex)},{HunkLineInfo.RenderCount(info.OldLength)} +{HunkLineInfo.RenderCount(info.NewStartIndex)},{HunkLineInfo.RenderCount(info.NewLength)}");

                break;
            }
            case HunkExpandDirection.DownAll :
            {
                UnhideSplitRange(info.StartHiddenIndex, info.EndHiddenIndex);
                current.SplitInfo = info.With(info.EndHiddenIndex, clearPlainText : true);
                break;
            }
            case HunkExpandDirection.Up :
            {
                if (current.IsLast == true)
                    return;

                UnhideSplitRange(info.EndHiddenIndex - _composeLen, info.EndHiddenIndex);
                current.SplitInfo = new HunkLineInfo
                {
                    StartHiddenIndex = info.StartHiddenIndex,
                    EndHiddenIndex   = info.EndHiddenIndex - _composeLen,
                    PlainText =
                        $"@@ -{HunkLineInfo.RenderCount(info.OldStartIndex - _composeLen)},{HunkLineInfo.RenderCount(info.OldLength + _composeLen)} +{HunkLineInfo.RenderCount(info.NewStartIndex - _composeLen)},{HunkLineInfo.RenderCount(info.NewLength + _composeLen)}",
                    StartHiddenIndexSnapshot = info.StartHiddenIndexSnapshot,
                    EndHiddenIndexSnapshot   = info.EndHiddenIndexSnapshot,
                    PlainTextSnapshot        = info.PlainTextSnapshot,
                    OldStartIndex            = info.OldStartIndex - _composeLen,
                    OldLength                = info.OldLength     + _composeLen,
                    NewStartIndex            = info.NewStartIndex - _composeLen,
                    NewLength                = info.NewLength     + _composeLen,
                    OldStartIndexSnapshot    = info.OldStartIndexSnapshot,
                    OldLengthSnapshot        = info.OldLengthSnapshot,
                    NewStartIndexSnapshot    = info.NewStartIndexSnapshot,
                    NewLengthSnapshot        = info.NewLengthSnapshot
                };

                _splitHunksLines.Remove(index);

                _splitHunksLines[current.SplitInfo.EndHiddenIndex] = current;
                break;
            }
            case HunkExpandDirection.UpAll :
            {
                if (current.IsLast == true) return;

                UnhideSplitRange(info.StartHiddenIndex, info.EndHiddenIndex);
                current.SplitInfo = info.With(endHiddenIndex : info.StartHiddenIndex, clearPlainText : true);

                _splitHunksLines.Remove(index);

                _splitHunksLines[current.SplitInfo.EndHiddenIndex] = current;
                break;
            }
        }

        if (needTrigger) NotifyAll();
    }

    /// <summary>
    ///     展开统一视图的一个折叠区；最后一个 hunk 不支持向上展开（静默忽略），向上展开后锚点会迁移到新的 EndHiddenIndex 键。<br />Expands a collapsed region of the
    ///     unified view; the last hunk cannot expand up (silently ignored), and upward moves re-key the anchor to its new
    ///     EndHiddenIndex.
    /// </summary>
    /// <param name="dir">展开方向。Expand direction.</param>
    /// <param name="index">
    ///     折叠区锚点索引（<see cref="UnifiedHunkLineIndexes" /> 之一）。Collapsed-region anchor index (one of
    ///     <see cref="UnifiedHunkLineIndexes" />).
    /// </param>
    /// <param name="needTrigger">
    ///     是否触发 <see cref="Updated" />；批量操作时可传 false。Whether to raise <see cref="Updated" />; pass false
    ///     for batch operations.
    /// </param>
    public void OnUnifiedHunkExpand(HunkExpandDirection dir, int index, bool needTrigger = true)
    {
        if (!IsExpandEnabled) return;

        if (_unifiedHunksLines == null || !_unifiedHunksLines.TryGetValue(index, out var current)) return;

        if (current.UnifiedInfo == null) return;

        var info = current.UnifiedInfo;
        switch (dir)
        {
            case HunkExpandDirection.All :
            {
                UnhideUnifiedRange(info.StartHiddenIndex, info.EndHiddenIndex);
                current.UnifiedInfo = info.WithHunkInfo(current.HunkInfo ?? new HunkInfo(),
                                                        info.EndHiddenIndex, current.Text);
                break;
            }
            case HunkExpandDirection.Down :
            {
                UnhideUnifiedRange(info.StartHiddenIndex, info.StartHiddenIndex + _composeLen);
                if (current.IsLast == true)
                    current.UnifiedInfo = info.With(info.StartHiddenIndex + _composeLen);
                else
                    current.UnifiedInfo = info.With(info.StartHiddenIndex + _composeLen,
                                                    plainText :
                                                    $"@@ -{HunkLineInfo.RenderCount(info.OldStartIndex)},{HunkLineInfo.RenderCount(info.OldLength)} +{HunkLineInfo.RenderCount(info.NewStartIndex)},{HunkLineInfo.RenderCount(info.NewLength)}");

                break;
            }
            case HunkExpandDirection.DownAll :
            {
                UnhideUnifiedRange(info.StartHiddenIndex, info.EndHiddenIndex);
                current.UnifiedInfo = info.With(info.EndHiddenIndex, clearPlainText : true);
                break;
            }
            case HunkExpandDirection.Up :
            {
                if (current.IsLast == true) return;

                UnhideUnifiedRange(info.EndHiddenIndex - _composeLen, info.EndHiddenIndex);
                current.UnifiedInfo = new HunkLineInfo
                {
                    StartHiddenIndex = info.StartHiddenIndex,
                    EndHiddenIndex   = info.EndHiddenIndex - _composeLen,
                    PlainText =
                        $"@@ -{HunkLineInfo.RenderCount(info.OldStartIndex - _composeLen)},{HunkLineInfo.RenderCount(info.OldLength + _composeLen)} +{HunkLineInfo.RenderCount(info.NewStartIndex - _composeLen)},{HunkLineInfo.RenderCount(info.NewLength + _composeLen)}",
                    StartHiddenIndexSnapshot = info.StartHiddenIndexSnapshot,
                    EndHiddenIndexSnapshot   = info.EndHiddenIndexSnapshot,
                    PlainTextSnapshot        = info.PlainTextSnapshot,
                    OldStartIndex            = info.OldStartIndex - _composeLen,
                    OldLength                = info.OldLength     + _composeLen,
                    NewStartIndex            = info.NewStartIndex - _composeLen,
                    NewLength                = info.NewLength     + _composeLen,
                    OldStartIndexSnapshot    = info.OldStartIndexSnapshot,
                    OldLengthSnapshot        = info.OldLengthSnapshot,
                    NewStartIndexSnapshot    = info.NewStartIndexSnapshot,
                    NewLengthSnapshot        = info.NewLengthSnapshot
                };

                _unifiedHunksLines.Remove(index);

                _unifiedHunksLines[current.UnifiedInfo.EndHiddenIndex] = current;
                break;
            }
            case HunkExpandDirection.UpAll :
            {
                if (current.IsLast == true) return;

                UnhideUnifiedRange(info.StartHiddenIndex, info.EndHiddenIndex);
                current.UnifiedInfo = info.With(endHiddenIndex : info.StartHiddenIndex, clearPlainText : true);

                _unifiedHunksLines.Remove(index);

                _unifiedHunksLines[current.UnifiedInfo.EndHiddenIndex] = current;
                break;
            }
        }

        if (needTrigger) NotifyAll();
    }

    /// <summary>onAllExpand(mode: "split" | "unified") 的移植：展开指定视图的全部折叠区。<br />Port of onAllExpand(mode: "split" | "unified").</summary>
    /// <param name="mode">目标视图：分栏或统一。Target view: split or unified.</param>
    public void OnAllExpand(ExpandViewMode mode)
    {
        if (!IsExpandEnabled) return;

        if (mode == ExpandViewMode.Split)
        {
            foreach (var key in _splitHunksLines?.Keys.ToList() ?? [])
                OnSplitHunkExpand(HunkExpandDirection.All, key, false);

            HasExpandSplitAll = true;
        }
        else
        {
            foreach (var key in _unifiedHunksLines?.Keys.ToList() ?? [])
                OnUnifiedHunkExpand(HunkExpandDirection.All, key, false);

            HasExpandUnifiedAll = true;
        }

        NotifyAll();
    }

    /// <summary>
    ///     onAllCollapse(mode: "split" | "unified") 的移植：收起指定视图的全部折叠区（按构建时快照恢复初始折叠状态）。<br />Port of onAllCollapse(mode:
    ///     "split" | "unified").
    /// </summary>
    /// <param name="mode">目标视图：分栏或统一。Target view: split or unified.</param>
    public void OnAllCollapse(ExpandViewMode mode)
    {
        if (!IsExpandEnabled) return;

        if (mode == ExpandViewMode.Split)
        {
            foreach (var item in _splitLeftLines.Where(item => item is { IsHidden: false, IsHiddenSnapshot: true }))
                item.IsHidden = item.IsHiddenSnapshot;

            foreach (var item in _splitRightLines.Where(item => item is { IsHidden: false, IsHiddenSnapshot: true }))
                item.IsHidden = item.IsHiddenSnapshot;

            if (_splitHunksLines != null)
                foreach (var item in _splitHunksLines.Values)
                {
                    if (item.SplitInfo == null) continue;

                    item.SplitInfo = item.SplitInfo.RestoreOriginal();
                }

            foreach (var key in _splitHunksLines?.Keys.ToList() ?? [])
            {
                var item = _splitHunksLines![key];
                if (item.SplitInfo == null) continue;

                if (item.SplitInfo.EndHiddenIndex == key) continue;
                _splitHunksLines!.Remove(key);

                _splitHunksLines[item.SplitInfo.EndHiddenIndex] = item;
            }

            HasExpandSplitAll = false;
        }
        else
        {
            foreach (var item in _unifiedLines.Where(item => item is { IsHidden: false, IsHiddenSnapshot: true }))
                item.IsHidden = item.IsHiddenSnapshot;

            if (_unifiedHunksLines != null)
                foreach (var item in _unifiedHunksLines.Values)
                {
                    if (item.UnifiedInfo == null) continue;

                    item.UnifiedInfo = item.UnifiedInfo.RestoreOriginal();
                }

            foreach (var key in _unifiedHunksLines?.Keys.ToList() ?? [])
            {
                var item = _unifiedHunksLines![key];
                if (item.UnifiedInfo == null) continue;

                if (item.UnifiedInfo.EndHiddenIndex == key) continue;
                _unifiedHunksLines!.Remove(key);

                _unifiedHunksLines[item.UnifiedInfo.EndHiddenIndex] = item;
            }

            HasExpandUnifiedAll = false;
        }

        NotifyAll();
    }

    /// <summary>
    ///     触发 <see cref="Updated" /> 事件并递增 <see cref="UpdateCount" />。<br />Raises the <see cref="Updated" /> event and
    ///     increments <see cref="UpdateCount" />.
    /// </summary>
    /// <param name="skipSyncExternal">
    ///     为对齐 JS 签名保留的参数，当前实现未使用。Parameter kept for JS signature parity; unused in the current
    ///     implementation.
    /// </param>
    public void NotifyAll(bool skipSyncExternal = false)
    {
        UpdateCount++;

        Updated?.Invoke();
    }
}

/// <summary>
///     折叠区的展开方向，对应 JS 字符串 dir："up" | "down" | "all" | "up-all" | "down-all"。<br />JS string dir: "up" | "down" |
///     "all" | "up-all" | "down-all".
/// </summary>
public enum HunkExpandDirection
{
    /// <summary>
    ///     向上展开一段（<see cref="DiffFile.CurrentComposeLength" /> 行）。<br />Expand up one step (
    ///     <see cref="DiffFile.CurrentComposeLength" /> lines).
    /// </summary>
    Up,

    /// <summary>
    ///     向下展开一段（<see cref="DiffFile.CurrentComposeLength" /> 行）。<br />Expand down one step (
    ///     <see cref="DiffFile.CurrentComposeLength" /> lines).
    /// </summary>
    Down,

    /// <summary>展开整个折叠区。<br />Expand the entire collapsed region.</summary>
    All,

    /// <summary>向上全部展开。<br />Expand all the way up.</summary>
    UpAll,

    /// <summary>向下全部展开。<br />Expand all the way down.</summary>
    DownAll
}

/// <summary>目标视图模式，对应 JS 字符串 mode："split" | "unified"。<br />JS string mode: "split" | "unified".</summary>
public enum ExpandViewMode
{
    /// <summary>分栏视图。<br />Split view.</summary>
    Split,

    /// <summary>统一视图。<br />Unified view.</summary>
    Unified
}
