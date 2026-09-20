using EvaGest.Data;
using EvaGest.Models;
using Microsoft.EntityFrameworkCore;

using Serilog;

namespace EvaGest.Services;

public class AppointmentService(IDbContextFactory<ShopDbContext> factory, IAppointmentChangeNotifier? notifier = null)
    : IAppointmentService
{
    public async Task<List<Appointment>> GetByDay(DateOnly date)
    {
        await using var db = await factory.CreateDbContextAsync();
        return await Query(db).Where(c => c.Date == date).OrderBy(c => c.Time).ToListAsync();
    }

    public async Task<List<Appointment>> GetByRange(DateOnly from, DateOnly to)
    {
        await using var db = await factory.CreateDbContextAsync();
        return await Query(db)
            .Where(c => c.Date >= from && c.Date <= to)
            .OrderBy(c => c.Date).ThenBy(c => c.Time)
            .ToListAsync();
    }

    public async Task<List<Appointment>> GetByClient(int clientId)
    {
        await using var db = await factory.CreateDbContextAsync();
        return await Query(db)
            .Where(c => c.ClientId == clientId)
            .OrderByDescending(c => c.Date).ThenByDescending(c => c.Time)
            .ToListAsync();
    }

    public async Task<Appointment?> GetById(int id)
    {
        await using var db = await factory.CreateDbContextAsync();
        return await Query(db).FirstOrDefaultAsync(c => c.Id == id);
    }

    public async Task<List<Appointment>> GetCompletedWithoutSale()
    {
        await using var db = await factory.CreateDbContextAsync();
        return await Query(db)
            .Where(c => c.Status == AppointmentStatus.Completed && c.Sale == null)
            .OrderBy(c => c.Date).ThenBy(c => c.Time)
            .ToListAsync();
    }

    public async Task<List<Appointment>> GetOverduePending(DateTime asOf)
    {
        await using var db = await factory.CreateDbContextAsync();
        var since = DateOnly.FromDateTime(asOf.AddDays(-1));
        var today = DateOnly.FromDateTime(asOf);

        var candidates = await Query(db)
            .Where(c => c.Status == AppointmentStatus.Pending && c.Date >= since && c.Date <= today)
            .ToListAsync();

        return candidates
            .Where(c => c.Date.ToDateTime(c.Time).AddHours(1) <= asOf)
            .OrderBy(c => c.Date).ThenBy(c => c.Time)
            .ToList();
    }

    public async Task<int> CountByStatus(DateOnly from, DateOnly to, AppointmentStatus status)
    {
        await using var db = await factory.CreateDbContextAsync();
        return await db.Appointments.CountAsync(c => c.Date >= from && c.Date <= to && c.Status == status);
    }

    public async Task<int> Create(Appointment appointment)
    {
        await using var db = await factory.CreateDbContextAsync();
        db.Appointments.Add(appointment);
        await db.SaveChangesAsync();
        Log.Information("Appointment {AppointmentId} created for {Date} {Time}",
            appointment.Id, appointment.Date, appointment.Time);

        // Create was the one change that did not notify, so an appointment booked for
        // earlier today did not reach the shell's overdue check until the next timer tick.
        notifier?.NotifyChanged();
        return appointment.Id;
    }

    public async Task Update(Appointment appointment)
    {
        await using var db = await factory.CreateDbContextAsync();
        db.Appointments.Update(appointment);
        await db.SaveChangesAsync();
        Log.Information("Appointment {AppointmentId} updated to {Date} {Time}",
            appointment.Id, appointment.Date, appointment.Time);
        notifier?.NotifyChanged();
    }

    public async Task<bool> ChangeStatus(int appointmentId, AppointmentStatus newStatus)
    {
        await using var db = await factory.CreateDbContextAsync();
        var appointment = await db.Appointments.Include(c => c.Sale).FirstAsync(c => c.Id == appointmentId);

        if (newStatus == AppointmentStatus.Pending && appointment.Sale is { Status: SaleStatus.Active })
            return false;

        appointment.Status = newStatus;
        await db.SaveChangesAsync();

        // Cancelling an appointment is a business event CU-04 requires to be traceable.
        Log.Information("Appointment {AppointmentId} status changed to {Status}",
            appointmentId, newStatus);
        notifier?.NotifyChanged();
        return true;
    }

    public async Task<DeleteResult> Delete(int appointmentId)
    {
        await using var db = await factory.CreateDbContextAsync();

        var appointment = await db.Appointments.FirstOrDefaultAsync(c => c.Id == appointmentId);
        if (appointment is null) return DeleteResult.Deleted;

        // The sale's appointment_id is SET NULL, so this would succeed and quietly cut the sale
        // loose from the appointment it was charged for. Refuse instead and say why.
        if (await db.Sales.AnyAsync(v => v.AppointmentId == appointmentId))
            return DeleteResult.Blocked;

        db.Appointments.Remove(appointment);
        await db.SaveChangesAsync();
        Log.Information("Appointment {AppointmentId} deleted", appointmentId);
        notifier?.NotifyChanged();
        return DeleteResult.Deleted;
    }

    private static IQueryable<Appointment> Query(ShopDbContext db)
        => db.Appointments.AsNoTracking()
            .Include(c => c.Client)
            .Include(c => c.Service)
            .Include(c => c.Worker);
}
