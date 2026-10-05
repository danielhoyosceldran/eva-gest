using System.Windows;

namespace EvaGest.Services;

/// <summary>The real <see cref="IAppShutdown"/>. A UI-side service, like DialogService.</summary>
public class AppShutdown : IAppShutdown
{
    /// <summary>
    /// Set once the app has asked itself to close (after a restore). The main window
    /// reads it to skip the backup it takes on closing: Application.Shutdown ignores a
    /// cancelled Closing, so that copy could not be awaited, and it would copy a database
    /// the user has only just restored.
    /// </summary>
    public static bool Requested { get; private set; }

    public void Shutdown()
    {
        Requested = true;
        Application.Current.Shutdown();
    }
}
