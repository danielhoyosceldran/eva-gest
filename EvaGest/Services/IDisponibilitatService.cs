using EvaGest.Models;

namespace EvaGest.Services;

public record ResultatDisponibilitat(
    bool HiHaSolapament,
    bool ForaHorari,
    bool DiaTancat,
    string? MotiuDiaTancat,
    int TreballadoresDisponibles,
    int CitesExistents);

public interface IDisponibilitatService
{
    /// <summary>
    /// Checks an appointment slot. Never blocks: it only reports what the UI should warn about.
    /// Pass citaIdExclosa when editing, so the appointment does not clash with itself.
    /// </summary>
    Task<ResultatDisponibilitat> Comprovar(
        DateOnly data, TimeOnly hora, int duradaMin,
        int? treballadoraId, int? citaIdExclosa = null);

    /// <summary>Active workers whose weekly schedule covers this slot.</summary>
    Task<List<Treballadora>> TreballadoresDisponibles(DateOnly data, TimeOnly hora, int duradaMin);

    Task<bool> EsDiaObert(DateOnly data);
    Task<List<(TimeOnly inici, TimeOnly fi)>> FranjesObertura(DateOnly data);

    /// <summary>Closed dates within a range, with their reason. Used by the weekly
    /// agenda to dim whole columns without one query per day.</summary>
    Task<Dictionary<DateOnly, string?>> DiesTancatsA(DateOnly des, DateOnly fins);
}
