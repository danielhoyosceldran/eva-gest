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
}
