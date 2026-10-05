using AwesomeAssertions;
using EvaGest.Services;
using EvaGest.Tests.Infra;
using EvaGest.ViewModels.Pages;
using Microsoft.Data.Sqlite;
using Xunit;

namespace EvaGest.Tests.Services;

/// <summary>
/// A backup that comes out damaged must not be counted as taken. It used to be listed
/// and reported as done, and only found to be useless on the day it had to be restored.
///
/// How the damage is made: SQLite's online backup copies pages as they are, without
/// reading the tables inside them, so a live database with one mangled table page is
/// copied faithfully into an equally mangled backup. That stands in for a copy damaged
/// while it is written (a full disk, an antivirus holding the file) without having to
/// interfere with the file system mid-write, and it is the real situation of a shop
/// whose live database has started to go bad: exactly when an unusable copy hurts most.
/// The database is otherwise an EvaGest one (it has EF's migration history table), so
/// the only thing wrong with the copy is that its pages are damaged.
/// </summary>
public sealed class BackupVerificationTests : IDisposable
{
    private const int PageSize = 4096;

    private readonly string _folder = Path.Combine(Path.GetTempPath(), "EvaGestBackupCheck_" + Guid.NewGuid());
    private readonly string _live;

    public BackupVerificationTests()
    {
        Directory.CreateDirectory(_folder);
        _live = Path.Combine(_folder, "barberia.db");
        WriteDatabase(_live);
    }

    public void Dispose()
    {
        SqliteConnection.ClearAllPools();
        Directory.Delete(_folder, recursive: true);
    }

    private string BackupsFolder => Path.Combine(_folder, "Backups");

    private BackupService Service() => new(new AppPaths(_live, _folder), new TestSettings());

    /// <summary>A sound database with EF's history table and enough rows to span many
    /// pages, so one of them can be damaged without touching the schema on page 1.</summary>
    private static void WriteDatabase(string path)
    {
        using var connection = new SqliteConnection($"Data Source={path};Pooling=False");
        connection.Open();
        using var command = connection.CreateCommand();
        command.CommandText =
            $"PRAGMA page_size = {PageSize}; " +
            "CREATE TABLE __EFMigrationsHistory (migration_id TEXT PRIMARY KEY, product_version TEXT); " +
            "INSERT INTO __EFMigrationsHistory VALUES ('20260905121102_InitialCreate', '9.0.0'); " +
            "CREATE TABLE content (v TEXT); " +
            "WITH RECURSIVE n(i) AS (SELECT 1 UNION ALL SELECT i + 1 FROM n WHERE i < 2000) " +
            "INSERT INTO content SELECT 'fila ' || i || ' ' || hex(zeroblob(80)) FROM n;";
        command.ExecuteNonQuery();
    }

    /// <summary>Overwrites the header of a table page in the middle of the file.</summary>
    private static void DamageAPage(string path)
    {
        var bytes = File.ReadAllBytes(path);
        int middlePage = bytes.Length / PageSize / 2;
        for (int i = 0; i < 64; i++) bytes[middlePage * PageSize + i] = 0xFF;
        File.WriteAllBytes(path, bytes);
    }

    private static string QuickCheck(string path)
    {
        using var connection = new SqliteConnection($"Data Source={path};Mode=ReadOnly;Pooling=False");
        connection.Open();
        using var command = connection.CreateCommand();
        command.CommandText = "PRAGMA quick_check";
        return command.ExecuteScalar() as string ?? string.Empty;
    }

    private static int CountRows(string path)
    {
        using var connection = new SqliteConnection($"Data Source={path};Mode=ReadOnly;Pooling=False");
        connection.Open();
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT count(*) FROM content";
        return Convert.ToInt32(command.ExecuteScalar());
    }

    [Fact]
    public void The_damage_used_here_is_one_SQLite_itself_reports()
    {
        // Guards the premise of every other test in this class.
        DamageAPage(_live);

        QuickCheck(_live).Should().NotBe("ok");
    }

    [Fact]
    public async Task A_sound_backup_is_taken_and_listed()
    {
        var backup = Service();

        var taken = await backup.MakeManualBackup();

        (await backup.ListAll()).Select(b => b.Path).Should().Equal(taken.Path);
        QuickCheck(taken.Path).Should().Be("ok");
        CountRows(taken.Path).Should().Be(2000);
    }

    [Fact]
    public async Task A_backup_that_does_not_read_back_as_sound_is_reported_as_failed()
    {
        DamageAPage(_live);
        var backup = Service();

        var take = () => backup.MakeManualBackup();

        await take.Should().ThrowAsync<InvalidDataException>();
    }

    [Fact]
    public async Task A_backup_that_does_not_read_back_as_sound_is_not_left_in_the_list()
    {
        DamageAPage(_live);
        var backup = Service();

        try { await backup.MakeManualBackup(); }
        catch (InvalidDataException) { /* asserted by the test above; here only the list matters */ }

        (await backup.ListAll()).Should().BeEmpty("a copy that cannot be restored must not look like one that can");
        (Directory.Exists(BackupsFolder) ? Directory.GetFiles(BackupsFolder, "*.db") : [])
            .Should().BeEmpty();
    }

    [Fact]
    public async Task Backing_up_now_from_settings_says_no_copy_was_taken_when_it_comes_out_damaged()
    {
        DamageAPage(_live);
        await using var testDb = new TestDatabase();
        var factory = new TestFactory(testDb.Options);
        var dialogs = new TestDialogService();
        var config = new TestSettings();
        var vm = new SettingsViewModel(
            new BackupService(new AppPaths(_live, _folder), config), new ExportService(factory), config,
            new AvailabilityService(factory), dialogs, TestOwner.New());
        await vm.Load();

        await vm.BackupNowCommand.ExecuteAsync(null);

        dialogs.InfoDisplayed.Should().BeEmpty("the \"backup done\" message would be a lie");
        vm.ErrorBackup.Should().NotBeNull("the user has to learn that no copy was taken");
    }

    [Fact]
    public async Task Backing_up_now_from_settings_confirms_a_sound_copy()
    {
        await using var testDb = new TestDatabase();
        var factory = new TestFactory(testDb.Options);
        var dialogs = new TestDialogService();
        var config = new TestSettings();
        var vm = new SettingsViewModel(
            new BackupService(new AppPaths(_live, _folder), config), new ExportService(factory), config,
            new AvailabilityService(factory), dialogs, TestOwner.New());
        await vm.Load();

        await vm.BackupNowCommand.ExecuteAsync(null);

        dialogs.InfoDisplayed.Should().ContainSingle();
        vm.ErrorBackup.Should().BeNull();
    }

    [Fact]
    public async Task Restoring_over_a_damaged_live_database_still_works_and_keeps_its_safety_copy()
    {
        // A damaged live database is the commonest reason to restore. The safety copy
        // taken right before the restore must not be refused for being damaged, or the
        // restore would be refused with it.
        var backup = Service();
        var good = await backup.MakeManualBackup();
        DamageAPage(_live);

        await backup.Restore(good.Path);

        QuickCheck(_live).Should().Be("ok");
        CountRows(_live).Should().Be(2000);
        (await backup.ListAll()).Should().HaveCount(2, "the good copy plus the safety copy of the damaged state");
    }
}
