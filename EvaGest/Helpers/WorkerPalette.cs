using EvaGest.Resources;

namespace EvaGest.Helpers;

/// <summary>
/// The six worker colours from disseny-ui.md section 5. None of them is blue: blue is
/// the application's accent, so a blue dot next to a name would read as something
/// clickable rather than as an identity. They also avoid the status hues (green =
/// completed, red = cancelled, amber = pending).
/// </summary>
public static class WorkerPalette
{
    /// <summary>The name is looked up when read, so the picker shows it in whatever
    /// language the session runs in.</summary>
    public record WorkerColor(string NameKey, string Hex)
    {
        public string Name => Texts.Get(NameKey);
    }

    public static readonly IReadOnlyList<WorkerColor> Colors =
    [
        new(nameof(Texts.ColorTeal),    "#0F766E"),
        new(nameof(Texts.ColorMagenta), "#A21CAF"),
        new(nameof(Texts.ColorPlum),    "#7E3F8F"),
        new(nameof(Texts.ColorOlive),   "#4D7C0F"),
        new(nameof(Texts.ColorOchre),   "#92400E"),
        new(nameof(Texts.ColorGraphite), "#374151"),
    ];

    /// <summary>
    /// Suggests the first colour nobody is using yet, so two workers do not end up with
    /// the same dot by default. Once all six are taken it cycles from the start again —
    /// a duplicate is better than refusing to create the worker.
    /// </summary>
    public static string FreeColor(IEnumerable<string> alreadyUsed)
    {
        var used = alreadyUsed
            .Where(c => !string.IsNullOrWhiteSpace(c))
            .Select(c => c.Trim())
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var free = Colors.FirstOrDefault(c => !used.Contains(c.Hex));
        return free?.Hex ?? Colors[used.Count % Colors.Count].Hex;
    }

    public static string ColorName(string? hex)
        => Colors.FirstOrDefault(c => string.Equals(c.Hex, hex?.Trim(), StringComparison.OrdinalIgnoreCase))?.Name
           ?? Texts.Custom;
}
