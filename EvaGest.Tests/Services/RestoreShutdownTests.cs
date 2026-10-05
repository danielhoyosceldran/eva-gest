using AwesomeAssertions;
using EvaGest.Services;
using EvaGest.Tests.Infra;
using EvaGest.ViewModels.Dialogs;
using EvaGest.ViewModels.Pages;
using Microsoft.Data.Sqlite;
using Xunit;

namespace EvaGest.Tests.Services;

/// <summary>
/// After a restore the app must close. Every page ViewModel lives for the whole session,
/// so the pages went on showing rows from the database that had just been replaced, and
/// anything saved from them was written against records that might no longer exist;
/// the app only asked the user to reopen it.
/// </summary>
public sealed class RestoreShutdownTests : IDisposable
{
    private readonly string _folder = Path.Combine(Path.GetTempPath(), "EvaGestRestoreShutdown_" + Guid.NewGuid());
    private readonly string _live;

    public RestoreShutdownTests()
    {
        Directory.CreateDirectory(_folder);
        _live = Path.Combine(_folder, "barberia.db");
        WriteContent(_live, "abans");
    }

    public void Dispose()
    {
        SqlitePools.Release(_live);
        Directory.Delete(_folder, recursive: true);
    }

    private sealed class TestShutdown : IAppShutdown
    {
        public int Requested { get; private set; }
        public void Shutdown() => Requested++;
    }

    /// <summary>A one-row database carrying EF's history table, which is how a restore
    /// recognises an EvaGest database.</summary>
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

    private async Task<(SettingsViewModel Page, TestDialogService Dialogs, TestShutdown Shutdown, TestDatabase Db)>
        Build(bool userGoesThroughWithIt)
    {
        var backup = new BackupService(new AppPaths(_live, _folder), new TestSettings());
        await backup.MakeManualBackup();          // holds "abans"
        WriteContent(_live, "després");

        var dialogs = new TestDialogService
        {
            ResultConfirm = userGoesThroughWithIt,
            ResultDialog = userGoesThroughWithIt,
            // What the user does inside the restore dialog: pick the copy and restore it,
            // or close the dialog without restoring.
            FillDialog = async vm =>
            {
                if (vm is RestoreDialogViewModel restore && userGoesThroughWithIt)
                {
                    restore.Selected = restore.Backups.First();
                    await restore.RestoreCommand.ExecuteAsync(null);
                }
            }
        };

        var testDb = new TestDatabase();
        var factory = new TestFactory(testDb.Options);
        var shutdown = new TestShutdown();
        var page = new SettingsViewModel(backup, new ExportService(factory), new TestSettings(),
            new AvailabilityService(factory), dialogs, TestOwner.New(), shutdown);
        await page.Load();
        return (page, dialogs, shutdown, testDb);
    }

    [Fact]
    public async Task A_confirmed_restore_closes_the_app()
    {
        var (page, dialogs, shutdown, testDb) = await Build(userGoesThroughWithIt: true);
        await using var _ = testDb;

        await page.RestoreBackupCommand.ExecuteAsync(null);

        ReadContent(_live).Should().Be("abans", "the restore itself went through");
        shutdown.Requested.Should().Be(1, "every page still holds rows from the database that was replaced");
    }

    [Fact]
    public async Task Cancelling_the_restore_dialog_leaves_the_app_open()
    {
        var (page, dialogs, shutdown, testDb) = await Build(userGoesThroughWithIt: false);
        await using var _ = testDb;

        await page.RestoreBackupCommand.ExecuteAsync(null);

        dialogs.DialogsDisplayed.Should().ContainSingle(d => d is RestoreDialogViewModel);
        ReadContent(_live).Should().Be("després");
        shutdown.Requested.Should().Be(0);
    }
}
