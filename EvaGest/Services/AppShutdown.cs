using System.Windows;

namespace EvaGest.Services;

/// <summary>The real <see cref="IAppShutdown"/>. A UI-side service, like DialogService.</summary>
public class AppShutdown : IAppShutdown
{
    public void Shutdown() => Application.Current.Shutdown();
}
