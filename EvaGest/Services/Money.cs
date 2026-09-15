using System.Globalization;

namespace EvaGest.Services;

/// <summary>Cents are the storage unit. Euros exist only for display and export.</summary>
public static class Money
{
    /// <summary>Formats cents for the UI: 1500 -> "15,00 €".</summary>
    public static string Format(int cents)
        => (cents / 100m).ToString("C2", AppLanguage.Culture);

    /// <summary>Formats cents for CSV export without the currency symbol: 1500 -> "15,00".</summary>
    public static string FormatExport(int cents)
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

        text = text.Replace("€", "").Replace(" ", "").Trim();

        int posDecimal = text.LastIndexOfAny(['.', ',']);

        string normalized;
        if (posDecimal < 0)
        {
            normalized = text;
        }
        else
        {
            string whole = text[..posDecimal].Replace(",", "").Replace(".", "");
            string decimals = text[(posDecimal + 1)..];
            normalized = $"{whole}.{decimals}";
        }

        if (!decimal.TryParse(normalized, NumberStyles.Number,
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
