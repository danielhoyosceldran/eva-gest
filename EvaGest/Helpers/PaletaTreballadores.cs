namespace EvaGest.Helpers;

/// <summary>
/// The six worker colours from disseny-ui.md section 5. None of them is blue: blue is
/// the application's accent, so a blue dot next to a name would read as something
/// clickable rather than as an identity. They also avoid the status hues (green =
/// realitzada, red = cancel·lada, amber = pendent).
/// </summary>
public static class PaletaTreballadores
{
    public record ColorTreballadora(string Nom, string Hex);

    public static readonly IReadOnlyList<ColorTreballadora> Colors =
    [
        new("Verd blau", "#0F766E"),
        new("Magenta",   "#A21CAF"),
        new("Prunya",    "#7E3F8F"),
        new("Oliva",     "#4D7C0F"),
        new("Ocre",      "#92400E"),
        new("Grafit",    "#374151"),
    ];

    /// <summary>
    /// Suggests the first colour nobody is using yet, so two workers do not end up with
    /// the same dot by default. Once all six are taken it cycles from the start again —
    /// a duplicate is better than refusing to create the worker.
    /// </summary>
    public static string ColorLliure(IEnumerable<string> jaUsats)
    {
        var usats = jaUsats
            .Where(c => !string.IsNullOrWhiteSpace(c))
            .Select(c => c.Trim())
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var lliure = Colors.FirstOrDefault(c => !usats.Contains(c.Hex));
        return lliure?.Hex ?? Colors[usats.Count % Colors.Count].Hex;
    }

    public static string NomDe(string? hex)
        => Colors.FirstOrDefault(c => string.Equals(c.Hex, hex?.Trim(), StringComparison.OrdinalIgnoreCase))?.Nom
           ?? "Personalitzat";
}
