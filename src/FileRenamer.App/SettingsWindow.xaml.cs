using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using FileRenamer.Core;

namespace FileRenamer.App;

public partial class SettingsWindow : Window
{
    private readonly AppSettings _original;

    public SettingsWindow(AppSettings current)
    {
        InitializeComponent();
        _original = current;
        Result = current;
        DateFormatBox.Text = current.DateFormat;
        CompanyBox.Text = current.CompanyName;
        UserBox.Text = current.UserName;
        UpdateSample();
    }

    public AppSettings Result { get; private set; }

    private void OnDateFormatChanged(object sender, TextChangedEventArgs e) => UpdateSample();

    private bool UpdateSample()
    {
        try
        {
            SampleText.Text = "미리보기: " + DateTime.Today.ToString(DateFormatBox.Text, CultureInfo.InvariantCulture);
            SampleText.Foreground = System.Windows.Media.Brushes.Black;
            OkButton.IsEnabled = true;
            return true;
        }
        catch (FormatException)
        {
            SampleText.Text = "날짜 형식이 올바르지 않습니다.";
            SampleText.Foreground = System.Windows.Media.Brushes.Firebrick;
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
        };
        DialogResult = true;
    }
}
