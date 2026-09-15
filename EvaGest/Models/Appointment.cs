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

    /// <summary>At most one sale per appointment.</summary>
    public Sale? Sale { get; set; }

    [NotMapped]
    public string DisplayName => Client?.Name ?? GuestName ?? string.Empty;

    [NotMapped]
    public bool IsGuest => ClientId is null;

    /// <summary>Clamped to the last minute of the day: TimeOnly.AddMinutes wraps
    /// silently past midnight (10:00 + 16h would read back as 02:00), which would make
    /// a very long or very late appointment look like it ends before it starts.
    /// TimeOnly cannot represent 24:00 either, so a duration that would cross midnight
    /// is shown as running to the end of the day instead.</summary>
    [NotMapped]
    public TimeOnly EndTime
        => Time.Hour * 60 + Time.Minute + DurationMin >= 24 * 60
            ? new TimeOnly(23, 59, 59)
            : Time.AddMinutes(DurationMin);
}
