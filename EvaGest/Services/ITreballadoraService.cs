using EvaGest.Models;

namespace EvaGest.Services;

/// <summary>
/// Workers and their weekly schedules (RF-06). The schedule is not incidental: the
/// overlap calculation counts how many active workers have the slot inside their
/// hours, so nothing in <see cref="IDisponibilitatService"/> works until these rows exist.
/// </summary>
public interface ITreballadoraService
{
    Task<List<Treballadora>> ObtenirTotes(bool nomesActives = false);

    /// <summary>The weekly ranges of one worker, grouped by day and ordered by start time.</summary>
    Task<Dictionary<DiaSetmana, List<(TimeOnly inici, TimeOnly fi)>>> HorariDe(int treballadoraId);

    Task<Treballadora> Crear(
        Treballadora treballadora,
        IReadOnlyDictionary<DiaSetmana, List<(TimeOnly inici, TimeOnly fi)>> horari);

    Task Actualitzar(
        Treballadora treballadora,
        IReadOnlyDictionary<DiaSetmana, List<(TimeOnly inici, TimeOnly fi)>> horari);

    /// <summary>Holidays and departures. There is deliberately no delete: removing a
    /// worker would orphan the sales history attached to her (pantalles 3.6).</summary>
    Task CanviarEstat(int treballadoraId, bool actiu);
}
