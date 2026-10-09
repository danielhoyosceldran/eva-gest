using AwesomeAssertions;
using EvaGest.Models;
using EvaGest.Services;
using EvaGest.Tests.Infra;
using Microsoft.Data.Sqlite;
using Xunit;

namespace EvaGest.Tests.Services;

/// <summary>
/// E-05. Two steps run after a copy has been written and checked: pruning the old ones
/// and noting the date of an automatic one in the settings. Either failing used to make
/// the whole backup throw, so a good copy sitting in the folder was reported to the user
/// as a failure and the "backup failed" notice stayed up.
/// </summary>
public sealed class BackupAftermathTests : IDisposable
{
    private readonly string _folder = Path.Combine(Path.GetTempPath(), "EvaGestBackupAftermath_" + Guid.NewGuid());
    private readonly string _live;

    public BackupAftermathTests()
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

    /// <summary>Settings whose retention read fails, the way the real service does
    /// when the database cannot be read at that moment.</summary>
    private sealed class RetentionUnreadable : TestSettings
    {
        public RetentionUnreadable() : base((ConfigKeys.BackupTime, "00:00")) { }

        public new Task<int> GetInt(string key, int perDefault)
            => key == ConfigKeys.BackupsToKeep
                ? Task.FromException<int>(new SqliteException("database is locked", 5))
                : base.GetInt(key, perDefault);
    }

    /// <summary>Forwards to <see cref="RetentionUnreadable"/> through the interface, so
    /// its hiding GetInt is the one the service calls.</summary>
    private sealed class Forward(RetentionUnreadable inner) : ISettingsService
    {
        public Task<string?> Get(string key) => inner.Get(key);
        public Task<int> GetInt(string key, int perDefault) => inner.GetInt(key, perDefault);
        public Task<bool> GetBool(string key, bool perDefault) => inner.GetBool(key, perDefault);
        public Task Save(string key, string value) => inner.Save(key, value);
        public Task SaveBool(string key, bool value) => inner.SaveBool(key, value);
        public void InvalidateCache() => inner.InvalidateCache();
    }

    [Fact]
    public async Task A_copy_whose_prune_fails_is_still_reported_as_taken()
    {
        var backups = new BackupService(new AppPaths(_live, _folder), new Forward(new RetentionUnreadable()));
        BackupInfo? announced = null;
        backups.BackupTaken += b => announced = b;

        var backup = await backups.MakeManualBackup();

        File.Exists(backup.Path).Should().BeTrue();
        announced.Should().Be(backup, "the failed-backup notice goes away on this event");
    }

    [Fact]
    public async Task An_automatic_copy_whose_date_cannot_be_noted_is_still_reported_as_taken()
    {
        var settings = new TestSettings((ConfigKeys.BackupTime, "00:00"));
        settings.FailsToSave.Add(ConfigKeys.LastAutomaticBackup);
        var backups = new BackupService(new AppPaths(_live, _folder), settings);

        var backup = await backups.RunAutomaticBackupIfDue();

        backup.Should().NotBeNull();
        File.Exists(backup!.Path).Should().BeTrue();
    }
}
