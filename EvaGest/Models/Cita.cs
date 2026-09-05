using System.ComponentModel.DataAnnotations.Schema;

namespace EvaGest.Models;

public class Cita
{
    public int Id { get; set; }

    public DateOnly Data { get; set; }
    public TimeOnly Hora { get; set; }
    public int DuradaMin { get; set; }

    // Registered client XOR guest client: exactly one of these two must be set.
    public int? ClientId { get; set; }
    public Client? Client { get; set; }
    public string? NomConvidat { get; set; }
    public string? TelefonConvidat { get; set; }

    /// <summary>Optional. When set, it is preloaded into the sale form (RF-02).</summary>
    public int? ServeiId { get; set; }
    public Servei? Servei { get; set; }

    /// <summary>Optional. Drives how the overlap check is performed (RF-06).</summary>
    public int? TreballadoraId { get; set; }
    public Treballadora? Treballadora { get; set; }

    public EstatCita Estat { get; set; } = EstatCita.Pendent;
    public string? Observacions { get; set; }

    /// <summary>At most one sale per appointment.</summary>
    public Venda? Venda { get; set; }

    [NotMapped]
    public string NomMostrat => Client?.Nom ?? NomConvidat ?? string.Empty;

    [NotMapped]
    public bool EsConvidat => ClientId is null;

    [NotMapped]
    public TimeOnly HoraFi => Hora.AddMinutes(DuradaMin);
}
