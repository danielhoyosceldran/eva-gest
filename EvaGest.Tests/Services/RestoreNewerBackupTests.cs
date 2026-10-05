using AwesomeAssertions;
using EvaGest.Data;
using EvaGest.Resources;
using EvaGest.Services;
using EvaGest.Tests.Infra;
using EvaGest.ViewModels.Dialogs;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace EvaGest.Tests.Services;

/// <summary>
/// A backup made by a newer version of the app must not be restored by this one. The
/// restore check used to prove only that the file was a sound EvaGest database, so a copy
/// taken after an update could be restored by an older install, which would then run on
/// tables it does not know, with no pending migration to put it right.
/// The "newer" backup here is a real copy of a migrated database whose migration history
/// also lists a migration this build has never heard of.
/// </summary>
public sealed class RestoreNewerBackupTests : IDisposable
{
    private readonly string _folder = Path.Combine(Path.GetTempPath(), "EvaGestNewerBackup_" + Guid.NewGuid());
    private readonly string _live;

    public RestoreNewerBackupTests()
    {
        Directory.CreateDirectory(_folder);
        _live = Path.Combine(_folder, "barberia.db");
    }

    public void Dispose()
    {
        SqliteConnection.ClearAllPools();
        Directory.Delete(_folder, recursive: true);
    }

    private TestFactory LiveFactory() => new(new DbContextOptionsBuilder<ShopDbContext>()
        .UseSqlite($"Data Source={_live}")
        .UseSnakeCaseNamingConvention()
        .Options);

    private BackupService Service() => new(new AppPaths(_live, _folder), new TestSettings());

    private async Task AddClient(string name)
    {
        await using var db = LiveFactory().CreateDbContext();
        db.Clients.Add(Make.Client(name));
        await db.SaveChangesAsync();
    }

    private async Task<List<string>> LiveClients()
    {
        await using var db = LiveFactory().CreateDbContext();
        return await db.Clients.OrderBy(c => c.Name).Select(c => c.Name).ToListAsync();
    }

    /// <summary>
    /// The live database migrated by this build with Anna in it, a backup of it, then
    /// Bernat added to the live one only. With <paramref name="fromNewerVersion"/>, the
    /// backup's history also lists a migration from a later version.
    /// </summary>
    private async Task<BackupInfo> Prepare(bool fromNewerVersion)
    {
        await using (var db = LiveFactory().CreateDbContext())
            await db.Database.MigrateAsync();
        await AddClient("Anna");

        var backup = await Service().MakeManualBackup();
        if (fromNewerVersion)
        {
            using var connection = new SqliteConnection($"Data Source={backup.Path};Pooling=False");
            connection.Open();
            using var command = connection.CreateCommand();
            command.CommandText =
                "INSERT INTO __EFMigrationsHistory VALUES ('29991231120000_AddSomethingFromTheFuture', '99.0.0')";
            command.ExecuteNonQuery();
        }

        await AddClient("Bernat");
        return backup;
    }

    [Fact]
    public async Task A_backup_from_a_newer_version_is_refused_and_the_live_database_is_left_alone()
    {
        var newer = await Prepare(fromNewerVersion: true);

        var restore = () => Service().Restore(newer.Path);

        await restore.Should().ThrowAsync<Exception>();
        (await LiveClients()).Should().Equal("Anna", "Bernat");
    }

    [Fact]
    public async Task A_backup_from_this_version_still_restores()
    {
        var current = await Prepare(fromNewerVersion: false);

        await Service().Restore(current.Path);

        (await LiveClients()).Should().Equal("Anna");
    }

    [Fact]
    public async Task The_restore_dialog_tells_the_user_to_update_the_app_for_a_newer_backup()
    {
        var newer = await Prepare(fromNewerVersion: true);
        var dialogs = new TestDialogService { ResultConfirm = true };
        var vm = new RestoreDialogViewModel(Service(), dialogs);
        await vm.Load();
        vm.Selected = vm.Backups.Single(b => b.Path == newer.Path);

        await vm.RestoreCommand.ExecuteAsync(null);

        vm.ErrorValidation.Should().Be(Texts.BackupFromNewerVersion);
        vm.ErrorValidation.Should().NotBe(Texts.BackupNotRestorable,
            "the copy is not damaged: picking another copy is not the only way out, updating is");
        dialogs.InfoDisplayed.Should().BeEmpty("nothing was restored");
        (await LiveClients()).Should().Equal("Anna", "Bernat");
    }
}
