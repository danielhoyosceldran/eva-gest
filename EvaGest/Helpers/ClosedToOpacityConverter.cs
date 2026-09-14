using System.Globalization;
using System.Windows.Data;

namespace EvaGest.Helpers;

/// <summary>Dims a closed day's column without hiding it: appointments can still
/// be created on a closed day (pantalles 2.2).</summary>
public class ClosedToOpacityConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object parameter, CultureInfo culture)
        => value is true ? 0.5 : 1.0;

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
