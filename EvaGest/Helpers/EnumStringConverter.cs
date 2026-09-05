using System.Globalization;
using System.Windows.Data;

namespace EvaGest.Helpers;

/// <summary>Two-way bridge between an enum property and a ComboBoxItem's string Tag,
/// since SelectedValuePath alone cannot convert types.</summary>
public class EnumStringConverter : IValueConverter
{
    public Type? TipusEnum { get; set; }

    public object Convert(object? value, Type targetType, object parameter, CultureInfo culture)
        => value?.ToString() ?? string.Empty;

    public object? ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => TipusEnum is not null && value is string s ? Enum.Parse(TipusEnum, s) : value;
}
