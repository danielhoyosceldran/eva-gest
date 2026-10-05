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

    /// <summary>Still-Pending appointments whose start time is more than an hour before
    /// <paramref name="asOf"/>, so the shell can warn that a cite was never closed.</summary>
    Task<List<Appointment>> GetOverduePending(DateTime asOf);

    /// <summary>Counts appointments in one state over a range. Used by the dashboard
    /// counters and by the day-scenario tests.</summary>
    Task<int> CountByStatus(DateOnly from, DateOnly to, AppointmentStatus status);

    Task<int> Create(Appointment appointment);

    /// <summary>Saves the appointment's details (date, time, duration, client, service,
    /// worker, notes). Never its <see cref="Appointment.Status"/>: that only moves through
    /// <see cref="ChangeStatus"/> or a sale, whatever the passed object carries.</summary>
    Task Update(Appointment appointment);

    /// <summary>
    /// Changes state. Cancelled and no-show can never carry a sale (RF-02, F-05).
    /// Returns <c>false</c> and changes nothing when the appointment still carries an
    /// active sale and the new state is Pending, Cancelled or NoShow: the sale names
    /// the appointment and already moved money, so it has to be voided first.
    /// </summary>
    Task<bool> ChangeStatus(int appointmentId, AppointmentStatus newStatus);

    /// <summary>
    /// Removes the appointment. Returns <see cref="DeleteResult.Blocked"/> when a
    /// sale was taken from it: the sale would survive but lose the appointment it names,
    /// so the sale has to be dealt with first.
    /// </summary>
    Task<DeleteResult> Delete(int appointmentId);
}
