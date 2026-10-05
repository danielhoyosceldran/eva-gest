using AwesomeAssertions;
using EvaGest.Models;
using EvaGest.Services;
using EvaGest.Tests.Infra;
using EvaGest.ViewModels.Dialogs;
using Xunit;

namespace EvaGest.Tests.Services;

/// <summary>
/// CU-01b: an overlapping appointment only warns, and can be saved anyway. The problem
/// was that the warning was a red line the user could click straight past: booking a
/// worker into a slot already taken saved with no question asked. Save has to ask;
/// "no" saves nothing, "yes" saves it, and a booking with room asks nothing at all.
/// Two workers with the whole week on their schedule, so a slot can actually have room
/// (a shop where nobody has a schedule has no capacity anywhere).
/// </summary>
public class AppointmentOverlapConfirmTests
{
    private static readonly DateOnly Monday = new(2026, 10, 12);
    private static readonly TimeOnly Ten = new(10, 0);

    private sealed record Shop(
        AppointmentService Appointments, AvailabilityService Availability, ClientService Clients,
        CatalogService Catalog, WorkerService Workers, SettingsService Settings,
        int MartaId, int PereId);

    private static async Task<Shop> Build(TestDatabase testDb)
    {
        var factory = new TestFactory(testDb.Options);
        var workers = new WorkerService(factory);

        var allWeek = Enum.GetValues<Weekday>().ToDictionary(
            d => d, _ => new List<(TimeOnly start, TimeOnly fi)> { (new TimeOnly(9, 0), new TimeOnly(20, 0)) });
        var marta = await workers.Create(Make.Worker("Marta"), allWeek);
        var pere = await workers.Create(Make.Worker("Pere"), allWeek);

        return new Shop(new AppointmentService(factory), new AvailabilityService(factory),
            new ClientService(factory), new CatalogService(factory), workers, new SettingsService(factory),
            marta.Id, pere.Id);
    }

    /// <summary>Marta already has 10:00-10:30 on Monday.</summary>
    private static async Task<int> MartaIsTakenAtTen(Shop shop)
        => await shop.Appointments.Create(new Appointment
        {
            Date = Monday, Time = Ten, DurationMin = 30, GuestName = "Anna",
            WorkerId = shop.MartaId, Status = AppointmentStatus.Pending
        });

    private static async Task<AppointmentDialogViewModel> NewBooking(
        Shop shop, TestDialogService dialogs, int? workerId, string time = "10:00")
    {
        var vm = new AppointmentDialogViewModel(shop.Appointments, shop.Availability, shop.Clients,
            shop.Catalog, shop.Workers, shop.Settings, dialogs, Monday);
        await vm.Initialization;
        vm.TextClient = "Bernat";
        vm.TimeText = time;
        vm.DurationText = "30";
        vm.Worker = workerId is int id ? vm.ActiveWorkers.Single(w => w.Id == id) : null;
        return vm;
    }

    private static async Task<List<string>> BookedOnMonday(Shop shop)
        => (await shop.Appointments.GetByDay(Monday)).Select(a => a.DisplayName).OrderBy(n => n).ToList();

    [Fact]
    public async Task Booking_a_worker_into_a_slot_already_taken_asks_first()
    {
        await using var testDb = new TestDatabase();
        var shop = await Build(testDb);
        await MartaIsTakenAtTen(shop);
        var dialogs = new TestDialogService { ResultConfirm = true };

        var vm = await NewBooking(shop, dialogs, shop.MartaId);
        await vm.SaveCommand.ExecuteAsync(null);

        dialogs.ConfirmacionsRequested.Should().ContainSingle("the overlap must be a question, not a line to click past");
    }

    [Fact]
    public async Task Answering_no_to_the_overlap_question_saves_nothing()
    {
        await using var testDb = new TestDatabase();
        var shop = await Build(testDb);
        await MartaIsTakenAtTen(shop);
        var dialogs = new TestDialogService { ResultConfirm = false };

        var vm = await NewBooking(shop, dialogs, shop.MartaId);
        bool closed = false;
        vm.Close += _ => closed = true;
        await vm.SaveCommand.ExecuteAsync(null);

        dialogs.ConfirmacionsRequested.Should().ContainSingle();
        (await BookedOnMonday(shop)).Should().Equal("Anna");
        closed.Should().BeFalse("the dialog stays open so the time can be changed");
    }

    [Fact]
    public async Task Answering_yes_to_the_overlap_question_saves_it_anyway()
    {
        // CU-01b: an overlap never forbids saving.
        await using var testDb = new TestDatabase();
        var shop = await Build(testDb);
        await MartaIsTakenAtTen(shop);
        var dialogs = new TestDialogService { ResultConfirm = true };

        var vm = await NewBooking(shop, dialogs, shop.MartaId);
        await vm.SaveCommand.ExecuteAsync(null);

        dialogs.ConfirmacionsRequested.Should().ContainSingle();
        (await BookedOnMonday(shop)).Should().Equal("Anna", "Bernat");
    }

    [Fact]
    public async Task Booking_a_worker_who_is_free_asks_nothing()
    {
        await using var testDb = new TestDatabase();
        var shop = await Build(testDb);
        await MartaIsTakenAtTen(shop);
        var dialogs = new TestDialogService { ResultConfirm = false };

        var vm = await NewBooking(shop, dialogs, shop.PereId);
        await vm.SaveCommand.ExecuteAsync(null);

        dialogs.ConfirmacionsRequested.Should().BeEmpty();
        (await BookedOnMonday(shop)).Should().Equal("Anna", "Bernat");
    }

    [Fact]
    public async Task Booking_without_a_worker_where_someone_is_still_free_asks_nothing()
    {
        await using var testDb = new TestDatabase();
        var shop = await Build(testDb);
        await MartaIsTakenAtTen(shop);   // one of two workers busy: room for one more
        var dialogs = new TestDialogService { ResultConfirm = false };

        var vm = await NewBooking(shop, dialogs, workerId: null);
        await vm.SaveCommand.ExecuteAsync(null);

        dialogs.ConfirmacionsRequested.Should().BeEmpty();
        (await BookedOnMonday(shop)).Should().Equal("Anna", "Bernat");
    }

    [Fact]
    public async Task Booking_the_worker_right_after_their_appointment_ends_asks_nothing()
    {
        // 10:30 starts exactly when Anna's ends: touching is not overlapping.
        await using var testDb = new TestDatabase();
        var shop = await Build(testDb);
        await MartaIsTakenAtTen(shop);
        var dialogs = new TestDialogService { ResultConfirm = false };

        var vm = await NewBooking(shop, dialogs, shop.MartaId, time: "10:30");
        await vm.SaveCommand.ExecuteAsync(null);

        dialogs.ConfirmacionsRequested.Should().BeEmpty();
        (await BookedOnMonday(shop)).Should().Equal("Anna", "Bernat");
    }

    [Fact]
    public async Task Editing_an_accepted_overlap_without_moving_it_does_not_ask_again()
    {
        await using var testDb = new TestDatabase();
        var shop = await Build(testDb);
        await MartaIsTakenAtTen(shop);
        // Saved earlier on top of Anna, once the user said "save anyway".
        int bernat = await shop.Appointments.Create(new Appointment
        {
            Date = Monday, Time = Ten, DurationMin = 30, GuestName = "Bernat",
            WorkerId = shop.MartaId, Status = AppointmentStatus.Pending
        });
        var dialogs = new TestDialogService { ResultConfirm = false };

        var vm = new AppointmentDialogViewModel(shop.Appointments, shop.Availability, shop.Clients,
            shop.Catalog, shop.Workers, shop.Settings, dialogs, (await shop.Appointments.GetById(bernat))!);
        await vm.Initialization;
        vm.Notes = "Arribarà deu minuts tard";
        await vm.SaveCommand.ExecuteAsync(null);

        dialogs.ConfirmacionsRequested.Should().BeEmpty("the user already accepted this overlap");
        (await shop.Appointments.GetById(bernat))!.Notes.Should().Be("Arribarà deu minuts tard");
    }

    [Fact]
    public async Task Editing_an_appointment_into_a_taken_slot_asks_first()
    {
        await using var testDb = new TestDatabase();
        var shop = await Build(testDb);
        await MartaIsTakenAtTen(shop);
        int bernat = await shop.Appointments.Create(new Appointment
        {
            Date = Monday, Time = new TimeOnly(12, 0), DurationMin = 30, GuestName = "Bernat",
            WorkerId = shop.MartaId, Status = AppointmentStatus.Pending
        });
        var dialogs = new TestDialogService { ResultConfirm = false };

        var vm = new AppointmentDialogViewModel(shop.Appointments, shop.Availability, shop.Clients,
            shop.Catalog, shop.Workers, shop.Settings, dialogs, (await shop.Appointments.GetById(bernat))!);
        await vm.Initialization;
        vm.TimeText = "10:15";
        await vm.SaveCommand.ExecuteAsync(null);

        dialogs.ConfirmacionsRequested.Should().ContainSingle();
        (await shop.Appointments.GetById(bernat))!.Time.Should().Be(new TimeOnly(12, 0), "the answer was no");
    }
}
