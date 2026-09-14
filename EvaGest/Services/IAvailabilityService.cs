using EvaGest.Models;

namespace EvaGest.Services;

public record AvailabilityResult(
    bool HasOverlap,
    bool OutsideSchedule,
    bool ClosedDay,
    string? ClosedDayReason,
    int AvailableWorkers,
    int ExistingAppointments);

public interface IAvailabilityService
{
    /// <summary>
    /// Checks an appointment slot. Never blocks: it only reports what the UI should warn about.
    /// Pass excludedAppointmentId when editing, so the appointment does not clash with itself.
    /// </summary>
    Task<AvailabilityResult> Check(
        DateOnly date, TimeOnly time, int durationMin,
        int? workerId, int? excludedAppointmentId = null);

    /// <summary>Active workers whose weekly schedule covers this slot.</summary>
    Task<List<Worker>> AvailableWorkers(DateOnly date, TimeOnly time, int durationMin);

    Task<bool> IsDayOpen(DateOnly date);
    Task<List<(TimeOnly start, TimeOnly fi)>> OpeningIntervals(DateOnly date);

    /// <summary>All weekly opening ranges in a single query, so the weekly grid does
    /// not issue one round trip per day. Days with no schedule are absent.</summary>
    Task<Dictionary<Weekday, List<(TimeOnly start, TimeOnly fi)>>> WeeklyIntervals();

    /// <summary>
    /// Replaces the whole weekly opening schedule in one go. The settings screen edits
    /// all seven days as a single form, so a partial update would leave the week in a
    /// state the user never asked for. Days absent from the dictionary are closed.
    /// </summary>
    Task SaveWeeklySchedule(IReadOnlyDictionary<Weekday, List<(TimeOnly start, TimeOnly fi)>> schedule);

    /// <summary>Closed dates within a range, with their reason. Used by the weekly
    /// agenda to dim whole columns without one query per day.</summary>
    Task<Dictionary<DateOnly, string?>> ClosedDaysIn(DateOnly from, DateOnly to);

    /// <summary>Every closed date on record, soonest first. The settings screen lists
    /// them all rather than only the future ones: last year's holidays are what the
    /// user copies from when filling in this year's.</summary>
    Task<List<ClosedDay>> ClosedDays();

    /// <summary>Marks a date as closed. Setting the same date twice updates its reason
    /// instead of failing on the unique index.</summary>
    Task AddClosedDay(DateOnly date, string? reason);

    Task DeleteClosedDay(int id);
}
