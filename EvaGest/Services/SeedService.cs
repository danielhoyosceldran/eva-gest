using EvaGest.Data;
using EvaGest.Models;
using Microsoft.EntityFrameworkCore;

namespace EvaGest.Services;

/// <summary>
/// Without this, a fresh database has no payment method, and a sale cannot be saved
/// without one — the application would install and then refuse to take any money.
///
/// Deliberately idempotent rather than a migration seed: settings keys get added as
/// features land, and an existing database has to pick up the new ones too. Payment
/// methods are only seeded when the table is completely empty, so a user who deleted
/// "Bizum" does not find it back after every restart.
/// </summary>
public class SeedService(
    IDbContextFactory<ShopDbContext> factory,
    ISettingsService settings) : ISeedService
{
    /// <summary>Defaults from esquema-bbdd 2.14. Booleans are stored as 0/1.</summary>
    private static readonly (string key, string value)[] DefaultSettings =
    [
        (ConfigKeys.ShopName, ""),
        (ConfigKeys.ShopAddress, ""),
        (ConfigKeys.ShopPhone, ""),
        (ConfigKeys.DefaultVatBp, "2100"),
        (ConfigKeys.CurrentVatMode, nameof(VatMode.Included)),
        (ConfigKeys.ApplyVatToTill, "0"),
        (ConfigKeys.DefaultAppointmentDurationMin, "30"),
        (ConfigKeys.BackupTime, "20:00"),
        (ConfigKeys.BackupsToKeep, "15"),
        (ConfigKeys.LastAutomaticBackup, ""),
        (ConfigKeys.ShowGuestNotice, "1"),
        (ConfigKeys.ConfirmationSound, "1"),
        (ConfigKeys.Language, nameof(Models.Language.Catalan)),
    ];

    /// <summary>Seeded once, then owned by the user: they are rows she can rename, not
    /// interface text, so they are not translated when the language changes.</summary>
    private static readonly string[] DefaultMethods = ["Efectiu", "Targeta", "Bizum"];

    public async Task Seed()
    {
        await using var db = await factory.CreateDbContextAsync();

        var alreadyPresent = await db.Settings.AsNoTracking()
            .Select(c => c.Key)
            .ToListAsync();

        var whatToSet = DefaultSettings
            .Where(p => !alreadyPresent.Contains(p.key))
            .Select(p => new SettingItem { Key = p.key, Value = p.value })
            .ToList();

        if (whatToSet.Count > 0)
        {
            db.Settings.AddRange(whatToSet);
            await db.SaveChangesAsync();
            settings.InvalidateCache();
        }

        if (!await db.PaymentMethods.AnyAsync())
        {
            db.PaymentMethods.AddRange(
                DefaultMethods.Select(name => new PaymentMethod { Name = name, Active = true }));
            await db.SaveChangesAsync();
        }
    }
}
