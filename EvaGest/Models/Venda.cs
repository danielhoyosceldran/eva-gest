using System.ComponentModel.DataAnnotations.Schema;

namespace EvaGest.Models;

public class Venda
{
    public int Id { get; set; }

    public DateOnly Data { get; set; }
    public TimeOnly Hora { get; set; }

    // Registered client XOR guest client.
    public int? ClientId { get; set; }
    public Client? Client { get; set; }
    public string? NomConvidat { get; set; }
    public string? TelefonConvidat { get; set; }

    /// <summary>Optional link to the appointment that generated this sale. Unique index.</summary>
    public int? CitaId { get; set; }
    public Cita? Cita { get; set; }

    public int? TreballadoraId { get; set; }
    public Treballadora? Treballadora { get; set; }

    public int MetodePagamentId { get; set; }
    public MetodePagament MetodePagament { get; set; } = null!;

    // Frozen totals, computed once by IvaCalculator when the sale is saved.
    // Invariant: BaseCents + IvaCents == TotalCents
    public int BaseCents { get; set; }
    public int IvaCents { get; set; }
    public int TotalCents { get; set; }

    /// <summary>Snapshot of the VAT mode in force when this sale was recorded,
    /// so past sales stay auditable if the global setting changes later.</summary>
    public IvaMode IvaMode { get; set; }

    public EstatVenda Estat { get; set; } = EstatVenda.Activa;
    public string? Observacions { get; set; }

    public List<VendaLinia> Linies { get; set; } = [];

    /// <summary>Frozen per-rate VAT breakdown. Period totals are summed from here.</summary>
    public List<VendaDesglossament> Desglossaments { get; set; } = [];

    [NotMapped]
    public string NomMostrat => Client?.Nom ?? NomConvidat ?? string.Empty;

    [NotMapped]
    public bool EsConvidat => ClientId is null;
}
