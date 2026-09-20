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
    /// Whether this set of lines can be represented at all: every per-rate group, and
    /// their total, has to fit in int cents.
    ///
    /// <see cref="ComputeByRate"/> sums a group with Enumerable.Sum over int, which is
    /// checked — it throws rather than wrapping. Two lines that are each individually
    /// valid (a line is bounded on its own, not against its neighbours) could therefore
    /// take the group past int.MaxValue and throw on a keystroke, because the sale
    /// dialog recomputes its live footer on every change. Asked first, this returns
    /// false instead and the dialog refuses the sale with a message.
    /// </summary>
    public static bool FitsInOneSale(IEnumerable<SaleLine> lines)
    {
        long total = 0;
        foreach (var group in lines.GroupBy(l => l.VatBp))
        {
            long sum = group.Sum(l => (long)l.AmountCents);

            // A VAT-exclusive sale adds its quota on top, so the group's total is what
            // has to fit, not just the base.
            long withVat = sum + sum * group.Key / 10000;
            if (sum is < int.MinValue or > int.MaxValue) return false;
            if (withVat is < int.MinValue or > int.MaxValue) return false;

            total += withVat;
        }

        return total is >= int.MinValue and <= int.MaxValue;
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
