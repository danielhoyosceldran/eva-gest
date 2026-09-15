using EvaGest.Data;
using EvaGest.Models;
using Microsoft.EntityFrameworkCore;

namespace EvaGest.Services;

/// <summary>
/// The least obvious calculation in the project (capa-mvvm 4.4, casos-us CU-01b),
/// isolated in its own service precisely because it is the one worth testing hardest.
/// Never blocks saving: it only reports what the UI should warn about.
/// </summary>
public class AvailabilityService(IDbContextFactory<ShopDbContext> factory) : IAvailabilityService
{
    public async Task<AvailabilityResult> Check(
        DateOnly date, TimeOnly time, int durationMin,
        int? workerId, int? excludedAppointmentId = null)
    {
        await using var db = await factory.CreateDbContextAsync();

        var closedDay = await db.ClosedDays.AsNoTracking().FirstOrDefaultAsync(d => d.Date == date);
        bool outsideSchedule = !await IsInsideSchedule(db, date, time, durationMin);

        var overlapping = await db.Appointments.AsNoTracking()
            .Where(c => c.Date == date
                     && c.Status != AppointmentStatus.Cancelled && c.Status != AppointmentStatus.NoShow
                     && (excludedAppointmentId == null || c.Id != excludedAppointmentId))
            .ToListAsync();

        // Strict overlap on both ends: two appointments that merely touch (one starts
        // exactly when the other ends) do not conflict.
        bool Overlaps(Appointment c) => time < ClampedEnd(c.Time, c.DurationMin) && ClampedEnd(time, durationMin) > c.Time;

        if (workerId is int id)
        {
            int workerAppointments = overlapping.Count(c => c.WorkerId == id && Overlaps(c));
            return new AvailabilityResult(
                HasOverlap: workerAppointments > 0,
                OutsideSchedule: outsideSchedule,
                ClosedDay: closedDay is not null,
                ClosedDayReason: closedDay?.Reason,
                AvailableWorkers: 1,
                ExistingAppointments: workerAppointments);
        }

        var available = await AvailableWorkers(date, time, durationMin);
        int existingAppointments = overlapping.Count(Overlaps);

        return new AvailabilityResult(
            HasOverlap: existingAppointments >= available.Count,
            OutsideSchedule: outsideSchedule,
            ClosedDay: closedDay is not null,
            ClosedDayReason: closedDay?.Reason,
            AvailableWorkers: available.Count,
            ExistingAppointments: existingAppointments);
    }

    public async Task<List<Worker>> AvailableWorkers(DateOnly date, TimeOnly time, int durationMin)
    {
        await using var db = await factory.CreateDbContextAsync();
        var day = ToWeekday(date);
        var endTime = ClampedEnd(time, durationMin);

        return await db.Workers.AsNoTracking()
            .Where(t => t.Active && t.Schedules.Any(h =>
                h.Weekday == day && h.StartTime <= time && h.EndTime >= endTime))
            .ToListAsync();
    }

    public async Task<bool> IsDayOpen(DateOnly date)
    {
        await using var db = await factory.CreateDbContextAsync();
        var day = ToWeekday(date);
        return await db.ShopSchedule.AsNoTracking().AnyAsync(h => h.Weekday == day);
    }

    public async Task<List<(TimeOnly start, TimeOnly fi)>> OpeningIntervals(DateOnly date)
    {
        await using var db = await factory.CreateDbContextAsync();
        var day = ToWeekday(date);
        var intervals = await db.ShopSchedule.AsNoTracking()
            .Where(h => h.Weekday == day)
            .OrderBy(h => h.OpeningTime)
            .Select(h => new { h.OpeningTime, h.ClosingTime })
            .ToListAsync();

        return intervals.Select(f => (f.OpeningTime, f.ClosingTime)).ToList();
    }

    public async Task<Dictionary<Weekday, List<(TimeOnly start, TimeOnly fi)>>> WeeklyIntervals()
    {
        await using var db = await factory.CreateDbContextAsync();
        var rows = await db.ShopSchedule.AsNoTracking()
            .OrderBy(h => h.Weekday).ThenBy(h => h.OpeningTime)
            .Select(h => new { h.Weekday, h.OpeningTime, h.ClosingTime })
            .ToListAsync();

        return rows
            .GroupBy(h => h.Weekday)
            .ToDictionary(g => g.Key, g => g.Select(h => (h.OpeningTime, h.ClosingTime)).ToList());
    }

    public async Task SaveWeeklySchedule(
        IReadOnlyDictionary<Weekday, List<(TimeOnly start, TimeOnly fi)>> schedule)
    {
        await using var db = await factory.CreateDbContextAsync();
        await using var transaction = await db.Database.BeginTransactionAsync();

        // Replace rather than reconcile: the rows carry no identity of their own beyond
        // the day and the times, so there is nothing worth preserving.
        db.ShopSchedule.RemoveRange(await db.ShopSchedule.ToListAsync());
        await db.SaveChangesAsync();

        foreach (var (day, intervals) in schedule)
            foreach (var (start, fi) in intervals)
                db.ShopSchedule.Add(new ShopSchedule
                {
                    Weekday = day,
                    OpeningTime = start,
                    ClosingTime = fi
                });

        await db.SaveChangesAsync();
        await transaction.CommitAsync();
    }

    public async Task<Dictionary<DateOnly, string?>> ClosedDaysIn(DateOnly from, DateOnly to)
    {
        await using var db = await factory.CreateDbContextAsync();
        var closed = await db.ClosedDays.AsNoTracking()
            .Where(d => d.Date >= from && d.Date <= to)
            .ToListAsync();

        return closed.ToDictionary(d => d.Date, d => d.Reason);
    }

    public async Task<List<ClosedDay>> ClosedDays()
    {
        await using var db = await factory.CreateDbContextAsync();
        return await db.ClosedDays.AsNoTracking().OrderBy(d => d.Date).ToListAsync();
    }

    public async Task AddClosedDay(DateOnly date, string? reason)
    {
        await using var db = await factory.CreateDbContextAsync();

        string? cleaned = string.IsNullOrWhiteSpace(reason) ? null : reason.Trim();
        var existing = await db.ClosedDays.FirstOrDefaultAsync(d => d.Date == date);

        if (existing is null) db.ClosedDays.Add(new ClosedDay { Date = date, Reason = cleaned });
        else existing.Reason = cleaned;

        await db.SaveChangesAsync();
    }

    public async Task DeleteClosedDay(int id)
    {
        await using var db = await factory.CreateDbContextAsync();

        var day = await db.ClosedDays.FirstOrDefaultAsync(d => d.Id == id);
        if (day is null) return;

        db.ClosedDays.Remove(day);
        await db.SaveChangesAsync();
    }

    private async Task<bool> IsInsideSchedule(ShopDbContext db, DateOnly date, TimeOnly time, int durationMin)
    {
        var day = ToWeekday(date);
        var endTime = ClampedEnd(time, durationMin);

        return await db.ShopSchedule.AsNoTracking()
            .AnyAsync(h => h.Weekday == day && h.OpeningTime <= time && h.ClosingTime >= endTime);
    }

    /// <summary>Monday = Mon, ..., Sunday = Sun, matching the enum declared in models-domini.</summary>
    private static Weekday ToWeekday(DateOnly date) => (Weekday)(((int)date.DayOfWeek + 6) % 7);

    /// <summary>Start time plus a duration, clamped to the last moment of the day.
    /// TimeOnly.AddMinutes wraps silently past midnight (23:30 + 90 min would read back
    /// as 01:00), which broke both the overlap check and the opening-hours check for a
    /// late appointment with a long enough duration. TimeOnly cannot represent 24:00
    /// either, so anything that would cross midnight is treated as running to the end
    /// of the day instead of wrapping to an early-morning time.</summary>
    private static TimeOnly ClampedEnd(TimeOnly time, int durationMin)
        => time.Hour * 60 + time.Minute + durationMin >= 24 * 60
            ? new TimeOnly(23, 59, 59)
            : time.AddMinutes(durationMin);
}
