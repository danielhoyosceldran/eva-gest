using System.Globalization;
using System.Text.RegularExpressions;

namespace EvaGest.Services;

/// <summary>
/// Strict dd/mm/yyyy format check for typed dates. Pure and stateless, like
/// <see cref="ContactValidator"/>.
/// </summary>
public static partial class DateValidator
{
    /// <summary>
    /// Requires exactly two digits, a literal "/", two digits, a literal "/", four
    /// digits — "1/1/2024", "01-01-2024" and "01/01/24" are all rejected before the
    /// date is even looked at. What is left is then checked against the real
    /// calendar (day 1-28/29/30/31 depending on month and leap year, month 1-12),
    /// which <see cref="DateOnly.TryParseExact(string, string, IFormatProvider?, DateTimeStyles, out DateOnly)"/>
    /// already does correctly with the "dd/MM/yyyy" pattern.
    /// </summary>
    public static bool IsValidDate(string text, out DateOnly date)
    {
        date = default;
        return text is not null
            && FormatRegex().IsMatch(text)
            && DateOnly.TryParseExact(text, "dd/MM/yyyy", CultureInfo.InvariantCulture,
                DateTimeStyles.None, out date);
    }

    [GeneratedRegex(@"^\d{2}/\d{2}/\d{4}$")]
    private static partial Regex FormatRegex();
}
