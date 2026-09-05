using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace EvaGest.Helpers;

/// <summary>Collapses a control when the bound value is null or an empty string.
/// Used for inline validation messages that should not reserve space when empty.</summary>
public class NullToVisibilityConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object parameter, CultureInfo culture)
        => string.IsNullOrEmpty(value as string) ? Visibility.Collapsed : Visibility.Visible;

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
