using AwesomeAssertions;
using EvaGest.Data;
using EvaGest.Models;
using EvaGest.Services;
using EvaGest.Tests.Infra;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace EvaGest.Tests.Services;

/// <summary>
/// Block L (backups). The database is a real SQLite file: the service copies through
/// SQLite's backup API, and a plain text file standing in for it is what let a
/// File.Copy that ignored the WAL pass every test here.
/// </summary>
public class BackupServiceTests : IDisposable
{
    private readonly string _folder = Path.Combine(Path.GetTempPath(), "EvaGestTests_" + Guid.NewGuid());
    private readonly string _pathDb;

    public BackupServiceTests()
    {
        Directory.CreateDirectory(_folder);
        _pathDb = Path.Combine(_folder, "barberia.db");
        WriteContent(_pathDb, "contingut original");
    }

    public void Dispose()
    {
        // The WAL tests go through EF's pooled connections, which keep the file open.
        SqliteConnection.ClearAllPools();
        Directory.Delete(_folder, recursive: true);
    }

    /// <summary>A one-row database standing for the shop's, written without pooling so
    /// nothing keeps the file open behind the test's back. It carries EF's migration
    /// history table, which is how a restore recognises an EvaGest database.</summary>
    private static void WriteContent(string path, string value)
    {
        using var connection = new SqliteConnection($"Data Source={path};Pooling=False");
        connection.Open();
        using var command = connection.CreateCommand();
        command.CommandText = "CREATE TABLE IF NOT EXISTS __EFMigrationsHistory (id TEXT); " +
                              "CREATE TABLE IF NOT EXISTS content (v TEXT); DELETE FROM content; " +
                              "INSERT INTO content VALUES ($v);";
        command.Parameters.AddWithValue("$v", value);
        command.ExecuteNonQuery();
    }

    private static string ReadContent(string path)
    {
        using var connection = new SqliteConnection($"Data Source={path};Mode=ReadOnly;Pooling=False");
        connection.Open();
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT v FROM content";
        return (string)command.ExecuteScalar()!;
    }

    /// <summary>Defaults to an hour already past, so the tests that are not about the
    /// schedule do not depend on what time of day they happen to run. The ones that ARE
    /// about the schedule pass <paramref name="now"/> and do not depend on it either.</summary>
    private BackupService CreatesService(TestSettings? config = null, DateTime? now = null)
        => new(new AppPaths(_pathDb, _folder),
               config ?? new TestSettings((ConfigKeys.BackupTime, "00:00")),
               now is DateTime moment ? () => moment : null);

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

        WriteContent(_pathDb, "dades malmeses");
        await backup.Restore(backupFile.Path);

        ReadContent(_pathDb).Should().Be("contingut original");
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
        // The clock is pinned rather than reasoned about. This used to configure "23:59"
        // and call it an hour that could not have passed yet, which was false for the one
        // minute a day the suite happened to start inside it.
        var backup = CreatesService(new TestSettings((ConfigKeys.BackupTime, "20:00")),
                                    now: new DateTime(2026, 3, 4, 19, 59, 0));

        var result = await backup.RunAutomaticBackupIfDue();

        result.Should().BeNull("the daily backup only runs from the configured hour on");
    }

    [Fact] // L-08b
    public async Task From_the_configured_hour_on_the_automatic_backup_runs()
    {
        // The other side of the same boundary, which nothing pinned before.
        var backup = CreatesService(new TestSettings((ConfigKeys.BackupTime, "20:00")),
                                    now: new DateTime(2026, 3, 4, 20, 0, 0));

        var result = await backup.RunAutomaticBackupIfDue();

        result.Should().NotBeNull();
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

    /// <summary>The shop's database as the app opens it: a file, migrated by EF (which
    /// puts it in WAL mode), read and written through pooled connections.</summary>
    private async Task<TestFactory> LiveDatabase(string path)
    {
        var factory = new TestFactory(new DbContextOptionsBuilder<ShopDbContext>()
            .UseSqlite($"Data Source={path}")
            .UseSnakeCaseNamingConvention()
            .Options);
        await using var db = factory.CreateDbContext();
        await db.Database.MigrateAsync();
        return factory;
    }

    private static async Task AddClient(TestFactory factory, string name)
    {
        await using var db = factory.CreateDbContext();
        db.Clients.Add(Make.Client(name));
        await db.SaveChangesAsync();
    }

    private static int CountClients(string path)
    {
        using var connection = new SqliteConnection($"Data Source={path};Mode=ReadOnly;Pooling=False");
        connection.Open();
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT count(*) FROM clients";
        return Convert.ToInt32(command.ExecuteScalar());
    }

    [Fact] // L-11
    public async Task A_backup_taken_while_the_app_is_running_holds_the_latest_changes()
    {
        // The last commits sit in barberia.db-wal until SQLite checkpoints, and the app's
        // pooled connections keep that from happening on close. Copying the main file
        // alone produced a backup without them - on a young database, without the tables.
        string live = Path.Combine(_folder, "live.db");
        var factory = await LiveDatabase(live);
        await AddClient(factory, "Anna");

        var backup = new BackupService(new AppPaths(live, _folder), new TestSettings());
        var backupFile = await backup.MakeManualBackup();

        CountClients(backupFile.Path).Should().Be(1);
    }

    [Fact] // L-12
    public async Task A_restore_is_what_the_running_app_reads_afterwards()
    {
        // Overwriting the file left the live -wal behind; the next read replayed it over
        // the restored pages, so the restore silently did nothing.
        string live = Path.Combine(_folder, "live.db");
        var factory = await LiveDatabase(live);
        await AddClient(factory, "Anna");

        var backup = new BackupService(new AppPaths(live, _folder), new TestSettings());
        var backupFile = await backup.MakeManualBackup();
        await AddClient(factory, "Bernat");

        await backup.Restore(backupFile.Path);

        await using var db = factory.CreateDbContext();
        (await db.Clients.Select(c => c.Name).ToListAsync()).Should().Equal("Anna");
    }

    [Fact] // L-13
    public async Task A_damaged_backup_is_refused_and_the_database_is_left_alone()
    {
        var backup = CreatesService();
        string damaged = Path.Combine(_folder, "Backups", "20260101_120000000_manual.db");
        Directory.CreateDirectory(Path.GetDirectoryName(damaged)!);
        await File.WriteAllTextAsync(damaged, "not a database");

        var restore = () => backup.Restore(damaged);

        await restore.Should().ThrowAsync<InvalidDataException>();
        ReadContent(_pathDb).Should().Be("contingut original");
    }

    [Fact]
    public async Task A_sound_database_that_is_not_EvaGest_is_refused()
    {
        // quick_check passes any healthy SQLite file, an empty one included.
        var backup = CreatesService();
        string foreign = Path.Combine(_folder, "Backups", "20260101_120000000_manual.db");
        Directory.CreateDirectory(Path.GetDirectoryName(foreign)!);
        using (var connection = new SqliteConnection($"Data Source={foreign};Pooling=False"))
        {
            connection.Open();
            using var command = connection.CreateCommand();
            command.CommandText = "CREATE TABLE notes (text TEXT)";
            command.ExecuteNonQuery();
        }

        var restore = () => backup.Restore(foreign);

        await restore.Should().ThrowAsync<InvalidDataException>();
        ReadContent(_pathDb).Should().Be("contingut original");
    }

    [Fact] // L-14
    public async Task Restoring_the_oldest_backup_at_the_retention_limit_still_restores_it()
    {
        // The pre-restore safety copy used to prune straight away, so with the folder at
        // its limit it deleted the oldest backup - the very one being restored - before
        // it was read.
        var backup = CreatesService(new TestSettings(
            (ConfigKeys.BackupTime, "00:00"),
            (ConfigKeys.BackupsToKeep, "1")));
        var only = await backup.MakeManualBackup();
        WriteContent(_pathDb, "dades malmeses");

        await backup.Restore(only.Path);

        ReadContent(_pathDb).Should().Be("contingut original");
    }

    [Fact] // L-15
    public async Task Backups_of_a_WAL_database_leave_no_side_files_behind()
    {
        // SQLite's backup API copies page 1 as it is, so a backup of the WAL-mode live
        // database came out marked WAL too, and opening it again (the check before a
        // restore) could leave -wal/-shm files that pruning, which only deletes *.db,
        // never removes.
        string live = Path.Combine(_folder, "live.db");
        var factory = await LiveDatabase(live);
        await AddClient(factory, "Anna");

        var backup = new BackupService(new AppPaths(live, _folder), new TestSettings());
        var backupFile = await backup.MakeManualBackup();
        await backup.Restore(backupFile.Path);

        Directory.GetFiles(Path.Combine(_folder, "Backups"))
            .Should().OnlyContain(f => f.EndsWith(".db"));
        JournalMode(backupFile.Path).Should().Be("delete");
        JournalMode(live).Should().Be("wal", "restoring a plain copy must not take the live database out of WAL");
    }

    private static string JournalMode(string path)
    {
        using var connection = new SqliteConnection($"Data Source={path};Pooling=False");
        connection.Open();
        using var command = connection.CreateCommand();
        command.CommandText = "PRAGMA journal_mode";
        return (string)command.ExecuteScalar()!;
    }
}
