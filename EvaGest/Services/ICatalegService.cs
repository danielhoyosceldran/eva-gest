using EvaGest.Models;

namespace EvaGest.Services;

public interface ICatalegService
{
    // --- Serveis ---
    Task<List<Servei>> ObtenirServeis(bool nomesActius = false);
    Task<Servei?> ObtenirServei(int id);
    Task<int> CrearServei(Servei servei);
    Task ActualitzarServei(Servei servei);
    Task CanviarEstatServei(int id, bool actiu);

    /// <summary>
    /// Removes the service, or deactivates it when an appointment or a sale line already
    /// points at it: deleting it there would empty a row of the history. Either way it
    /// stops being offered when booking or charging.
    /// </summary>
    Task<ResultatEsborrat> EliminarServei(int id);

    // --- Productes ---
    Task<List<Producte>> ObtenirProductes(bool nomesActius = false);
    Task<Producte?> ObtenirProducte(int id);
    Task<int> CrearProducte(Producte producte);
    Task ActualitzarProducte(Producte producte);
    Task CanviarEstatProducte(int id, bool actiu);

    /// <summary>Same rule as <see cref="EliminarServei"/>: sold products are deactivated,
    /// never removed.</summary>
    Task<ResultatEsborrat> EliminarProducte(int id);

    // --- Mètodes de pagament ---
    Task<List<MetodePagament>> ObtenirMetodes(bool nomesActius = false);
    Task<int> CrearMetode(string nom);
    Task ActualitzarMetode(MetodePagament metode);

    /// <summary>
    /// Deactivating the last active payment method would make it impossible to take
    /// money, so the service refuses it and the UI explains why (pantalles 2.5).
    /// </summary>
    Task<bool> PotDesactivarMetode(int id);
    Task CanviarEstatMetode(int id, bool actiu);

    /// <summary>
    /// Removes the payment method, or deactivates it when a sale or a cash movement uses
    /// it. Returns <see cref="ResultatEsborrat.Bloquejat"/> for the last active one:
    /// with none left there would be no way to take money (pantalles 2.5).
    /// </summary>
    Task<ResultatEsborrat> EliminarMetode(int id);
}
