namespace EvaGest.Models;

/// <summary>
/// Frozen VAT breakdown for one tax rate within a sale. Modelo 303 requires base and
/// quota reported per rate, and recomputing them later from lines would drift,
/// so they are stored once and only ever summed.
/// </summary>
public class SaleBreakdown
{
    public int Id { get; set; }

    public int SaleId { get; set; }
    public Sale Sale { get; set; } = null!;

    public int VatBp { get; set; }
    public int BaseCents { get; set; }
    public int VatCents { get; set; }
    public int TotalCents { get; set; }
}
