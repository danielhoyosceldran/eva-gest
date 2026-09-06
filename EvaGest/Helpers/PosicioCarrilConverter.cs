using System.Globalization;
using System.Windows.Data;

namespace EvaGest.Helpers;

/// <summary>
/// Horizontal placement of an appointment block inside its day column. This cannot live
/// in the ViewModel: the column width is only known at arrange time, and the ViewModel
/// must not know about ActualWidth. Pass ConverterParameter "Esquerra" or "Amplada".
/// </summary>
public class PosicioCarrilConverter : IMultiValueConverter
{
    private const double Separacio = 2;
    private const double MargeDreta = 4;

    public object Convert(object[] valors, Type tipus, object? parametre, CultureInfo cultura)
    {
        if (valors is not [double amplada, int carril, int total]) return 0d;
        // The first layout pass reports NaN/0; without this guard every block lands at Left=0.
        if (total <= 0 || double.IsNaN(amplada) || amplada <= 0) return 0d;

        double ampladaCarril = Math.Max(0, amplada - MargeDreta) / total;

        return parametre as string == "Esquerra"
            ? carril * ampladaCarril
            : Math.Max(0, ampladaCarril - Separacio);
    }

    public object[] ConvertBack(object valor, Type[] tipus, object? parametre, CultureInfo cultura)
        => throw new NotSupportedException();
}
