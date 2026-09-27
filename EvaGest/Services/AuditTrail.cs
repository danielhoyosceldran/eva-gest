using System.Text.Json;
using EvaGest.Data;
using EvaGest.Models;

namespace EvaGest.Services;

/// <summary>
/// Writes <see cref="AuditEntry"/> rows. The entry is added to the caller's context and
/// saved by the caller's own SaveChanges, so the change and its record commit together
/// or not at all.
/// </summary>
public static class AuditTrail
{
    public const string SaleEntity = "Sale";
    public const string CashMovementEntity = "CashMovement";

    /// <summary>A sale's full state as the audit keeps it: every field an edit can
    /// touch, and every line with its frozen price and VAT rate.</summary>
    public record SaleSnapshot(
        DateOnly Date, TimeOnly Time,
        int? ClientId, string? GuestName, string? GuestPhone,
        int? WorkerId, int PaymentMethodId, int? AppointmentId,
        string Status, string VatMode,
        int BaseCents, int VatCents, int TotalCents,
        string? Notes,
        List<SaleLineSnapshot> Lines);

    public record SaleLineSnapshot(
        string Description, int Quantity, int UnitPriceCents, int VatBp, int AmountCents,
        int? ServiceId, int? ProductId);

    /// <summary>Takes the snapshot from a sale whose lines are loaded.</summary>
    public static string Snapshot(Sale sale)
        => JsonSerializer.Serialize(new SaleSnapshot(
            sale.Date, sale.Time,
            sale.ClientId, sale.GuestName, sale.GuestPhone,
            sale.WorkerId, sale.PaymentMethodId, sale.AppointmentId,
            sale.Status.ToString(), sale.VatMode.ToString(),
            sale.BaseCents, sale.VatCents, sale.TotalCents,
            sale.Notes,
            [.. sale.Lines.Select(l => new SaleLineSnapshot(
                l.Description, l.Quantity, l.UnitPriceCents, l.VatBp, l.AmountCents,
                l.ServiceId, l.ProductId))]));

    /// <summary>Queues one audit row on <paramref name="db"/>; the caller saves it.</summary>
    public static void Record(ShopDbContext db, string entity, int entityId, string action,
        string? before, string? after)
        => db.AuditEntries.Add(new AuditEntry
        {
            AtUtc = DateTime.UtcNow,
            Entity = entity,
            EntityId = entityId,
            Action = action,
            Before = before,
            After = after
        });
}
