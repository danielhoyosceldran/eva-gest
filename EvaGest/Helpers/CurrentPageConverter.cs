using System.Globalization;
using System.Windows.Data;

namespace EvaGest.Helpers;

/// <summary>
/// True when the shell's current page is of the type named by the parameter
/// (e.g. "HomeViewModel"). Keeps a sidebar RadioButton checked for the page on screen
/// even when the page changes without a click — closing owner mode sends a private page
/// back to Home, and the Home button has to light up with it.
/// </summary>
public class CurrentPageConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value is not null && value.GetType().Name == parameter as string;

    /// <summary>One-way: a click navigates through the button's command, not through
    /// this binding.</summary>
    public object ConvertBack(object value, Type targetType, object? parameter, CultureInfo culture)
        => Binding.DoNothing;
}
