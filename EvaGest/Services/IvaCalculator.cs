using EvaGest.Models;

namespace EvaGest.Services;

public record DesglossamentIva(int BaseCents, int IvaCents, int TotalCents);

public record DesglossamentPerTipus(int IvaBp, int BaseCents, int IvaCents, int TotalCents);

public static class IvaCalculator
{
    /// <summary>
    /// Computes a sale's VAT breakdown by grouping lines per VAT rate.
    ///
    /// Grouping matters: rounding each line separately and summing does NOT give the
    /// same result as rounding the total. Grouping per rate keeps the figures consistent
    /// with how Modelo 303 is filed, and guarantees the invariant
    /// BaseCents + IvaCents == TotalCents.
    /// </summary>
    public static DesglossamentIva Calcular(IEnumerable<VendaLinia> linies, IvaMode mode)
    {
        var perTipus = CalcularPerTipus(linies, mode);

        return new DesglossamentIva(
            perTipus.Sum(g => g.BaseCents),
            perTipus.Sum(g => g.IvaCents),
            perTipus.Sum(g => g.TotalCents));
    }

    /// <summary>Same calculation, kept split per VAT rate. This is what the quarterly
    /// export needs, since Modelo 303 reports each rate separately.</summary>
    public static List<DesglossamentPerTipus> CalcularPerTipus(
        IEnumerable<VendaLinia> linies, IvaMode mode)
    {
        return linies
            .GroupBy(l => l.IvaBp)
            .OrderBy(g => g.Key)
            .Select(g =>
            {
                int ivaBp = g.Key;
                int suma = g.Sum(l => l.ImportCents);  // exact, never rounded

                if (mode == IvaMode.Inclos)
                {
                    int baseCents = ArrodonirCents((decimal)suma * 10000m / (10000m + ivaBp));
                    return new DesglossamentPerTipus(ivaBp, baseCents, suma - baseCents, suma);
                }
                else
                {
                    int quota = ArrodonirCents((decimal)suma * ivaBp / 10000m);
                    return new DesglossamentPerTipus(ivaBp, suma, quota, suma + quota);
                }
            })
            .ToList();
    }

    /// <summary>
    /// Rows to persist in VendaDesglossaments. These are the frozen figures that every
    /// period total and every Modelo 303 export is summed from, so they are written
    /// once when the sale is saved and never recalculated afterwards.
    /// </summary>
    public static List<VendaDesglossament> ARegistres(
        IEnumerable<VendaLinia> linies, IvaMode mode)
        => CalcularPerTipus(linies, mode)
            .Select(g => new VendaDesglossament
            {
                IvaBp = g.IvaBp,
                BaseCents = g.BaseCents,
                IvaCents = g.IvaCents,
                TotalCents = g.TotalCents
            })
            .ToList();

    /// <summary>
    /// Rounds to whole cents away from zero. Math.Round defaults to banker's rounding
    /// (0.5 -> 0, 2.5 -> 2), which is not what Spanish accounting expects, so the
    /// mode must always be stated explicitly.
    /// </summary>
    private static int ArrodonirCents(decimal valor)
        => (int)Math.Round(valor, MidpointRounding.AwayFromZero);
}
