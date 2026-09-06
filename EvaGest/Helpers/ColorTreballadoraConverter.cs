using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;

namespace EvaGest.Helpers;

/// <summary>
/// Turns a worker's hex colour into a brush. Brushes are cached and frozen: a fresh
/// SolidColorBrush per appointment per week reload is real churn on the render thread.
/// An unknown or empty colour falls back to the neutral grey, never to a random hue.
/// </summary>
public class ColorTreballadoraConverter : IValueConverter
{
    private static readonly Dictionary<string, SolidColorBrush> Cache = [];
    private static readonly SolidColorBrush Recanvi = Congelar(new SolidColorBrush(Color.FromRgb(0x45, 0x51, 0x60)));

    /// <summary>ConverterParameter "Fons" returns the same hue mixed over white, for the
    /// block fill; without it the full-strength colour is returned, for the 4px side bar.</summary>
    public object Convert(object? valor, Type tipus, object? parametre, CultureInfo cultura)
    {
        bool fons = parametre as string == "Fons";
        if (valor is not string hex || string.IsNullOrWhiteSpace(hex))
            return fons ? Barrejar(Recanvi.Color) : Recanvi;

        string clau = fons ? $"{hex}|fons" : hex;
        lock (Cache)
        {
            if (Cache.TryGetValue(clau, out var guardat)) return guardat;

            SolidColorBrush pinzell;
            try
            {
                var color = (Color)ColorConverter.ConvertFromString(hex);
                pinzell = fons ? Barrejar(color) : Congelar(new SolidColorBrush(color));
            }
            catch (FormatException) { pinzell = fons ? Barrejar(Recanvi.Color) : Recanvi; }

            Cache[clau] = pinzell;
            return pinzell;
        }
    }

    public object ConvertBack(object? valor, Type tipus, object? parametre, CultureInfo cultura)
        => throw new NotSupportedException();

    /// <summary>12% of the hue over white: enough to tell workers apart, light enough
    /// to keep dark text on top readable.</summary>
    private static SolidColorBrush Barrejar(Color color)
    {
        const double proporcio = 0.12;
        byte Mescla(byte canal) => (byte)(canal * proporcio + 255 * (1 - proporcio));
        return Congelar(new SolidColorBrush(Color.FromRgb(Mescla(color.R), Mescla(color.G), Mescla(color.B))));
    }

    private static SolidColorBrush Congelar(SolidColorBrush pinzell)
    {
        pinzell.Freeze();
        return pinzell;
    }
}
