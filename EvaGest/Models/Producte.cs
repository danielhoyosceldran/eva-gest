namespace EvaGest.Models;

public class Producte
{
    public int Id { get; set; }
    public string Nom { get; set; } = string.Empty;

    public int PreuCents { get; set; }
    public string? Categoria { get; set; }

    /// <summary>This item's own VAT rate in basis points.</summary>
    public int IvaBp { get; set; }

    public bool Actiu { get; set; } = true;
}
