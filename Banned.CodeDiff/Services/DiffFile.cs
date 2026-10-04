using Banned.CodeDiff.Models;

namespace Banned.CodeDiff.Services;

/// <summary>
/// Port of packages/core/src/diff-file.ts — the M1 (pure logic) subset plus the
/// syntax state (M5): raw file composition, diff parsing + word-level ranges,
/// the split/unified line models with expand/collapse, and initSyntax.
///
/// Not ported (web-specific):
/// - bundle serialization (getBundle / mergeBundle / _getFullBundle)
/// - cloned-instance sync and DOM ids (subscribe stays as a simple event)
/// </summary>
public sealed class DiffFile
{
    /// <summary>JS module-level composeLen.</summary>
    private static int _composeLen = 40;

    /// <summary>JS: getCurrentComposeLength.</summary>
    public static int CurrentComposeLength => _composeLen;

    public static void ChangeDefaultComposeLength(int compose)
    {
        _composeLen = compose;
    }

    public static void ResetDefaultComposeLength()
    {
        _composeLen = 40;
    }

    private SourceFile? _oldFileResult;
    private SourceFile? _newFileResult;

    private List<RawDiff>?             _diffListResults;
    private List<DiffLine>?            _diffLines;
    private Dictionary<int, DiffLine>? _oldFileDiffLines;
    private Dictionary<int, DiffLine>? _newFileDiffLines;
    private Dictionary<int, string>?   _oldFileLines;
    private Dictionary<int, string>?   _newFileLines;
    private Dictionary<int, bool>?     _oldFilePlaceholderLines;
    private Dictionary<int, bool>?     _newFilePlaceholderLines;

    private Dictionary<int, SyntaxLine>? _oldFileSyntaxLines;
    private Dictionary<int, SyntaxLine>? _newFileSyntaxLines;

    private string? _highlighterName;
    private HighlighterType? _highlighterType;
    private string? _theme;

    private bool _hasInitSyntax;

    private readonly List<SplitLineItem> _splitLeftLines  = [];
    private readonly List<SplitLineItem> _splitRightLines = [];

    private Dictionary<int, DiffLine>? _splitHunksLines;

    private readonly List<UnifiedLineItem> _unifiedLines = [];

    private Dictionary<int, DiffLine>? _unifiedHunksLines;

    // Line-number → list-index maps for the O(1) *ByLineNumber lookups. The lists are only
    // appended inside their (idempotent, guarded) Build methods and expansions merely flip
    // IsHidden / rewrite SplitInfo / UnifiedInfo, so the non-null line numbers — strictly
    // increasing as each append increments its counter — never change once built. The maps are
    // therefore built once at the end of the Build methods and stay valid for the instance
    // lifetime. Placeholder half-rows carry a null line number and never match a query.
    private Dictionary<int, int>? _splitLeftLineNumberIndex;
    private Dictionary<int, int>? _splitRightLineNumberIndex;
    private Dictionary<int, int>? _unifiedOldLineNumberIndex;
    private Dictionary<int, int>? _unifiedNewLineNumberIndex;

    private bool _hasInitRaw;
    private bool _hasBuildSplit;
    private bool _hasBuildUnified;

    private bool _composeByDiff;

    // JS: set by _mergeFullBundle (bundle serialization is M2 scope)
    private bool _composeByRange = false;
    private bool _hasExpandSplitAll;
    private bool _hasExpandUnifiedAll;
    public  string OldFileName    { get; private set; }
    public  string OldFileContent { get; private set; }
    public  string OldFileLang    { get; private set; }
    public  string NewFileName    { get; private set; }
    public  string NewFileContent { get; private set; }
    public  string NewFileLang    { get; private set; }

    public IReadOnlyList<string> DiffList { get; private set; }

    public int  DiffLineLength       { get; private set; }
    public int  SplitLineLength      { get; private set; }
    public int  UnifiedLineLength    { get; private set; }
    public int  FileLineLength       { get; private set; }
    public int  AdditionLength       { get; private set; }
    public int  DeletionLength       { get; private set; }
    public bool HasSomeLineCollapsed { get; private set; }

    /// <summary>JS: subscribe/notifyAll — the render layer listens for model changes.</summary>
    public event Action? Updated;

    public int UpdateCount { get; private set; }

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

        // JS: getLang(_oldFileLang || _oldFileName || _newFileLang || _newFileName) || "txt"
        var oldLangSource = FirstNonEmpty(oldFileLang, oldFileName, newFileLang, newFileName);
        var newLangSource = FirstNonEmpty(newFileLang, newFileName, oldFileLang, oldFileName);
        OldFileLang = oldLangSource.Length > 0 ? DiffTool.GetLang(oldLangSource) : "txt";
        NewFileLang = newLangSource.Length > 0 ? DiffTool.GetLang(newLangSource) : "txt";

        OldFileContent = oldFileContent;

        NewFileContent = newFileContent;
    }

    private static string FirstNonEmpty(params string?[] values)
    {
        foreach (var v in values)
        {
            if (!string.IsNullOrEmpty(v))
            {
                return v;
            }
        }

        return "";
    }

    // ---- JS: Number() emulation for hunk header counts ----

    /// <summary>Number(undefined) = NaN → null; Number("") = 0; digits → value.</summary>
    private static int? JsNumber(string? s)
    {
        if (s == null)
        {
            return null;
        }

        if (s.Length == 0)
        {
            return 0;
        }

        return int.TryParse(s, out var v) ? v : null;
    }

    // ---- file composition ----

    private void DoFile()
    {
        if (string.IsNullOrEmpty(OldFileContent) && string.IsNullOrEmpty(NewFileContent))
        {
            return;
        }

        if (!string.IsNullOrEmpty(OldFileContent))
        {
            _oldFileResult = new SourceFile(OldFileContent, OldFileLang, OldFileName);
        }

        if (!string.IsNullOrEmpty(NewFileContent))
        {
            _newFileResult = new SourceFile(NewFileContent, NewFileLang, NewFileName);
        }
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
        if (DiffList == null)
        {
            return;
        }

        _diffListResults = DiffList.Select(s => DiffParser.Shared.Parse(s)).ToList();
    }

    private void ComposeDiff()
    {
        if (_diffListResults == null || _diffListResults.Count == 0)
        {
            return;
        }

        _diffLines = [];

        AdditionLength = 0;

        DeletionLength = 0;

        var tmp = new List<DiffLine>();

        foreach (var item in _diffListResults)
        {
            var hunks = item.Hunks;
            foreach (var hunk in hunks)
            {
                var additions = new List<DiffLine>();
                var deletions = new List<DiffLine>();
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
                            additions = [];
                            deletions = [];
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
                    // JS: typedI.text.split("@@")?.[1].split(" ").filter(Boolean)
                    var segs          = i.Text.Split("@@");
                    var numInfoSeg    = segs.Length > 1 ? segs[1] : null;
                    var numParts      = (numInfoSeg ?? "").Split(' ', StringSplitOptions.RemoveEmptyEntries);
                    var oldNumInfo    = numParts.Length > 0 ? numParts[0] : "";
                    var newNumInfo    = numParts.Length > 1 ? numParts[1] : "";
                    var oldNumParts   = oldNumInfo.Split(',');
                    var newNumParts   = newNumInfo.Split(',');
                    // The "?? 0" fallbacks are unreachable: the parser's DiffHeaderRegex
                    // guarantees the first (start index) capture groups are pure digits.
                    var oldStartIndex = -JsNumber(oldNumParts[0]) ?? 0;
                    var oldLength     = oldNumParts.Length > 1 ? JsNumber(oldNumParts[1]) : null;
                    var newStartIndex = JsNumber(newNumParts[0]) ?? 0;
                    var newLength     = newNumParts.Length > 1 ? JsNumber(newNumParts[1]) : null;
                    i.HunkInfo = new HunkInfo
                    {
                        OldStartIndex          = oldStartIndex,
                        OldLength              = oldLength,
                        NewStartIndex          = newStartIndex,
                        NewLength              = newLength,
                        OldStartIndexSnapshot  = oldStartIndex,
                        OldLengthSnapshot      = oldLength,
                        NewStartIndexSnapshot  = newStartIndex,
                        NewLengthSnapshot      = newLength,
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
        if (!string.IsNullOrEmpty(OldFileContent) && !string.IsNullOrEmpty(NewFileContent))
        {
            return;
        }

        var oldFilePlaceholderLines = new Dictionary<int, bool>();

        var newFilePlaceholderLines = new Dictionary<int, bool>();

        // all of the file content not exist, try to use diff result to compose
        if (string.IsNullOrEmpty(OldFileContent) && string.IsNullOrEmpty(NewFileContent))
        {
            var newLineNumber    = 1;
            var oldLineNumber    = 1;
            var oldFileContent   = "";
            var newFileContent   = "";
            var hasSymbolChanged = false;
            while (oldLineNumber <= DiffLineLength || newLineNumber <= DiffLineLength)
            {
                var oldIndex    = oldLineNumber++;
                var newIndex    = newLineNumber++;
                var oldDiffLine = GetOldDiffLine(oldIndex);
                var newDiffLine = GetNewDiffLine(newIndex);
                if (oldDiffLine != null)
                {
                    oldFileContent += oldDiffLine.Text;
                }
                else
                {
                    // empty line for placeholder
                    oldFileContent                    += "\n";
                    oldFilePlaceholderLines[oldIndex] =  true;
                }

                if (newDiffLine != null)
                {
                    newFileContent += newDiffLine.Text;
                }
                else
                {
                    // empty line for placeholder
                    newFileContent                    += "\n";
                    newFilePlaceholderLines[newIndex] =  true;
                }

                if (!hasSymbolChanged && oldDiffLine != null && newDiffLine != null)
                {
                    hasSymbolChanged =
                        hasSymbolChanged || oldDiffLine.NoTrailingNewLine != newDiffLine.NoTrailingNewLine;
                }
            }

            if (!hasSymbolChanged && oldFileContent == newFileContent)
            {
                // JS warns in dev mode; invalid diff string
                return;
            }

            OldFileContent           = oldFileContent;
            NewFileContent           = newFileContent;
            _oldFileResult           = new SourceFile(OldFileContent, OldFileLang, OldFileName);
            _newFileResult           = new SourceFile(NewFileContent, NewFileLang, NewFileName);
            _oldFilePlaceholderLines = oldFilePlaceholderLines;
            _newFilePlaceholderLines = newFilePlaceholderLines;
            // all of the file just compose by diff, so we can not do the expand action
            _composeByDiff = true;
        }
        else if (_oldFileResult != null)
        {
            var newLineNumber    = 1;
            var oldLineNumber    = 1;
            var newFileContent   = "";
            var hasSymbolChanged = false;
            while (oldLineNumber <= _oldFileResult.MaxLineNumber)
            {
                var newDiffLine = GetNewDiffLine(newLineNumber++);
                var oldDiffLine = GetOldDiffLine(oldLineNumber);
                if (newDiffLine != null)
                {
                    newFileContent += newDiffLine.Text;
                    oldLineNumber  =  newDiffLine.OldLineNumber is { } ol ? ol + 1 : oldLineNumber;
                }
                else
                {
                    if (oldDiffLine == null)
                    {
                        newFileContent += GetOldRawLine(oldLineNumber) ?? "";
                    }

                    oldLineNumber++;
                }

                if (!hasSymbolChanged && newDiffLine != null && oldDiffLine != null)
                {
                    hasSymbolChanged =
                        hasSymbolChanged || newDiffLine.NoTrailingNewLine != oldDiffLine.NoTrailingNewLine;
                }
            }

            if (!hasSymbolChanged && newFileContent == OldFileContent)
            {
                return;
            }

            NewFileContent = newFileContent;
            _newFileResult = new SourceFile(NewFileContent, NewFileLang, NewFileName);
        }
        else if (_newFileResult != null)
        {
            var oldLineNumber    = 1;
            var newLineNumber    = 1;
            var oldFileContent   = "";
            var hasSymbolChanged = false;
            while (newLineNumber <= _newFileResult.MaxLineNumber)
            {
                var oldDiffLine = GetOldDiffLine(oldLineNumber++);
                var newDiffLine = GetNewDiffLine(newLineNumber);
                if (oldDiffLine != null)
                {
                    oldFileContent += oldDiffLine.Text;
                    newLineNumber  =  oldDiffLine.NewLineNumber is { } nl ? nl + 1 : newLineNumber;
                }
                else
                {
                    if (newDiffLine == null)
                    {
                        oldFileContent += GetNewRawLine(newLineNumber) ?? "";
                    }

                    newLineNumber++;
                }

                if (!hasSymbolChanged && newDiffLine != null && oldDiffLine != null)
                {
                    hasSymbolChanged =
                        hasSymbolChanged || newDiffLine.NoTrailingNewLine != oldDiffLine.NoTrailingNewLine;
                }
            }

            if (!hasSymbolChanged && oldFileContent == NewFileContent)
            {
                return;
            }

            OldFileContent = oldFileContent;
            _oldFileResult = new SourceFile(OldFileContent, OldFileLang, OldFileName);
        }

        ComposeRaw();
    }

    private DiffLine? GetOldDiffLine(int? lineNumber)
    {
        if (lineNumber is not { } ln || ln == 0)
        {
            return null;
        }

        return _oldFileDiffLines != null && _oldFileDiffLines.TryGetValue(ln, out var l) ? l : null;
    }

    private DiffLine? GetNewDiffLine(int? lineNumber)
    {
        if (lineNumber is not { } ln || ln == 0)
        {
            return null;
        }

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

    /// <summary>Port of initRaw (plus the initRaw-time #syncSyntax call).</summary>
    public void InitRaw()
    {
        if (_hasInitRaw)
        {
            return;
        }

        DoFile();
        ComposeRaw();
        DoDiff();
        ComposeDiff();
        ComposeFile();
        SyncSyntax();
        _hasInitRaw = true;
    }

    // ---- syntax (initSyntax) ----

    /// <summary>Port of initTheme: theme ?? existing ?? "light".</summary>
    public void InitTheme(string? theme)
    {
        _theme = theme ?? _theme ?? "light";
    }

    /// <summary>JS: _getTheme.</summary>
    public string? Theme => _theme;

    /// <summary>JS: _getHighlighterName.</summary>
    public string? HighlighterName => _highlighterName;

    /// <summary>JS: _getHighlighterType.</summary>
    public HighlighterType? HighlighterType => _highlighterType;

    /// <summary>Port of initSyntax({ registerHighlighter }).</summary>
    public void InitSyntax(IDiffHighlighter? registerHighlighter = null)
    {
        if (_hasInitSyntax && (registerHighlighter == null ||
                               (registerHighlighter.Name == _highlighterName &&
                                registerHighlighter.Type == _highlighterType)))
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
        // JS: the composeByMerge-without-full-merge bail-out is not ported
        // (bundle serialization is not ported).

        ComposeSyntax(registerHighlighter);

        SyncSyntax();
    }

    private void ComposeSyntax(IDiffHighlighter? registerHighlighter)
    {
        _oldFileResult?.DoSyntax(registerHighlighter, _theme);

        _oldFileSyntaxLines = _oldFileResult?.SyntaxFile;

        _newFileResult?.DoSyntax(registerHighlighter, _theme);

        _newFileSyntaxLines = _newFileResult?.SyntaxFile;
    }

    private void SyncSyntax()
    {
        _highlighterName = !string.IsNullOrEmpty(_oldFileResult?.HighlighterName) ? _oldFileResult!.HighlighterName
                           : !string.IsNullOrEmpty(_newFileResult?.HighlighterName) ? _newFileResult!.HighlighterName
                           : _highlighterName;

        _highlighterType = _oldFileResult?.HighlighterType
                       ?? _newFileResult?.HighlighterType
                       ?? _highlighterType;

        if (!string.IsNullOrEmpty(_oldFileResult?.HighlighterName))
        {
            _oldFileSyntaxLines = _oldFileResult!.SyntaxFile;
        }

        if (!string.IsNullOrEmpty(_newFileResult?.HighlighterName))
        {
            _newFileSyntaxLines = _newFileResult!.SyntaxFile;
        }
    }

    /// <summary>Port of getOldSyntaxLine — syntax spans of the old file line, or <c>null</c>.</summary>
    public SyntaxLine? GetOldSyntaxLine(int lineNumber)
    {
        return _oldFileSyntaxLines != null && _oldFileSyntaxLines.TryGetValue(lineNumber, out var line) ? line : null;
    }

    /// <summary>Port of getNewSyntaxLine — syntax spans of the new file line, or <c>null</c>.</summary>
    public SyntaxLine? GetNewSyntaxLine(int lineNumber)
    {
        return _newFileSyntaxLines != null && _newFileSyntaxLines.TryGetValue(lineNumber, out var line) ? line : null;
    }

    public void Init()
    {
        InitRaw();
        InitSyntax();
    }

    // ---- split / unified line models ----

    public void BuildSplitDiffLines()
    {
        if (_hasBuildSplit)
        {
            return;
        }

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
            {
                switch (oldDiffLine.NewLineNumber)
                {
                    case { } onl when onl > newFileLineNumber :
                        newFileLineNumber++;
                        continue;
                    case null :
                        newFileLineNumber++;
                        break;
                }
            }

            if (newDiffLine != null && oldDiffLine == null)
            {
                switch (newDiffLine.OldLineNumber)
                {
                    case { } nol when nol > oldFileLineNumber :
                        oldFileLineNumber++;
                        continue;
                    case null :
                        oldFileLineNumber++;
                        break;
                }
            }

            if (oldDiffLine == null && string.IsNullOrEmpty(oldRawLine) && newDiffLine == null &&
                string.IsNullOrEmpty(newRawLine))
            {
                break;
            }

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
                        IsHiddenSnapshot = isHidden,
                    });
                    _splitRightLines.Add(new SplitLineItem
                    {
                        LineNumber = newFileLineNumber++, Value = newRawLine, Diff = newDiffLine, IsHidden = isHidden,
                        IsHiddenSnapshot = isHidden,
                    });
                    break;
                case true :
                    _splitLeftLines.Add(new SplitLineItem
                    {
                        LineNumber = oldFileLineNumber++, Value = oldRawLine, Diff = oldDiffLine, IsHidden = isHidden,
                        IsHiddenSnapshot = isHidden,
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
                            IsHidden         = isHidden, IsHiddenSnapshot = isHidden,
                        });
                    }

                    break;
                }
            }

            if (!prevIsHidden && isHidden)
            {
                hideStart = len;
            }

            if (isHidden)
            {
                HasSomeLineCollapsed = true;
            }

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

        // have last hunk
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

                    // just for placeholder
                    PlainText     = "",
                    OldStartIndex = 0,
                    NewStartIndex = 0,
                    OldLength     = 0,
                    NewLength     = 0,
                }
            };
            _splitHunksLines                         ??= new Dictionary<int, DiffLine>();
            _splitHunksLines[_splitRightLines.Count] =   lastDiff;
            hideStart                                =   null;
        }

        SplitLineLength = _splitRightLines.Count;

        _splitLeftLineNumberIndex  = BuildSplitLineNumberIndex(_splitLeftLines);
        _splitRightLineNumberIndex = BuildSplitLineNumberIndex(_splitRightLines);

        _hasBuildSplit = true;

        NotifyAll();
    }

    public void BuildUnifiedDiffLines()
    {
        if (_hasBuildUnified)
        {
            return;
        }

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
            {
                switch (oldDiffLine.NewLineNumber)
                {
                    case { } onl when onl > newFileLineNumber :
                        newFileLineNumber++;
                        continue;
                    case null :
                        newFileLineNumber++;
                        break;
                }
            }

            if (newDiffLine != null && oldDiffLine == null)
            {
                switch (newDiffLine.OldLineNumber)
                {
                    case { } nol when nol > oldFileLineNumber :
                        oldFileLineNumber++;
                        continue;
                    case null :
                        oldFileLineNumber++;
                        break;
                }
            }

            if (string.IsNullOrEmpty(oldRawLine) &&
                string.IsNullOrEmpty(newRawLine) &&
                newDiffLine == null              &&
                oldDiffLine == null)
            {
                break;
            }

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
                        IsHiddenSnapshot = isHidden,
                    });
                    break;
                case true :
                    _unifiedLines.Add(new UnifiedLineItem
                    {
                        OldLineNumber    = oldFileLineNumber++,
                        Value            = oldRawLine,
                        Diff             = oldDiffLine,
                        IsHidden         = isHidden,
                        IsHiddenSnapshot = isHidden,
                    });
                    break;
                default :
                {
                    if (newLineHasChange)
                    {
                        _unifiedLines.Add(new UnifiedLineItem
                        {
                            NewLineNumber    = newFileLineNumber++,
                            Value            = newRawLine,
                            Diff             = newDiffLine,
                            IsHidden         = isHidden,
                            IsHiddenSnapshot = isHidden,
                        });
                    }

                    break;
                }
            }

            if (!prevIsHidden && isHidden)
            {
                hideStart = len;
            }

            if (isHidden)
            {
                HasSomeLineCollapsed = true;
            }

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

        // have last hunk
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

                    // just for placeholder
                    PlainText     = "",
                    OldStartIndex = 0,
                    NewStartIndex = 0,
                    OldLength     = 0,
                    NewLength     = 0,
                }
            };
            _unifiedHunksLines                      ??= new Dictionary<int, DiffLine>();
            _unifiedHunksLines[_unifiedLines.Count] =   lastDiff;
            hideStart                               =   null;
        }

        UnifiedLineLength = _unifiedLines.Count;

        _unifiedOldLineNumberIndex = BuildUnifiedLineNumberIndex(_unifiedLines, SplitSide.Old);
        _unifiedNewLineNumberIndex = BuildUnifiedLineNumberIndex(_unifiedLines, SplitSide.New);

        _hasBuildUnified = true;

        NotifyAll();
    }

    // ---- split accessors ----

    public SplitLineItem? GetSplitLeftLine(int index) =>
        index >= 0 && index < _splitLeftLines.Count ? _splitLeftLines[index] : null;


    public SplitLineItem? GetSplitRightLine(int index) =>
        index >= 0 && index < _splitRightLines.Count ? _splitRightLines[index] : null;


    public SplitLineItem? GetSplitLineByLineNumber(int lineNumber, SplitSide side) => side == SplitSide.Old
        ? GetByLineNumber(_splitLeftLines, _splitLeftLineNumberIndex, lineNumber)
        : GetByLineNumber(_splitRightLines, _splitRightLineNumberIndex, lineNumber);


    public int GetSplitLineIndexByLineNumber(int lineNumber, SplitSide side) => side == SplitSide.Old
        ? GetIndexByLineNumber(_splitLeftLineNumberIndex, lineNumber)
        : GetIndexByLineNumber(_splitRightLineNumberIndex, lineNumber);

    /// <summary>Builds the line-number → index map backing the split lookups (see the field
    /// remarks for why it never needs rebuilding).</summary>
    private static Dictionary<int, int> BuildSplitLineNumberIndex(List<SplitLineItem> lines)
    {
        var index = new Dictionary<int, int>(lines.Count);

        for (var i = 0; i < lines.Count; i++)
        {
            if (lines[i].LineNumber is { } n)
            {
                index[n] = i;
            }
        }

        return index;
    }

    /// <summary>Builds the old/new line-number → index map backing the unified lookups.</summary>
    private static Dictionary<int, int> BuildUnifiedLineNumberIndex(List<UnifiedLineItem> lines, SplitSide side)
    {
        var index = new Dictionary<int, int>(lines.Count);

        for (var i = 0; i < lines.Count; i++)
        {
            var n = side == SplitSide.Old ? lines[i].OldLineNumber : lines[i].NewLineNumber;

            if (n != null)
            {
                index[n.Value] = i;
            }
        }

        return index;
    }

    /// <summary>O(1) equivalent of <c>lines.FirstOrDefault(i => i.LineNumber == lineNumber)</c>
    /// — a miss (or a not-yet-built model) returns <c>null</c> like the scan did.</summary>
    private static T? GetByLineNumber<T>(List<T> lines, Dictionary<int, int>? index, int lineNumber)
        where T : class
        => index != null && index.TryGetValue(lineNumber, out var i) ? lines[i] : null;

    /// <summary>O(1) equivalent of <c>lines.FindIndex(i => i.LineNumber == lineNumber)</c>
    /// — a miss (or a not-yet-built model) returns <c>-1</c> like the scan did.</summary>
    private static int GetIndexByLineNumber(Dictionary<int, int>? index, int lineNumber)
        => index != null && index.TryGetValue(lineNumber, out var i) ? i : -1;


    public DiffLine? GetSplitHunkLine(int index) =>
        _splitHunksLines != null && _splitHunksLines.TryGetValue(index, out var h) ? h : null;


    // ---- unified accessors ----

    public UnifiedLineItem? GetUnifiedLine(int index) =>
        index >= 0 && index < _unifiedLines.Count ? _unifiedLines[index] : null;


    public UnifiedLineItem? GetUnifiedLineByLineNumber(int lineNumber, SplitSide side) => side == SplitSide.Old
        ? GetByLineNumber(_unifiedLines, _unifiedOldLineNumberIndex, lineNumber)
        : GetByLineNumber(_unifiedLines, _unifiedNewLineNumberIndex, lineNumber);


    public int GetUnifiedLineIndexByLineNumber(int lineNumber, SplitSide side) => side == SplitSide.Old
        ? GetIndexByLineNumber(_unifiedOldLineNumberIndex, lineNumber)
        : GetIndexByLineNumber(_unifiedNewLineNumberIndex, lineNumber);


    public DiffLine? GetUnifiedHunkLine(int index) =>
        _unifiedHunksLines != null && _unifiedHunksLines.TryGetValue(index, out var h) ? h : null;


    // ---- expansion ----

    /// <summary>JS: getExpandEnabled.</summary>
    public bool IsExpandEnabled => !_composeByDiff && !_composeByRange;


    public bool HasExpandSplitAll => _hasExpandSplitAll;

    public bool HasExpandUnifiedAll => _hasExpandUnifiedAll;

    private void UnhideSplitRange(int start, int end)
    {
        for (var i = start; i < end; i++)
        {
            if (i >= 0 && i < _splitLeftLines.Count && _splitLeftLines[i].IsHidden)
            {
                _splitLeftLines[i].IsHidden = false;
            }

            if (i >= 0 && i < _splitRightLines.Count && _splitRightLines[i].IsHidden)
            {
                _splitRightLines[i].IsHidden = false;
            }
        }
    }

    private void UnhideUnifiedRange(int start, int end)
    {
        for (var i = start; i < end; i++)
        {
            if (i >= 0 && i < _unifiedLines.Count && _unifiedLines[i].IsHidden)
            {
                _unifiedLines[i].IsHidden = false;
            }
        }
    }

    public void OnSplitHunkExpand(HunkExpandDirection dir, int index, bool needTrigger = true)
    {
        if (!IsExpandEnabled)
        {
            return;
        }

        if (_splitHunksLines == null || !_splitHunksLines.TryGetValue(index, out var current))
        {
            return;
        }

        if (current.SplitInfo == null)
        {
            return;
        }

        var info = current.SplitInfo;
        switch (dir)
        {
            case HunkExpandDirection.All :
            {
                UnhideSplitRange(info.StartHiddenIndex, info.EndHiddenIndex);
                current.SplitInfo = info.WithHunkInfo(
                                                      current.HunkInfo ?? new HunkInfo(),
                                                      startHiddenIndex : info.EndHiddenIndex,
                                                      plainText : current.Text
                                                     );
                break;
            }
            case HunkExpandDirection.Down :
            {
                UnhideSplitRange(info.StartHiddenIndex, info.StartHiddenIndex + _composeLen);
                if (current.IsLast == true)
                {
                    current.SplitInfo = info.With(startHiddenIndex : info.StartHiddenIndex + _composeLen);
                }
                else
                {
                    current.SplitInfo = info.With(startHiddenIndex : info.StartHiddenIndex + _composeLen,
                                                  plainText :
                                                  $"@@ -{HunkLineInfo.RenderCount(info.OldStartIndex)},{HunkLineInfo.RenderCount(info.OldLength)} +{HunkLineInfo.RenderCount(info.NewStartIndex)},{HunkLineInfo.RenderCount(info.NewLength)}");
                }

                break;
            }
            case HunkExpandDirection.DownAll :
            {
                UnhideSplitRange(info.StartHiddenIndex, info.EndHiddenIndex);
                current.SplitInfo = info.With(startHiddenIndex : info.EndHiddenIndex, clearPlainText : true);
                break;
            }
            case HunkExpandDirection.Up :
            {
                if (current.IsLast == true)
                {
                    // JS: console.error "[@git-diff-view/core] The last hunk cannot expand up!"
                    return;
                }

                UnhideSplitRange(info.EndHiddenIndex - _composeLen, info.EndHiddenIndex);
                current.SplitInfo = new HunkLineInfo
                {
                    StartHiddenIndex         = info.StartHiddenIndex,
                    EndHiddenIndex           = info.EndHiddenIndex - _composeLen,
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
                    NewLengthSnapshot        = info.NewLengthSnapshot,
                };

                _splitHunksLines.Remove(index);

                _splitHunksLines[current.SplitInfo.EndHiddenIndex] = current;
                break;
            }
            case HunkExpandDirection.UpAll :
            {
                if (current.IsLast == true)
                {
                    return;
                }

                UnhideSplitRange(info.StartHiddenIndex, info.EndHiddenIndex);
                current.SplitInfo = info.With(endHiddenIndex : info.StartHiddenIndex, clearPlainText : true);

                _splitHunksLines.Remove(index);

                _splitHunksLines[current.SplitInfo.EndHiddenIndex] = current;
                break;
            }
        }

        if (needTrigger)
        {
            NotifyAll();
        }
    }

    public void OnUnifiedHunkExpand(HunkExpandDirection dir, int index, bool needTrigger = true)
    {
        if (!IsExpandEnabled)
        {
            return;
        }

        if (_unifiedHunksLines == null || !_unifiedHunksLines.TryGetValue(index, out var current))
        {
            return;
        }

        if (current.UnifiedInfo == null)
        {
            return;
        }

        var info = current.UnifiedInfo;
        switch (dir)
        {
            case HunkExpandDirection.All :
            {
                UnhideUnifiedRange(info.StartHiddenIndex, info.EndHiddenIndex);
                current.UnifiedInfo = info.WithHunkInfo(current.HunkInfo ?? new HunkInfo(),
                                                        startHiddenIndex : info.EndHiddenIndex,
                                                        plainText : current.Text);
                break;
            }
            case HunkExpandDirection.Down :
            {
                UnhideUnifiedRange(info.StartHiddenIndex, info.StartHiddenIndex + _composeLen);
                if (current.IsLast == true)
                {
                    current.UnifiedInfo = info.With(startHiddenIndex : info.StartHiddenIndex + _composeLen);
                }
                else
                {
                    current.UnifiedInfo = info.With(startHiddenIndex : info.StartHiddenIndex + _composeLen,
                                                    plainText :
                                                    $"@@ -{HunkLineInfo.RenderCount(info.OldStartIndex)},{HunkLineInfo.RenderCount(info.OldLength)} +{HunkLineInfo.RenderCount(info.NewStartIndex)},{HunkLineInfo.RenderCount(info.NewLength)}");
                }

                break;
            }
            case HunkExpandDirection.DownAll :
            {
                UnhideUnifiedRange(info.StartHiddenIndex, info.EndHiddenIndex);
                current.UnifiedInfo = info.With(startHiddenIndex : info.EndHiddenIndex, clearPlainText : true);
                break;
            }
            case HunkExpandDirection.Up :
            {
                if (current.IsLast == true)
                {
                    return;
                }

                UnhideUnifiedRange(info.EndHiddenIndex - _composeLen, info.EndHiddenIndex);
                current.UnifiedInfo = new HunkLineInfo
                {
                    StartHiddenIndex         = info.StartHiddenIndex,
                    EndHiddenIndex           = info.EndHiddenIndex - _composeLen,
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
                    NewLengthSnapshot        = info.NewLengthSnapshot,
                };

                _unifiedHunksLines.Remove(index);

                _unifiedHunksLines[current.UnifiedInfo.EndHiddenIndex] = current;
                break;
            }
            case HunkExpandDirection.UpAll :
            {
                if (current.IsLast == true)
                {
                    return;
                }

                UnhideUnifiedRange(info.StartHiddenIndex, info.EndHiddenIndex);
                current.UnifiedInfo = info.With(endHiddenIndex : info.StartHiddenIndex, clearPlainText : true);

                _unifiedHunksLines.Remove(index);

                _unifiedHunksLines[current.UnifiedInfo.EndHiddenIndex] = current;
                break;
            }
        }

        if (needTrigger)
        {
            NotifyAll();
        }
    }

    /// <summary>Port of onAllExpand(mode: "split" | "unified").</summary>
    public void OnAllExpand(ExpandViewMode mode)
    {
        if (!IsExpandEnabled)
        {
            return;
        }

        if (mode == ExpandViewMode.Split)
        {
            foreach (var key in _splitHunksLines?.Keys.ToList() ?? [])
            {
                OnSplitHunkExpand(HunkExpandDirection.All, key, false);
            }

            _hasExpandSplitAll = true;
        }
        else
        {
            foreach (var key in _unifiedHunksLines?.Keys.ToList() ?? [])
            {
                OnUnifiedHunkExpand(HunkExpandDirection.All, key, false);
            }

            _hasExpandUnifiedAll = true;
        }

        NotifyAll();
    }

    /// <summary>Port of onAllCollapse(mode: "split" | "unified").</summary>
    public void OnAllCollapse(ExpandViewMode mode)
    {
        if (!IsExpandEnabled)
        {
            return;
        }

        if (mode == ExpandViewMode.Split)
        {
            foreach (var item in _splitLeftLines.Where(item => item is { IsHidden: false, IsHiddenSnapshot: true }))
            {
                item.IsHidden = item.IsHiddenSnapshot;
            }

            foreach (var item in _splitRightLines.Where(item => item is { IsHidden: false, IsHiddenSnapshot: true }))
            {
                item.IsHidden = item.IsHiddenSnapshot;
            }

            if (_splitHunksLines != null)
            {
                foreach (var item in _splitHunksLines.Values)
                {
                    if (item.SplitInfo == null)
                    {
                        continue;
                    }

                    item.SplitInfo = item.SplitInfo.RestoreOriginal();
                }
            }

            foreach (var key in _splitHunksLines?.Keys.ToList() ?? [])
            {
                var item = _splitHunksLines![key];
                if (item.SplitInfo == null)
                {
                    continue;
                }

                if (item.SplitInfo.EndHiddenIndex == key) continue;
                _splitHunksLines!.Remove(key);

                _splitHunksLines[item.SplitInfo.EndHiddenIndex] = item;
            }

            _hasExpandSplitAll = false;
        }
        else
        {
            foreach (var item in _unifiedLines.Where(item => item is { IsHidden: false, IsHiddenSnapshot: true }))
            {
                item.IsHidden = item.IsHiddenSnapshot;
            }

            if (_unifiedHunksLines != null)
            {
                foreach (var item in _unifiedHunksLines.Values)
                {
                    if (item.UnifiedInfo == null)
                    {
                        continue;
                    }

                    item.UnifiedInfo = item.UnifiedInfo.RestoreOriginal();
                }
            }

            foreach (var key in _unifiedHunksLines?.Keys.ToList() ?? [])
            {
                var item = _unifiedHunksLines![key];
                if (item.UnifiedInfo == null)
                {
                    continue;
                }

                if (item.UnifiedInfo.EndHiddenIndex == key) continue;
                _unifiedHunksLines!.Remove(key);

                _unifiedHunksLines[item.UnifiedInfo.EndHiddenIndex] = item;
            }

            _hasExpandUnifiedAll = false;
        }

        NotifyAll();
    }

    // ---- misc accessors ----

    /// <summary>
    /// JS: getOldFileContent — the transform-processed raw of the old file
    /// (<c>null</c> before <see cref="InitRaw"/>). Distinct from the
    /// <see cref="OldFileContent"/> input property.
    /// </summary>
    public string? OldFileRaw => _oldFileResult?.Raw;

    /// <summary>
    /// JS: getNewFileContent — the transform-processed raw of the new file
    /// (<c>null</c> before <see cref="InitRaw"/>). Distinct from the
    /// <see cref="NewFileContent"/> input property.
    /// </summary>
    public string? NewFileRaw => _newFileResult?.Raw;

    /// <summary>JS: _getIsPureDiffRender.</summary>
    public bool IsPureDiffRender => _composeByDiff;

    public void NotifyAll(bool skipSyncExternal = false)
    {
        UpdateCount++;

        Updated?.Invoke();
    }

    public IReadOnlyList<SplitLineItem> SplitLeftLines => _splitLeftLines;

    public IReadOnlyList<SplitLineItem> SplitRightLines => _splitRightLines;

    public IReadOnlyList<UnifiedLineItem> UnifiedLines => _unifiedLines;

    public IReadOnlyCollection<int> SplitHunkLineIndexes => _splitHunksLines?.Keys.ToArray() ?? [];

    public IReadOnlyCollection<int> UnifiedHunkLineIndexes => _unifiedHunksLines?.Keys.ToArray() ?? [];
}

/// <summary>JS string dir: "up" | "down" | "all" | "up-all" | "down-all".</summary>
public enum HunkExpandDirection
{
    Up,
    Down,
    All,
    UpAll,
    DownAll,
}

/// <summary>JS string mode: "split" | "unified".</summary>
public enum ExpandViewMode
{
    Split,
    Unified,
}
