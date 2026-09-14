using System.ComponentModel.DataAnnotations.Schema;

namespace EvaGest.Models;

public class Sale
{
    public int Id { get; set; }

    public DateOnly Date { get; set; }
    public TimeOnly Time { get; set; }

    // Registered client XOR guest client.
    public int? ClientId { get; set; }
    public Client? Client { get; set; }
    public string? GuestName { get; set; }
    public string? GuestPhone { get; set; }

    /// <summary>Optional link to the appointment that generated this sale. Unique index.</summary>
    public int? AppointmentId { get; set; }
    public Appointment? Appointment { get; set; }

    public int? WorkerId { get; set; }
    public Worker? Worker { get; set; }

    public int PaymentMethodId { get; set; }
    public PaymentMethod PaymentMethod { get; set; } = null!;

    // Frozen totals, computed once by VatCalculator when the sale is saved.
    // Invariant: BaseCents + VatCents == TotalCents
    public int BaseCents { get; set; }
    public int VatCents { get; set; }
    public int TotalCents { get; set; }

    /// <summary>Snapshot of the VAT mode in force when this sale was recorded,
    /// so past sales stay auditable if the global setting changes later.</summary>
    public VatMode VatMode { get; set; }

    public SaleStatus Status { get; set; } = SaleStatus.Active;
    public string? Notes { get; set; }

    public List<SaleLine> Lines { get; set; } = [];

    /// <summary>Frozen per-rate VAT breakdown. Period totals are summed from here.</summary>
    public List<SaleBreakdown> Breakdowns { get; set; } = [];

    [NotMapped]
    public string DisplayName => Client?.Name ?? GuestName ?? string.Empty;

    [NotMapped]
    public bool IsGuest => ClientId is null;
}
