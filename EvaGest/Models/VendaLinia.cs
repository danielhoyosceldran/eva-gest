using System.ComponentModel.DataAnnotations.Schema;

namespace EvaGest.Models;

/// <summary>Each line freezes its own price and VAT rate: a later catalogue change
/// never moves past sales.</summary>
public class VendaLinia
{
    public int Id { get; set; }

    public int VendaId { get; set; }
    public Venda Venda { get; set; } = null!;

    // Optional catalogue links, used ONLY for reporting aggregations (RF-16-D).
    // They are never the source of price or VAT: those are frozen copies below.
    public int? ServeiId { get; set; }
    public Servei? Servei { get; set; }
    public int? ProducteId { get; set; }
    public Producte? Producte { get; set; }

    /// <summary>Free-text copy. Allows fully custom lines (favours, special prices).</summary>
    public string Descripcio { get; set; } = string.Empty;

    public int Quantitat { get; set; } = 1;

    /// <summary>Frozen copy of the unit price at the time of the sale.</summary>
    public int PreuUnitariCents { get; set; }

    /// <summary>Frozen copy of the VAT rate applied at the time of the sale.</summary>
    public int IvaBp { get; set; }

    /// <summary>PreuUnitariCents * Quantitat. Exact, never rounded.
    /// Stored rather than computed so the sale total is reproducible.</summary>
    public int ImportCents { get; set; }

    /// <summary>Classification for the per-worker reports. Custom lines count as Altres,
    /// so that Serveis% + Productes% + Altres% == 100%.</summary>
    [NotMapped]
    public TipusLinia Tipus => ServeiId is not null  ? TipusLinia.Servei
                            : ProducteId is not null ? TipusLinia.Producte
                            : TipusLinia.Altres;
}
