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
    /// An active sale is the history, so deleting it only cancels it (RF-10) and reports
    /// <see cref="DeleteResult.Deactivated"/>. Asking again on an already cancelled
    /// sale removes it for good, together with its lines and VAT breakdown: by then the
    /// user has seen it excluded from the totals and asked twice.
    /// </summary>
    Task<DeleteResult> Delete(int saleId);

    /// <summary>Builds an unsaved sale prefilled from an appointment (RF-09).
    /// Rejects appointments that cannot carry a sale (cancelled or no-show, F-05).</summary>
    Task<Sale> PrepareFromAppointment(int appointmentId);
}
