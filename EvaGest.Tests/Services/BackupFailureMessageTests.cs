using AwesomeAssertions;
using EvaGest.Resources;
using EvaGest.Services;
using EvaGest.Tests.Infra;
using EvaGest.ViewModels.Dialogs;
using EvaGest.ViewModels.Pages;
using Microsoft.Data.Sqlite;
using Xunit;

namespace EvaGest.Tests.Services;

/// <summary>
/// E-08. "Fer còpia ara" and a restore only handled the failures they expected (a copy
/// that failed its check, a damaged or newer backup). An ordinary one — the data file
/// busy past every retry, a full disk — went to the generic error, which says neither
/// that nothing was copied or restored nor what to try.
///
/// The failure is real: another connection holds the live file under BEGIN EXCLUSIVE.
/// </summary>
public sealed class BackupFailureMessageTests : IDisposable
{
    private readonly string _folder = Path.Combine(Path.GetTempPath(), "EvaGestBackupMessage_" + Guid.NewGuid());
    private readonly string _live;

    public BackupFailureMessageTests()
    {
        Directory.CreateDirectory(Path.Combine(_folder, "Backups"));
        _live = Path.Combine(_folder, "barberia.db");
        Write(_live, "avui");
        Write(Path.Combine(_folder, "Backups", "20260101_100000000_manual.db"), "abans");
    }

    public void Dispose()
    {
        SqlitePools.Release(_live);
        Directory.Delete(_folder, recursive: true);
    }

    private static void Write(string path, string value)
    {
        using var connection = new SqliteConnection($"Data Source={path};Pooling=False");
        connection.Open();
        using var command = connection.CreateCommand();
        command.CommandText = "CREATE TABLE __EFMigrationsHistory (id TEXT); " +
                              "CREATE TABLE content (v TEXT); INSERT INTO content VALUES ($v);";
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

    /// <summary>Another program holding the live file exclusively until disposed.</summary>
    private sealed class StuckLock : IDisposable
    {
        private readonly SqliteConnection _connection;

        public StuckLock(string path)
        {
            _connection = new SqliteConnection($"Data Source={path};Pooling=False");
            _connection.Open();
            using var command = _connection.CreateCommand();
            command.CommandText = "BEGIN EXCLUSIVE;";
            command.ExecuteNonQuery();
        }

        public void Dispose()
        {
            using (var command = _connection.CreateCommand())
            {
                command.CommandText = "ROLLBACK;";
                command.ExecuteNonQuery();
            }
            _connection.Dispose();
        }
    }

    private sealed class CountingShutdown : IAppShutdown
    {
        public int Requested { get; private set; }
        public void Shutdown() => Requested++;
    }

    private static SettingsViewModel Page(BackupService backup, TestDialogService dialogs,
        TestDatabase testDb, IAppShutdown? shutdown = null)
    {
        var factory = new TestFactory(testDb.Options);
        return new SettingsViewModel(backup, new ExportService(factory), new TestSettings(),
            new AvailabilityService(factory), dialogs, TestOwner.New(), shutdown);
    }

    [Fact]
    public async Task A_backup_that_cannot_be_taken_says_so_on_the_page()
    {
        await using var testDb = new TestDatabase();
        var page = Page(new BackupService(new AppPaths(_live, _folder), new TestSettings()),
            new TestDialogService(), testDb);

        using (new StuckLock(_live))
        {
            await page.Invoking(p => p.BackupNowCommand.ExecuteAsync(null)).Should().NotThrowAsync();
        }

        page.ErrorBackup.Should().Be(Texts.BackupNotTaken);
    }

    [Fact]
    public async Task A_restore_that_cannot_run_says_so_in_its_dialog_and_keeps_the_app_open()
    {
        await using var testDb = new TestDatabase();
        RestoreDialogViewModel? restore = null;
        bool closed = false;
        var dialogs = new TestDialogService
        {
            ResultConfirm = true,
            ResultDialog = false,   // the user closes the dialog after reading the error
            FillDialog = async vm =>
            {
                if (vm is not RestoreDialogViewModel r) return;
                restore = r;
                r.Close += _ => closed = true;
                r.Selected = r.Backups.Single();
                await r.Invoking(x => x.RestoreCommand.ExecuteAsync(null)).Should().NotThrowAsync();
            }
        };
        var shutdown = new CountingShutdown();
        var page = Page(new BackupService(new AppPaths(_live, _folder), new TestSettings()), dialogs, testDb, shutdown);

        using (new StuckLock(_live))
        {
            await page.RestoreBackupCommand.ExecuteAsync(null);
        }

        restore!.ErrorValidation.Should().Be(Texts.RestoreNotDone);
        closed.Should().BeFalse("the user has to read why it did not happen");
        shutdown.Requested.Should().Be(0);
        Read(_live).Should().Be("avui");
    }
}
