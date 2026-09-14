using System.Globalization;
using EvaGest.Models;

namespace EvaGest.Services;

/// <summary>
/// The language the interface runs in, and the culture that formats its dates and
/// amounts. Set once at startup from <see cref="ConfigKeys.Language"/> and never
/// changed afterwards (RF-23): the setting page says a restart is needed, which keeps
/// every already-built view and every cached string consistent with each other.
///
/// It is a static rather than a service because the pure calculators format money and
/// hours too, and they take no dependencies.
/// </summary>
public static class AppLanguage
{
    public static Language Current { get; private set; } = Language.Catalan;

    /// <summary>Culture used for every number, currency and date the user sees.</summary>
    public static CultureInfo Culture { get; private set; } = CultureInfo.GetCultureInfo("ca-ES");

    public static void Use(Language language)
    {
        Current = language;
        Culture = CultureInfo.GetCultureInfo(language == Language.Spanish ? "es-ES" : "ca-ES");

        // DefaultThread* rather than the current thread: services run on pool threads and
        // must format the same way as the UI thread.
        CultureInfo.DefaultThreadCurrentCulture = Culture;
        CultureInfo.DefaultThreadCurrentUICulture = Culture;
        CultureInfo.CurrentCulture = Culture;
        CultureInfo.CurrentUICulture = Culture;
    }

    /// <summary>Reads the stored value. Anything unknown falls back to Catalan rather
    /// than failing: a settings row is not worth refusing to start over.</summary>
    public static Language Parse(string? stored)
        => Enum.TryParse<Language>(stored, out var language) ? language : Language.Catalan;
}
