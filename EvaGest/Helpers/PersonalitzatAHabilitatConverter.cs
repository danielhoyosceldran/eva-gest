using System.Globalization;
using System.Windows.Data;
using EvaGest.ViewModels.Pagines;

namespace EvaGest.Helpers;

/// <summary>The date pickers only make sense to edit in the "Personalitzat" period (pantalles 2.7).</summary>
public class PersonalitzatAHabilitatConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object parameter, CultureInfo culture)
        => value is PeriodeCaixa.Personalitzat;

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
