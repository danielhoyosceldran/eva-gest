using AwesomeAssertions;
using EvaGest.Models;
using EvaGest.Resources;
using EvaGest.Services;
using EvaGest.Tests.Infra;
using EvaGest.ViewModels.Dialogs;
using Xunit;

namespace EvaGest.Tests.Services;

/// <summary>
/// E-03. The appointment dialog fills itself in from a task nothing in the app awaits.
/// When that load failed the exception went unobserved: the dialog opened half empty,
/// said nothing, logged nothing, and saving an edited appointment wrote its service and
/// worker back as empty.
/// </summary>
public class AppointmentDialogLoadFailureTests
{
    private static readonly DateOnly Today = DateOnly.FromDateTime(DateTime.Today);

    /// <summary>A guest appointment with a service and a worker, and a dialog editing it
    /// whose catalogue fails, so the load stops before the combos are filled.</summary>
    private static async Task<(AppointmentDialogViewModel Vm, IAppointmentService Appointments, int Id, int ServiceId, int WorkerId)>
        EditWithFailingLoad(TestDatabase testDb)
    {
        var factory = new TestFactory(testDb.Options);
        var appointments = new AppointmentService(factory);
        var workers = new WorkerService(factory);
        var catalog = new CatalogService(factory);

        int serviceId = await catalog.CreateService(Make.Service("Tall"));
        var worker = await workers.Create(Make.Worker("Marta"), new Dictionary<Weekday, List<(TimeOnly, TimeOnly)>>());
        int id = await appointments.Create(new Appointment
        {
            Date = Today, Time = new TimeOnly(10, 0), DurationMin = 30,
            GuestName = "Pere", ServiceId = serviceId, WorkerId = worker.Id
        });
        var appointment = await appointments.GetById(id);

        var vm = new AppointmentDialogViewModel(appointments, new AvailabilityService(factory),
            new ClientService(factory), FailingService.Create<ICatalogService>(), workers,
            new SettingsService(factory), new TestDialogService(), appointment!);

        return (vm, appointments, id, serviceId, worker.Id);
    }

    [Fact]
    public async Task A_failed_load_is_shown_in_the_dialog_instead_of_vanishing()
    {
        await using var testDb = new TestDatabase();
        var (vm, _, _, _, _) = await EditWithFailingLoad(testDb);

        await vm.Invoking(v => v.Initialization).Should().NotThrowAsync(
            "nothing in the app awaits it, so a fault there is never seen");

        vm.ErrorValidation.Should().Be(Texts.AppointmentNotLoaded);
    }

    [Fact]
    public async Task A_dialog_that_did_not_load_does_not_save_over_the_appointment()
    {
        await using var testDb = new TestDatabase();
        var (vm, appointments, id, serviceId, workerId) = await EditWithFailingLoad(testDb);
        await vm.Initialization;
        bool closed = false;
        vm.Close += _ => closed = true;

        await vm.SaveCommand.ExecuteAsync(null);

        closed.Should().BeFalse();
        vm.ErrorValidation.Should().Be(Texts.AppointmentNotLoaded);
        var stored = await appointments.GetById(id);
        stored!.ServiceId.Should().Be(serviceId, "the empty combo is not what the appointment holds");
        stored.WorkerId.Should().Be(workerId);
    }

    [Fact]
    public async Task A_save_clicked_while_loading_waits_for_the_load()
    {
        await using var testDb = new TestDatabase();
        var factory = new TestFactory(testDb.Options);
        var appointments = new AppointmentService(factory);
        var vm = new AppointmentDialogViewModel(appointments, new AvailabilityService(factory),
            new ClientService(factory), new CatalogService(factory), new WorkerService(factory),
            // Yes to the overlap question: with no workers the shop has no capacity at all.
            new SettingsService(factory), new TestDialogService { ResultConfirm = true }, Today, new TimeOnly(11, 0));
        vm.TextClient = "Pere";

        // No await on Initialization first: Save itself has to wait for it.
        await vm.SaveCommand.ExecuteAsync(null);

        vm.ErrorValidation.Should().BeNull();
        (await appointments.GetByDay(Today)).Should().ContainSingle(a => a.GuestName == "Pere");
    }
}
