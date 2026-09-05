namespace EvaGest.Models;

public class Client
{
    public int Id { get; set; }

    /// <summary>Duplicate-detection hash: normalised first name + last 9 phone digits.
    /// Must be recalculated by the service layer whenever Nom or Mobil change.</summary>
    public string ClientKey { get; set; } = string.Empty;

    public string Nom { get; set; } = string.Empty;
    public string Mobil { get; set; } = string.Empty;

    public string? Correu { get; set; }
    public DateOnly? DataNaixement { get; set; }
    public string? Observacions { get; set; }

    /// <summary>Hidden from searches and pickers, but still counted in statistics.</summary>
    public bool Adormit { get; set; }

    public List<Cita> Cites { get; set; } = [];
    public List<Venda> Vendes { get; set; } = [];
}
