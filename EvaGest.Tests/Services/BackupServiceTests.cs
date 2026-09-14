using AwesomeAssertions;
using EvaGest.Models;
using EvaGest.Services;
using EvaGest.Tests.Infra;
using Xunit;

namespace EvaGest.Tests.Services;

/// <summary>Block L (backups): pure file I/O, no database involved.</summary>
public class BackupServiceTests : IDisposable
{
    private readonly string _folder = Path.Combine(Path.GetTempPath(), "EvaGestTests_" + Guid.NewGuid());
    private readonly string _pathDb;

    public BackupServiceTests()
    {
        Directory.CreateDirectory(_folder);
        _pathDb = Path.Combine(_folder, "barberia.db");
        File.WriteAllText(_pathDb, "contingut original");
    }

    public void Dispose() => Directory.Delete(_folder, recursive: true);

    /// <summary>Defaults to an hour already past, so the tests that are not about the
    /// schedule do not depend on what time of day they happen to run.</summary>
    private BackupService CreatesService(TestSettings? config = null)
        => new(new AppPaths(_pathDb, _folder),
               config ?? new TestSettings((ConfigKeys.BackupTime, "00:00")));

    [Fact] // L-01
    public async Task A_manual_backup_creates_a_file()
    {
        var backup = CreatesService();
        var backupFile = await backup.MakeManualBackup();

        File.Exists(backupFile.Path).Should().BeTrue();
        new FileInfo(backupFile.Path).Length.Should().BeGreaterThan(0);
    }

    [Fact] // L-02
    public async Task A_backup_can_be_restored()
    {
        var backup = CreatesService();
        var backupFile = await backup.MakeManualBackup();

        File.WriteAllText(_pathDb, "dades malmeses");
        await backup.Restore(backupFile.Path);

        File.ReadAllText(_pathDb).Should().Be("contingut original");
    }

    [Fact] // L-03
    public async Task With_20_backups_and_a_limit_of_15_the_15_newest_remain()
    {
        var backup = CreatesService();
        for (int i = 0; i < 20; i++)
        {
            string name = Path.Combine(_folder, "Backups", $"202601{i + 1:00}_120000000_manual.db");
            Directory.CreateDirectory(Path.Combine(_folder, "Backups"));
            File.WriteAllText(name, "x");
        }

        await backup.DeleteOldBackups();

        (await backup.ListAll()).Should().HaveCount(15);
    }

    [Fact] // L-04
    public async Task An_automatic_backup_due_since_yesterday_runs()
    {
        var backup = CreatesService();
        string folderBackups = Path.Combine(_folder, "Backups");
        Directory.CreateDirectory(folderBackups);
        string yesterday = DateTime.Now.AddDays(-1).ToString("yyyyMMdd_HHmmssfff");
        File.WriteAllText(Path.Combine(folderBackups, $"{yesterday}_auto.db"), "x");

        var result = await backup.RunAutomaticBackupIfDue();

        result.Should().NotBeNull();
    }

    [Fact] // L-05
    public async Task An_automatic_backup_already_done_today_is_not_repeated()
    {
        var backup = CreatesService();
        string folderBackups = Path.Combine(_folder, "Backups");
        Directory.CreateDirectory(folderBackups);
        string today = DateTime.Now.ToString("yyyyMMdd_HHmmssfff");
        File.WriteAllText(Path.Combine(folderBackups, $"{today}_auto.db"), "x");

        var result = await backup.RunAutomaticBackupIfDue();

        result.Should().BeNull();
    }

    [Fact] // L-06
    public async Task Restoring_backs_up_the_current_state_first()
    {
        var backup = CreatesService();
        var backupInitial = await backup.MakeManualBackup();

        int before = (await backup.ListAll()).Count;
        await backup.Restore(backupInitial.Path);
        int after = (await backup.ListAll()).Count;

        after.Should().Be(before + 1); // the pre-restore safety copy
    }

    [Fact] // L-07
    public async Task The_configured_retention_wins_over_the_default()
    {
        var backup = CreatesService(new TestSettings(
            (ConfigKeys.BackupTime, "00:00"),
            (ConfigKeys.BackupsToKeep, "3")));

        Directory.CreateDirectory(Path.Combine(_folder, "Backups"));
        for (int i = 0; i < 10; i++)
            File.WriteAllText(
                Path.Combine(_folder, "Backups", $"202601{i + 1:00}_120000000_manual.db"), "x");

        await backup.DeleteOldBackups();

        (await backup.ListAll()).Should().HaveCount(3);
    }

    [Fact] // L-08
    public async Task Before_the_configured_hour_the_automatic_backup_waits()
    {
        // An hour that cannot have passed yet today, whatever time the suite runs at
        var backup = CreatesService(new TestSettings((ConfigKeys.BackupTime, "23:59")));

        var result = await backup.RunAutomaticBackupIfDue();

        result.Should().BeNull("the daily backup only runs from the configured hour on");
    }

    [Fact] // L-09
    public async Task The_automatic_backup_records_its_date_in_the_settings()
    {
        var config = new TestSettings((ConfigKeys.BackupTime, "00:00"));
        var backup = CreatesService(config);

        await backup.RunAutomaticBackupIfDue();

        (await config.Get(ConfigKeys.LastAutomaticBackup))
            .Should().Be(DateTime.Today.ToString("yyyy-MM-dd"));
    }

    [Fact] // L-10
    public async Task Restoring_invalidates_the_in_memory_settings()
    {
        var config = new TestSettings((ConfigKeys.BackupTime, "00:00"));
        var backup = CreatesService(config);
        var backupFile = await backup.MakeManualBackup();

        await backup.Restore(backupFile.Path);

        // The settings table lives inside the file that was just overwritten, so keeping
        // the old cache would describe a database that no longer exists.
        config.TimesInvalidated.Should().Be(1);
    }
}
