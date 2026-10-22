using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using Microsoft.Win32;

namespace FileRenamer.App;

public partial class MainWindow : Window
{
    private const string RowFormat = "FileRenamer.Row";
    private readonly MainViewModel _vm = new();
    private Point _dragStart;
    private FileRow? _dragRow;

    public MainWindow()
    {
        InitializeComponent();
        DataContext = _vm;
    }

    private void OnAddFiles(object sender, RoutedEventArgs e)
    {
        var dlg = new OpenFileDialog { Multiselect = true, Title = "이름을 바꿀 파일 선택" };
        if (dlg.ShowDialog(this) == true) _vm.AddPaths(dlg.FileNames);
    }

    private void OnSettings(object sender, RoutedEventArgs e)
    {
        var win = new SettingsWindow(_vm.Settings) { Owner = this };
        if (win.ShowDialog() == true) _vm.UpdateSettings(win.Result);
    }

    private void OnClosing(object? sender, System.ComponentModel.CancelEventArgs e) => _vm.SaveSettings();

    private void OnDragOver(object sender, DragEventArgs e)
    {
        if (e.Data.GetDataPresent(DataFormats.FileDrop)) e.Effects = DragDropEffects.Copy;
        else if (e.Data.GetDataPresent(RowFormat)) e.Effects = DragDropEffects.Move;
        else e.Effects = DragDropEffects.None;
        e.Handled = true;
    }

    private void OnDrop(object sender, DragEventArgs e)
    {
        e.Handled = true;
        if (e.Data.GetDataPresent(RowFormat) && e.Data.GetData(RowFormat) is FileRow row)
        {
            var target = FindRow(e.OriginalSource as DependencyObject);
            int to = target != null ? _vm.Rows.IndexOf(target.Item as FileRow ?? row) : _vm.Rows.Count - 1;
            _vm.MoveRow(_vm.Rows.IndexOf(row), to);
        }
        else if (e.Data.GetData(DataFormats.FileDrop) is string[] paths)
        {
            _vm.AddPaths(paths);
        }
    }

    private void OnGridMouseDown(object sender, MouseButtonEventArgs e)
    {
        _dragStart = e.GetPosition(null);
        _dragRow = FindRow(e.OriginalSource as DependencyObject)?.Item as FileRow;
    }

    private void OnGridMouseMove(object sender, MouseEventArgs e)
    {
        if (e.LeftButton != MouseButtonState.Pressed || _dragRow == null) return;
        var pos = e.GetPosition(null);
        if (Math.Abs(pos.X - _dragStart.X) < SystemParameters.MinimumHorizontalDragDistance &&
            Math.Abs(pos.Y - _dragStart.Y) < SystemParameters.MinimumVerticalDragDistance) return;

        var row = _dragRow;
        _dragRow = null;
        DragDrop.DoDragDrop(FileGrid, new DataObject(RowFormat, row), DragDropEffects.Move);
    }

    private static DataGridRow? FindRow(DependencyObject? d)
    {
        while (d != null && d is not DataGridRow)
            d = d is Visual or System.Windows.Media.Media3D.Visual3D ? VisualTreeHelper.GetParent(d) : LogicalTreeHelper.GetParent(d);
        return d as DataGridRow;
    }
}
