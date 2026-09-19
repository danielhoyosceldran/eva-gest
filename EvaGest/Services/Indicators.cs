namespace EvaGest.Services;

/// <summary>
/// The four indicators at risk of division by zero. Return decimal?/double? on purpose,
/// so the UI shows "—" instead of a misleading "0 %" when there is nothing to compute.
/// </summary>
public static class Indicators
{
    /// <summary>Share of total takings billed by one worker. Null when the period had no sales.
    /// Takes long because period totals are summed as long: clamping them down to int at
    /// the call site used to silently report 100 % instead of overflowing.</summary>
    public static decimal? WorkPercentage(long workerCents, long periodTotalCents)
        => periodTotalCents == 0 ? null : workerCents * 100m / periodTotalCents;

    /// <summary>Share of a worker's takings that came from products.
    /// Null when that worker had no sales in the period.</summary>
    public static decimal? ProductsPercentage(long productsCents, long workerCents)
        => workerCents == 0 ? null : productsCents * 100m / workerCents;

    /// <summary>Average spend per completed visit. Null when the client has no completed visits.</summary>
    public static decimal? AveragePerVisit(long totalSpentCents, int completedVisits)
        => completedVisits == 0 ? null : totalSpentCents / 100m / completedVisits;

    /// <summary>
    /// Average number of days between consecutive completed visits.
    /// Needs at least two visits to have one interval, so it returns null below that.
    /// </summary>
    public static double? FrequencyDays(IEnumerable<DateOnly> completedVisits)
    {
        var dates = completedVisits.OrderBy(d => d).ToList();
        if (dates.Count < 2) return null;

        double totalDays = dates
            .Zip(dates.Skip(1), (previous, next) => (next.ToDateTime(TimeOnly.MinValue)
                                                      - previous.ToDateTime(TimeOnly.MinValue)).TotalDays)
            .Sum();

        return totalDays / (dates.Count - 1);
    }
}
