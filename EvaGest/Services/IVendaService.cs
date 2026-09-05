using EvaGest.Models;

namespace EvaGest.Services;

public record FiltreVendes(
    DateOnly? Des = null, DateOnly? Fins = null,
    int? ClientId = null, int? ServeiId = null, int? ProducteId = null,
    int? MetodePagamentId = null, int? TreballadoraId = null,
    EstatVenda? Estat = null);

public interface IVendaService
{
    Task<List<Venda>> Cercar(FiltreVendes filtre);
    Task<Venda?> ObtenirPerId(int id);
    Task<List<Venda>> ObtenirPerClient(int clientId);

    /// <summary>
    /// Creates a sale. Computes the VAT breakdown by grouping lines per rate,
    /// then freezes price, VAT rate and the current VAT mode on the saved rows (6.4).
    /// </summary>
    Task<int> Crear(Venda venda, List<VendaLinia> linies);

    Task Actualitzar(Venda venda, List<VendaLinia> linies);

    /// <summary>Marks the sale as cancelled. Never deletes it (RF-10).</summary>
    Task Anullar(int vendaId);

    /// <summary>Builds an unsaved sale prefilled from an appointment (RF-09).
    /// Rejects appointments that cannot carry a sale (cancelled or no-show, F-05).</summary>
    Task<Venda> PreparaDesDeCita(int citaId);
}
