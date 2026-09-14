using EvaGest.Models;

namespace EvaGest.Services;

/// <summary>
/// Workers and their weekly schedules (RF-06). The schedule is not incidental: the
/// overlap calculation counts how many active workers have the slot inside their
/// hours, so nothing in <see cref="IAvailabilityService"/> works until these rows exist.
/// </summary>
public interface IWorkerService
{
    Task<List<Worker>> GetAll(bool onlyActive = false);

    /// <summary>The weekly ranges of one worker, grouped by day and ordered by start time.</summary>
    Task<Dictionary<Weekday, List<(TimeOnly start, TimeOnly fi)>>> GetSchedule(int workerId);

    Task<Worker> Create(
        Worker worker,
        IReadOnlyDictionary<Weekday, List<(TimeOnly start, TimeOnly fi)>> schedule);

    Task Update(
        Worker worker,
        IReadOnlyDictionary<Weekday, List<(TimeOnly start, TimeOnly fi)>> schedule);

    /// <summary>Holidays and departures (pantalles 3.6).</summary>
    Task ChangeStatus(int workerId, bool active);

    /// <summary>
    /// Removes the worker and her schedule, or just deactivates her when an appointment
    /// or a sale is attached: those rows name her, and the per-worker reports are built
    /// from them. A deactivated worker stops being offered when booking or charging.
    /// </summary>
    Task<DeleteResult> Delete(int workerId);
}
