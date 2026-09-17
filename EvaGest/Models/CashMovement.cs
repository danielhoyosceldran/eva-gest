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

    /// <summary>Only meaningful for a cash-out; unset for a cash-in and for movements
    /// entered before the category list existed.</summary>
    public int? CategoryId { get; set; }
    public ExpenseCategory? Category { get; set; }

    /// <summary>Set when a cash-out pays a specific worker (a salary or a commission),
    /// so the month-close report can compare what she was paid against what she billed.</summary>
    public int? WorkerId { get; set; }
    public Worker? Worker { get; set; }

    public string Concept { get; set; } = string.Empty;
    public string? Notes { get; set; }

    /// <summary>Signed value for balance calculations: entries add, exits subtract.</summary>
    [NotMapped]
    public int SignedAmountCents => Type == MovementType.In ? AmountCents : -AmountCents;
}
