using System.ComponentModel.DataAnnotations.Schema;

namespace EvaGest.Models;

public class MovimentCaixa
{
    public int Id { get; set; }

    public DateOnly Data { get; set; }
    public TipusMoviment Tipus { get; set; }

    /// <summary>Final amount, equivalent to a sale's TotalCents.</summary>
    public int ImportCents { get; set; }

    // Only filled in when the "apply VAT to cash movements" setting is on (RF-13).
    // Stored as fixed values, never derived, so past movements never change retroactively.
    public int? BaseCents { get; set; }
    public int? IvaCents { get; set; }
    public int? IvaBp { get; set; }

    public int MetodePagamentId { get; set; }
    public MetodePagament MetodePagament { get; set; } = null!;

    public string Concepte { get; set; } = string.Empty;
    public string? Observacions { get; set; }

    /// <summary>Signed value for balance calculations: entries add, exits subtract.</summary>
    [NotMapped]
    public int ImportSignatCents => Tipus == TipusMoviment.Entrada ? ImportCents : -ImportCents;
}
