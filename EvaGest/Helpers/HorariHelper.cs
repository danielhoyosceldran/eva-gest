using System.Globalization;

namespace EvaGest.Helpers;

/// <summary>
/// Parsing and validation of one day's opening hours, kept pure so every rule is
/// testable without a UI. The afternoon shift is optional; a half-filled one is an
/// error rather than a silently ignored field.
/// </summary>
public static class HorariHelper
{
    public record ResultatHorari(List<(TimeOnly inici, TimeOnly fi)> Franges, string? Error)
    {
        public bool EsValid => Error is null;
    }

    // "%H" and not "H": a one-character format string is read as a STANDARD specifier,
    // and TryParseExact throws FormatException instead of returning false.
    private static readonly string[] Formats =
        ["HH:mm", "H:mm", "HHmm", "Hmm", "HH.mm", "H.mm", "HH", "%H"];

    /// <summary>Accepts 9, 9:00, 09:00, 9.00 and 0900 — people type hours in all of these.</summary>
    public static TimeOnly? Analitzar(string? text)
    {
        if (string.IsNullOrWhiteSpace(text)) return null;
        var net = text.Trim();

        foreach (var format in Formats)
            if (TimeOnly.TryParseExact(net, format, CultureInfo.InvariantCulture, DateTimeStyles.None, out var hora))
                return hora;

        return null;
    }

    public static string Format(TimeOnly hora) => hora.ToString("HH\\:mm", CultureInfo.InvariantCulture);

    /// <summary>
    /// Turns the four text fields of one day into the ranges to store. Returns at most
    /// two ranges: a single continuous shift, or morning plus afternoon.
    /// </summary>
    public static ResultatHorari Comprovar(bool obert, string? matiInici, string? matiFi,
        string? tardaInici, string? tardaFi)
    {
        if (!obert) return new ResultatHorari([], null);

        var mi = Analitzar(matiInici);
        var mf = Analitzar(matiFi);
        if (mi is null || mf is null) return Malament("Cal indicar l'hora d'obertura i la de tancament.");
        if (mf <= mi) return Malament("La primera franja ha d'acabar després de començar.");

        bool tardaBuida = string.IsNullOrWhiteSpace(tardaInici) && string.IsNullOrWhiteSpace(tardaFi);
        if (tardaBuida) return new ResultatHorari([(mi.Value, mf.Value)], null);

        var ti = Analitzar(tardaInici);
        var tf = Analitzar(tardaFi);
        if (ti is null || tf is null) return Malament("La segona franja necessita hora d'inici i de fi, o cap de les dues.");
        if (tf <= ti) return Malament("La segona franja ha d'acabar després de començar.");
        if (ti < mf) return Malament("La segona franja no pot començar abans que acabi la primera.");

        return new ResultatHorari([(mi.Value, mf.Value), (ti.Value, tf.Value)], null);
    }

    private static ResultatHorari Malament(string error) => new([], error);
}
