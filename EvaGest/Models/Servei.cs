namespace EvaGest.Models;

public class Servei
{
    public int Id { get; set; }
    public string Nom { get; set; } = string.Empty;

    public int PreuCents { get; set; }

    /// <summary>This item's own VAT rate in basis points (2100 = 21.00%).
    /// Usually the default, but it can differ per item.</summary>
    public int IvaBp { get; set; }

    /// <summary>When null, the configured default appointment duration is used.</summary>
    public int? DuradaMin { get; set; }

    public bool Actiu { get; set; } = true;

    public override string ToString() => Nom;
}
