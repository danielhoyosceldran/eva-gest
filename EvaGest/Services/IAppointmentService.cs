using EvaGest.Models;

namespace EvaGest.Services;

public interface IAppointmentService
{
    Task<List<Appointment>> GetByDay(DateOnly date);

    /// <summary>Whole range in one query, used by the weekly agenda (RF-02)
    /// so navigating between weeks does not need one round trip per day.</summary>
    Task<List<Appointment>> GetByRange(DateOnly from, DateOnly to);
    Task<List<Appointment>> GetByClient(int clientId);
    Task<Appointment?> GetById(int id);

    /// <summary>Completed appointments with no sale yet, for manual association (RF-09).</summary>
    Task<List<Appointment>> GetCompletedWithoutSale();

    /// <summary>Counts appointments in one state over a range. Used by the dashboard
    /// counters and by the day-scenario tests.</summary>
    Task<int> CountByStatus(DateOnly from, DateOnly to, AppointmentStatus status);

    Task<int> Create(Appointment appointment);
    Task Update(Appointment appointment);

    /// <summary>
    /// Changes state. Cancelled and no-show can never carry a sale (RF-02). Resetting a
    /// Completed appointment back to Pending is refused when it still carries an active
    /// sale (returns <c>false</c>): the sale names the appointment and already moved
    /// money, so it has to be voided first.
    /// </summary>
    Task<bool> ChangeStatus(int appointmentId, AppointmentStatus newStatus);

    /// <summary>
    /// Removes the appointment. Returns <see cref="DeleteResult.Blocked"/> when a
    /// sale was taken from it: the sale would survive but lose the appointment it names,
    /// so the sale has to be dealt with first.
    /// </summary>
    Task<DeleteResult> Delete(int appointmentId);
}
