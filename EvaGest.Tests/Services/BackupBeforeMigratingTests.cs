using AwesomeAssertions;
using EvaGest.Data;
using EvaGest.Services;
using EvaGest.Tests.Infra;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Xunit;

namespace EvaGest.Tests.Services;

/// <summary>
/// The first start of a new version must copy the database before its migrations change
/// the schema. Startup used to migrate straight away, and the daily backup only runs after
/// that, so there was no copy of the data as the previous version left it.
/// The "previous version" here is a real file database migrated by EF to every migration
/// but the last, so this build sees exactly one pending migration.
/// </summary>
public sealed class BackupBeforeMigratingTests : IDisposable
{
    private readonly string _folder = Path.Combine(Path.GetTempPath(), "EvaGestBeforeUpdate_" + Guid.NewGuid());
    private readonly string _live;

    public BackupBeforeMigratingTests()
    {
        Directory.CreateDirectory(_folder);
        _live = Path.Combine(_folder, "barberia.db");
    }

    public void Dispose()
    {
        SqlitePools.Release(_live);
        Directory.Delete(_folder, recursive: true);
    }

    private ShopDbContext Context() => new(new DbContextOptionsBuilder<ShopDbContext>()
        .UseSqlite($"Data Source={_live}")
        .UseSnakeCaseNamingConvention()
        .Options);

    private BackupService Service() => new(new AppPaths(_live, _folder), new TestSettings());

    private static string LatestMigration()
    {
        using var db = new ShopDbContext(new DbContextOptionsBuilder<ShopDbContext>()
            .UseSqlite("Data Source=:memory:").UseSnakeCaseNamingConvention().Options);
        return db.Database.GetMigrations().Last();
    }

    /// <summary>A database as the previous version left it: every migration applied but
    /// the newest, with a client in it.</summary>
    private async Task WritePreviousVersionDatabase()
    {
        await using var db = Context();
        var all = db.Database.GetMigrations().ToList();
        await db.GetService<IMigrator>().MigrateAsync(all[^2]);

        // Raw SQL rather than EF: the current model describes the schema after the
        // newest migration, not the one this file is still on.
        await using var connection = new SqliteConnection($"Data Source={_live};Pooling=False");
        connection.Open();
        await using var command = connection.CreateCommand();
        command.CommandText =
            "INSERT INTO clients (name, mobile, client_key, asleep) VALUES ('Anna', '612345678', 'anna', 0)";
        command.ExecuteNonQuery();
    }

    private static List<string> AppliedMigrations(string path)
    {
        using var connection = new SqliteConnection($"Data Source={path};Mode=ReadOnly;Pooling=False");
        connection.Open();
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT * FROM __EFMigrationsHistory";
        using var reader = command.ExecuteReader();
        var ids = new List<string>();
        while (reader.Read()) ids.Add(reader.GetString(0));
        return ids;
    }

    private static List<string> ClientNames(string path)
    {
        using var connection = new SqliteConnection($"Data Source={path};Mode=ReadOnly;Pooling=False");
        connection.Open();
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT name FROM clients";
        using var reader = command.ExecuteReader();
        var names = new List<string>();
        while (reader.Read()) names.Add(reader.GetString(0));
        return names;
    }

    [Fact]
    public async Task A_pending_migration_on_an_existing_database_takes_a_backup_first()
    {
        await WritePreviousVersionDatabase();
        var backups = Service();

        await using var db = Context();
        var taken = await DatabaseUpdate.BackupBeforeMigrating(db, _live, backups);

        taken.Should().NotBeNull();
        (await backups.ListAll()).Should().ContainSingle(b => b.Path == taken!.Path);
    }

    [Fact]
    public async Task The_backup_holds_the_database_as_it_was_before_the_migration()
    {
        await WritePreviousVersionDatabase();
        var backups = Service();

        // Startup, in order: the copy, then the migration.
        BackupInfo? taken;
        await using (var db = Context())
        {
            taken = await DatabaseUpdate.BackupBeforeMigrating(db, _live, backups);
            await db.Database.MigrateAsync();
        }

        AppliedMigrations(_live).Should().Contain(LatestMigration());
        AppliedMigrations(taken!.Path).Should().NotContain(LatestMigration(),
            "the copy is of the schema the previous version left, not the migrated one");
        ClientNames(taken.Path).Should().Equal("Anna");
    }

    [Fact]
    public async Task The_restore_list_marks_the_copy_as_taken_before_an_update()
    {
        await WritePreviousVersionDatabase();
        var backups = Service();

        await using var db = Context();
        await DatabaseUpdate.BackupBeforeMigrating(db, _live, backups);

        var listed = (await backups.ListAll()).Should().ContainSingle().Subject;
        listed.IsBeforeUpdate.Should().BeTrue();
        listed.IsAutomatic.Should().BeFalse();
    }

    [Fact]
    public async Task A_first_run_with_no_database_file_takes_no_backup()
    {
        var backups = Service();

        await using var db = Context();
        var taken = await DatabaseUpdate.BackupBeforeMigrating(db, _live, backups);

        taken.Should().BeNull("there is nothing to lose yet");
        (await backups.ListAll()).Should().BeEmpty();
        File.Exists(_live).Should().BeFalse("checking must not create the file");
    }

    [Fact]
    public async Task A_database_already_up_to_date_takes_no_backup()
    {
        await using (var db = Context())
            await db.Database.MigrateAsync();
        var backups = Service();

        await using var again = Context();
        var taken = await DatabaseUpdate.BackupBeforeMigrating(again, _live, backups);

        taken.Should().BeNull("an ordinary start changes no schema");
        (await backups.ListAll()).Should().BeEmpty();
    }

    [Fact]
    public async Task A_backup_that_cannot_be_written_stops_startup_before_anything_is_migrated()
    {
        await WritePreviousVersionDatabase();
        // A file where the backups folder should be: the copy cannot be written.
        await File.WriteAllTextAsync(Path.Combine(_folder, "Backups"), "not a folder");

        await using var db = Context();
        var step = () => DatabaseUpdate.BackupBeforeMigrating(db, _live, Service());

        // It has to throw: returning normally is what lets startup go on to migrate.
        await step.Should().ThrowAsync<BackupBeforeUpdateFailedException>();
        AppliedMigrations(_live).Should().NotContain(LatestMigration());
        (await db.Database.GetPendingMigrationsAsync()).Should().ContainSingle();
    }
}
