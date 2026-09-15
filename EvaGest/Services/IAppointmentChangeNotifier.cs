namespace EvaGest.Services;

/// <summary>
/// Fired by <see cref="AppointmentService"/> after an update, status change or delete
/// that actually persisted. Registered as a singleton so it can bridge across the
/// separate (transient) <see cref="IAppointmentService"/> instances each ViewModel
/// gets: <see cref="ViewModels.MainWindowViewModel"/> subscribes once to re-run its
/// overdue check right away instead of waiting for the next timer tick, no matter
/// which page made the change.
/// </summary>
public interface IAppointmentChangeNotifier
{
    event Action? Changed;

    void NotifyChanged();
}

public class AppointmentChangeNotifier : IAppointmentChangeNotifier
{
    public event Action? Changed;

    public void NotifyChanged() => Changed?.Invoke();
}
