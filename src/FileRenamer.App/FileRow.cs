using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace FileRenamer.App;

public sealed class FileRow : INotifyPropertyChanged
{
    private string _path;
    private string _newName = "";
    private string _statusText = "";
    private bool _isConflict;

    public FileRow(string path) => _path = path;

    public event PropertyChangedEventHandler? PropertyChanged;

    public string Path
    {
        get => _path;
        set
        {
            if (_path == value) return;
            _path = value;
            Notify();
            Notify(nameof(CurrentName));
        }
    }

    public string CurrentName => System.IO.Path.GetFileName(_path);

    public string NewName { get => _newName; set => Set(ref _newName, value); }
    public string StatusText { get => _statusText; set => Set(ref _statusText, value); }
    public bool IsConflict { get => _isConflict; set => Set(ref _isConflict, value); }

    private void Set<T>(ref T field, T value, [CallerMemberName] string? name = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value)) return;
        field = value;
        Notify(name);
    }

    private void Notify([CallerMemberName] string? name = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
