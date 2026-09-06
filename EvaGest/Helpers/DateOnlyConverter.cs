using System.Globalization;
using System.Windows.Data;

namespace EvaGest.Helpers;

/// <summary>
/// DatePicker.SelectedDate is a DateTime?, the domain uses DateOnly. Without this the
/// binding silently fails in both directions: the picker shows empty and never writes back.
/// </summary>
public class DateOnlyConverter : IValueConverter
{
    public object? Convert(object? valor, Type tipus, object? parametre, CultureInfo cultura)
        => valor switch
        {
            DateOnly data => data.ToDateTime(TimeOnly.MinValue),
            DateTime moment => moment.Date,
            _ => null
        };

    /// <summary>Clearing the picker leaves the appointment's date untouched: an appointment
    /// with no date makes no sense, so DoNothing beats writing a null into a DateOnly.</summary>
    public object ConvertBack(object? valor, Type tipus, object? parametre, CultureInfo cultura)
        => valor is DateTime moment ? DateOnly.FromDateTime(moment) : Binding.DoNothing;
}
