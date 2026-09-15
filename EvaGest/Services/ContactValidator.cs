using System.Text.RegularExpressions;

namespace EvaGest.Services;

/// <summary>
/// Format checks for phone numbers and emails typed by the user. Pure and
/// stateless, like <see cref="VatCalculator"/> and <see cref="Money"/>.
/// </summary>
public static partial class ContactValidator
{
    /// <summary>
    /// A phone is 9 digits once spaces are ignored: "111 111 111", "11 11 111 11"
    /// and "111111111" are all valid, and equivalent. Any other character
    /// (letters, dashes, a "+" prefix) makes it invalid.
    /// </summary>
    public static bool IsValidPhone(string phone)
    {
        string digits = new(phone.Where(c => !char.IsWhiteSpace(c)).ToArray());
        return digits.Length == 9 && digits.All(char.IsAsciiDigit);
    }

    /// <summary>Requires an "@" and at least one "." after it, with something on
    /// both sides of each — good enough to catch typos without rejecting real
    /// addresses the way a stricter RFC 5322 regex would.</summary>
    public static bool IsValidEmail(string email) => EmailRegex().IsMatch(email);

    [GeneratedRegex(@"^[^\s@]+@[^\s@]+\.[^\s@]+$")]
    private static partial Regex EmailRegex();
}
