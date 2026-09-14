using System.ComponentModel.DataAnnotations.Schema;

namespace EvaGest.Models;

public class CashMovement
{
    public int Id { get; set; }

    public DateOnly Date { get; set; }
    public MovementType Type { get; set; }

    /// <summary>Final amount, equivalent to a sale's TotalCents.</summary>
    public int AmountCents { get; set; }

    // Only filled in when the "apply VAT to cash movements" setting is on (RF-13).
    // Stored as fixed values, never derived, so past movements never change retroactively.
    public int? BaseCents { get; set; }
    public int? VatCents { get; set; }
    public int? VatBp { get; set; }

    public int PaymentMethodId { get; set; }
    public PaymentMethod PaymentMethod { get; set; } = null!;

    public string Concept { get; set; } = string.Empty;
    public string? Notes { get; set; }

    /// <summary>Signed value for balance calculations: entries add, exits subtract.</summary>
    [NotMapped]
    public int SignedAmountCents => Type == MovementType.In ? AmountCents : -AmountCents;
}
