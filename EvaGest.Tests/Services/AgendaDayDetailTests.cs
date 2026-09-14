using AwesomeAssertions;
using EvaGest.Models;
using EvaGest.Services;
using EvaGest.Tests.Infra;
using EvaGest.ViewModels.Pages;
using Xunit;

namespace EvaGest.Tests.Services;

/// <summary>
/// Block U: the agenda's day detail (pantalles 2.2). The grid is deliberately compact
/// and hides the status buttons; this panel is where they are always visible.
/// </summary>
public class AgendaDetailDayTests
{
    private static readonly DateOnly Monday = new(2026, 9, 7);

    private static async Task<AgendaViewModel> Build(TestDatabase testDb, TestDialogService? dialogs = null)
    {
        var factory = new TestFactory(testDb.Options);
        var config = new SettingsService(factory);
        await new SeedService(factory, config).Seed();

        return new AgendaViewModel(
            new AppointmentService(factory), new SaleService(factory, config),
            new AvailabilityService(factory), new ClientService(factory),
            new CatalogService(factory), new WorkerService(factory), config,
            new TestSoundService(), dialogs ?? new TestDialogService());
    }

    private static async Task<Appointment> AddsAppointment(
        TestDatabase testDb, DateOnly date, TimeOnly time, AppointmentStatus status = AppointmentStatus.Pending)
    {
        await using var db = testDb.Context();
        var appointment = new Appointment
        {
            Date = date, Time = time, DurationMin = 30,
            GuestName = "Pere", Status = status
        };
        db.Appointments.Add(appointment);
        await db.SaveChangesAsync();
        return appointment;
    }

    [Fact] // U-01
    public async Task No_day_is_selected_at_first()
    {
        await using var testDb = new TestDatabase();
        var vm = await Build(testDb);
        await vm.Load();

        vm.HasSelectedDay.Should().BeFalse();
        vm.DayAppointments.Should().BeEmpty();
    }

    [Fact] // U-02
    public async Task Selecting_a_day_lists_its_appointments_in_order()
    {
        await using var testDb = new TestDatabase();
        await AddsAppointment(testDb, Monday, new TimeOnly(16, 0));
        await AddsAppointment(testDb, Monday, new TimeOnly(10, 0));
        await AddsAppointment(testDb, Monday.AddDays(1), new TimeOnly(11, 0));

        var vm = await Build(testDb);
        await vm.Load();
        await vm.SelectDayCommand.ExecuteAsync(Monday);

        vm.HasSelectedDay.Should().BeTrue();
        vm.SelectedDayText.Should().StartWith("Dilluns");
        vm.DayAppointments.Select(f => f.TimeText).Should().Equal("10:00", "16:00");
        vm.DayAppointments[0].ClientText.Should().Be("Pere");
        vm.DayAppointments[0].IsGuest.Should().BeTrue();
        vm.DayAppointments[0].ServiceText.Should().Be("Sense servei");
        vm.DayAppointments[0].WorkerText.Should().Be("Sense assignar");
    }

    [Fact] // U-03
    public async Task A_day_without_appointments_says_so_instead_of_going_blank()
    {
        await using var testDb = new TestDatabase();
        var vm = await Build(testDb);
        await vm.Load();

        await vm.SelectDayCommand.ExecuteAsync(Monday.AddDays(3));

        vm.HasSelectedDay.Should().BeTrue();
        vm.DayWithoutAppointments.Should().BeTrue();
    }

    [Fact] // U-04
    public async Task Only_a_pending_appointment_offers_a_status_change()
    {
        await using var testDb = new TestDatabase();
        await AddsAppointment(testDb, Monday, new TimeOnly(10, 0));
        await AddsAppointment(testDb, Monday, new TimeOnly(11, 0), AppointmentStatus.Cancelled);

        var vm = await Build(testDb);
        await vm.Load();
        await vm.SelectDayCommand.ExecuteAsync(Monday);

        vm.DayAppointments[0].IsPending.Should().BeTrue();
        vm.DayAppointments[1].IsPending.Should().BeFalse();
        vm.DayAppointments[1].StatusText.Should().Be("Cancel·lada");
    }

    [Fact] // U-05
    public async Task Changing_the_status_from_the_detail_refreshes_the_list()
    {
        await using var testDb = new TestDatabase();
        await AddsAppointment(testDb, Monday, new TimeOnly(10, 0));

        var vm = await Build(testDb);
        await vm.Grid.LoadWeek(Monday);
        await vm.SelectDayCommand.ExecuteAsync(Monday);

        await vm.MarkNoShowCommand.ExecuteAsync(vm.DayAppointments[0].Appointment);

        vm.DayAppointments[0].StatusText.Should().Be("No assistida");
        vm.DayAppointments[0].IsPending.Should().BeFalse();
    }

    [Fact] // U-06
    public async Task Closing_the_detail_hides_it()
    {
        await using var testDb = new TestDatabase();
        await AddsAppointment(testDb, Monday, new TimeOnly(10, 0));

        var vm = await Build(testDb);
        await vm.Load();
        await vm.SelectDayCommand.ExecuteAsync(Monday);

        vm.CloseDetailCommand.Execute(null);

        vm.HasSelectedDay.Should().BeFalse();
        vm.DayAppointments.Should().BeEmpty();
    }

    [Fact] // U-07
    public async Task Changing_week_closes_the_detail_of_a_day_no_longer_shown()
    {
        await using var testDb = new TestDatabase();
        await AddsAppointment(testDb, Monday, new TimeOnly(10, 0));

        var vm = await Build(testDb);
        await vm.Grid.LoadWeek(Monday);
        await vm.SelectDayCommand.ExecuteAsync(Monday);

        await vm.Grid.WeekNextCommand.ExecuteAsync(null);

        // The grid loads the new week on its own; the page notices on its next refresh
        vm.Grid.WeekStart.Should().Be(Monday.AddDays(7));
        await vm.MarkCancelledCommand.ExecuteAsync(vm.DayAppointments[0].Appointment);

        vm.HasSelectedDay.Should().BeFalse(
            "the panel must not keep showing a day that has left the grid");
    }
}
