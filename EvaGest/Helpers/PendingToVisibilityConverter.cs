using System.Globalization;
using System.Windows;
using System.Windows.Data;
using EvaGest.Models;

namespace EvaGest.Helpers;

/// <summary>Status action buttons only make sense while an appointment is still Pending (pantalles 2.1).</summary>
public class PendingToVisibilityConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object parameter, CultureInfo culture)
        => value is AppointmentStatus.Pending ? Visibility.Visible : Visibility.Collapsed;

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotSupportedException();
}

/// <summary>The reset action only makes sense once an appointment has left Pending.</summary>
public class PendingToVisibilityInverseConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object parameter, CultureInfo culture)
        => value is AppointmentStatus.Pending ? Visibility.Collapsed : Visibility.Visible;

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
