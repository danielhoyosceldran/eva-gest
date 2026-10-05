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
/// F-01, closing side: when the app closes and no automatic copy has been taken today,
/// it takes one, whatever the configured hour (20:00 here). A copy taken at startup that
/// morning only caught up yesterday, so it does not count as today's.
///
/// "Today" is Wednesday 4 March 2026 and the clock is moved by the test.
/// </summary>
public sealed class BackupOnCloseTests : IDisposable
{
    private const int PageSize = 4096;
    private static readonly DateTime Today = new(2026, 3, 4);

    private readonly string _folder = Path.Combine(Path.GetTempPath(), "EvaGestBackupOnClose_" + Guid.NewGuid());
    private readonly string _live;
    private readonly string _backupsFolder;
    private DateTime _now = Today.AddHours(9);

    public BackupOnCloseTests()
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

    /// <summary>A sound database with EF's history table and rows spanning many pages,
    /// so one page in the middle can be damaged.</summary>
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

    private static void DamageAPage(string path)
    {
        var bytes = File.ReadAllBytes(path);
        int middlePage = bytes.Length / PageSize / 2;
        for (int i = 0; i < 64; i++) bytes[middlePage * PageSize + i] = 0xFF;
        File.WriteAllBytes(path, bytes);
    }

    /// <summary>One service for the whole test, reading the clock the test moves.</summary>
    private BackupService Backups()
        => new(new AppPaths(_live, _folder), new TestSettings((ConfigKeys.BackupTime, "20:00")), () => _now);

    private void ExistingBackup(DateTime takenAt, string kind)
        => File.WriteAllText(Path.Combine(_backupsFolder, $"{takenAt:yyyyMMdd_HHmmssfff}_{kind}.db"), "x");

    private async Task<List<BackupInfo>> TakenToday(IBackupService backups)
        => (await backups.ListAll()).Where(b => b.Date.Date == Today).ToList();

    [Fact]
    public async Task Closing_at_one_with_no_copy_today_takes_one_listed_as_automatic()
    {
        ExistingBackup(Today.AddDays(-1).AddHours(20).AddMinutes(30), "auto");
        var backups = Backups();
        _now = Today.AddHours(13);

        var result = await backups.RunBackupOnCloseIfDue();

        result.Should().NotBeNull("closing is the end of the day's work, whatever the hour");
        File.Exists(result!.Path).Should().BeTrue();
        var today = await TakenToday(backups);
        today.Should().ContainSingle();
        today[0].IsAutomatic.Should().BeTrue("the closing copy is the day's automatic copy");
        today[0].IsOnClose.Should().BeTrue();
    }

    [Fact]
    public async Task Closing_twice_the_same_day_takes_only_one_copy()
    {
        ExistingBackup(Today.AddDays(-1).AddHours(20).AddMinutes(30), "auto");
        var backups = Backups();

        _now = Today.AddHours(13);
        var first = await backups.RunBackupOnCloseIfDue();
        _now = Today.AddHours(18);
        var second = await backups.RunBackupOnCloseIfDue();

        first.Should().NotBeNull();
        second.Should().BeNull("today's copy already exists");
        (await TakenToday(backups)).Should().ContainSingle();
    }

    [Fact]
    public async Task A_morning_catch_up_copy_does_not_count_as_todays_on_closing()
    {
        // No copy covers yesterday, so this morning's start catches it up...
        var backups = Backups();
        _now = Today.AddHours(9);
        var catchUp = await backups.RunAutomaticBackupIfDue();
        catchUp.Should().NotBeNull("precondition: the start caught up yesterday");

        // ...and that copy is yesterday's: closing at 13:00 still takes today's.
        _now = Today.AddHours(13);
        var onClose = await backups.RunBackupOnCloseIfDue();

        onClose.Should().NotBeNull("the morning copy only covered yesterday's 20:00");
        (await TakenToday(backups)).Should().HaveCount(2);
    }

    [Fact]
    public async Task Closing_after_the_evening_copy_of_today_takes_nothing()
    {
        var backups = Backups();
        _now = Today.AddHours(20).AddMinutes(30);
        (await backups.RunAutomaticBackupIfDue()).Should().NotBeNull("precondition: today's copy");

        _now = Today.AddHours(21);
        var onClose = await backups.RunBackupOnCloseIfDue();

        onClose.Should().BeNull("today already has its automatic copy");
        (await TakenToday(backups)).Should().ContainSingle();
    }

    // ── The shell, as the window calls it on closing ─────────────────────────

    private static MainWindowViewModel Shell(TestDatabase testDb, IBackupService backups, TestDialogService dialogs)
    {
        var factory = new TestFactory(testDb.Options);
        var config = new SettingsService(factory);
        var owner = TestOwner.New();
        var appointments = new AppointmentService(factory);
        var sales = new SaleService(factory, config);

        return new MainWindowViewModel(
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
            new SettingsViewModel(backups, new ExportService(factory), config,
                new AvailabilityService(factory), dialogs, owner),
            dialogs, appointments, new AppointmentChangeNotifier(), owner, backups);
    }

    [Fact]
    public async Task Closing_with_a_copy_that_fails_tells_the_user_and_keeps_no_bad_copy()
    {
        DamageAPage(_live);
        _now = Today.AddHours(13);
        var backups = Backups();
        var dialogs = new TestDialogService();
        await using var testDb = new TestDatabase();
        var shell = Shell(testDb, backups, dialogs);

        var closing = () => shell.RunBackupOnClose();

        await closing.Should().NotThrowAsync("the app must still be able to close");
        dialogs.InfoDisplayed.Should().NotBeEmpty("the owner must learn that today has no copy");
        (await backups.ListAll()).Should().BeEmpty("a copy that failed its check is not kept");
        Directory.GetFiles(_backupsFolder, "*.db").Should().BeEmpty();
    }

    [Fact]
    public async Task Closing_with_a_copy_that_works_keeps_the_days_automatic_copy()
    {
        _now = Today.AddHours(13);
        var backups = Backups();
        var dialogs = new TestDialogService();
        await using var testDb = new TestDatabase();
        var shell = Shell(testDb, backups, dialogs);

        await shell.RunBackupOnClose();

        (await TakenToday(backups)).Should().ContainSingle(b => b.IsAutomatic);
    }
}
