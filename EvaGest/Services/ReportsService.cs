using EvaGest.Data;
using EvaGest.Models;
using Microsoft.EntityFrameworkCore;

namespace EvaGest.Services;

public record ClientIndicators(
    int Visits, int Cancelled, int NoShows,
    long TotalSpentCents,
    decimal? AveragePerVisitEuros,     // null when no completed visits
    double? FrequencyDays,             // null with fewer than 2 visits
    DateOnly? FirstVisit, DateOnly? LastVisit);

/// <summary>
/// Phase 8. Reports only make sense once there is real data to aggregate over, so
/// this is deliberately one of the last modules built. Everything here reads
/// Sales / SaleLines / SaleBreakdowns; it never writes.
/// </summary>
public class ReportsService(IDbContextFactory<ShopDbContext> factory) : IReportsService
{
    public async Task<ClientIndicators> GetClientIndicators(int clientId)
    {
        await using var db = await factory.CreateDbContextAsync();

        int cancelled = await db.Appointments.CountAsync(c => c.ClientId == clientId && c.Status == AppointmentStatus.Cancelled);
        int noShows = await db.Appointments.CountAsync(c => c.ClientId == clientId && c.Status == AppointmentStatus.NoShow);

        var activeSalesDates = await db.Sales.AsNoTracking()
            .Where(v => v.ClientId == clientId && v.Status == SaleStatus.Active)
            .Select(v => new { v.Date, v.TotalCents })
            .ToListAsync();

        int visits = activeSalesDates.Count;
        long totalCents = activeSalesDates.Sum(v => (long)v.TotalCents);
        decimal? average = visits == 0 ? null : totalCents / 100m / visits;
        double? frequency = Indicators.FrequencyDays(activeSalesDates.Select(v => v.Date));

        DateOnly? first = activeSalesDates.Count == 0 ? null : activeSalesDates.Min(v => v.Date);
        DateOnly? last = activeSalesDates.Count == 0 ? null : activeSalesDates.Max(v => v.Date);

        return new ClientIndicators(visits, cancelled, noShows, totalCents,
            average, frequency, first, last);
    }

    public async Task<List<(Client client, int visits, long totalCents)>> TopByVisits(int limit = 10)
    {
        await using var db = await factory.CreateDbContextAsync();

        // Guest sales have no ClientId and never appear in a per-client ranking (I-13),
        // even though they still count towards global totals elsewhere (I-14).
        var groups = await db.Sales.AsNoTracking()
            .Where(v => v.Status == SaleStatus.Active && v.ClientId != null)
            .GroupBy(v => v.ClientId!.Value)
            .Select(g => new { ClientId = g.Key, Visits = g.Count(), Total = g.Sum(v => (long)v.TotalCents) })
            .OrderByDescending(g => g.Visits)
            .Take(limit)
            .ToListAsync();

        var clients = await ClientsPerIds(db, groups.Select(g => g.ClientId));
        return groups.Select(g => (clients[g.ClientId], g.Visits, g.Total)).ToList();
    }

    public async Task<List<(Client client, long totalCents)>> TopBySpend(int limit = 10)
    {
        await using var db = await factory.CreateDbContextAsync();

        var groups = await db.Sales.AsNoTracking()
            .Where(v => v.Status == SaleStatus.Active && v.ClientId != null)
            .GroupBy(v => v.ClientId!.Value)
            .Select(g => new { ClientId = g.Key, Total = g.Sum(v => (long)v.TotalCents) })
            .OrderByDescending(g => g.Total)
            .Take(limit)
            .ToListAsync();

        var clients = await ClientsPerIds(db, groups.Select(g => g.ClientId));
        return groups.Select(g => (clients[g.ClientId], g.Total)).ToList();
    }

    public async Task<List<(Client client, decimal averageEuros)>> TopByAverage(int limit = 10)
    {
        await using var db = await factory.CreateDbContextAsync();

        var groups = await db.Sales.AsNoTracking()
            .Where(v => v.Status == SaleStatus.Active && v.ClientId != null)
            .GroupBy(v => v.ClientId!.Value)
            .Select(g => new { ClientId = g.Key, Total = g.Sum(v => (long)v.TotalCents), Visits = g.Count() })
            .ToListAsync();

        var ordered = groups
            .Select(g => (g.ClientId, Average: g.Total / 100m / g.Visits))
            .OrderByDescending(g => g.Average)
            .Take(limit)
            .ToList();

        var clients = await ClientsPerIds(db, ordered.Select(g => g.ClientId));
        return ordered.Select(g => (clients[g.ClientId], g.Average)).ToList();
    }

    public async Task<List<(Client client, DateOnly last, int daysSince)>> GetNotSeenRecently(int limit = 10)
    {
        await using var db = await factory.CreateDbContextAsync();
        var today = DateOnly.FromDateTime(DateTime.Today);

        var groups = await db.Sales.AsNoTracking()
            .Where(v => v.Status == SaleStatus.Active && v.ClientId != null)
            .GroupBy(v => v.ClientId!.Value)
            .Select(g => new { ClientId = g.Key, Last = g.Max(v => v.Date) })
            .OrderBy(g => g.Last)
            .Take(limit)
            .ToListAsync();

        var clients = await ClientsPerIds(db, groups.Select(g => g.ClientId));
        return groups.Select(g => (clients[g.ClientId], g.Last, today.DayNumber - g.Last.DayNumber)).ToList();
    }

    public async Task<WorkerDetail> GetWorkerDetail(int workerId, DateOnly from, DateOnly to)
    {
        await using var db = await factory.CreateDbContextAsync();

        var worker = await db.Workers.AsNoTracking().FirstAsync(t => t.Id == workerId);

        var sales = await db.Sales.AsNoTracking()
            .Where(v => v.WorkerId == workerId && v.Status == SaleStatus.Active
                     && v.Date >= from && v.Date <= to)
            .Include(v => v.Lines)
            .ToListAsync();

        long periodTotalCents = await db.Sales.AsNoTracking()
            .Where(v => v.Status == SaleStatus.Active && v.Date >= from && v.Date <= to)
            .SumAsync(v => (long)v.TotalCents);

        long incomeCents = sales.Sum(v => (long)v.TotalCents);
        var allLines = sales.SelectMany(v => v.Lines).ToList();

        long productsCents = allLines.Where(l => l.Type == LineType.Product).Sum(l => (long)l.AmountCents);
        long otherCents = allLines.Where(l => l.Type == LineType.Other).Sum(l => (long)l.AmountCents);

        var services = allLines.Where(l => l.Type == LineType.Service)
            .GroupBy(l => l.Description)
            .Select(g => (g.Key, g.Count()))
            .ToList();

        var products = allLines.Where(l => l.Type == LineType.Product)
            .GroupBy(l => l.Description)
            .Select(g => (g.Key, g.Sum(l => l.Quantity)))
            .ToList();

        var activity = sales
            .GroupBy(v => ToWeekday(v.Date))
            .Select(g => (g.Key, g.Count(), g.Sum(v => (long)v.TotalCents)))
            .ToList();

        return new WorkerDetail(
            workerId, worker.Name,
            sales.Count, incomeCents,
            // Indicators.WorkPercentage takes int; clamp rather than overflow on an
            // implausibly large period total instead of throwing.
            Indicators.WorkPercentage((int)Math.Min(incomeCents, int.MaxValue), (int)Math.Min(periodTotalCents, int.MaxValue)),
            Indicators.ProductsPercentage((int)Math.Min(productsCents, int.MaxValue), (int)Math.Min(incomeCents, int.MaxValue)),
            services, products, otherCents, activity);
    }

    public async Task<List<WorkerDetail>> WorkerRanking(DateOnly from, DateOnly to)
    {
        await using var db = await factory.CreateDbContextAsync();
        var ids = await db.Workers.AsNoTracking().Select(t => t.Id).ToListAsync();

        var results = new List<WorkerDetail>();
        foreach (var id in ids) results.Add(await GetWorkerDetail(id, from, to));

        return results.OrderByDescending(r => r.IncomeCents).ToList();
    }

    public async Task<List<(int year, int month, long totalCents)>> MonthlyEvolution(int months = 12)
    {
        await using var db = await factory.CreateDbContextAsync();
        var today = DateOnly.FromDateTime(DateTime.Today);
        var from = new DateOnly(today.Year, today.Month, 1).AddMonths(-(months - 1));

        var sales = await db.Sales.AsNoTracking()
            .Where(v => v.Status == SaleStatus.Active && v.Date >= from)
            .Select(v => new { v.Date, v.TotalCents })
            .ToListAsync();

        var result = new List<(int year, int month, long totalCents)>();
        for (int i = 0; i < months; i++)
        {
            var monthDate = from.AddMonths(i);
            long total = sales
                .Where(v => v.Date.Year == monthDate.Year && v.Date.Month == monthDate.Month)
                .Sum(v => (long)v.TotalCents);
            result.Add((monthDate.Year, monthDate.Month, total));
        }
        return result;
    }

    public async Task<(Client client, int visits, long totalCents)?> ClientOfTheMonth()
    {
        await using var db = await factory.CreateDbContextAsync();
        var today = DateOnly.FromDateTime(DateTime.Today);
        var firstOfMonth = new DateOnly(today.Year, today.Month, 1);

        var group = await db.Sales.AsNoTracking()
            .Where(v => v.Status == SaleStatus.Active && v.ClientId != null && v.Date >= firstOfMonth && v.Date <= today)
            .GroupBy(v => v.ClientId!.Value)
            .Select(g => new { ClientId = g.Key, Visits = g.Count(), Total = g.Sum(v => (long)v.TotalCents) })
            .OrderByDescending(g => g.Total)
            .FirstOrDefaultAsync();

        if (group is null) return null;

        var client = await db.Clients.AsNoTracking().FirstAsync(c => c.Id == group.ClientId);
        return (client, group.Visits, group.Total);
    }

    private static async Task<Dictionary<int, Client>> ClientsPerIds(ShopDbContext db, IEnumerable<int> ids)
    {
        var list = ids.Distinct().ToList();
        var clients = await db.Clients.AsNoTracking().Where(c => list.Contains(c.Id)).ToListAsync();
        return clients.ToDictionary(c => c.Id);
    }

    /// <summary>Monday = Mon, ..., Sunday = Sun, matching AvailabilityService.</summary>
    private static Weekday ToWeekday(DateOnly date) => (Weekday)(((int)date.DayOfWeek + 6) % 7);
}
