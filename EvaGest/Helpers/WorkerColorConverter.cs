using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;

namespace EvaGest.Helpers;

/// <summary>
/// Turns a worker's hex colour into a brush. Brushes are cached and frozen: a fresh
/// SolidColorBrush per appointment per week reload is real churn on the render thread.
/// An unknown or empty colour falls back to the neutral grey, never to a random hue.
/// </summary>
public class WorkerColorConverter : IValueConverter
{
    private static readonly Dictionary<string, SolidColorBrush> Cache = [];
    private static readonly SolidColorBrush Fallback = Freeze(new SolidColorBrush(Color.FromRgb(0x45, 0x51, 0x60)));

    /// <summary>ConverterParameter "Background" returns the same hue mixed over white, for the
    /// block fill; without it the full-strength colour is returned, for the 4px side bar.</summary>
    public object Convert(object? value, Type type, object? parameter, CultureInfo culture)
    {
        bool background = parameter as string == "Background";
        if (value is not string hex || string.IsNullOrWhiteSpace(hex))
            return background ? Soften(Fallback.Color) : Fallback;

        string key = background ? $"{hex}|fons" : hex;
        lock (Cache)
        {
            if (Cache.TryGetValue(key, out var saved)) return saved;

            SolidColorBrush brush;
            try
            {
                var color = (Color)ColorConverter.ConvertFromString(hex);
                brush = background ? Soften(color) : Freeze(new SolidColorBrush(color));
            }
            catch (FormatException) { brush = background ? Soften(Fallback.Color) : Fallback; }

            Cache[key] = brush;
            return brush;
        }
    }

    public object ConvertBack(object? value, Type type, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();

    /// <summary>12% of the hue over white: enough to tell workers apart, light enough
    /// to keep dark text on top readable.</summary>
    private static SolidColorBrush Soften(Color color)
    {
        const double ratio = 0.12;
        byte Blend(byte channel) => (byte)(channel * ratio + 255 * (1 - ratio));
        return Freeze(new SolidColorBrush(Color.FromRgb(Blend(color.R), Blend(color.G), Blend(color.B))));
    }

    private static SolidColorBrush Freeze(SolidColorBrush brush)
    {
        brush.Freeze();
        return brush;
    }
}
