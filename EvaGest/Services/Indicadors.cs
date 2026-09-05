namespace EvaGest.Services;

/// <summary>
/// The four indicators at risk of division by zero. Return decimal?/double? on purpose,
/// so the UI shows "—" instead of a misleading "0 %" when there is nothing to compute.
/// </summary>
public static class Indicadors
{
    /// <summary>Share of total takings billed by one worker. Null when the period had no sales.</summary>
    public static decimal? PercentatgeTreball(int treballadoraCents, int totalPeriodeCents)
        => totalPeriodeCents == 0 ? null : treballadoraCents * 100m / totalPeriodeCents;

    /// <summary>Share of a worker's takings that came from products.
    /// Null when that worker had no sales in the period.</summary>
    public static decimal? PercentatgeProductes(int productesCents, int treballadoraCents)
        => treballadoraCents == 0 ? null : productesCents * 100m / treballadoraCents;

    /// <summary>Average spend per completed visit. Null when the client has no completed visits.</summary>
    public static decimal? MitjanaPerVisita(int totalGastatCents, int visitesRealitzades)
        => visitesRealitzades == 0 ? null : totalGastatCents / 100m / visitesRealitzades;

    /// <summary>
    /// Average number of days between consecutive completed visits.
    /// Needs at least two visits to have one interval, so it returns null below that.
    /// </summary>
    public static double? FrequenciaDies(IEnumerable<DateOnly> visitesRealitzades)
    {
        var dates = visitesRealitzades.OrderBy(d => d).ToList();
        if (dates.Count < 2) return null;

        double totalDies = dates
            .Zip(dates.Skip(1), (anterior, seguent) => (seguent.ToDateTime(TimeOnly.MinValue)
                                                      - anterior.ToDateTime(TimeOnly.MinValue)).TotalDays)
            .Sum();

        return totalDies / (dates.Count - 1);
    }
}
