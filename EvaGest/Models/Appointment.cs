using System.ComponentModel.DataAnnotations.Schema;

namespace EvaGest.Models;

public class Appointment
{
    public int Id { get; set; }

    public DateOnly Date { get; set; }
    public TimeOnly Time { get; set; }
    public int DurationMin { get; set; }

    // Registered client XOR guest client: exactly one of these two must be set.
    public int? ClientId { get; set; }
    public Client? Client { get; set; }
    public string? GuestName { get; set; }
    public string? GuestPhone { get; set; }

    /// <summary>Optional. When set, it is preloaded into the sale form (RF-02).</summary>
    public int? ServiceId { get; set; }
    public Service? Service { get; set; }

    /// <summary>Optional. Drives how the overlap check is performed (RF-06).</summary>
    public int? WorkerId { get; set; }
    public Worker? Worker { get; set; }

    public AppointmentStatus Status { get; set; } = AppointmentStatus.Pending;
    public string? Notes { get; set; }

    /// <summary>
    /// Every sale ever charged for this appointment. At most one is Active (a filtered
    /// unique index enforces it); the others were voided and stay linked so the history
    /// still shows the visit was charged, voided and charged again.
    /// </summary>
    public List<Sale> Sales { get; set; } = [];

    [NotMapped]
    public string DisplayName => Client?.Name ?? GuestName ?? string.Empty;

    [NotMapped]
    public bool IsGuest => ClientId is null;

    /// <summary>Clamped to the last minute of the day; see
    /// <see cref="Helpers.GridHelper.ClampedEnd"/> for why, and note that the overlap
    /// check in AvailabilityService reads the very same helper.</summary>
    [NotMapped]
    public TimeOnly EndTime => Helpers.GridHelper.ClampedEnd(Time, DurationMin);
}
