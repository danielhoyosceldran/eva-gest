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

    /// <summary>All weekly opening ranges in a single query, so the weekly grid does
    /// not issue one round trip per day. Days with no schedule are absent.</summary>
    Task<Dictionary<DiaSetmana, List<(TimeOnly inici, TimeOnly fi)>>> FranjesSetmanals();

    /// <summary>
    /// Replaces the whole weekly opening schedule in one go. The settings screen edits
    /// all seven days as a single form, so a partial update would leave the week in a
    /// state the user never asked for. Days absent from the dictionary are closed.
    /// </summary>
    Task GuardarHorariSetmanal(IReadOnlyDictionary<DiaSetmana, List<(TimeOnly inici, TimeOnly fi)>> horari);

    /// <summary>Closed dates within a range, with their reason. Used by the weekly
    /// agenda to dim whole columns without one query per day.</summary>
    Task<Dictionary<DateOnly, string?>> DiesTancatsA(DateOnly des, DateOnly fins);

    /// <summary>Every closed date on record, soonest first. The settings screen lists
    /// them all rather than only the future ones: last year's holidays are what the
    /// user copies from when filling in this year's.</summary>
    Task<List<DiaTancat>> DiesTancats();

    /// <summary>Marks a date as closed. Setting the same date twice updates its reason
    /// instead of failing on the unique index.</summary>
    Task AfegirDiaTancat(DateOnly data, string? motiu);

    Task EliminarDiaTancat(int id);
}
