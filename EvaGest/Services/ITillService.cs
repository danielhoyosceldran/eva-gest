using EvaGest.Models;

namespace EvaGest.Services;

// Aggregates use long: individual amounts fit in int, but a multi-year sum
// should never depend on turnover staying below int.MaxValue.
public record TillSummary(
    long SalesCents,
    long CashInCents,
    long CashOutCents,
    long BalanceCents,
    long BaseCents,
    long VatCents,
    List<RateBreakdown> VatBreakdown);

public interface ITillService
{
    Task<List<CashMovement>> GetByPeriod(DateOnly from, DateOnly to);

    /// <summary>
    /// Period summary. Cancelled sales are always excluded.
    ///
    /// CANONICAL RULE: totals are summed from the frozen values on Sales and
    /// SaleBreakdowns. They are NEVER recomputed from SaleLines: the two
    /// methods disagree, and the gap grows without bound. See decision 6.4 / Block B.
    /// </summary>
    Task<TillSummary> Summary(DateOnly from, DateOnly to);

    Task<int> Create(CashMovement movement);
    Task Update(CashMovement movement);
    Task Delete(int movementId);
}
