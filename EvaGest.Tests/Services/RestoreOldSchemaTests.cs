using AwesomeAssertions;
using EvaGest.Data;
using EvaGest.Services;
using EvaGest.Tests.Infra;
using EvaGest.ViewModels.Dialogs;
using EvaGest.ViewModels.Pages;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace EvaGest.Tests.Services;

/// <summary>
/// E-01. A restore prunes the backups folder once the live file has been replaced, and
/// the prune reads how many copies to keep from the settings table. A copy made before
/// the RenameToEnglish migration has no "settings" table (it was "configuracio" then),
/// so that read threw after the restore had already happened: the user was told it
/// failed, the dialog stayed open and the app never closed, so it went on running on a
/// file whose migrations only run at startup.
///
/// These tests use the real <see cref="SettingsService"/> over the live file, which is
/// what the other restore tests (with an in-memory TestSettings) could not see.
/// </summary>
public sealed class RestoreOldSchemaTests : IDisposable
{
    private readonly string _folder = Path.Combine(Path.GetTempPath(), "EvaGestRestoreOld_" + Guid.NewGuid());
    private readonly string _live;
    private readonly string _oldBackup;

    public RestoreOldSchemaTests()
    {
        Directory.CreateDirectory(Path.Combine(_folder, "Backups"));
        _live = Path.Combine(_folder, "barberia.db");
        Write(_live, "today", withSettingsTable: true);

        // Named like any backup, but with the old schema: no settings table.
        _oldBackup = Path.Combine(_folder, "Backups", "20260101_100000000_manual.db");
        Write(_oldBackup, "old copy", withSettingsTable: false);
    }

    public void Dispose()
    {
        SqlitePools.Release(_live);
        Directory.Delete(_folder, recursive: true);
    }

    /// <summary>An EvaGest-looking database (it has EF's history table) holding one row.</summary>
    private static void Write(string path, string value, bool withSettingsTable)
    {
        using var connection = new SqliteConnection($"Data Source={path};Pooling=False");
        connection.Open();
        using var command = connection.CreateCommand();
        command.CommandText =
            "CREATE TABLE __EFMigrationsHistory (migration_id TEXT PRIMARY KEY, product_version TEXT); " +
            "CREATE TABLE content (v TEXT); INSERT INTO content VALUES ($v);" +
            (withSettingsTable ? "CREATE TABLE settings (key TEXT PRIMARY KEY, value TEXT NOT NULL);" : "");
        command.Parameters.AddWithValue("$v", value);
        command.ExecuteNonQuery();
    }

    private static string Read(string path)
    {
        using var connection = new SqliteConnection($"Data Source={path};Mode=ReadOnly;Pooling=False");
        connection.Open();
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT v FROM content";
        return (string)command.ExecuteScalar()!;
    }

    /// <summary>The settings service the app uses, reading the live file.</summary>
    private SettingsService RealSettings() => new(new TestFactory(
        new DbContextOptionsBuilder<ShopDbContext>()
            .UseSqlite($"Data Source={_live}")
            .UseSnakeCaseNamingConvention()
            .Options));

    [Fact]
    public async Task Restoring_a_copy_without_the_settings_table_completes()
    {
        var backup = new BackupService(new AppPaths(_live, _folder), RealSettings());

        await backup.Invoking(b => b.Restore(_oldBackup)).Should().NotThrowAsync(
            "the restore itself went through; only the prune after it cannot read the old schema");

        Read(_live).Should().Be("old copy");
        File.Exists(_oldBackup).Should().BeTrue("a failed prune must not take the restored copy with it");
    }

    [Fact]
    public async Task Restoring_a_copy_without_the_settings_table_still_closes_the_app()
    {
        var backup = new BackupService(new AppPaths(_live, _folder), RealSettings());
        var dialogs = new TestDialogService
        {
            ResultConfirm = true,
            ResultDialog = true,
            FillDialog = async vm =>
            {
                if (vm is RestoreDialogViewModel restore)
                {
                    restore.Selected = restore.Backups.Single();
                    await restore.RestoreCommand.ExecuteAsync(null);
                }
            }
        };

        await using var testDb = new TestDatabase();
        var factory = new TestFactory(testDb.Options);
        var shutdown = new CountingShutdown();
        var page = new SettingsViewModel(backup, new ExportService(factory), new TestSettings(),
            new AvailabilityService(factory), dialogs, TestOwner.New(), shutdown);

        await page.RestoreBackupCommand.ExecuteAsync(null);

        Read(_live).Should().Be("old copy");
        shutdown.Requested.Should().Be(1,
            "the pending migrations of the old copy only run at the next startup");
    }

    private sealed class CountingShutdown : IAppShutdown
    {
        public int Requested { get; private set; }
        public void Shutdown() => Requested++;
    }
}
