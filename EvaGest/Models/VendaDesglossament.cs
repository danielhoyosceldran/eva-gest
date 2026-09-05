namespace EvaGest.Models;

/// <summary>
/// Frozen VAT breakdown for one tax rate within a sale. Modelo 303 requires base and
/// quota reported per rate, and recomputing them later from lines would drift,
/// so they are stored once and only ever summed.
/// </summary>
public class VendaDesglossament
{
    public int Id { get; set; }

    public int VendaId { get; set; }
    public Venda Venda { get; set; } = null!;

    public int IvaBp { get; set; }
    public int BaseCents { get; set; }
    public int IvaCents { get; set; }
    public int TotalCents { get; set; }
}
