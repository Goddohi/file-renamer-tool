using System.IO;
using System.Collections;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Text;
using System.Windows;
using System.Windows.Input;
using FileRenamer.Core;

namespace FileRenamer.App;

public sealed class MainViewModel : INotifyPropertyChanged
{
    private readonly SettingsStore _store = new(SettingsStore.DefaultPath);
    private readonly IFileSystem _fs = new PhysicalFileSystem();
    private AppSettings _settings;
    private IReadOnlyList<RenamePlanItem>? _plan;
    private RenameResult? _lastResult;
    private bool _loading = true;

    private int _modeIndex;
    private string _replaceFind = "";
    private string _replaceWith = "";
    private bool _useRegex;
    private string _template = "";
    private int _caseIndex;
    private string _seqBaseName = "";
    private int _seqStart = 1;
    private int _seqDigits = 1;
    private int _seqSepIndex;
    private string _errorMessage = "";
    private int _changedCount;
    private int _unchangedCount;
    private int _conflictCount;
    private string _conflictMessage = "";
    private string _applyText = "적용";

    public MainViewModel()
    {
        _settings = _store.Load();
        _modeIndex = Math.Clamp(_settings.SelectedMode, 0, 3);
        _replaceFind = _settings.ReplaceFind;
        _replaceWith = _settings.ReplaceWith;
        _useRegex = _settings.UseRegex;
        _template = _settings.Template;
        _caseIndex = (int)_settings.CaseStyle;
        _seqBaseName = _settings.SequenceBaseName;
        _seqStart = _settings.SequenceStart;
        _seqDigits = _settings.SequenceDigits;
        _seqSepIndex = (int)_settings.SequenceSeparator;

        Rows.CollectionChanged += (_, _) =>
        {
            Notify(nameof(HasNoRows));
            Notify(nameof(HasRows));
            Refresh();
        };

        RemoveSelectedCommand = new RelayCommand(p => RemoveSelected(p as IList), p => p is IList { Count: > 0 });
        ClearCommand = new RelayCommand(_ => Rows.Clear(), _ => Rows.Count > 0);
        MoveUpCommand = new RelayCommand(p => Move(p as FileRow, -1), p => CanMove(p as FileRow, -1));
        MoveDownCommand = new RelayCommand(p => Move(p as FileRow, +1), p => CanMove(p as FileRow, +1));
        ApplyCommand = new RelayCommand(_ => Apply(), _ => CanApply);
        UndoCommand = new RelayCommand(_ => Undo(), _ => _lastResult is { Succeeded.Count: > 0 });

        _loading = false;
        Refresh();
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public ObservableCollection<FileRow> Rows { get; } = [];
    public bool HasNoRows => Rows.Count == 0;
    public bool HasRows => Rows.Count > 0;
    public bool HasError => _errorMessage.Length > 0;
    public bool HasConflictMessage => _conflictMessage.Length > 0;
    public string DateSample => FormatDateSample();
    public string CompanyName => _settings.CompanyName;
    public string UserName => _settings.UserName;
    public string ThemeName => _settings.Theme == AppTheme.Dark ? "다크 모드" : "일반 모드";
    public bool CanApply => _plan != null && RenamePlanner.CanApply(_plan);
    public AppSettings Settings => _settings;

    public ICommand RemoveSelectedCommand { get; }
    public ICommand ClearCommand { get; }
    public ICommand MoveUpCommand { get; }
    public ICommand MoveDownCommand { get; }
    public ICommand ApplyCommand { get; }
    public ICommand UndoCommand { get; }

    public int ModeIndex { get => _modeIndex; set => SetInput(ref _modeIndex, Math.Clamp(value, 0, 3)); }
    public string ReplaceFind { get => _replaceFind; set => SetInput(ref _replaceFind, value); }
    public string ReplaceWith { get => _replaceWith; set => SetInput(ref _replaceWith, value); }
    public bool UseRegex { get => _useRegex; set => SetInput(ref _useRegex, value); }
    public string Template { get => _template; set => SetInput(ref _template, value); }
    public int CaseIndex { get => _caseIndex; set => SetInput(ref _caseIndex, Math.Clamp(value, 0, 3)); }
    public string SeqBaseName { get => _seqBaseName; set => SetInput(ref _seqBaseName, value); }
    public int SeqStart { get => _seqStart; set => SetInput(ref _seqStart, Math.Clamp(value, 0, 999_999_999)); }
    public int SeqDigits { get => _seqDigits; set => SetInput(ref _seqDigits, Math.Clamp(value, 0, 10)); }
    public int SeqSepIndex { get => _seqSepIndex; set => SetInput(ref _seqSepIndex, Math.Clamp(value, 0, 3)); }

    public string ErrorMessage { get => _errorMessage; private set { if (_errorMessage != value) { _errorMessage = value; Notify(); Notify(nameof(HasError)); } } }
    public int ChangedCount { get => _changedCount; private set { if (_changedCount != value) { _changedCount = value; Notify(); } } }
    public int UnchangedCount { get => _unchangedCount; private set { if (_unchangedCount != value) { _unchangedCount = value; Notify(); } } }
    public int ConflictCount { get => _conflictCount; private set { if (_conflictCount != value) { _conflictCount = value; Notify(); } } }
    public string ConflictMessage { get => _conflictMessage; private set { if (_conflictMessage != value) { _conflictMessage = value; Notify(); Notify(nameof(HasConflictMessage)); } } }
    public string ApplyText { get => _applyText; private set { if (_applyText != value) { _applyText = value; Notify(); } } }

    private string FormatDateSample()
    {
        try { return DateTime.Today.ToString(_settings.DateFormat, System.Globalization.CultureInfo.InvariantCulture); }
        catch (FormatException) { return ""; }
    }

    public void AddPaths(IEnumerable<string> dropped)
    {
        var existing = new HashSet<string>(Rows.Select(r => r.Path), StringComparer.OrdinalIgnoreCase);
        foreach (var p in FolderExpander.Expand(dropped))
            if (existing.Add(p)) Rows.Add(new FileRow(p));
    }

    public void MoveRow(int from, int to)
    {
        if (from < 0 || from >= Rows.Count || to < 0 || to >= Rows.Count || from == to) return;
        Rows.Move(from, to);
    }

    public void UpdateSettings(AppSettings updated)
    {
        _settings = _settings with
        {
            DateFormat = updated.DateFormat,
            CompanyName = updated.CompanyName,
            UserName = updated.UserName,
            Theme = updated.Theme,
        };
        App.ApplyTheme(_settings.Theme);
        Notify(nameof(Settings));
        Notify(nameof(DateSample));
        Notify(nameof(CompanyName));
        Notify(nameof(UserName));
        Notify(nameof(ThemeName));
        Refresh();
        SaveSettings(warnOnFailure: true);
    }

    public void SaveSettings() => SaveSettings(warnOnFailure: false);

    private void SaveSettings(bool warnOnFailure)
    {
        _settings = BuildSettings();
        Notify(nameof(Settings));
        try { _store.Save(_settings); }
        catch (Exception ex)
        {
            if (warnOnFailure)
                MessageBox.Show(
                    $"설정을 저장하지 못했습니다: {ex.Message}\n바뀐 설정은 이번 실행에만 적용됩니다.",
                    "설정 저장 실패", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    private AppSettings BuildSettings() => _settings with
    {
        SelectedMode = _modeIndex,
        ReplaceFind = _replaceFind,
        ReplaceWith = _replaceWith,
        UseRegex = _useRegex,
        Template = _template,
        CaseStyle = (CaseStyle)Math.Clamp(_caseIndex, 0, 3),
        SequenceBaseName = _seqBaseName,
        SequenceStart = _seqStart,
        SequenceDigits = _seqDigits,
        SequenceSeparator = (SequenceSeparator)Math.Clamp(_seqSepIndex, 0, 3),
    };

    private IRenameMode CreateMode() => _modeIndex switch
    {
        0 => new ReplaceMode(_replaceFind, _replaceWith, _useRegex),
        1 => new TemplateMode(_template, _settings, DateTime.Today),
        2 => new CaseMode((CaseStyle)Math.Clamp(_caseIndex, 0, 3)),
        _ => new SequenceMode(_seqBaseName, _seqStart, _seqDigits, (SequenceSeparator)Math.Clamp(_seqSepIndex, 0, 3)),
    };

    private void Refresh()
    {
        if (_loading) return;
        try
        {
            _plan = RenamePlanner.Build(Rows.Select(r => r.Path).ToList(), CreateMode(), p => File.Exists(p) || Directory.Exists(p));
            ErrorMessage = "";
            string? firstConflict = null;
            int changed = 0, unchanged = 0, conflicts = 0;
            for (int i = 0; i < Rows.Count; i++)
            {
                var item = _plan[i];
                var row = Rows[i];
                row.Index = i + 1;
                row.NewName = item.NewName;
                (row.Kind, row.StatusText) = item.Status switch
                {
                    PlanStatus.Ok => (RowKind.Changed, "변경"),
                    PlanStatus.Unchanged => (RowKind.Unchanged, "그대로"),
                    PlanStatus.DuplicateInBatch => (RowKind.Conflict, "이름 중복"),
                    PlanStatus.ExistsOnDisk => (RowKind.Conflict, "이미 있는 이름"),
                    _ => (RowKind.Conflict, "잘못된 이름"),
                };
                (row.OldSegments, row.NewSegments) = NameDiff.Compute(row.CurrentName, item.NewName);
                switch (row.Kind)
                {
                    case RowKind.Changed: changed++; break;
                    case RowKind.Unchanged: unchanged++; break;
                    default:
                        conflicts++;
                        firstConflict ??= $"{item.NewName} — " + (item.Status switch
                        {
                            PlanStatus.DuplicateInBatch => "이번에 바꾸는 파일 중에 같은 이름이 있습니다",
                            PlanStatus.ExistsOnDisk => "같은 폴더에 같은 이름의 파일이 있습니다",
                            _ => "사용할 수 없는 이름입니다",
                        });
                        break;
                }
            }
            ChangedCount = changed;
            UnchangedCount = unchanged;
            ConflictCount = conflicts;
            ConflictMessage = firstConflict == null ? "" : firstConflict + (conflicts > 1 ? $" 외 {conflicts - 1}건" : "");
            ApplyText = changed > 0 ? $"{changed}개 이름 바꾸기" : "적용";
        }
        catch (Exception ex)
        {
            _plan = null;
            ErrorMessage = ex is RenameRuleException ? ex.Message : "처리 중 오류: " + ex.Message;
            ChangedCount = UnchangedCount = ConflictCount = 0;
            ConflictMessage = "";
            ApplyText = "적용";
            for (int i = 0; i < Rows.Count; i++)
            {
                var row = Rows[i];
                row.Index = i + 1;
                row.NewName = "";
                row.StatusText = "";
                row.Kind = RowKind.None;
                row.OldSegments = [new DiffSegment(row.CurrentName, DiffKind.Same)];
                row.NewSegments = [];
            }
        }
        Notify(nameof(CanApply));
        CommandManager.InvalidateRequerySuggested();
    }

    private void RemoveSelected(IList? selected)
    {
        if (selected == null) return;
        foreach (var r in selected.Cast<FileRow>().ToList()) Rows.Remove(r);
    }

    private void Move(FileRow? row, int delta)
    {
        if (row == null) return;
        int i = Rows.IndexOf(row);
        MoveRow(i, i + delta);
    }

    private bool CanMove(FileRow? row, int delta)
    {
        if (row == null) return false;
        int index = Rows.IndexOf(row);
        return index >= 0 && index + delta >= 0 && index + delta < Rows.Count;
    }

    private void Apply()
    {
        if (_plan == null || !RenamePlanner.CanApply(_plan)) return;
        var result = RenameExecutor.Execute(_plan, _fs);
        _lastResult = result;
        Remap(result.Succeeded.ToDictionary(s => s.From, s => s.To, StringComparer.OrdinalIgnoreCase));
        ShowFailures("이름 변경", result.Failed);
        Refresh();
        SaveSettings();
    }

    private void Undo()
    {
        if (_lastResult == null) return;
        var undo = RenameExecutor.Undo(_lastResult, _fs);
        Remap(undo.Succeeded.ToDictionary(s => s.From, s => s.To, StringComparer.OrdinalIgnoreCase));
        // 되돌리기에 실패한 항목만 다시 시도할 수 있도록 남긴다.
        var failedPaths = new HashSet<string>(undo.Failed.Select(f => f.Path), StringComparer.OrdinalIgnoreCase);
        var remaining = _lastResult.Succeeded.Where(s => failedPaths.Contains(s.To)).ToList();
        _lastResult = remaining.Count > 0 ? new RenameResult(remaining, []) : null;
        ShowFailures("되돌리기", undo.Failed);
        Refresh();
    }

    private void Remap(Dictionary<string, string> map)
    {
        foreach (var row in Rows)
            if (map.TryGetValue(row.Path, out var to)) row.Path = to;
    }

    private static void ShowFailures(string title, IReadOnlyList<RenameFailure> failed)
    {
        if (failed.Count == 0) return;
        var sb = new StringBuilder();
        sb.AppendLine($"{failed.Count}개 파일을 처리하지 못했습니다.").AppendLine();
        foreach (var f in failed)
            sb.AppendLine($"{System.IO.Path.GetFileName(f.Path)} - {f.Reason}");
        MessageBox.Show(sb.ToString(), $"{title} - 일부 실패", MessageBoxButton.OK, MessageBoxImage.Warning);
    }

    private void SetInput<T>(ref T field, T value, [CallerMemberName] string? name = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value)) return;
        field = value;
        Notify(name);
        Refresh();
    }

    private void Notify([CallerMemberName] string? name = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
