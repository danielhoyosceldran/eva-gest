using System.Globalization;
using System.Windows.Data;

namespace EvaGest.Helpers;

/// <summary>
/// Horizontal placement of an appointment block inside its day column. This cannot live
/// in the ViewModel: the column width is only known at arrange time, and the ViewModel
/// must not know about ActualWidth. Pass ConverterParameter "Left" or "Width".
/// </summary>
public class LanePositionConverter : IMultiValueConverter
{
    private const double Gap = 2;
    private const double MarginRight = 4;

    public object Convert(object[] values, Type type, object? parameter, CultureInfo culture)
    {
        if (values is not [double width, int lane, int total]) return 0d;
        // The first layout pass reports NaN/0; without this guard every block lands at Left=0.
        if (total <= 0 || double.IsNaN(width) || width <= 0) return 0d;

        double laneWidth = Math.Max(0, width - MarginRight) / total;

        return parameter as string == "Left"
            ? lane * laneWidth
            : Math.Max(0, laneWidth - Gap);
    }

    public object[] ConvertBack(object value, Type[] type, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
