using AwesomeAssertions;
using EvaGest.Models;
using EvaGest.Services;
using EvaGest.Tests.Infra;
using Microsoft.Data.Sqlite;
using Xunit;

namespace EvaGest.Tests.Services;

/// <summary>
/// E-04. A copy that failed while being written left its file in the Backups folder,
/// named like any good copy. An empty *_auto.db then counted as the day's automatic
/// backup, so the next startup did not try again; it was also listed for restore and
/// held a retention slot.
///
/// The failure is real: another connection holds the live file under BEGIN EXCLUSIVE
/// for longer than every retry of the copy.
/// </summary>
public sealed class BackupFailureLeftoverTests : IDisposable
{
    private readonly string _folder = Path.Combine(Path.GetTempPath(), "EvaGestBackupLeftover_" + Guid.NewGuid());
    private readonly string _live;

    public BackupFailureLeftoverTests()
    {
        Directory.CreateDirectory(_folder);
        _live = Path.Combine(_folder, "barberia.db");

        using var connection = new SqliteConnection($"Data Source={_live};Pooling=False");
        connection.Open();
        using var command = connection.CreateCommand();
        command.CommandText = "CREATE TABLE __EFMigrationsHistory (id TEXT); " +
                              "CREATE TABLE content (v TEXT); INSERT INTO content VALUES ('dades');";
        command.ExecuteNonQuery();
    }

    public void Dispose()
    {
        SqlitePools.Release(_live);
        Directory.Delete(_folder, recursive: true);
    }

    private string BackupsFolder => Path.Combine(_folder, "Backups");

    private string[] FilesInBackups()
        => Directory.Exists(BackupsFolder) ? Directory.GetFiles(BackupsFolder) : [];

    private BackupService Backups()
        => new(new AppPaths(_live, _folder), new TestSettings((ConfigKeys.BackupTime, "00:00")));

    /// <summary>Holds the live file under an exclusive lock until disposed.</summary>
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

    [Fact]
    public async Task A_manual_backup_that_fails_while_writing_leaves_no_file()
    {
        var backups = Backups();

        using (new StuckLock(_live))
        {
            await backups.Invoking(b => b.MakeManualBackup()).Should().ThrowAsync<SqliteException>();
        }

        FilesInBackups().Should().BeEmpty("a copy that was never written must not look like one");
        (await backups.ListAll()).Should().BeEmpty();
    }

    [Fact]
    public async Task A_failed_automatic_backup_is_tried_again_at_the_next_startup()
    {
        var backups = Backups();

        using (new StuckLock(_live))
        {
            await backups.Invoking(b => b.RunAutomaticBackupIfDue()).Should().ThrowAsync<SqliteException>();
        }

        // The next startup, with the lock gone.
        var retried = await backups.RunAutomaticBackupIfDue();

        retried.Should().NotBeNull("the failed attempt left nothing that covers the day");
        retried!.SizeBytes.Should().BeGreaterThan(0);
        (await backups.ListAll()).Should().ContainSingle();
    }

    [Fact]
    public async Task A_failed_backup_on_closing_leaves_the_day_without_a_copy()
    {
        var backups = Backups();

        using (new StuckLock(_live))
        {
            await backups.Invoking(b => b.RunBackupOnCloseIfDue()).Should().ThrowAsync<SqliteException>();
        }

        (await backups.RunBackupOnCloseIfDue()).Should().NotBeNull(
            "closing again the same day still owes the day its copy");
    }
}
