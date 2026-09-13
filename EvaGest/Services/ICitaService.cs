using EvaGest.Models;

namespace EvaGest.Services;

public interface ICitaService
{
    Task<List<Cita>> ObtenirPerDia(DateOnly data);

    /// <summary>Whole range in one query, used by the weekly agenda (RF-02)
    /// so navigating between weeks does not need one round trip per day.</summary>
    Task<List<Cita>> ObtenirPerRang(DateOnly des, DateOnly fins);
    Task<List<Cita>> ObtenirPerClient(int clientId);
    Task<Cita?> ObtenirPerId(int id);

    /// <summary>Completed appointments with no sale yet, for manual association (RF-09).</summary>
    Task<List<Cita>> ObtenirRealitzadesSenseVenda();

    /// <summary>Counts appointments in one state over a range. Used by the dashboard
    /// counters and by the day-scenario tests.</summary>
    Task<int> ComptarPerEstat(DateOnly des, DateOnly fins, EstatCita estat);

    Task<int> Crear(Cita cita);
    Task Actualitzar(Cita cita);

    /// <summary>
    /// Changes state. Cancelled and no-show can never carry a sale (RF-02). Resetting a
    /// Realitzada appointment back to Pendent is refused when it still carries an active
    /// sale (returns <c>false</c>): the sale names the appointment and already moved
    /// money, so it has to be voided first.
    /// </summary>
    Task<bool> CanviarEstat(int citaId, EstatCita nouEstat);

    /// <summary>
    /// Removes the appointment. Returns <see cref="ResultatEsborrat.Bloquejat"/> when a
    /// sale was taken from it: the sale would survive but lose the appointment it names,
    /// so the sale has to be dealt with first.
    /// </summary>
    Task<ResultatEsborrat> Eliminar(int citaId);
}
