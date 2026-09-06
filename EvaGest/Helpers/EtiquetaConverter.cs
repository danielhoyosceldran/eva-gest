using System.Globalization;
using System.Windows.Data;
using EvaGest.Models;
using EvaGest.Services;

namespace EvaGest.Helpers;

/// <summary>
/// Shows an enum with the wording from <see cref="Etiquetes"/> rather than its stored
/// identifier, so a picker never displays "NoInclos" to the user.
/// </summary>
public class EtiquetaConverter : IValueConverter
{
    public object Convert(object? valor, Type tipus, object? parametre, CultureInfo cultura) => valor switch
    {
        IvaMode mode => Etiquetes.Text(mode),
        EstatCita estat => Etiquetes.Text(estat),
        EstatVenda estat => Etiquetes.Text(estat),
        TipusMoviment moviment => Etiquetes.Text(moviment),
        TipusLinia linia => Etiquetes.Text(linia),
        DiaSetmana dia => Etiquetes.Text(dia),
        _ => valor?.ToString() ?? string.Empty
    };

    public object ConvertBack(object? valor, Type tipus, object? parametre, CultureInfo cultura)
        => throw new NotSupportedException();
}
