using Avalonia.Styling;
using Banned.CodeDiff.Avalonia.Demo.Models;
using Banned.CodeDiff.Avalonia.Models;
using Banned.CodeDiff.Services;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;

namespace Banned.CodeDiff.Avalonia.Demo.ViewModels;

public sealed class MainWindowViewModel : INotifyPropertyChanged
{
    private string       _diffText = SampleDiff.ProgramCs;
    private DiffFile?    _diffFile;
    private bool         _isDark;
    private DiffViewMode _viewMode   = DiffViewMode.Split;
    private bool         _isFastDiff = true;

    public MainWindowViewModel()
    {
        // Word-level ranges are computed at DiffFile.Init() time; the global switch must be set
        // before the first render. Toggling re-creates the file.
        TemplateOptions.SetEnableFastDiffTemplate(_isFastDiff);
        RenderCommand          = new RelayCommand(Render);
        LoadSampleCommand      = new RelayCommand(LoadSample);
        LoadExpandableCommand  = new RelayCommand(LoadExpandable);
        ExpandAllCommand       = new RelayCommand(ExpandAll);
        CollapseAllCommand     = new RelayCommand(CollapseAll);
        Render();
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public ICommand RenderCommand { get; }

    public ICommand LoadSampleCommand { get; }

    public ICommand LoadExpandableCommand { get; }

    public ICommand ExpandAllCommand { get; }

    public ICommand CollapseAllCommand { get; }

    public bool IsFastDiff
    {
        get => _isFastDiff;
        set
        {
            if (!Set(ref _isFastDiff, value)) return;
            TemplateOptions.SetEnableFastDiffTemplate(value);
            Render();
        }
    }

    public string DiffText
    {
        get => _diffText;
        set => Set(ref _diffText, value);
    }

    public DiffFile? DiffFile
    {
        get => _diffFile;
        set
        {
            if (_diffFile != null)
            {
                _diffFile.Updated -= OnFileUpdated;
            }

            if (Set(ref _diffFile, value))
            {
                if (_diffFile != null)
                {
                    // Expansion mutates the model in place; refresh stats and button states.
                    _diffFile.Updated += OnFileUpdated;
                }

                OnPropertyChanged(nameof(Stats));
                OnPropertyChanged(nameof(CanExpandHunks));
            }
        }
    }

    /// <summary>Gets whether the current model still has collapsed hunks that can be expanded.</summary>
    public bool CanExpandHunks => DiffFile?.HasSomeLineCollapsed == true && DiffFile.GetExpandEnabled();

    public DiffViewMode ViewMode
    {
        get => _viewMode;
        set => Set(ref _viewMode, value);
    }

    public bool IsUnified
    {
        get => _viewMode == DiffViewMode.Unified;
        set
        {
            var mode = value ? DiffViewMode.Unified : DiffViewMode.Split;

            if (Set(ref _viewMode, mode))
            {
                OnPropertyChanged(nameof(ViewMode));
            }
        }
    }

    public bool IsDark
    {
        get => _isDark;
        set
        {
            if (Set(ref _isDark, value))
            {
                OnPropertyChanged(nameof(Theme));
            }
        }
    }

    public ThemeVariant Theme => IsDark ? ThemeVariant.Dark : ThemeVariant.Light;

    public string Stats
    {
        get
        {
            var file = DiffFile;

            if (file == null)
            {
                return "无模型";
            }

            var collapsed = file.HasSomeLineCollapsed ? " / 有折叠行" : "";

            return $"+{file.AdditionLength} -{file.DeletionLength} / split {file.SplitLineLength} 行 / unified {file.UnifiedLineLength} 行{collapsed}";
        }
    }

    private void LoadSample()
    {
        DiffText = SampleDiff.ProgramCs;
        Render();
    }

    private void LoadExpandable()
    {
        var (oldContent, newContent, diffText) = ExpandableSample.Create();

        DiffText = diffText;

        // Real file contents (not paste-only) keep the expand state machine enabled.
        var file = new DiffFile(oldFileName : "Sample.cs", oldFileContent : oldContent,
                                newFileName : "Sample.cs", newFileContent : newContent,
                                diffList    : [diffText]);
        file.Init();
        file.BuildSplitDiffLines();
        file.BuildUnifiedDiffLines();
        DiffFile = file;
    }

    private void ExpandAll() =>
        DiffFile?.OnAllExpand(_viewMode == DiffViewMode.Unified ? ExpandViewMode.Unified : ExpandViewMode.Split);

    private void CollapseAll() =>
        DiffFile?.OnAllCollapse(_viewMode == DiffViewMode.Unified ? ExpandViewMode.Unified : ExpandViewMode.Split);

    private void OnFileUpdated()
    {
        OnPropertyChanged(nameof(Stats));
        OnPropertyChanged(nameof(CanExpandHunks));
    }

    private void Render()
    {
        // Paste-only mode: no full file contents, so the core composes both sides from the diff
        // text itself (word-level ranges fall back to diff-line text and hunk expansion is disabled).
        // Both row models are built here so the stats are accurate; the control's own Build calls
        // are guarded no-ops.
        if (string.IsNullOrWhiteSpace(DiffText))
        {
            DiffFile = null;
            return;
        }

        var file = new DiffFile(oldFileName : "", oldFileContent : "", newFileName : "", newFileContent : "",
                                diffList : [DiffText]);
        file.Init();
        file.BuildSplitDiffLines();
        file.BuildUnifiedDiffLines();
        DiffFile = file;
    }

    private bool Set<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value))
        {
            return false;
        }

        field = value;
        OnPropertyChanged(propertyName);
        return true;
    }

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}
