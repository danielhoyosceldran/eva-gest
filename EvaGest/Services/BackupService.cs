using System.Globalization;
using System.IO;
using EvaGest.Models;

using Serilog;

namespace EvaGest.Services;

/// <summary>
/// Phase 9. The backup folder itself is the source of truth: each file's name encodes
/// its timestamp and whether it was automatic or manual, so no extra table is needed.
/// </summary>
public class BackupService(AppPaths paths, ISettingsService settings) : IBackupService
{
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
        if (TimeOnly.FromDateTime(DateTime.Now) < await BackupTime()) return null;

        var backups = await ListAll();
        bool alreadyDoneToday = backups.Any(c => c.IsAutomatic && c.Date.Date == DateTime.Today);
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
        foreach (var old in backups.Skip(toKeep))
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

    private async Task<TimeOnly> BackupTime()
        => TimeOnly.TryParse(await settings.Get(ConfigKeys.BackupTime),
                             CultureInfo.InvariantCulture, out var time)
            ? time
            : FallbackTime;

    public async Task Restore(string backupPath)
    {
        // Back up the CURRENT state first, so an accidental restore can still be
        // undone (CU-09b) — this must happen before the file is overwritten.
        await Copy(isAutomatic: false);

        File.Copy(backupPath, paths.DbPath, overwrite: true);

        // The settings live inside the file that was just replaced, so anything cached
        // in memory now describes a database that no longer exists.
        settings.InvalidateCache();

        // The whole database has just been overwritten. Without this line the log has
        // nothing between "Application started" and "Application closed" to explain
        // why a day's work is missing (CU-09b).
        Log.Information("Database restored from backup {BackupPath}", backupPath);
    }

    private async Task<BackupInfo> Copy(bool isAutomatic)
    {
        Directory.CreateDirectory(paths.BackupsFolder);

        var now = DateTime.Now;
        string suffix = isAutomatic ? "auto" : "manual";
        string name = $"{now.ToString(Format, CultureInfo.InvariantCulture)}_{suffix}.db";
        string destination = Path.Combine(paths.BackupsFolder, name);

        File.Copy(paths.DbPath, destination, overwrite: false);
        Log.Information("{BackupKind} backup taken: {BackupPath}",
            isAutomatic ? "Automatic" : "Manual", destination);

        await DeleteOldBackups();

        return ReadBackup(destination)!;
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
