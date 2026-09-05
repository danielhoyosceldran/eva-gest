using EvaGest.Models;

namespace EvaGest.Services;

/// <summary>
/// Minimal for now: only what the Cita dialog's worker picker needs (Fase 5). Full
/// CRUD with weekly schedules is a separate concern for the Treballadores page.
/// </summary>
public interface ITreballadoraService
{
    Task<List<Treballadora>> ObtenirTotes(bool nomesActives = false);
}
