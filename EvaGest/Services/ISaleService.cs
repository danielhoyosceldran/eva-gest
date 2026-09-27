using EvaGest.Models;

namespace EvaGest.Services;

public record SalesFilter(
    DateOnly? From = null, DateOnly? To = null,
    int? ClientId = null, int? ServiceId = null, int? ProductId = null,
    int? PaymentMethodId = null, int? WorkerId = null,
    SaleStatus? Status = null);

public interface ISaleService
{
    Task<List<Sale>> Search(SalesFilter filter);
    Task<Sale?> GetById(int id);
    Task<List<Sale>> GetByClient(int clientId);

    /// <summary>
    /// Creates a sale. Computes the VAT breakdown by grouping lines per rate,
    /// then freezes price, VAT rate and the current VAT mode on the saved rows (6.4).
    /// </summary>
    Task<int> Create(Sale sale, List<SaleLine> lines);

    Task Update(Sale sale, List<SaleLine> lines);

    /// <summary>Marks the sale as cancelled. Never deletes it (RF-10).</summary>
    Task Void(int saleId);

    /// <summary>
    /// A sale is never deleted (RF-10). Asking to delete an active one voids it and
    /// reports <see cref="DeleteResult.Deactivated"/>; asking on an already voided one
    /// changes nothing and reports <see cref="DeleteResult.Blocked"/>.
    /// </summary>
    Task<DeleteResult> Delete(int saleId);

    /// <summary>Builds an unsaved sale prefilled from an appointment (RF-09).
    /// Rejects appointments that cannot carry a sale (cancelled or no-show, F-05).</summary>
    Task<Sale> PrepareFromAppointment(int appointmentId);
}
