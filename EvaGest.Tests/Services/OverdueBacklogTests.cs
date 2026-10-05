using AwesomeAssertions;
using EvaGest.Models;
using EvaGest.Resources;
using EvaGest.Services;
using EvaGest.Tests.Infra;
using EvaGest.ViewModels;
using EvaGest.ViewModels.Pages;
using Xunit;

namespace EvaGest.Tests.Services;

/// <summary>
/// The overdue-appointments check must keep reporting an appointment left Pending until
/// someone resolves it, however long ago it was. It used to look back a single day, so a
/// Friday appointment nobody marked had silently dropped out of the notice by Sunday.
/// With no lower bound the backlog can be long, so the shell notice must stay readable.
/// </summary>
public class OverdueBacklogTests
{
    // A Sunday, at noon.
    private static readonly DateTime SundayNoon = new(2026, 10, 4, 12, 0, 0);

    private static async Task<int> AddAppointment(TestDatabase testDb, DateOnly date, TimeOnly time,
        string guest, AppointmentStatus status = AppointmentStatus.Pending)
    {
        await using var db = testDb.Context();
        var appointment = new Appointment
        {
            Date = date, Time = time, DurationMin = 30, GuestName = guest, Status = status
        };
        db.Appointments.Add(appointment);
        await db.SaveChangesAsync();
        return appointment.Id;
    }

    [Fact]
    public async Task A_pending_appointment_from_last_friday_is_still_overdue_on_sunday()
    {
        await using var testDb = new TestDatabase();
        int friday = await AddAppointment(testDb, new DateOnly(2026, 10, 2), new TimeOnly(10, 0), "Pere");

        var overdue = await new AppointmentService(new TestFactory(testDb.Options)).GetOverduePending(SundayNoon);

        overdue.Select(a => a.Id).Should().Equal([friday],
            "nobody charged, cancelled or marked it, so it is still waiting to be resolved");
    }

    [Fact]
    public async Task A_pending_appointment_from_weeks_ago_is_still_overdue()
    {
        await using var testDb = new TestDatabase();
        int threeWeeksAgo = await AddAppointment(testDb, new DateOnly(2026, 9, 13), new TimeOnly(17, 30), "Pere");
        int lastMonth = await AddAppointment(testDb, new DateOnly(2026, 8, 20), new TimeOnly(9, 0), "Anna");

        var overdue = await new AppointmentService(new TestFactory(testDb.Options)).GetOverduePending(SundayNoon);

        overdue.Select(a => a.Id).Should().BeEquivalentTo([threeWeeksAgo, lastMonth]);
    }

    [Fact]
    public async Task An_old_appointment_already_resolved_is_not_overdue()
    {
        // The other side: dropping the lower bound must not drag in what was dealt with.
        await using var testDb = new TestDatabase();
        await AddAppointment(testDb, new DateOnly(2026, 10, 2), new TimeOnly(10, 0), "Pere", AppointmentStatus.Cancelled);
        await AddAppointment(testDb, new DateOnly(2026, 9, 13), new TimeOnly(10, 0), "Anna", AppointmentStatus.NoShow);
        await AddAppointment(testDb, new DateOnly(2026, 9, 1), new TimeOnly(10, 0), "Joan", AppointmentStatus.Completed);

        var overdue = await new AppointmentService(new TestFactory(testDb.Options)).GetOverduePending(SundayNoon);

        overdue.Should().BeEmpty();
    }

    private static MainWindowViewModel BuildShell(TestDatabase testDb)
    {
        var factory = new TestFactory(testDb.Options);
        var config = new SettingsService(factory);
        var dialogs = new TestDialogService();
        var owner = TestOwner.New();
        var appointments = new AppointmentService(factory);
        var sales = new SaleService(factory, config);
        var paths = new AppPaths(Path.Combine(Path.GetTempPath(), "eva-prova.db"), Path.GetTempPath());

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
            new SettingsViewModel(new BackupService(paths, config), new ExportService(factory), config,
                new AvailabilityService(factory), dialogs, owner),
            dialogs, appointments, new AppointmentChangeNotifier(), owner);
    }

    [Fact]
    public async Task A_long_backlog_names_the_five_oldest_and_counts_the_rest()
    {
        // Yesterday, so the old one-day window would still have found all seven: this is
        // about the notice staying readable, not about how far back the check looks.
        await using var testDb = new TestDatabase();
        var yesterday = DateOnly.FromDateTime(DateTime.Today.AddDays(-1));
        string[] names = ["Anna", "Bernat", "Carla", "David", "Elena", "Ferran", "Gemma"];
        for (int i = 0; i < names.Length; i++)
            await AddAppointment(testDb, yesterday, new TimeOnly(9, 0).AddMinutes(30 * i), names[i]);

        var shell = BuildShell(testDb);
        await shell.CheckOverdueAppointments();

        string? notice = shell.OverdueAppointmentsNotice;
        notice.Should().NotBeNull();
        foreach (var name in names.Take(5))
            notice.Should().Contain(name, "the five oldest are named");
        notice.Should().NotContain("Ferran").And.NotContain("Gemma");
        notice.Should().Contain(string.Format(Texts.OverdueAppointmentsMore, 2), "the rest are counted");
    }

    [Fact]
    public async Task A_short_backlog_names_everyone_and_counts_nothing()
    {
        await using var testDb = new TestDatabase();
        var yesterday = DateOnly.FromDateTime(DateTime.Today.AddDays(-1));
        string[] names = ["Anna", "Bernat", "Carla", "David", "Elena"];
        for (int i = 0; i < names.Length; i++)
            await AddAppointment(testDb, yesterday, new TimeOnly(9, 0).AddMinutes(30 * i), names[i]);

        var shell = BuildShell(testDb);
        await shell.CheckOverdueAppointments();

        string? notice = shell.OverdueAppointmentsNotice;
        notice.Should().NotBeNull();
        foreach (var name in names) notice.Should().Contain(name);
    }

    [Fact]
    public async Task An_appointment_left_pending_days_ago_reaches_the_shell_notice()
    {
        await using var testDb = new TestDatabase();
        await AddAppointment(testDb, DateOnly.FromDateTime(DateTime.Today.AddDays(-5)), new TimeOnly(10, 0), "Pere");

        var shell = BuildShell(testDb);
        await shell.CheckOverdueAppointments();

        shell.OverdueAppointmentsNotice.Should().NotBeNull().And.Contain("Pere");
    }
}
