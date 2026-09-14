using System.Globalization;
using System.Windows.Data;
using EvaGest.ViewModels.Pages;

namespace EvaGest.Helpers;

/// <summary>The date pickers only make sense to edit in the "Custom" period (pantalles 2.7).</summary>
public class CustomToEnabledConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object parameter, CultureInfo culture)
        => value is TillPeriod.Custom;

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
