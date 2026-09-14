using EvaGest.Data;
using EvaGest.Models;
using Microsoft.EntityFrameworkCore;

namespace EvaGest.Services;

public class WorkerService(IDbContextFactory<ShopDbContext> factory) : IWorkerService
{
    public async Task<List<Worker>> GetAll(bool onlyActive = false)
    {
        await using var db = await factory.CreateDbContextAsync();
        var query = db.Workers.AsNoTracking().Include(t => t.Schedules).AsQueryable();
        if (onlyActive) query = query.Where(t => t.Active);
        return await query.OrderBy(t => t.Name).ToListAsync();
    }

    public async Task<Dictionary<Weekday, List<(TimeOnly start, TimeOnly fi)>>> GetSchedule(int workerId)
    {
        await using var db = await factory.CreateDbContextAsync();
        var rows = await db.WorkerSchedules.AsNoTracking()
            .Where(h => h.WorkerId == workerId)
            .OrderBy(h => h.Weekday).ThenBy(h => h.StartTime)
            .Select(h => new { h.Weekday, h.StartTime, h.EndTime })
            .ToListAsync();

        return rows
            .GroupBy(h => h.Weekday)
            .ToDictionary(g => g.Key, g => g.Select(h => (h.StartTime, h.EndTime)).ToList());
    }

    public async Task<Worker> Create(
        Worker worker,
        IReadOnlyDictionary<Weekday, List<(TimeOnly start, TimeOnly fi)>> schedule)
    {
        await using var db = await factory.CreateDbContextAsync();

        var created = new Worker
        {
            Name = worker.Name,
            Active = worker.Active,
            Color = worker.Color,
            Schedules = ToRows(schedule)
        };

        db.Workers.Add(created);
        await db.SaveChangesAsync();
        return created;
    }

    public async Task Update(
        Worker worker,
        IReadOnlyDictionary<Weekday, List<(TimeOnly start, TimeOnly fi)>> schedule)
    {
        await using var db = await factory.CreateDbContextAsync();
        await using var transaction = await db.Database.BeginTransactionAsync();

        var existing = await db.Workers
            .Include(t => t.Schedules)
            .FirstOrDefaultAsync(t => t.Id == worker.Id)
            ?? throw new InvalidOperationException($"No worker exists with id {worker.Id}.");

        existing.Name = worker.Name;
        existing.Active = worker.Active;
        existing.Color = worker.Color;

        // Replace rather than reconcile: a schedule row carries no identity beyond its
        // day and times, so there is nothing worth matching up (same call as the
        // barbershop's own opening hours in AvailabilityService).
        db.WorkerSchedules.RemoveRange(existing.Schedules);
        await db.SaveChangesAsync();

        foreach (var row in ToRows(schedule))
        {
            row.WorkerId = existing.Id;
            db.WorkerSchedules.Add(row);
        }

        await db.SaveChangesAsync();
        await transaction.CommitAsync();
    }

    public async Task ChangeStatus(int workerId, bool active)
    {
        await using var db = await factory.CreateDbContextAsync();

        var worker = await db.Workers.FirstOrDefaultAsync(t => t.Id == workerId);
        if (worker is null) return;

        worker.Active = active;
        await db.SaveChangesAsync();
    }

    public async Task<DeleteResult> Delete(int workerId)
    {
        await using var db = await factory.CreateDbContextAsync();

        var worker = await db.Workers
            .Include(t => t.Schedules)
            .FirstOrDefaultAsync(t => t.Id == workerId);
        if (worker is null) return DeleteResult.Deleted;

        bool used = await db.Appointments.AnyAsync(c => c.WorkerId == workerId)
                     || await db.Sales.AnyAsync(v => v.WorkerId == workerId);

        if (used)
        {
            worker.Active = false;
            await db.SaveChangesAsync();
            return DeleteResult.Deactivated;
        }

        // Her schedule has no life of its own, so it goes with her rather than being
        // left behind pointing at nobody.
        db.WorkerSchedules.RemoveRange(worker.Schedules);
        db.Workers.Remove(worker);
        await db.SaveChangesAsync();
        return DeleteResult.Deleted;
    }

    private static List<WorkerSchedule> ToRows(
        IReadOnlyDictionary<Weekday, List<(TimeOnly start, TimeOnly fi)>> schedule)
        => [.. schedule
            .SelectMany(pair => pair.Value.Select(interval => new WorkerSchedule
            {
                Weekday = pair.Key,
                StartTime = interval.start,
                EndTime = interval.fi
            }))];
}
