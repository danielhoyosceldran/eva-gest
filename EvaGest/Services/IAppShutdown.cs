namespace EvaGest.Services;

/// <summary>
/// Lets a ViewModel close the application without touching <c>Application.Current</c>,
/// which ViewModels must not see (they are built off the UI thread in the tests).
/// </summary>
public interface IAppShutdown
{
    /// <summary>Closes the app the normal way: OnExit runs and the log is flushed.</summary>
    void Shutdown();
}
