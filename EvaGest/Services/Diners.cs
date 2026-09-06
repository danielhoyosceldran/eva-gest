using System.Globalization;

namespace EvaGest.Services;

/// <summary>Cents are the storage unit. Euros exist only for display and export.</summary>
public static class Diners
{
    private static readonly CultureInfo Cultura = new("ca-ES");

    /// <summary>Formats cents for the UI: 1500 -> "15,00 €".</summary>
    public static string Format(int cents)
        => (cents / 100m).ToString("C2", Cultura);

    /// <summary>Formats cents for CSV export without the currency symbol: 1500 -> "15,00".</summary>
    public static string FormatExport(int cents)
        => (cents / 100m).ToString("N2", Cultura);

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

        string normalitzat;
        if (posDecimal < 0)
        {
            normalitzat = text;
        }
        else
        {
            string entera = text[..posDecimal].Replace(",", "").Replace(".", "");
            string decimals = text[(posDecimal + 1)..];
            normalitzat = $"{entera}.{decimals}";
        }

        if (!decimal.TryParse(normalitzat, NumberStyles.Number,
                              CultureInfo.InvariantCulture, out decimal euros))
            return false;

        cents = (int)Math.Round(euros * 100m, MidpointRounding.AwayFromZero);
        return true;
    }
}

/// <summary>Formats VAT basis points for display: 2100 -> "21 %", 520 -> "5,2 %".</summary>
public static class Percentatges
{
    public static string Format(int bp)
        => (bp / 100m).ToString("0.##", new CultureInfo("ca-ES")) + " %";

    /// <summary>Same value without the unit, for a text box the user edits: 2100 -> "21".</summary>
    public static string FormatSenseUnitat(int bp)
        => (bp / 100m).ToString("0.##", new CultureInfo("ca-ES"));

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
