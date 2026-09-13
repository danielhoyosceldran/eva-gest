using System.Globalization;
using System.Windows;
using System.Windows.Data;
using EvaGest.Models;

namespace EvaGest.Helpers;

/// <summary>Status action buttons only make sense while a cita is still Pendent (pantalles 2.1).</summary>
public class PendentAVisibilitatConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object parameter, CultureInfo culture)
        => value is EstatCita.Pendent ? Visibility.Visible : Visibility.Collapsed;

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotSupportedException();
}

/// <summary>The reset action only makes sense once a cita has left Pendent.</summary>
public class PendentAVisibilitatInversaConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object parameter, CultureInfo culture)
        => value is EstatCita.Pendent ? Visibility.Collapsed : Visibility.Visible;

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
