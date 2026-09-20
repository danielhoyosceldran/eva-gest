using EvaGest.Models;

namespace EvaGest.Services;

public record VatBreakdown(int BaseCents, int VatCents, int TotalCents);

/// <summary>One VAT rate's share of a SINGLE sale. int cents, because one sale's
/// amounts are bounded by what a sale line can hold.</summary>
public record RateBreakdown(int VatBp, int BaseCents, int VatCents, int TotalCents);

/// <summary>
/// One VAT rate's share of a PERIOD, summed from the frozen SaleBreakdowns rows.
/// Deliberately a separate record from <see cref="RateBreakdown"/> and deliberately
/// long: a period total must never depend on turnover staying below int.MaxValue,
/// and sharing the per-sale record forced a narrowing cast at exactly the point the
/// figure reached the screen.
/// </summary>
public record RateTotals(int VatBp, long BaseCents, long VatCents, long TotalCents);

public static class VatCalculator
{
    /// <summary>
    /// Computes a sale's VAT breakdown by grouping lines per VAT rate.
    ///
    /// Grouping matters: rounding each line separately and summing does NOT give the
    /// same result as rounding the total. Grouping per rate keeps the figures consistent
    /// with how Modelo 303 is filed, and guarantees the invariant
    /// BaseCents + VatCents == TotalCents.
    /// </summary>
    public static VatBreakdown Compute(IEnumerable<SaleLine> lines, VatMode mode)
    {
        var byRate = ComputeByRate(lines, mode);

        return new VatBreakdown(
            byRate.Sum(g => g.BaseCents),
            byRate.Sum(g => g.VatCents),
            byRate.Sum(g => g.TotalCents));
    }

    /// <summary>Same calculation, kept split per VAT rate. This is what the quarterly
    /// export needs, since Modelo 303 reports each rate separately.</summary>
    public static List<RateBreakdown> ComputeByRate(
        IEnumerable<SaleLine> lines, VatMode mode)
    {
        return lines
            .GroupBy(l => l.VatBp)
            .OrderBy(g => g.Key)
            .Select(g =>
            {
                int vatBp = g.Key;
                int sum = g.Sum(l => l.AmountCents);  // exact, never rounded

                if (mode == VatMode.Included)
                {
                    int baseCents = RoundCents((decimal)sum * 10000m / (10000m + vatBp));
                    return new RateBreakdown(vatBp, baseCents, sum - baseCents, sum);
                }
                else
                {
                    int quota = RoundCents((decimal)sum * vatBp / 10000m);
                    return new RateBreakdown(vatBp, sum, quota, sum + quota);
                }
            })
            .ToList();
    }

    /// <summary>
    /// Rows to persist in SaleBreakdowns. These are the frozen figures that every
    /// period total and every Modelo 303 export is summed from, so they are written
    /// once when the sale is saved and never recalculated afterwards.
    /// </summary>
    public static List<SaleBreakdown> ToBreakdownRows(
        IEnumerable<SaleLine> lines, VatMode mode)
        => ComputeByRate(lines, mode)
            .Select(g => new SaleBreakdown
            {
                VatBp = g.VatBp,
                BaseCents = g.BaseCents,
                VatCents = g.VatCents,
                TotalCents = g.TotalCents
            })
            .ToList();

    /// <summary>
    /// Rounds to whole cents away from zero. Math.Round defaults to banker's rounding
    /// (0.5 -> 0, 2.5 -> 2), which is not what Spanish accounting expects, so the
    /// mode must always be stated explicitly.
    /// </summary>
    private static int RoundCents(decimal value)
        => (int)Math.Round(value, MidpointRounding.AwayFromZero);
}
