using EvaGest.Data;
using EvaGest.Models;
using Microsoft.EntityFrameworkCore;

using Serilog;

namespace EvaGest.Services;

public class TillService(IDbContextFactory<ShopDbContext> factory) : ITillService
{
    public async Task<List<CashMovement>> GetByPeriod(DateOnly from, DateOnly to)
    {
        await using var db = await factory.CreateDbContextAsync();
        // Category and Worker are included because the movements table shows them: without
        // these two the columns bound to them stayed permanently blank, which looked like
        // the fields were never saved.
        return await db.CashMovements.AsNoTracking()
            .Include(m => m.PaymentMethod)
            .Include(m => m.Category)
            .Include(m => m.Worker)
            .Where(m => m.Date >= from && m.Date <= to)
            .OrderByDescending(m => m.Date)
            .ToListAsync();
    }

    public async Task<TillSummary> Summary(DateOnly from, DateOnly to)
    {
        await using var db = await factory.CreateDbContextAsync();

        // Cast to long before summing: SQLite SUM is 64-bit, and long avoids any risk
        // of overflow on a very long period regardless of turnover.
        long salesCents = await db.Sales
            .Where(v => v.Status == SaleStatus.Active && v.Date >= from && v.Date <= to)
            .SumAsync(v => (long)v.TotalCents);

        long cashInCents = await db.CashMovements
            .Where(m => m.Type == MovementType.In && m.Date >= from && m.Date <= to)
            .SumAsync(m => (long)m.AmountCents);

        long cashOutCents = await db.CashMovements
            .Where(m => m.Type == MovementType.Out && m.Date >= from && m.Date <= to)
            .SumAsync(m => (long)m.AmountCents);

        // Per-rate breakdown straight from the frozen rows (never from SaleLines, Block B).
        var byRate = await db.SaleBreakdowns
            .Where(d => d.Sale.Status == SaleStatus.Active && d.Sale.Date >= from && d.Sale.Date <= to)
            .GroupBy(d => d.VatBp)
            .Select(g => new
            {
                VatBp = g.Key,
                Base = g.Sum(x => (long)x.BaseCents),
                Vat = g.Sum(x => (long)x.VatCents),
                Total = g.Sum(x => (long)x.TotalCents)
            })
            .OrderBy(g => g.VatBp)
            .ToListAsync();

        long baseTotal = byRate.Sum(g => g.Base);
        long vatTotal = byRate.Sum(g => g.Vat);

        // Summed as long and kept as long: casting back to int here reintroduced the
        // overflow the (long) casts above exist to avoid.
        var breakdown = byRate
            .Select(g => new RateTotals(g.VatBp, g.Base, g.Vat, g.Total))
            .ToList();

        return new TillSummary(
            SalesCents: salesCents,
            CashInCents: cashInCents,
            CashOutCents: cashOutCents,
            BalanceCents: salesCents + cashInCents - cashOutCents,
            BaseCents: baseTotal,
            VatCents: vatTotal,
            VatBreakdown: breakdown);
    }

    public async Task<int> Create(CashMovement movement)
    {
        await using var db = await factory.CreateDbContextAsync();
        db.CashMovements.Add(movement);
        await db.SaveChangesAsync();
        Log.Information("Cash movement {MovementId} created: {MovementType} {AmountCents} cents",
            movement.Id, movement.Type, movement.AmountCents);
        return movement.Id;
    }

    public async Task Update(CashMovement movement)
    {
        await using var db = await factory.CreateDbContextAsync();
        db.CashMovements.Update(movement);
        await db.SaveChangesAsync();
        Log.Information("Cash movement {MovementId} updated", movement.Id);
    }

    public async Task Delete(int movementId)
    {
        await using var db = await factory.CreateDbContextAsync();
        var movement = await db.CashMovements.FirstAsync(m => m.Id == movementId);
        db.CashMovements.Remove(movement);
        await db.SaveChangesAsync();

        // A cash movement is deleted outright, not voided like a sale, so the log line
        // is the only record left that it ever existed.
        Log.Information("Cash movement {MovementId} deleted: {MovementType} {AmountCents} cents",
            movementId, movement.Type, movement.AmountCents);
    }
}
