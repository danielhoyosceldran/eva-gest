using System.Globalization;
using System.IO;
using System.Reflection;
using EvaGest.Data;
using EvaGest.Models;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore.Migrations;

using Serilog;

namespace EvaGest.Services;

/// <summary>
/// Phase 9. The backup folder itself is the source of truth: each file's name encodes
/// its timestamp and whether it was automatic or manual, so no extra table is needed.
/// </summary>
/// <param name="now">
/// How the service reads the clock. Defaulted rather than injected, so the container
/// still resolves it from AppPaths and ISettingsService alone; the tests pass a fixed
/// moment, because pinning the configured hour at "23:59" and asserting the backup had
/// not run yet was true for all but the one minute a day the suite started in it.
/// </param>
public class BackupService(
    AppPaths paths, ISettingsService settings, Func<DateTime>? now = null) : IBackupService
{
    private readonly Func<DateTime> _now = now ?? (() => DateTime.Now);

    // Millisecond precision matters: a restore always takes a safety copy right before
    // overwriting the database, and a manual backup can follow another within the same
    // second — second-only precision made those two collide on the same filename.
    private const string Format = "yyyyMMdd_HHmmssfff";

    // Fallbacks only, for a database whose settings row is missing or unreadable.
    // The real values come from Configuració (esquema-bbdd 2.14).
    private const int FallbackRetention = 15;
    private static readonly TimeOnly FallbackTime = new(20, 0);

    public Task<List<BackupInfo>> ListAll()
    {
        Directory.CreateDirectory(paths.BackupsFolder);

        var backups = Directory.GetFiles(paths.BackupsFolder, "*.db")
            .Select(ReadBackup)
            .Where(c => c is not null)
            .Select(c => c!)
            .OrderByDescending(c => c.Date)
            .ToList();

        return Task.FromResult(backups);
    }

    public async Task<BackupInfo> MakeManualBackup() => await Copy(isAutomatic: false);

    public async Task<BackupInfo?> RunAutomaticBackupIfDue()
    {
        // Checked at startup rather than by a timer, because the machine is usually off
        // at the configured hour (CU-11). Waiting for the hour to pass means the copy
        // lands on the first launch after it, which is the intended behaviour.
        if (TimeOnly.FromDateTime(_now()) < await BackupTime()) return null;

        var backups = await ListAll();
        bool alreadyDoneToday = backups.Any(c => c.IsAutomatic && c.Date.Date == _now().Date);
        if (alreadyDoneToday) return null;

        var backupFile = await Copy(isAutomatic: true);

        await settings.Save(ConfigKeys.LastAutomaticBackup,
            DateOnly.FromDateTime(backupFile.Date).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture));

        return backupFile;
    }

    public async Task DeleteOldBackups()
    {
        int toKeep = Math.Max(1, await settings.GetInt(
            ConfigKeys.BackupsToKeep, FallbackRetention));

        var backups = await ListAll(); // newest first
        var retained = Retained(backups, toKeep);
        foreach (var old in backups.Where(b => !retained.Contains(b)))
        {
            // Caught per file rather than round the loop: one copy held open by an
            // antivirus scan or an Explorer preview used to abort the whole prune and,
            // worse, propagate out of Copy — so a backup that had already been written
            // successfully was reported to the user as a failure.
            try
            {
                File.Delete(old.Path);
                Log.Information("Old backup deleted: {BackupPath}", old.Path);
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "Could not delete old backup {BackupPath}", old.Path);
            }
        }
    }

    /// <summary>
    /// Which backups survive a prune. Keeping only the newest N meant that, with the
    /// default of 15, nothing older than about two weeks existed: damage noticed late
    /// (a wrong restore, a corrupted file, a sale deleted by mistake) was already in
    /// every copy. So on top of the newest <paramref name="toKeep"/>, the newest copy of
    /// each of the last 12 months that have one is kept, and the newest copy of every
    /// year, for good: at most a handful of extra files, and the books stay recoverable
    /// for as long as they have to be kept.
    /// </summary>
    /// <param name="backups">Every backup, newest first, as <see cref="ListAll"/> returns them.</param>
    public static HashSet<BackupInfo> Retained(IReadOnlyList<BackupInfo> backups, int toKeep)
    {
        var retained = backups.Take(Math.Max(1, toKeep)).ToHashSet();

        // Newest first, so the first of each group is that period's newest copy.
        foreach (var monthly in backups
                     .GroupBy(b => (b.Date.Year, b.Date.Month))
                     .Take(12))
            retained.Add(monthly.First());

        foreach (var yearly in backups.GroupBy(b => b.Date.Year))
            retained.Add(yearly.First());

        return retained;
    }

    private async Task<TimeOnly> BackupTime()
        => TimeOnly.TryParse(await settings.Get(ConfigKeys.BackupTime),
                             CultureInfo.InvariantCulture, out var time)
            ? time
            : FallbackTime;

    public async Task Restore(string backupPath)
    {
        // A copy that SQLite itself cannot read must never replace the live database:
        // checked before anything is touched, so a damaged file leaves the shop's data
        // exactly as it was.
        await Task.Run(() =>
        {
            EnsureReadable(backupPath);
            EnsureNotNewer(backupPath);
        });

        // Back up the CURRENT state first, so an accidental restore can still be
        // undone (CU-09b) — this must happen before the file is overwritten. Not pruned
        // yet: with the folder at its limit, pruning here deleted the oldest backup,
        // which is the one being restored whenever the user picked the oldest.
        // Not verified either: a damaged live database is the commonest reason to
        // restore, and refusing its safety copy would refuse the restore with it. A copy
        // of exactly what was there is still worth keeping.
        await Copy(isAutomatic: false, prune: false, verify: false);

        // Written through SQLite, not over the file. The database runs in WAL mode and
        // the app keeps pooled connections open, so a File.Copy over barberia.db left
        // the live -wal file behind: the next read replayed it on top of the restored
        // pages, and a restore could come out unchanged or as a mix of the two.
        await Task.Run(() => Snapshot(backupPath, paths.DbPath));

        // The settings live inside the file that was just replaced, so anything cached
        // in memory now describes a database that no longer exists.
        settings.InvalidateCache();

        // The whole database has just been overwritten. Without this line the log has
        // nothing between "Application started" and "Application closed" to explain
        // why a day's work is missing (CU-09b).
        Log.Information("Database restored from backup {BackupPath}", backupPath);

        await DeleteOldBackups();
    }

    /// <param name="verify">
    /// Reads the new file back through <see cref="EnsureReadable"/> before calling it a
    /// backup. Without it a copy truncated by a full disk, or damaged by an antivirus
    /// holding it mid-write, was reported as taken and only found out on the day it had
    /// to be restored. A copy that fails is deleted and the call throws
    /// <see cref="InvalidDataException"/>, so it never sits in the list looking usable.
    /// </param>
    private async Task<BackupInfo> Copy(bool isAutomatic, bool prune = true, bool verify = true)
    {
        Directory.CreateDirectory(paths.BackupsFolder);

        var now = _now();
        string suffix = isAutomatic ? "auto" : "manual";
        string name = $"{now.ToString(Format, CultureInfo.InvariantCulture)}_{suffix}.db";
        string destination = Path.Combine(paths.BackupsFolder, name);

        // Never overwrite: two backups landing on the same name would lose one.
        if (File.Exists(destination))
            throw new IOException($"A backup named {name} already exists.");

        // Through SQLite's online backup, not File.Copy: in WAL mode the most recent
        // commits live in barberia.db-wal until a checkpoint, so copying the main file
        // alone produced a backup missing the day's latest sales (or, on a young
        // database, missing the tables altogether).
        await Task.Run(() =>
        {
            Snapshot(paths.DbPath, destination);
            UseRollbackJournal(destination);
            if (verify) VerifyOrDiscard(destination);
        });
        Log.Information("{BackupKind} backup taken: {BackupPath}",
            isAutomatic ? "Automatic" : "Manual", destination);

        if (prune) await DeleteOldBackups();

        return ReadBackup(destination)!;
    }

    /// <summary>
    /// Copies one whole database onto another with SQLite's online backup API, which
    /// reads committed pages from the WAL as well as the main file and writes them under
    /// SQLite's own locking, so connections already open on the destination see the
    /// result. Pooling is off on both ends: a pooled handle would keep a backup file
    /// open, and pruning or restoring it later would fail on the lock.
    /// Synchronous and proportional to the database size, so callers run it (and
    /// EnsureReadable) through Task.Run: awaited from the UI thread, it froze the window.
    /// </summary>
    private static void Snapshot(string sourcePath, string destinationPath)
    {
        using var source = new SqliteConnection($"Data Source={sourcePath};Pooling=False");
        using var destination = new SqliteConnection($"Data Source={destinationPath};Pooling=False");
        source.Open();
        destination.Open();
        source.BackupDatabase(destination);
    }

    /// <summary>
    /// Turns a fresh backup into a plain single-file database. The backup API copies
    /// page 1 as it is, so a copy of the WAL-mode live database came out marked WAL, and
    /// every later open of it (the check before a restore) could create -wal/-shm side
    /// files that pruning, which only deletes *.db, would never remove. Restoring is
    /// unaffected: the backup API keeps the live database's own WAL mode.
    /// </summary>
    private static void UseRollbackJournal(string path)
    {
        using var connection = new SqliteConnection($"Data Source={path};Pooling=False");
        connection.Open();
        using var command = connection.CreateCommand();
        command.CommandText = "PRAGMA journal_mode=DELETE";
        command.ExecuteNonQuery();
    }

    /// <summary>
    /// Checks a backup just written and deletes it if it is not sound, then rethrows.
    /// The delete is best effort: if the file cannot even be removed it is logged, and
    /// the restore check still refuses it should anyone pick it later.
    /// </summary>
    private static void VerifyOrDiscard(string path)
    {
        try
        {
            EnsureReadable(path);
        }
        catch (InvalidDataException ex)
        {
            Log.Error(ex, "Backup {BackupPath} failed its check after writing and is discarded", path);
            try
            {
                File.Delete(path);
            }
            catch (Exception deleteEx)
            {
                Log.Warning(deleteEx, "Could not delete the unsound backup {BackupPath}", path);
            }
            throw;
        }
    }

    /// <summary>
    /// The id of every migration compiled into this build, read from the
    /// <see cref="MigrationAttribute"/> EF puts on each one. A backup's history listing an
    /// id outside this set came from a later version of the app.
    /// </summary>
    private static readonly HashSet<string> KnownMigrations =
        typeof(ShopDbContext).Assembly.GetTypes()
            .Select(t => t.GetCustomAttribute<MigrationAttribute>()?.Id)
            .OfType<string>()
            .ToHashSet(StringComparer.Ordinal);

    /// <summary>
    /// Throws <see cref="BackupFromNewerVersionException"/> when the backup's migration
    /// history names a migration this build does not have. Without it a copy made after
    /// an update could be restored by the older version still installed (or reinstalled)
    /// on this PC, which would then run against a schema it does not know; and its next
    /// startup migration would not put that right, since nothing is pending as far as it
    /// can see. Reads the first column of the history table, which is where EF keeps the
    /// migration id.
    /// </summary>
    private static void EnsureNotNewer(string path)
    {
        var unknown = new List<string>();
        using (var connection = new SqliteConnection($"Data Source={path};Mode=ReadOnly;Pooling=False"))
        {
            connection.Open();
            using var command = connection.CreateCommand();
            command.CommandText = "SELECT * FROM __EFMigrationsHistory";
            using var reader = command.ExecuteReader();
            while (reader.Read())
                if (reader.GetValue(0) is string id && !KnownMigrations.Contains(id))
                    unknown.Add(id);
        }

        if (unknown.Count > 0)
            throw new BackupFromNewerVersionException(path, unknown);
    }

    /// <summary>Throws unless SQLite reads the file as a sound EvaGest database.</summary>
    private static void EnsureReadable(string path)
    {
        string result;
        try
        {
            using var connection = new SqliteConnection($"Data Source={path};Mode=ReadOnly;Pooling=False");
            connection.Open();
            using var command = connection.CreateCommand();
            command.CommandText = "PRAGMA quick_check";
            result = command.ExecuteScalar() as string ?? string.Empty;

            // quick_check also says "ok" for an empty database or another program's. Every
            // EvaGest database has EF's migration history table, so a file without it is
            // not one of ours, however sound.
            command.CommandText =
                "SELECT count(*) FROM sqlite_master WHERE type = 'table' AND name = '__EFMigrationsHistory'";
            if (result == "ok" && Convert.ToInt32(command.ExecuteScalar()) == 0)
                result = "not an EvaGest database";
        }
        catch (SqliteException ex)
        {
            throw new InvalidDataException($"The backup {path} is not a readable database.", ex);
        }

        if (result != "ok")
            throw new InvalidDataException($"The backup {path} failed its integrity check: {result}");
    }

    private static BackupInfo? ReadBackup(string path)
    {
        string name = Path.GetFileNameWithoutExtension(path);
        var parts = name.Split('_');
        if (parts.Length < 3) return null;

        string dateText = $"{parts[0]}_{parts[1]}";
        if (!DateTime.TryParseExact(dateText, Format, CultureInfo.InvariantCulture,
                DateTimeStyles.None, out var date))
            return null;

        bool isAutomatic = parts[2] == "auto";
        long size = new FileInfo(path).Length;
        return new BackupInfo(path, date, isAutomatic, size);
    }
}
