namespace EvaGest.Helpers;

/// <summary>
/// Remembers that the user closed the overdue-appointments notice, and decides when it
/// must come back: once <see cref="Duration"/> has passed with the same appointments
/// still overdue, or straight away if an appointment that was not on the closed notice
/// becomes overdue. Pure state, no clock of its own, so it is testable without timers.
/// </summary>
public class OverdueNoticeSnooze
{
    public static readonly TimeSpan Duration = TimeSpan.FromMinutes(15);

    private DateTime? _dismissedAt;
    private HashSet<int> _dismissedIds = [];

    /// <summary>Records that the notice listing <paramref name="overdueIds"/> was closed at <paramref name="now"/>.</summary>
    public void Dismiss(IEnumerable<int> overdueIds, DateTime now)
    {
        _dismissedAt = now;
        _dismissedIds = [.. overdueIds];
    }

    /// <summary>
    /// True when the notice for <paramref name="overdueIds"/> should be visible at
    /// <paramref name="now"/>. An empty list clears the snooze, so the next overdue
    /// appointment is announced as soon as it appears.
    /// </summary>
    public bool ShouldShow(IReadOnlyCollection<int> overdueIds, DateTime now)
    {
        if (overdueIds.Count == 0)
        {
            _dismissedAt = null;
            _dismissedIds = [];
            return false;
        }

        if (_dismissedAt is not DateTime dismissedAt) return true;
        if (now - dismissedAt >= Duration) return true;
        return overdueIds.Any(id => !_dismissedIds.Contains(id));
    }
}
