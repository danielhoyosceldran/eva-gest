namespace EvaGest.Models;

/// <summary>One row per time slot, which allows split schedules (morning and afternoon).</summary>
public class HorariTreballadora
{
    public int Id { get; set; }

    public int TreballadoraId { get; set; }
    public Treballadora Treballadora { get; set; } = null!;

    public DiaSetmana DiaSetmana { get; set; }
    public TimeOnly HoraInici { get; set; }
    public TimeOnly HoraFi { get; set; }
}
