namespace EvaGest.Models;

public class Product
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;

    public int PriceCents { get; set; }
    public string? Category { get; set; }

    /// <summary>This item's own VAT rate in basis points.</summary>
    public int VatBp { get; set; }

    public bool Active { get; set; } = true;
}
