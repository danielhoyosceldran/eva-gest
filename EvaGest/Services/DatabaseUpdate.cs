using System.IO;
using EvaGest.Data;
using Microsoft.EntityFrameworkCore;
using Serilog;

namespace EvaGest.Services;

/// <summary>
/// The step of startup (CU-12) that runs right before the migrations. Kept out of
/// App.xaml.cs so the decision of when to copy, and what happens when the copy fails,
/// can be tested against a real database.
/// </summary>
public static class DatabaseUpdate
{
    /// <summary>
    /// Copies the database before a new version's migrations change it. The daily
    /// automatic backup runs only after the migrations, so on the first start of an
    /// update the newest copy of the old data could be a day old, or not exist at all.
    /// Skipped on a first run (no file yet, nothing to lose) and when nothing is pending.
    /// </summary>
    /// <returns>The copy taken, or null when none was needed.</returns>
    /// <exception cref="BackupBeforeUpdateFailedException">The copy could not be written.
    /// The caller must then not migrate: changing the schema with no way back is what
    /// this step exists to prevent.</exception>
    public static async Task<BackupInfo?> BackupBeforeMigrating(
        ShopDbContext db, string dbPath, IBackupService backups)
    {
        // Checked before asking EF anything: opening the connection would create the file.
        if (!File.Exists(dbPath)) return null;

        var pending = (await db.Database.GetPendingMigrationsAsync()).ToList();
        if (pending.Count == 0) return null;

        try
        {
            var backup = await backups.MakeBackupBeforeUpdate();
            Log.Information("Backup {BackupPath} taken before applying migrations {Migrations}",
                backup.Path, pending);
            return backup;
        }
        catch (Exception ex)
        {
            throw new BackupBeforeUpdateFailedException(ex);
        }
    }
}

/// <summary>The copy <see cref="DatabaseUpdate.BackupBeforeMigrating"/> takes could not
/// be written, so the database must be left unmigrated.</summary>
public sealed class BackupBeforeUpdateFailedException(Exception inner)
    : Exception("Could not back up the database before migrating it.", inner);
