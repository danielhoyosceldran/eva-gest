using System.ComponentModel.DataAnnotations.Schema;
using EvaGest.Resources;

namespace EvaGest.Models;

public class Sale
{
    public int Id { get; set; }

    /// <summary>When the sale happened, as the user sees it (local, editable).</summary>
    public DateOnly Date { get; set; }
    public TimeOnly Time { get; set; }

    /// <summary>When the sale was actually recorded, in UTC, set once by SaleService and
    /// never edited. Date/Time can be corrected; this cannot, so a sale typed in after
    /// the fact is told apart from one charged on the spot. Null on sales recorded
    /// before the column existed.</summary>
    public DateTime? CreatedAtUtc { get; set; }

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

    /// <summary>Always set when the record is saved. Null only once the method itself was
    /// deleted afterwards: the record keeps its amounts, shown as "Mètode eliminat" (F-04).</summary>
    public int? PaymentMethodId { get; set; }
    public PaymentMethod? PaymentMethod { get; set; }

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

    /// <summary>
    /// What <see cref="GuestName"/> holds for a sale charged to nobody on purpose (the
    /// "no client" option of the sale dialog). ck_sales_client_xor wants a name on every
    /// sale without a client, so this fixed marker fills it. It is stored as is, never
    /// translated, so it is recognised whatever the interface language; only
    /// <see cref="DisplayName"/> turns it into words. Typing it as a guest's name is refused.
    /// </summary>
    public const string NoClientName = "no_registered_client";

    /// <summary>The name shown in lists and exports: the client's, the guest's, or the
    /// translated "no client" for a sale charged to nobody.</summary>
    [NotMapped]
    public string DisplayName => Client?.Name ?? (IsNoClient ? Texts.NoClient : GuestName) ?? string.Empty;

    /// <summary>Charged to nobody on purpose (see <see cref="NoClientName"/>).</summary>
    [NotMapped]
    public bool IsNoClient => ClientId is null && GuestName == NoClientName;

    [NotMapped]
    public bool IsGuest => ClientId is null;
}
