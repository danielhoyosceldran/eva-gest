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

    // --- Productes ---
    Task<List<Producte>> ObtenirProductes(bool nomesActius = false);
    Task<Producte?> ObtenirProducte(int id);
    Task<int> CrearProducte(Producte producte);
    Task ActualitzarProducte(Producte producte);
    Task CanviarEstatProducte(int id, bool actiu);

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
}
