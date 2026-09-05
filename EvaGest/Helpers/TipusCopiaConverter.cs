using System.Globalization;
using System.Windows.Data;

namespace EvaGest.Helpers;

public class TipusCopiaConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object parameter, CultureInfo culture)
        => value is true ? "Automàtica" : "Manual";

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
