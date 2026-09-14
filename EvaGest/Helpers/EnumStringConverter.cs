using System.Globalization;
using System.Windows.Data;

namespace EvaGest.Helpers;

/// <summary>Two-way bridge between an enum property and a ComboBoxItem's string Tag,
/// since SelectedValuePath alone cannot convert types.</summary>
public class EnumStringConverter : IValueConverter
{
    public Type? EnumType { get; set; }

    public object Convert(object? value, Type targetType, object parameter, CultureInfo culture)
        => value?.ToString() ?? string.Empty;

    public object? ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => EnumType is not null && value is string s ? Enum.Parse(EnumType, s) : value;
}
