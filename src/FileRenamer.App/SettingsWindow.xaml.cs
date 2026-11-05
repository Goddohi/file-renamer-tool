using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using FileRenamer.Core;

namespace FileRenamer.App;

public partial class SettingsWindow : Window
{
    private readonly AppSettings _original;
    private bool _initializing = true;

    public SettingsWindow(AppSettings current)
    {
        InitializeComponent();
        _original = current;
        Result = current;
        DateFormatBox.Text = current.DateFormat;
        CompanyBox.Text = current.CompanyName;
        UserBox.Text = current.UserName;
        (current.Theme == AppTheme.Dark ? DarkThemeRadio : LightThemeRadio).IsChecked = true;
        _initializing = false;
        UpdateSample();
    }

    private void OnSourceInitialized(object? sender, EventArgs e) => App.ApplyTitleBar(this);

    public AppSettings Result { get; private set; }

    private void OnDateFormatChanged(object sender, TextChangedEventArgs e)
    {
        if (!_initializing) UpdateSample();
    }

    private bool UpdateSample()
    {
        try
        {
            SampleText.Text = "오늘 → " + DateTime.Today.ToString(DateFormatBox.Text, CultureInfo.InvariantCulture);
            SampleText.SetResourceReference(TextBlock.ForegroundProperty, "TextBrush");
            OkButton.IsEnabled = true;
            return true;
        }
        catch (FormatException)
        {
            SampleText.Text = "날짜 형식이 올바르지 않습니다.";
            SampleText.SetResourceReference(TextBlock.ForegroundProperty, "BadFgBrush");
            OkButton.IsEnabled = false;
            return false;
        }
    }

    private void OnOk(object sender, RoutedEventArgs e)
    {
        if (!UpdateSample()) return;
        Result = _original with
        {
            DateFormat = DateFormatBox.Text,
            CompanyName = CompanyBox.Text,
            UserName = UserBox.Text,
            Theme = DarkThemeRadio.IsChecked == true ? AppTheme.Dark : AppTheme.Light,
        };
        DialogResult = true;
    }
}
