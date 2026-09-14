using System.Globalization;
using System.Windows.Data;

namespace EvaGest.Helpers;

/// <summary>
/// DatePicker.SelectedDate is a DateTime?, the domain uses DateOnly. Without this the
/// binding silently fails in both directions: the picker shows empty and never writes back.
/// </summary>
public class DateOnlyConverter : IValueConverter
{
    public object? Convert(object? value, Type type, object? parameter, CultureInfo culture)
        => value switch
        {
            DateOnly date => date.ToDateTime(TimeOnly.MinValue),
            DateTime moment => moment.Date,
            _ => null
        };

    /// <summary>
    /// Clearing the picker means "no filter" when the target is a DateOnly?, and means
    /// nothing at all when it is a plain DateOnly: an appointment with no date makes no
    /// sense, so there DoNothing beats writing a null the property cannot hold.
    /// </summary>
    public object? ConvertBack(object? value, Type type, object? parameter, CultureInfo culture)
    {
        if (value is DateTime moment) return DateOnly.FromDateTime(moment);
        return Nullable.GetUnderlyingType(type) is not null ? null : Binding.DoNothing;
    }
}
