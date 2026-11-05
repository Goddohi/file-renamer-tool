using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace FileRenamer.App;

/// <summary>
/// int 값이 ConverterParameter와 같은지. bool 대상이면 true/false, Visibility 대상이면 Visible/Collapsed.
/// 라디오 버튼 묶음을 인덱스 하나에 양방향 바인딩할 때 쓴다.
/// </summary>
public sealed class IndexConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        bool match = value is int i && i == ToInt(parameter);
        return targetType == typeof(Visibility) ? (match ? Visibility.Visible : Visibility.Collapsed) : match;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
        value is true ? ToInt(parameter) : Binding.DoNothing;

    private static int ToInt(object parameter) => System.Convert.ToInt32(parameter, CultureInfo.InvariantCulture);
}
