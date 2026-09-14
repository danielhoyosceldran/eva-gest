namespace EvaGest.Services;

public record BackupInfo(string Path, DateTime Date, bool IsAutomatic, long SizeBytes);

public interface IBackupService
{
    Task<List<BackupInfo>> ListAll();

    Task<BackupInfo> MakeManualBackup();

    /// <summary>
    /// Runs the daily backup if it is due. Called at startup, because the machine may
    /// have been off at the configured hour (CU-11).
    /// </summary>
    Task<BackupInfo?> RunAutomaticBackupIfDue();

    /// <summary>Deletes the oldest backups beyond the configured retention.</summary>
    Task DeleteOldBackups();

    /// <summary>
    /// Restores a backup. Always backs up the CURRENT state first, so an accidental
    /// restore can still be undone (CU-09b).
    /// </summary>
    Task Restore(string backupPath);
}
