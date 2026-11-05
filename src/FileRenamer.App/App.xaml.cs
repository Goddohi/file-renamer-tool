using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using FileRenamer.Core;

namespace FileRenamer.App;

public partial class App : Application
{
    private const int DwmUseImmersiveDarkMode = 20;

    public static AppTheme CurrentTheme { get; private set; } = AppTheme.Light;

    protected override void OnStartup(StartupEventArgs e)
    {
        ApplyTheme(new SettingsStore(SettingsStore.DefaultPath).Load().Theme);
        base.OnStartup(e);
    }

    public static void ApplyTheme(AppTheme theme)
    {
        CurrentTheme = theme;
        var dictionaries = Current.Resources.MergedDictionaries;
        dictionaries[0] = new ResourceDictionary
        {
            Source = new Uri($"/FileRenamer.App;component/Themes/{(theme == AppTheme.Dark ? "Dark" : "Light")}.xaml", UriKind.Relative),
        };
        foreach (Window w in Current.Windows) ApplyTitleBar(w);
    }

    /// <summary>Windows 10 20H1+ / 11에서 제목 표시줄도 테마에 맞춘다. 지원하지 않으면 무시된다.</summary>
    public static void ApplyTitleBar(Window window)
    {
        var hwnd = new WindowInteropHelper(window).Handle;
        if (hwnd == IntPtr.Zero) return;
        int dark = CurrentTheme == AppTheme.Dark ? 1 : 0;
        _ = DwmSetWindowAttribute(hwnd, DwmUseImmersiveDarkMode, ref dark, sizeof(int));
    }

    [DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attribute, ref int value, int size);
}
