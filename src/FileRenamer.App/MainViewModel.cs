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
    private string _summary = "";

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
            Refresh();
        };

        RemoveSelectedCommand = new RelayCommand(p => RemoveSelected(p as IList), p => p is IList { Count: > 0 });
        ClearCommand = new RelayCommand(_ => Rows.Clear(), _ => Rows.Count > 0);
        MoveUpCommand = new RelayCommand(p => Move(p as FileRow, -1), p => p is FileRow);
        MoveDownCommand = new RelayCommand(p => Move(p as FileRow, +1), p => p is FileRow);
        ApplyCommand = new RelayCommand(_ => Apply(), _ => CanApply);
        UndoCommand = new RelayCommand(_ => Undo(), _ => _lastResult is { Succeeded.Count: > 0 });

        _loading = false;
        Refresh();
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public ObservableCollection<FileRow> Rows { get; } = [];
    public bool HasNoRows => Rows.Count == 0;
    public bool CanApply => _plan != null && RenamePlanner.CanApply(_plan);
    public AppSettings Settings => _settings;

    public ICommand RemoveSelectedCommand { get; }
    public ICommand ClearCommand { get; }
    public ICommand MoveUpCommand { get; }
    public ICommand MoveDownCommand { get; }
    public ICommand ApplyCommand { get; }
    public ICommand UndoCommand { get; }

    public int ModeIndex { get => _modeIndex; set => SetInput(ref _modeIndex, value); }
    public string ReplaceFind { get => _replaceFind; set => SetInput(ref _replaceFind, value); }
    public string ReplaceWith { get => _replaceWith; set => SetInput(ref _replaceWith, value); }
    public bool UseRegex { get => _useRegex; set => SetInput(ref _useRegex, value); }
    public string Template { get => _template; set => SetInput(ref _template, value); }
    public int CaseIndex { get => _caseIndex; set => SetInput(ref _caseIndex, value); }
    public string SeqBaseName { get => _seqBaseName; set => SetInput(ref _seqBaseName, value); }
    public int SeqStart { get => _seqStart; set => SetInput(ref _seqStart, Math.Clamp(value, 0, 999_999_999)); }
    public int SeqDigits { get => _seqDigits; set => SetInput(ref _seqDigits, Math.Clamp(value, 0, 10)); }
    public int SeqSepIndex { get => _seqSepIndex; set => SetInput(ref _seqSepIndex, value); }

    public string ErrorMessage { get => _errorMessage; private set { if (_errorMessage != value) { _errorMessage = value; Notify(); } } }
    public string Summary { get => _summary; private set { if (_summary != value) { _summary = value; Notify(); } } }

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
        };
        Refresh();
        SaveSettings();
    }

    public void SaveSettings()
    {
        _settings = BuildSettings();
        try { _store.Save(_settings); }
        catch (Exception) { /* 설정 저장 실패는 무시 */ }
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
            for (int i = 0; i < Rows.Count; i++)
            {
                var item = _plan[i];
                var row = Rows[i];
                row.NewName = item.NewName;
                row.IsConflict = item.Status is PlanStatus.DuplicateInBatch or PlanStatus.ExistsOnDisk or PlanStatus.InvalidName;
                row.StatusText = item.Status switch
                {
                    PlanStatus.Ok => "변경",
                    PlanStatus.Unchanged => "변경 없음",
                    PlanStatus.DuplicateInBatch => "이름 중복",
                    PlanStatus.ExistsOnDisk => "이미 존재하는 파일",
                    _ => "잘못된 이름",
                };
            }
            int ok = _plan.Count(p => p.Status == PlanStatus.Ok);
            int bad = _plan.Count(p => p.Status is PlanStatus.DuplicateInBatch or PlanStatus.ExistsOnDisk or PlanStatus.InvalidName);
            Summary = Rows.Count == 0 ? "" : $"전체 {Rows.Count}개 / 변경 {ok}개" + (bad > 0 ? $" / 충돌 {bad}개" : "");
        }
        catch (Exception ex)
        {
            _plan = null;
            ErrorMessage = ex is RenameRuleException ? ex.Message : "처리 중 오류: " + ex.Message;
            Summary = "";
            foreach (var row in Rows)
            {
                row.NewName = "";
                row.StatusText = "";
                row.IsConflict = false;
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
