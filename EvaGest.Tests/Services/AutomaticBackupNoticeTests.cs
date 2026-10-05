using AwesomeAssertions;
using EvaGest.Models;
using EvaGest.Services;
using EvaGest.Tests.Infra;
using EvaGest.ViewModels;
using EvaGest.ViewModels.Pages;
using Microsoft.Data.Sqlite;
using Xunit;

namespace EvaGest.Tests.Services;

/// <summary>
/// The daily automatic backup runs at startup. When it failed, the app only wrote it to
/// the log, so the shop could go days without a new copy and nobody would know: once the
/// live database is damaged, every daily copy fails its check. The app must still open,
/// and the shell must say so on whatever page is open, until a good copy is taken or the
/// user closes the notice.
///
/// The failure is real, not a fake that throws: the live database has one damaged table
/// page, which SQLite's backup copies faithfully, so the new copy fails its check (the
/// same technique as <see cref="BackupVerificationTests"/>).
/// </summary>
public sealed class AutomaticBackupNoticeTests : IDisposable
{
    private const int PageSize = 4096;

    private readonly string _folder = Path.Combine(Path.GetTempPath(), "EvaGestAutoBackupNotice_" + Guid.NewGuid());
    private readonly string _live;

    public AutomaticBackupNoticeTests()
    {
        Directory.CreateDirectory(_folder);
        _live = Path.Combine(_folder, "barberia.db");
        WriteDatabase(_live);
    }

    public void Dispose()
    {
        SqlitePools.Release(_live);
        Directory.Delete(_folder, recursive: true);
    }

    /// <summary>A sound database with EF's history table and rows spanning many pages.</summary>
    private static void WriteDatabase(string path)
    {
        using var connection = new SqliteConnection($"Data Source={path};Pooling=False");
        connection.Open();
        using var command = connection.CreateCommand();
        command.CommandText =
            $"PRAGMA page_size = {PageSize}; " +
            "CREATE TABLE __EFMigrationsHistory (migration_id TEXT PRIMARY KEY, product_version TEXT); " +
            "INSERT INTO __EFMigrationsHistory VALUES ('20260905121102_InitialCreate', '9.0.0'); " +
            "CREATE TABLE content (v TEXT); " +
            "WITH RECURSIVE n(i) AS (SELECT 1 UNION ALL SELECT i + 1 FROM n WHERE i < 2000) " +
            "INSERT INTO content SELECT 'fila ' || i || ' ' || hex(zeroblob(80)) FROM n;";
        command.ExecuteNonQuery();
    }

    /// <summary>Overwrites the header of a table page in the middle of the file.</summary>
    private static void DamageAPage(string path)
    {
        var bytes = File.ReadAllBytes(path);
        int middlePage = bytes.Length / PageSize / 2;
        for (int i = 0; i < 64; i++) bytes[middlePage * PageSize + i] = 0xFF;
        File.WriteAllBytes(path, bytes);
    }

    /// <summary>The backups as the app wires them: one service shared by the shell and the
    /// Settings page. The clock starts after the configured hour, so the daily backup is
    /// due whatever time the suite runs, and moves a second per reading so two copies
    /// never get the same file name.</summary>
    private BackupService Backups(string backupTime = "20:00")
    {
        int ticks = 0;
        return new(new AppPaths(_live, _folder),
                   new TestSettings((ConfigKeys.BackupTime, backupTime)),
                   () => DateTime.Today.AddHours(21).AddSeconds(ticks++));
    }

    private sealed record App(MainWindowViewModel Shell, SettingsViewModel Settings, TestDialogService Dialogs);

    private static async Task<App> Build(TestDatabase testDb, IBackupService backups)
    {
        var factory = new TestFactory(testDb.Options);
        var config = new SettingsService(factory);
        var dialogs = new TestDialogService();
        var owner = await TestOwner.Unlocked();
        var appointments = new AppointmentService(factory);
        var sales = new SaleService(factory, config);
        var settingsPage = new SettingsViewModel(backups, new ExportService(factory), config,
            new AvailabilityService(factory), dialogs, owner);

        var shell = new MainWindowViewModel(
            new HomeViewModel(appointments, sales, new TillService(factory), new ClientService(factory),
                new AvailabilityService(factory), new CatalogService(factory), new WorkerService(factory),
                config, new TestSoundService(), dialogs, owner),
            new CatalogViewModel(new CatalogService(factory), config, dialogs),
            new ClientsViewModel(new ClientService(factory), new ReportsService(factory), dialogs, owner),
            new WorkersViewModel(new WorkerService(factory), dialogs),
            new AgendaViewModel(appointments, sales, new AvailabilityService(factory), new ClientService(factory),
                new CatalogService(factory), new WorkerService(factory), config, new TestSoundService(), dialogs),
            new SalesViewModel(sales, new ClientService(factory), new CatalogService(factory),
                new WorkerService(factory), new TestSoundService(), config, new ExportService(factory), dialogs),
            new TillViewModel(new TillService(factory), new CatalogService(factory), new WorkerService(factory),
                config, dialogs),
            new ReportsViewModel(new ReportsService(factory)),
            settingsPage,
            dialogs, appointments, new AppointmentChangeNotifier(), owner, backups);

        return new App(shell, settingsPage, dialogs);
    }

    [Fact]
    public async Task A_failed_automatic_backup_lets_the_app_open_and_puts_up_a_notice()
    {
        DamageAPage(_live);
        await using var testDb = new TestDatabase();
        var backups = Backups();
        var app = await Build(testDb, backups);

        var startup = () => app.Shell.RunAutomaticBackup();

        await startup.Should().NotThrowAsync("the shop still has to work without today's copy");
        app.Shell.BackupFailedNotice.Should().NotBeNullOrWhiteSpace();
        (await backups.ListAll()).Should().BeEmpty("the damaged copy was not kept");
    }

    [Fact]
    public async Task The_notice_stays_up_whichever_page_is_open()
    {
        DamageAPage(_live);
        await using var testDb = new TestDatabase();
        var app = await Build(testDb, Backups());
        await app.Shell.RunAutomaticBackup();
        string? notice = app.Shell.BackupFailedNotice;

        foreach (var navigate in new[]
                 {
                     app.Shell.NavigateAgendaCommand, app.Shell.NavigateClientsCommand,
                     app.Shell.NavigateSalesCommand, app.Shell.NavigateSettingsCommand,
                     app.Shell.NavigateHomeCommand
                 })
        {
            navigate.Execute(null);
            app.Shell.BackupFailedNotice.Should().NotBeNull().And.Be(notice);
        }
    }

    [Fact]
    public async Task A_good_backup_taken_from_settings_takes_the_notice_down()
    {
        DamageAPage(_live);
        await using var testDb = new TestDatabase();
        var app = await Build(testDb, Backups());
        await app.Shell.RunAutomaticBackup();
        app.Shell.BackupFailedNotice.Should().NotBeNull();

        // The database is put right (repaired, or a copy restored), and the owner presses
        // "Fer còpia ara" as the notice asks.
        File.Delete(_live);
        WriteDatabase(_live);
        await app.Settings.BackupNowCommand.ExecuteAsync(null);

        app.Settings.ErrorBackup.Should().BeNull("this copy passed its check");
        app.Shell.BackupFailedNotice.Should().BeNull("a good copy exists again");
    }

    [Fact]
    public async Task A_manual_backup_that_also_fails_leaves_the_notice_up()
    {
        DamageAPage(_live);
        await using var testDb = new TestDatabase();
        var app = await Build(testDb, Backups());
        await app.Shell.RunAutomaticBackup();

        await app.Settings.BackupNowCommand.ExecuteAsync(null);

        app.Settings.ErrorBackup.Should().NotBeNull();
        app.Shell.BackupFailedNotice.Should().NotBeNull("there is still no good copy");
    }

    [Fact]
    public async Task An_unchecked_safety_copy_does_not_count_as_a_good_backup()
    {
        // The copy taken right before a restore is deliberately not checked (it may be as
        // damaged as the database it copies), so it must not take the notice down.
        var backups = Backups();
        var good = await backups.MakeManualBackup();
        DamageAPage(_live);
        await using var testDb = new TestDatabase();
        var app = await Build(testDb, backups);
        await app.Shell.RunAutomaticBackup();
        app.Shell.BackupFailedNotice.Should().NotBeNull();

        await backups.Restore(good.Path);   // starts with an unchecked copy of the damaged file

        app.Shell.BackupFailedNotice.Should().NotBeNull();
    }

    [Fact]
    public async Task The_user_can_close_the_notice()
    {
        DamageAPage(_live);
        await using var testDb = new TestDatabase();
        var app = await Build(testDb, Backups());
        await app.Shell.RunAutomaticBackup();

        app.Shell.DismissBackupNoticeCommand.Execute(null);

        app.Shell.BackupFailedNotice.Should().BeNull();
    }

    [Fact]
    public async Task An_automatic_backup_that_works_shows_no_notice()
    {
        await using var testDb = new TestDatabase();
        var backups = Backups();
        var app = await Build(testDb, backups);

        await app.Shell.RunAutomaticBackup();

        app.Shell.BackupFailedNotice.Should().BeNull();
        (await backups.ListAll()).Should().ContainSingle(b => b.IsAutomatic);
    }

    [Fact]
    public async Task An_automatic_backup_not_due_yet_shows_no_notice()
    {
        // Damaged, but before today's configured hour, with yesterday's moment already
        // covered by a copy, nothing is attempted, so nothing failed.
        DamageAPage(_live);
        string folderBackups = Path.Combine(_folder, "Backups");
        Directory.CreateDirectory(folderBackups);
        string yesterday = DateTime.Today.AddDays(-1).AddHours(23).AddMinutes(30).ToString("yyyyMMdd_HHmmssfff");
        File.WriteAllText(Path.Combine(folderBackups, $"{yesterday}_auto.db"), "x");
        await using var testDb = new TestDatabase();
        var backups = Backups(backupTime: "23:00");
        var app = await Build(testDb, backups);

        await app.Shell.RunAutomaticBackup();

        app.Shell.BackupFailedNotice.Should().BeNull();
        (await backups.ListAll()).Should().ContainSingle("only yesterday's copy, nothing new");
    }

    [Fact]
    public async Task An_automatic_backup_already_taken_today_shows_no_notice()
    {
        await using var testDb = new TestDatabase();
        var backups = Backups();
        await backups.RunAutomaticBackupIfDue();   // this morning's start
        DamageAPage(_live);
        var app = await Build(testDb, backups);

        await app.Shell.RunAutomaticBackup();      // a second start the same day

        app.Shell.BackupFailedNotice.Should().BeNull();
        (await backups.ListAll()).Should().ContainSingle();
    }
}
