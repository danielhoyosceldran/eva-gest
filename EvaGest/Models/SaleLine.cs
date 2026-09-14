using System.ComponentModel.DataAnnotations.Schema;

namespace EvaGest.Models;

/// <summary>Each line freezes its own price and VAT rate: a later catalogue change
/// never moves past sales.</summary>
public class SaleLine
{
    public int Id { get; set; }

    public int SaleId { get; set; }
    public Sale Sale { get; set; } = null!;

    // Optional catalogue links, used ONLY for reporting aggregations (RF-16-D).
    // They are never the source of price or VAT: those are frozen backups below.
    public int? ServiceId { get; set; }
    public Service? Service { get; set; }
    public int? ProductId { get; set; }
    public Product? Product { get; set; }

    /// <summary>Free-text copy. Allows fully custom lines (favours, special prices).</summary>
    public string Description { get; set; } = string.Empty;

    public int Quantity { get; set; } = 1;

    /// <summary>Frozen copy of the unit price at the time of the sale.</summary>
    public int UnitPriceCents { get; set; }

    /// <summary>Frozen copy of the VAT rate applied at the time of the sale.</summary>
    public int VatBp { get; set; }

    /// <summary>UnitPriceCents * Quantity. Exact, never rounded.
    /// Stored rather than computed so the sale total is reproducible.</summary>
    public int AmountCents { get; set; }

    /// <summary>Classification for the per-worker reports. Custom lines count as Other,
    /// so that Services% + Products% + Other% == 100%.</summary>
    [NotMapped]
    public LineType Type => ServiceId is not null  ? LineType.Service
                            : ProductId is not null ? LineType.Product
                            : LineType.Other;
}
