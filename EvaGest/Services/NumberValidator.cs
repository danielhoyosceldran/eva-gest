using System.Text.RegularExpressions;

namespace EvaGest.Services;

/// <summary>
/// Whole-number checks for typed counts: durations, quantities, how many backups to
/// keep. Pure and stateless, like <see cref="ContactValidator"/>.
/// </summary>
public static partial class NumberValidator
{
    /// <summary>
    /// Digits and nothing else: letters, a sign, a decimal separator or an inner space
    /// are all rejected rather than silently swallowed the way int.Parse would. Outer
    /// whitespace is trimmed, since it is never what the user meant to type.
    /// </summary>
    public static bool TryParseWhole(string? text, out int value)
    {
        value = 0;
        if (text is null) return false;

        string net = text.Trim();
        return DigitsRegex().IsMatch(net) && int.TryParse(net, out value);
    }

    /// <summary>The same check, plus the value having to be at least <paramref name="minimum"/>.
    /// Used wherever zero makes no sense: a duration, a quantity, a number of backups.</summary>
    public static bool TryParseAtLeast(string? text, int minimum, out int value)
        => TryParseWhole(text, out value) && value >= minimum;

    [GeneratedRegex(@"^\d{1,9}$")]
    private static partial Regex DigitsRegex();
}
