using EvaGest.Data;
using EvaGest.Models;
using Microsoft.EntityFrameworkCore;
using Serilog;

namespace EvaGest.Services;

/// <summary>
/// The core of the business (Phase 6). Freezes price, VAT rate and the VAT mode in
/// force onto every line and breakdown row at save time (decision 6.4): past sales
/// must never move when the catalogue or the global VAT mode changes later.
/// </summary>
public class SaleService(
    IDbContextFactory<ShopDbContext> factory,
    ISettingsService settings,
    IAppointmentChangeNotifier? appointmentChanges = null) : ISaleService
{
    /// <summary>
    /// The mode in force right now, read at save time and then frozen onto the sale.
    /// Changing it in Configuració must never move a sale that is already recorded
    /// (decision 6.4), which is exactly why this is read per save and stored per row.
    /// Shared with the sale dialog's live footer through
    /// <see cref="VatModeSettings.CurrentVatMode"/>, so the two cannot disagree.
    /// </summary>
    private Task<VatMode> CurrentVatMode() => settings.CurrentVatMode();

    public async Task<List<Sale>> Search(SalesFilter filter)
    {
        await using var db = await factory.CreateDbContextAsync();
        var query = Query(db);

        if (filter.From is DateOnly from) query = query.Where(v => v.Date >= from);
        if (filter.To is DateOnly to) query = query.Where(v => v.Date <= to);
        if (filter.ClientId is int clientId) query = query.Where(v => v.ClientId == clientId);
        if (filter.PaymentMethodId is int methodId) query = query.Where(v => v.PaymentMethodId == methodId);
        if (filter.WorkerId is int workerId) query = query.Where(v => v.WorkerId == workerId);
        if (filter.Status is SaleStatus status) query = query.Where(v => v.Status == status);
        if (filter.ServiceId is int serviceId) query = query.Where(v => v.Lines.Any(l => l.ServiceId == serviceId));
        if (filter.ProductId is int productId) query = query.Where(v => v.Lines.Any(l => l.ProductId == productId));

        return await query.OrderByDescending(v => v.Date).ThenByDescending(v => v.Time).ToListAsync();
    }

    public async Task<Sale?> GetById(int id)
    {
        await using var db = await factory.CreateDbContextAsync();
        return await Query(db).FirstOrDefaultAsync(v => v.Id == id);
    }

    public async Task<List<Sale>> GetByClient(int clientId)
    {
        await using var db = await factory.CreateDbContextAsync();
        return await Query(db)
            .Where(v => v.ClientId == clientId)
            .OrderByDescending(v => v.Date).ThenByDescending(v => v.Time)
            .ToListAsync();
    }

    public async Task<int> Create(Sale sale, List<SaleLine> lines)
    {
        FreezeTotals(sale, lines, await CurrentVatMode());

        await using var db = await factory.CreateDbContextAsync();
        sale.Lines = lines;
        db.Sales.Add(sale);
        await db.SaveChangesAsync();

        if (sale.AppointmentId is int appointmentId)
        {
            var appointment = await db.Appointments.FirstAsync(c => c.Id == appointmentId);
            appointment.Status = AppointmentStatus.Completed;
            await db.SaveChangesAsync();
            Log.Information("Appointment {AppointmentId} completed by sale {SaleId}", appointmentId, sale.Id);

            // Charging an appointment closes it; without this the shell's overdue notice
            // kept naming it until the next timer tick.
            appointmentChanges?.NotifyChanged();
        }

        return sale.Id;
    }

    public async Task Update(Sale sale, List<SaleLine> lines)
    {
        await using var db = await factory.CreateDbContextAsync();

        var existing = await db.Sales
            .Include(v => v.Lines)
            .Include(v => v.Breakdowns)
            .FirstAsync(v => v.Id == sale.Id);

        // Recompute in the mode this sale was taken in, not the one in force today:
        // correcting a typo on an old ticket must not silently reinterpret its prices
        // because the shop switched to VAT-exclusive since (decision 6.4).
        FreezeTotals(sale, lines, existing.VatMode);

        db.SaleLines.RemoveRange(existing.Lines);
        db.SaleBreakdowns.RemoveRange(existing.Breakdowns);

        existing.Date = sale.Date;
        existing.Time = sale.Time;
        existing.ClientId = sale.ClientId;
        existing.GuestName = sale.GuestName;
        existing.GuestPhone = sale.GuestPhone;
        existing.WorkerId = sale.WorkerId;
        existing.PaymentMethodId = sale.PaymentMethodId;
        existing.Notes = sale.Notes;
        existing.BaseCents = sale.BaseCents;
        existing.VatCents = sale.VatCents;
        existing.TotalCents = sale.TotalCents;
        existing.Lines = lines;
        existing.Breakdowns = sale.Breakdowns;

        await db.SaveChangesAsync();

        // Audit trail required by CU-04: a sale's edit history must be traceable.
        Log.Information("Sale {SaleId} updated", sale.Id);
    }

    public async Task Void(int saleId)
    {
        await using var db = await factory.CreateDbContextAsync();
        var sale = await db.Sales.FirstAsync(v => v.Id == saleId);
        sale.Status = SaleStatus.Voided;
        await db.SaveChangesAsync();

        // Audit trail required by CU-04: a voided sale must be traceable in the log.
        Log.Information("Sale {SaleId} voided", saleId);
    }

    public async Task<DeleteResult> Delete(int saleId)
    {
        await using var db = await factory.CreateDbContextAsync();

        var sale = await db.Sales
            .Include(v => v.Lines)
            .Include(v => v.Breakdowns)
            .FirstOrDefaultAsync(v => v.Id == saleId);
        if (sale is null) return DeleteResult.Deleted;

        if (sale.Status == SaleStatus.Active)
        {
            sale.Status = SaleStatus.Voided;
            await db.SaveChangesAsync();
            Log.Information("Sale {SaleId} deactivated instead of deleted (was Active)", saleId);
            return DeleteResult.Deactivated;
        }

        // Lines and breakdown cascade in the schema; removing them here as well keeps
        // the intent visible and does not depend on the provider honouring it.
        db.SaleLines.RemoveRange(sale.Lines);
        db.SaleBreakdowns.RemoveRange(sale.Breakdowns);
        db.Sales.Remove(sale);
        await db.SaveChangesAsync();
        Log.Information("Sale {SaleId} permanently deleted", saleId);
        return DeleteResult.Deleted;
    }

    public async Task<Sale> PrepareFromAppointment(int appointmentId)
    {
        await using var db = await factory.CreateDbContextAsync();
        var appointment = await db.Appointments.AsNoTracking()
            .Include(c => c.Client)
            .Include(c => c.Service)
            .Include(c => c.Worker)
            .FirstOrDefaultAsync(c => c.Id == appointmentId)
            ?? throw new InvalidOperationException("The appointment does not exist.");

        // A cancelled or no-show appointment can never carry a sale (F-05).
        if (appointment.Status is AppointmentStatus.Cancelled or AppointmentStatus.NoShow)
            throw new InvalidOperationException(
                "A sale cannot be linked to a cancelled or no-show appointment.");

        return new Sale
        {
            Date = appointment.Date,
            Time = appointment.Time,
            ClientId = appointment.ClientId,
            GuestName = appointment.GuestName,
            GuestPhone = appointment.GuestPhone,
            WorkerId = appointment.WorkerId,
            AppointmentId = appointment.Id
        };
    }

    /// <summary>Computes the VAT breakdown and freezes it onto the sale and its lines.
    /// Never recomputed later: this is the one place BaseCents/VatCents/TotalCents are set.</summary>
    private static void FreezeTotals(Sale sale, List<SaleLine> lines, VatMode mode)
    {
        var broken = VatCalculator.Compute(lines, mode);
        sale.BaseCents = broken.BaseCents;
        sale.VatCents = broken.VatCents;
        sale.TotalCents = broken.TotalCents;
        sale.VatMode = mode;
        sale.Breakdowns = VatCalculator.ToBreakdownRows(lines, mode);
    }

    private static IQueryable<Sale> Query(ShopDbContext db)
        => db.Sales.AsNoTracking()
            .Include(v => v.Client)
            .Include(v => v.Worker)
            .Include(v => v.PaymentMethod)
            .Include(v => v.Lines);
}
