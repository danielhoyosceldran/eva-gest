namespace EvaGest.Models;

public class Service
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;

    public int PriceCents { get; set; }

    /// <summary>This item's own VAT rate in basis points (2100 = 21.00%).
    /// Usually the default, but it can differ per item.</summary>
    public int VatBp { get; set; }

    /// <summary>When null, the configured default appointment duration is used.</summary>
    public int? DurationMin { get; set; }

    public bool Active { get; set; } = true;

    public override string ToString() => Name;
}
