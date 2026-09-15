using System.Globalization;
using System.Text.RegularExpressions;

namespace EvaGest.Services;

/// <summary>
/// Strict hh:mm format check for typed times: exactly two digits, a literal ":",
/// two digits — hours 00-23, minutes 00-59. Pure and stateless, like
/// <see cref="ContactValidator"/>.
/// </summary>
public static partial class TimeValidator
{
    public static bool IsValidTime(string text, out TimeOnly time)
    {
        time = default;
        return text is not null
            && FormatRegex().IsMatch(text)
            && TimeOnly.TryParseExact(text, "HH:mm", CultureInfo.InvariantCulture,
                DateTimeStyles.None, out time);
    }

    [GeneratedRegex(@"^\d{2}:\d{2}$")]
    private static partial Regex FormatRegex();
}
