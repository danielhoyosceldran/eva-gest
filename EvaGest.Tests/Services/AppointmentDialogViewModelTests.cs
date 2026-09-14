using AwesomeAssertions;
using EvaGest.Models;
using EvaGest.Services;
using EvaGest.Tests.Infra;
using EvaGest.ViewModels.Dialogs;
using Xunit;

namespace EvaGest.Tests.Services;

public class AppointmentDialogViewModelTests
{
    private static readonly DateOnly Today = DateOnly.FromDateTime(DateTime.Today);

    private sealed record Services(
        IAppointmentService Appointments, IAvailabilityService Availability, IClientService Clients,
        ICatalogService Catalog, IWorkerService Workers, ISettingsService Settings);

    private static Services Build(TestDatabase testDb)
    {
        var factory = new TestFactory(testDb.Options);
        return new Services(new AppointmentService(factory), new AvailabilityService(factory),
            new ClientService(factory), new CatalogService(factory), new WorkerService(factory),
            new SettingsService(factory));
    }

    private static AppointmentDialogViewModel New(Services s, DateOnly? date = null, IDialogService? dialogs = null)
        => new(s.Appointments, s.Availability, s.Clients, s.Catalog, s.Workers, s.Settings,
               dialogs ?? new TestDialogService(), date ?? Today);

    private static AppointmentDialogViewModel Edit(Services s, Appointment appointment)
        => new(s.Appointments, s.Availability, s.Clients, s.Catalog, s.Workers, s.Settings,
               new TestDialogService(), appointment);

    [Fact] // F-07
    public async Task The_duration_comes_from_the_service_when_it_has_one()
    {
        await using var testDb = new TestDatabase();
        var s = Build(testDb);
        int serviceId = await s.Catalog.CreateService(Make.Service("Tall", duration: 45));
        var service = await s.Catalog.GetService(serviceId);

        var vm = New(s);
        vm.Service = service;

        vm.DurationMin.Should().Be(45);
    }

    [Fact] // F-08
    public async Task The_duration_falls_back_to_the_default_when_the_service_has_none()
    {
        await using var testDb = new TestDatabase();
        var s = Build(testDb);
        int serviceId = await s.Catalog.CreateService(Make.Service("Massatge", duration: null));
        var service = await s.Catalog.GetService(serviceId);

        var vm = New(s);
        int durationBefore = vm.DurationMin;
        vm.Service = service;

        vm.DurationMin.Should().Be(durationBefore); // untouched; the default (30) stays
    }

    [Fact] // F-09
    public async Task The_first_option_is_always_Guest_and_comes_selected()
    {
        await using var testDb = new TestDatabase();
        var s = Build(testDb);
        await s.Clients.Create(Make.Client("Joan García"));

        var vm = New(s);
        await vm.Initialization;

        vm.ClientOptions[0].IsGuest.Should().BeTrue();
        vm.SelectedOption.Should().BeSameAs(vm.ClientOptions[0]);
        vm.CanWriteGuest.Should().BeTrue();
        vm.SelectedClient.Should().BeNull();
    }

    [Fact] // F-10
    public async Task Editing_an_appointment_of_a_registered_client_shows_them_in_the_picker()
    {
        // The regression this guards: the appointment's Client instance comes from a
        // different AsNoTracking query than the one filling the list, so comparing by
        // reference left the ComboBox empty.
        await using var testDb = new TestDatabase();
        var s = Build(testDb);
        int clientId = await s.Clients.Create(Make.Client("Joan García"));
        int appointmentId = await s.Appointments.Create(new Appointment
        {
            Date = Today, Time = new TimeOnly(10, 0), DurationMin = 30, ClientId = clientId
        });
        var appointment = await s.Appointments.GetById(appointmentId);

        var vm = Edit(s, appointment!);
        await vm.Initialization;

        vm.SelectedClient.Should().NotBeNull();
        vm.SelectedClient!.Id.Should().Be(clientId);
        vm.SelectedOption!.Name.Should().Be("Joan García");
        vm.CanWriteGuest.Should().BeFalse();
    }

    [Fact] // F-10b
    public async Task Edit_keepsé_the_service_the_worker_i_the_duration_saved()
    {
        await using var testDb = new TestDatabase();
        var s = Build(testDb);
        int serviceId = await s.Catalog.CreateService(Make.Service("Tall", duration: 45));
        int workerId;
        await using (var db = testDb.Context())
        {
            var marta = Make.Worker("Marta");
            db.Workers.Add(marta);
            await db.SaveChangesAsync(TestContext.Current.CancellationToken);
            workerId = marta.Id;
        }
        int appointmentId = await s.Appointments.Create(new Appointment
        {
            Date = Today, Time = new TimeOnly(10, 0), DurationMin = 20, GuestName = "Algú",
            ServiceId = serviceId, WorkerId = workerId
        });
        var appointment = await s.Appointments.GetById(appointmentId);

        var vm = Edit(s, appointment!);
        await vm.Initialization;

        vm.Service!.Id.Should().Be(serviceId);
        vm.Worker!.Id.Should().Be(workerId);
        vm.DurationMin.Should().Be(20, "an edited appointment keeps the duration it was saved with");
    }

    [Fact] // F-11
    public async Task Going_back_to_Guest_frees_the_fields_and_clears_the_client_phone()
    {
        await using var testDb = new TestDatabase();
        var s = Build(testDb);
        await s.Clients.Create(Make.Client("Joan García", "612345678"));
        var vm = New(s);
        await vm.Initialization;

        vm.SelectedOption = vm.ClientOptions.First(o => !o.IsGuest);
        vm.CanWriteGuest.Should().BeFalse();
        vm.GuestPhone.Should().Be("612345678");

        vm.SelectedOption = vm.ClientOptions[0];

        vm.CanWriteGuest.Should().BeTrue();
        vm.SelectedClient.Should().BeNull();
        vm.GuestPhone.Should().BeNull("the previous client's mobile is not the guest's");
    }

    [Fact] // F-12
    public async Task Choosing_a_registered_client_clears_the_guest_name_typed_before()
    {
        await using var testDb = new TestDatabase();
        var s = Build(testDb);
        await s.Clients.Create(Make.Client("Joan García"));
        var vm = New(s);
        await vm.Initialization;
        vm.TextClient = "Algú de pas";

        vm.SelectedOption = vm.ClientOptions.First(o => !o.IsGuest);

        vm.TextClient.Should().BeEmpty();
    }

    [Fact] // F-13
    public async Task Without_a_client_or_a_guest_name_it_cannot_be_saved()
    {
        await using var testDb = new TestDatabase();
        var s = Build(testDb);
        var vm = New(s);
        await vm.Initialization;

        await vm.SaveCommand.ExecuteAsync(null);

        vm.ErrorValidation.Should().NotBeNull();
        (await s.Appointments.GetByDay(Today)).Should().BeEmpty();
    }

    [Fact] // F-14
    public async Task Saving_a_guest_leaves_no_registered_client_and_the_other_way_round()
    {
        await using var testDb = new TestDatabase();
        var s = Build(testDb);
        int clientId = await s.Clients.Create(Make.Client("Joan García"));

        var guest = New(s);
        await guest.Initialization;
        guest.TextClient = "Algú de pas";
        await guest.SaveCommand.ExecuteAsync(null);

        var registered = New(s);
        await registered.Initialization;
        registered.SelectedOption = registered.ClientOptions.First(o => !o.IsGuest);
        registered.Time = new TimeOnly(12, 0);
        await registered.SaveCommand.ExecuteAsync(null);

        var appointments = await s.Appointments.GetByDay(Today);
        appointments.Should().HaveCount(2);
        var ofGuest = appointments.Single(c => c.IsGuest);
        ofGuest.GuestName.Should().Be("Algú de pas");
        ofGuest.ClientId.Should().BeNull();
        appointments.Single(c => !c.IsGuest).ClientId.Should().Be(clientId);
    }

    [Fact] // F-15
    public async Task L_time_initial_the_mark_who_opens_the_dialog
        () {
        await using var testDb = new TestDatabase();
        var s = Build(testDb);

        var vm = new AppointmentDialogViewModel(s.Appointments, s.Availability, s.Clients, s.Catalog,
            s.Workers, s.Settings, new TestDialogService(), Today, new TimeOnly(16, 30));

        vm.Time.Should().Be(new TimeOnly(16, 30));
    }

    [Fact] // F-16
    public async Task The_dialog_grid_shows_the_ghost_at_the_chosen_time()
    {
        await using var testDb = new TestDatabase();
        var s = Build(testDb);
        var vm = new AppointmentDialogViewModel(s.Appointments, s.Availability, s.Clients, s.Catalog,
            s.Workers, s.Settings, new TestDialogService(), Today, new TimeOnly(11, 0));
        await vm.Initialization;

        vm.Grid.Should().NotBeNull();
        var column = vm.Grid!.Days.Single(d => d.Date == Today);
        column.Appointments.Should().ContainSingle(c => c.IsGhost);
    }

    [Fact] // F-17
    public async Task Clicking_a_slot_of_the_grid_sets_date_and_time()
    {
        await using var testDb = new TestDatabase();
        var s = Build(testDb);
        var vm = New(s);
        await vm.Initialization;
        var grid = vm.Grid!;
        var otherDay = grid.Days[grid.Days.Count - 1];

        otherDay.ClickAtPosition(60 * grid.PixelsPerMinute);

        vm.Date.Should().Be(otherDay.Date);
        vm.Time.Should().Be(EvaGest.Helpers.GridHelper.ATime(grid.GridStartMinute + 60));
    }
}
