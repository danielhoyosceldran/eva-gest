using System.Diagnostics;
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
/// F-06: SQLite's backup API does not wait on a lock the way an ordinary statement does,
/// so a backup or a restore that met another connection's write or checkpoint failed at
/// once with "database is locked". A lock held for a moment must be waited out; a lock
/// that is never released must still end in an error within seconds, not a hang.
///
/// The other connection is real: a second SQLite connection on the live file that takes
/// the lock with BEGIN EXCLUSIVE / BEGIN IMMEDIATE and lets it go a few hundred
/// milliseconds later.
/// </summary>
public sealed class BackupLockRetryTests : IDisposable
{
    private readonly string _folder = Path.Combine(Path.GetTempPath(), "EvaGestBackupLock_" + Guid.NewGuid());
    private readonly string _live;

    /// <summary>How long the momentary lock is held: the length of a busy write.</summary>
    private static readonly TimeSpan Moment = TimeSpan.FromMilliseconds(400);

    public BackupLockRetryTests()
    {
        Directory.CreateDirectory(_folder);
        _live = Path.Combine(_folder, "barberia.db");
        WriteContent(_live, "contingut original");
    }

    public void Dispose()
    {
        SqlitePools.Release(_live);
        Directory.Delete(_folder, recursive: true);
    }

    /// <summary>A one-row database with EF's history table, which is how a restore
    /// recognises an EvaGest database. Rollback-journal mode, written without pooling.</summary>
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

    private BackupService Backups()
        => new(new AppPaths(_live, _folder), new TestSettings((ConfigKeys.BackupTime, "00:00")));

    /// <summary>
    /// Another connection on <paramref name="path"/> takes the lock right now (the call
    /// returns once it holds it) and releases it after <paramref name="holdFor"/>, or
    /// never if null. Disposing the result releases it in any case.
    /// </summary>
    private sealed class OtherConnectionLock : IAsyncDisposable
    {
        private readonly SqliteConnection _connection;
        private readonly Task _release;
        private readonly CancellationTokenSource _stop = new();
        private int _released;

        public OtherConnectionLock(string path, string begin, TimeSpan? holdFor)
        {
            _connection = new SqliteConnection($"Data Source={path};Pooling=False");
            _connection.Open();
            using (var command = _connection.CreateCommand())
            {
                command.CommandText = begin;
                command.ExecuteNonQuery();
            }

            _release = holdFor is TimeSpan hold
                ? Task.Run(async () =>
                {
                    try { await Task.Delay(hold, _stop.Token); } catch (OperationCanceledException) { }
                    Release();
                })
                : Task.CompletedTask;
        }

        private void Release()
        {
            if (Interlocked.Exchange(ref _released, 1) == 1) return;
            using (var command = _connection.CreateCommand())
            {
                command.CommandText = "COMMIT;";
                command.ExecuteNonQuery();
            }
            _connection.Dispose();
        }

        public async ValueTask DisposeAsync()
        {
            _stop.Cancel();
            await _release;
            Release();
            _stop.Dispose();
        }
    }

    [Fact]
    public async Task A_manual_backup_waits_out_a_momentary_exclusive_lock_on_the_live_database()
    {
        var backups = Backups();
        await using var otherWriter = new OtherConnectionLock(_live, "BEGIN EXCLUSIVE;", Moment);

        var backup = await backups.MakeManualBackup();

        File.Exists(backup.Path).Should().BeTrue();
        ReadContent(backup.Path).Should().Be("contingut original");
    }

    [Fact]
    public async Task A_restore_waits_out_a_momentary_write_lock_on_the_live_database()
    {
        // BEGIN IMMEDIATE lets readers in, so the safety copy of the current state can be
        // read; it is writing the restored pages into the live file that has to wait.
        var backups = Backups();
        var good = await backups.MakeManualBackup();
        WriteContent(_live, "dades posteriors");
        await using var otherWriter = new OtherConnectionLock(_live, "BEGIN IMMEDIATE;", Moment);

        await backups.Restore(good.Path);

        ReadContent(_live).Should().Be("contingut original");
    }

    [Fact]
    public async Task A_restore_waits_out_a_momentary_exclusive_lock_on_the_live_database()
    {
        var backups = Backups();
        var good = await backups.MakeManualBackup();
        WriteContent(_live, "dades posteriors");
        await using var otherWriter = new OtherConnectionLock(_live, "BEGIN EXCLUSIVE;", Moment);

        await backups.Restore(good.Path);

        ReadContent(_live).Should().Be("contingut original");
    }

    [Fact]
    public async Task A_restore_into_the_WAL_live_database_waits_out_a_momentary_write_lock()
    {
        // The live database as the app runs it: migrated by EF, so in WAL mode.
        string live = Path.Combine(_folder, "live.db");
        var options = new DbContextOptionsBuilder<ShopDbContext>()
            .UseSqlite($"Data Source={live}")
            .UseSnakeCaseNamingConvention()
            .Options;
        await using (var db = new ShopDbContext(options))
        {
            await db.Database.MigrateAsync();
            db.Clients.Add(Make.Client("Anna"));
            await db.SaveChangesAsync();
        }

        try
        {
            var backups = new BackupService(new AppPaths(live, _folder), new TestSettings());
            var good = await backups.MakeManualBackup();
            await using (var db = new ShopDbContext(options))
            {
                db.Clients.Add(Make.Client("Bernat"));
                await db.SaveChangesAsync();
            }

            await using (new OtherConnectionLock(live, "BEGIN IMMEDIATE;", Moment))
            {
                await backups.Restore(good.Path);
            }

            await using var check = new ShopDbContext(options);
            (await check.Clients.Select(c => c.Name).ToListAsync()).Should().Equal("Anna");
        }
        finally
        {
            SqlitePools.Release(live);
        }
    }

    [Fact]
    public async Task A_lock_that_is_never_released_ends_in_an_error_within_seconds()
    {
        var backups = Backups();
        await using var stuck = new OtherConnectionLock(_live, "BEGIN EXCLUSIVE;", holdFor: null);

        var watch = Stopwatch.StartNew();
        var attempt = Task.Run(() => backups.MakeManualBackup());
        var finished = await Task.WhenAny(attempt, Task.Delay(TimeSpan.FromSeconds(20)));

        finished.Should().BeSameAs(attempt, "a lock nobody releases must not hang the backup");
        watch.Elapsed.Should().BeLessThan(TimeSpan.FromSeconds(15));
        var outcome = () => attempt;
        await outcome.Should().ThrowAsync<Exception>("a backup that could not be taken must say so");
    }
}
