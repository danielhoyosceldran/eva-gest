using AwesomeAssertions;
using EvaGest.Models;
using EvaGest.Services;
using EvaGest.Tests.Infra;
using Microsoft.Data.Sqlite;
using Xunit;

namespace EvaGest.Tests.Services;

/// <summary>
/// F-01, startup side: the daily automatic backup used to be checked only at startup
/// and only once the clock was past the configured hour, so a shop that opens in the
/// morning and closes before it never got a copy, and a missed day was never caught up.
/// At startup the app must now catch up a previous day that ended without its copy.
///
/// The configured hour is 20:00 and "today" is Wednesday 4 March 2026; the clock is
/// pinned, so nothing depends on when the suite runs.
/// </summary>
public sealed class AutomaticBackupCatchUpTests : IDisposable
{
    private static readonly DateTime Today = new(2026, 3, 4);

    private readonly string _folder = Path.Combine(Path.GetTempPath(), "EvaGestBackupCatchUp_" + Guid.NewGuid());
    private readonly string _live;
    private readonly string _backupsFolder;

    public AutomaticBackupCatchUpTests()
    {
        Directory.CreateDirectory(_folder);
        _live = Path.Combine(_folder, "barberia.db");
        _backupsFolder = Path.Combine(_folder, "Backups");
        Directory.CreateDirectory(_backupsFolder);
        WriteDatabase(_live);
    }

    public void Dispose()
    {
        SqlitePools.Release(_live);
        Directory.Delete(_folder, recursive: true);
    }

    private static void WriteDatabase(string path)
    {
        using var connection = new SqliteConnection($"Data Source={path};Pooling=False");
        connection.Open();
        using var command = connection.CreateCommand();
        command.CommandText =
            "CREATE TABLE __EFMigrationsHistory (migration_id TEXT PRIMARY KEY, product_version TEXT); " +
            "INSERT INTO __EFMigrationsHistory VALUES ('20260905121102_InitialCreate', '9.0.0'); " +
            "CREATE TABLE content (v TEXT); INSERT INTO content VALUES ('dades');";
        command.ExecuteNonQuery();
    }

    private BackupService Backups(DateTime now)
        => new(new AppPaths(_live, _folder), new TestSettings((ConfigKeys.BackupTime, "20:00")), () => now);

    /// <summary>A backup file already in the folder, named the way the app names them.</summary>
    private void ExistingBackup(DateTime takenAt, string kind)
        => File.WriteAllText(Path.Combine(_backupsFolder, $"{takenAt:yyyyMMdd_HHmmssfff}_{kind}.db"), "x");

    [Fact]
    public async Task A_morning_start_catches_up_a_day_that_ended_without_its_copy()
    {
        // The last automatic copy is from the day before yesterday: yesterday's 20:00
        // passed with no copy at all.
        ExistingBackup(Today.AddDays(-2).AddHours(20).AddMinutes(30), "auto");

        var result = await Backups(Today.AddHours(9)).RunAutomaticBackupIfDue();

        result.Should().NotBeNull("yesterday ended without its copy and this start must catch it up");
        result!.IsAutomatic.Should().BeTrue();
        File.Exists(result.Path).Should().BeTrue();
    }

    [Fact]
    public async Task A_morning_start_on_a_fresh_install_with_no_copy_at_all_takes_one()
    {
        var result = await Backups(Today.AddHours(9)).RunAutomaticBackupIfDue();

        result.Should().NotBeNull("no automatic copy covers yesterday's 20:00");
    }

    [Fact]
    public async Task A_morning_start_after_a_day_that_got_its_evening_copy_takes_nothing()
    {
        ExistingBackup(Today.AddDays(-1).AddHours(20).AddMinutes(30), "auto");

        var result = await Backups(Today.AddHours(9)).RunAutomaticBackupIfDue();

        result.Should().BeNull("yesterday is covered and today's hour has not come yet");
    }

    [Fact]
    public async Task A_morning_start_after_a_day_closed_with_its_copy_at_seven_takes_nothing()
    {
        // Yesterday the shop closed at 19:00 and the app took the day's copy on closing,
        // before the configured 20:00. That copy is yesterday's.
        ExistingBackup(Today.AddDays(-1).AddHours(19), "close");

        var result = await Backups(Today.AddHours(9)).RunAutomaticBackupIfDue();

        result.Should().BeNull("the copy taken on closing covers yesterday");
    }

    [Fact]
    public async Task An_evening_start_without_todays_copy_still_takes_one()
    {
        ExistingBackup(Today.AddDays(-1).AddHours(20).AddMinutes(30), "auto");

        var result = await Backups(Today.AddHours(20).AddMinutes(30)).RunAutomaticBackupIfDue();

        result.Should().NotBeNull("past the configured hour with no copy today, as before");
    }

    [Fact]
    public async Task An_evening_start_after_todays_copy_takes_nothing()
    {
        ExistingBackup(Today.AddHours(20).AddMinutes(5), "auto");

        var result = await Backups(Today.AddHours(21)).RunAutomaticBackupIfDue();

        result.Should().BeNull();
    }
}
