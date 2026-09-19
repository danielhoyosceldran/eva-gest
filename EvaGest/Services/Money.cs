using System.Globalization;

namespace EvaGest.Services;

/// <summary>Cents are the storage unit. Euros exist only for display and export.</summary>
public static class Money
{
    /// <summary>Formats cents for the UI: 1500 -> "15,00 €". Always two decimals, so a
    /// round amount still reads as money and columns line up on the separator.</summary>
    public static string Format(long cents)
        => (cents / 100m).ToString("C2", AppLanguage.Culture);

    /// <summary>
    /// Aggregates are summed as <see cref="long"/> (see TillSummary). Taking long here
    /// too is what removes the "(int)Math.Min(x, int.MaxValue)" clamps that used to sit
    /// at every point a period total reached the screen.
    /// </summary>
    public static string FormatExport(long cents)
        => (cents / 100m).ToString("N2", AppLanguage.Culture);

    /// <summary>
    /// Parses user input into cents. Accepts both comma and dot as decimal separator,
    /// and tolerates a group separator: "15", "15,50", "15.50" and "1.234,56" all work.
    /// The last comma or dot is treated as the decimal separator; earlier ones are
    /// group separators. For this domain that is unambiguous, since prices are small.
    /// </summary>
    public static bool TryParse(string? text, out int cents)
    {
        cents = 0;
        if (string.IsNullOrWhiteSpace(text)) return false;

        // Non-breaking spaces come in with anything pasted from a spreadsheet or from a
        // currency-formatted cell, and are invisible in the box.
        text = text.Replace("€", "")
                   .Replace(" ", "").Replace(" ", "").Replace(" ", "")
                   .Trim();
        if (text.Length == 0) return false;

        int posDecimal = text.LastIndexOfAny(['.', ',']);

        string whole = posDecimal < 0 ? text : text[..posDecimal];
        string decimals = posDecimal < 0 ? "" : text[(posDecimal + 1)..];

        if (!decimals.All(char.IsAsciiDigit)) return false;
        if (!IsWholePart(whole)) return false;

        string normalized = $"{whole.Replace(",", "").Replace(".", "")}.{(decimals.Length == 0 ? "0" : decimals)}";

        if (!decimal.TryParse(normalized, NumberStyles.AllowLeadingSign | NumberStyles.AllowDecimalPoint,
                              CultureInfo.InvariantCulture, out decimal euros))
            return false;

        // Rounded first, then range-checked before the cast: decimal-to-int is a
        // checked conversion in C# and throws OverflowException on anything the int
        // range cannot hold, which every "if (!Money.TryParse(...))" guard in the app
        // relies on never happening.
        decimal roundedCents = Math.Round(euros * 100m, MidpointRounding.AwayFromZero);
        if (roundedCents < int.MinValue || roundedCents > int.MaxValue) return false;

        cents = (int)roundedCents;
        return true;
    }

    /// <summary>
    /// The part before the decimal separator: digits, optionally signed, optionally split
    /// into groups of three by a separator used consistently.
    ///
    /// The grouping is checked rather than simply stripped. Stripping accepted anything:
    /// a stuck comma key turning "12,50" into "12,,50" was read as 12,50 and "1,2,3,4"
    /// was read as 123,40 — a number nobody typed, silently frozen onto a sale. A shape
    /// this parser cannot be sure about is now refused so the user retypes it.
    /// </summary>
    private static bool IsWholePart(string whole)
    {
        if (whole.StartsWith('-') || whole.StartsWith('+')) whole = whole[1..];
        if (whole.Length == 0) return true;               // ",50" is a valid way to type 0,50

        int firstSeparator = whole.IndexOfAny(['.', ',']);
        if (firstSeparator < 0) return whole.All(char.IsAsciiDigit);

        char separator = whole[firstSeparator];
        var groups = whole.Split(separator);

        // A second, different separator in the whole part means the shape is not a
        // consistent grouping (e.g. "1.234,567.89").
        if (groups.Any(g => g.IndexOfAny(['.', ',']) >= 0)) return false;

        if (groups[0].Length is < 1 or > 3) return false;
        return groups.All(g => g.All(char.IsAsciiDigit))
            && groups.Skip(1).All(g => g.Length == 3);
    }
}

/// <summary>Formats VAT basis points for display: 2100 -> "21 %", 520 -> "5,2 %".</summary>
public static class Percentages
{
    public static string Format(int bp)
        => (bp / 100m).ToString("0.##", AppLanguage.Culture) + " %";

    /// <summary>Same value without the unit, for a text box the user edits: 2100 -> "21".</summary>
    public static string FormatWithoutUnit(int bp)
        => (bp / 100m).ToString("0.##", AppLanguage.Culture);

    /// <summary>
    /// Parses a typed percentage into basis points. Accepts "21", "21 %", "5,2" and
    /// "5.2". Negative rates and anything above 100 % are rejected rather than stored:
    /// a mistyped rate would be frozen onto every sale taken afterwards.
    /// </summary>
    public static bool TryParse(string? text, out int bp)
    {
        bp = 0;
        if (string.IsNullOrWhiteSpace(text)) return false;

        string net = text.Replace("%", "").Replace(" ", "").Replace(",", ".").Trim();

        if (!decimal.TryParse(net, NumberStyles.Number, CultureInfo.InvariantCulture, out decimal percent))
            return false;
        if (percent < 0 || percent > 100) return false;

        bp = (int)Math.Round(percent * 100m, MidpointRounding.AwayFromZero);
        return true;
    }
}
