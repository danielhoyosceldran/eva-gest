namespace EvaGest.Services;

/// <param name="IsBeforeUpdate">Taken by the app itself right before a new version
/// changed the database's schema (see <see cref="IBackupService.MakeBackupBeforeUpdate"/>).</param>
public record BackupInfo(string Path, DateTime Date, bool IsAutomatic, long SizeBytes,
                         bool IsBeforeUpdate = false);

public interface IBackupService
{
    /// <summary>
    /// Raised after a backup has been written and has passed its check (automatic or
    /// manual). Lets the shell take down its "today's automatic backup failed" notice as
    /// soon as a good copy exists again, whichever page took it.
    /// </summary>
    event Action<BackupInfo>? BackupTaken;

    Task<List<BackupInfo>> ListAll();

    /// <summary>Takes a backup now. Every new backup is read back and checked before it
    /// counts; one that fails is deleted and this throws <see cref="System.IO.InvalidDataException"/>.</summary>
    Task<BackupInfo> MakeManualBackup();

    /// <summary>
    /// Copies the database as it is before the startup migrations change its schema
    /// (CU-12), so a new version that damages the data can still be undone. Not pruned
    /// and not verified; throws if the copy cannot be written at all.
    /// </summary>
    Task<BackupInfo> MakeBackupBeforeUpdate();

    /// <summary>
    /// Runs the daily backup if it is due. Called at startup, because the machine may
    /// have been off at the configured hour (CU-11).
    /// </summary>
    Task<BackupInfo?> RunAutomaticBackupIfDue();

    /// <summary>Deletes the oldest backups beyond the configured retention.</summary>
    Task DeleteOldBackups();

    /// <summary>
    /// Restores a backup. Always backs up the CURRENT state first, so an accidental
    /// restore can still be undone (CU-09b). Refuses, before touching anything, a file
    /// that is not a sound EvaGest database (<see cref="System.IO.InvalidDataException"/>)
    /// or one made by a newer version of the app (<see cref="BackupFromNewerVersionException"/>).
    /// </summary>
    Task Restore(string backupPath);
}
