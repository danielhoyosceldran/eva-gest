using System.Globalization;
using System.Windows.Data;
using EvaGest.Models;
using EvaGest.Services;

namespace EvaGest.Helpers;

/// <summary>
/// Shows an enum with the wording from <see cref="Labels"/> rather than its stored
/// identifier, so a picker never displays "NotIncluded" to the user.
/// </summary>
public class LabelConverter : IValueConverter
{
    public object Convert(object? value, Type type, object? parameter, CultureInfo culture) => value switch
    {
        VatMode mode => Labels.Text(mode),
        AppointmentStatus status => Labels.Text(status),
        SaleStatus status => Labels.Text(status),
        MovementType movement => Labels.Text(movement),
        LineType line => Labels.Text(line),
        Weekday day => Labels.Text(day),
        Language language => Labels.Text(language),
        _ => value?.ToString() ?? string.Empty
    };

    public object ConvertBack(object? value, Type type, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
