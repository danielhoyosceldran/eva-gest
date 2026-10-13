using AwesomeAssertions;
using EvaGest.Models;
using EvaGest.Services;
using EvaGest.Tests.Infra;
using Microsoft.Data.Sqlite;
using Xunit;

namespace EvaGest.Tests.Services;

/// <summary>
/// B-31. A copy whose writing is cut off part way (the PC switched off while the app was
/// taking its closing copy) used to stay in the Backups folder under its final name: a
/// damaged file with SQLite's journal beside it, listed for restore and counted as the
/// day's automatic copy, so no other was taken that day. Copies are now written under a
/// temporary name and renamed once checked; what an older version left is ignored.
///
/// The cut-off write is reproduced by the files it leaves, since a test cannot kill
/// itself half way through a copy.
/// </summary>
public sealed class BackupInterruptedTests : IDisposable
{
    private readonly string _folder = Path.Combine(Path.GetTempPath(), "EvaGestBackupInterrupted_" + Guid.NewGuid());
    private readonly string _live;

    public BackupInterruptedTests()
    {
        Directory.CreateDirectory(Backups);
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

    private string Backups => Path.Combine(_folder, "Backups");

    /// <summary>The configured hour is past, so the closing copy is due unless one exists.</summary>
    private BackupService Service()
        => new(new AppPaths(_live, _folder), new TestSettings((ConfigKeys.BackupTime, "00:00")),
               () => DateTime.Today.AddHours(21));

    /// <summary>What a cut-off write leaves: the first pages of the file, and its journal.</summary>
    private string LeaveCutOffCopy(string name)
    {
        string path = Path.Combine(Backups, name);
        File.WriteAllBytes(path, new byte[16384]);
        File.WriteAllBytes(path + "-journal", new byte[512]);
        return path;
    }

    [Fact]
    public async Task A_copy_cut_off_by_an_older_version_is_not_offered_and_does_not_count_for_the_day()
    {
        string cutOff = LeaveCutOffCopy($"{DateTime.Today:yyyyMMdd}_145459573_close.db");
        var service = Service();

        (await service.ListAll()).Should().BeEmpty("a half-written file is not a backup");

        var taken = await service.RunBackupOnCloseIfDue();

        taken.Should().NotBeNull("no sound copy of the day exists yet");
        (await service.ListAll()).Should().ContainSingle().Which.Path.Should().NotBe(cutOff);
    }

    [Fact]
    public async Task A_copy_cut_off_under_its_temporary_name_is_never_listed_and_the_next_copy_clears_it()
    {
        string partial = Path.Combine(Backups, $"{DateTime.Today:yyyyMMdd}_145459573_close.db.partial");
        File.WriteAllBytes(partial, new byte[16384]);
        File.WriteAllBytes(partial + "-journal", new byte[512]);
        var service = Service();

        (await service.ListAll()).Should().BeEmpty();

        await service.MakeManualBackup();

        Directory.GetFiles(Backups).Select(Path.GetFileName).Should().ContainSingle()
            .Which.Should().EndWith("_manual.db", "the leftovers are gone and the copy has its final name");
    }
}
