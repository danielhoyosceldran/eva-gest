using AwesomeAssertions;
using EvaGest.Models;
using EvaGest.Resources;
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
    public async Task A_new_appointment_opens_on_existing_client_with_nothing_picked()
    {
        await using var testDb = new TestDatabase();
        var s = Build(testDb);
        await s.Clients.Create(Make.Client("Joan García"));

        var vm = New(s);
        await vm.Initialization;

        vm.ClientPicker.Text.Should().BeEmpty();
        vm.ClientPicker.IsDropdownOpen.Should().BeFalse();
        vm.ClientPicker.IsExistingClient.Should().BeTrue();
        vm.CanWriteGuest.Should().BeFalse("the phone box belongs to a new client");
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
        vm.ClientPicker.Text.Should().Be("Joan García");
        vm.CanWriteGuest.Should().BeFalse();
    }

    [Fact]
    public async Task Editing_an_appointment_of_an_asleep_client_keeps_that_client()
    {
        // Asleep clients are left out of the search, and the picker used to be filled
        // from the active ones only: the appointment reopened as a guest with an empty
        // name, and saving it either failed or lost the client.
        await using var testDb = new TestDatabase();
        var s = Build(testDb);
        int clientId = await s.Clients.Create(Make.Client("Joan García"));
        int appointmentId = await s.Appointments.Create(new Appointment
        {
            Date = Today, Time = new TimeOnly(10, 0), DurationMin = 30, ClientId = clientId
        });
        await s.Clients.Sleep(clientId);
        var appointment = await s.Appointments.GetById(appointmentId);

        var vm = Edit(s, appointment!);
        await vm.Initialization;
        await vm.SaveCommand.ExecuteAsync(null);

        vm.ErrorValidation.Should().BeNull();
        (await s.Appointments.GetById(appointmentId))!.ClientId.Should().Be(clientId);
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
    public async Task Switching_to_new_client_after_a_pick_frees_the_phone_and_clears_the_client_one()
    {
        await using var testDb = new TestDatabase();
        var s = Build(testDb);
        await s.Clients.Create(Make.Client("Joan García", "612345678"));
        var vm = New(s);
        await vm.Initialization;

        vm.ClientPicker.Text = "joan";
        vm.ClientPicker.PickHighlightedCommand.Execute(null);
        vm.CanWriteGuest.Should().BeFalse();
        vm.GuestPhone.Should().Be("612345678");

        vm.ClientPicker.IsNewClient = true;

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

        vm.ClientPicker.Text = "garcia";
        vm.ClientPicker.PickHighlightedCommand.Execute(null);

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

    [Theory] // F-13d
    [InlineData("10.00")] // a dot for the colon: used to be read as 10:00
    [InlineData("20-00")] // a dash for the colon: used to be read as a time too
    [InlineData("10,00")]
    [InlineData("1000")]
    [InlineData("9:30")]  // missing the leading zero
    [InlineData("24:00")] // no such hour
    [InlineData("10:60")] // no such minute
    [InlineData("deu")]
    [InlineData("")]
    public async Task An_hour_that_is_not_hh_mm_cannot_be_saved(string typed)
    {
        await using var testDb = new TestDatabase();
        var s = Build(testDb);
        var vm = New(s);
        await vm.Initialization;
        vm.TextClient = "Algú de pas";
        vm.TimeText = typed;

        await vm.SaveCommand.ExecuteAsync(null);

        vm.ErrorValidation.Should().Be(Texts.TimeInvalid);
        (await s.Appointments.GetByDay(Today)).Should()
            .BeEmpty("a time nobody typed must not be guessed at and saved");
    }

    [Fact] // F-13e
    public async Task The_hour_saved_is_the_one_typed()
    {
        await using var testDb = new TestDatabase();
        var s = Build(testDb);
        var vm = New(s);
        await vm.Initialization;
        vm.TextClient = "Algú de pas";
        vm.TimeText = "16:45";

        await vm.SaveCommand.ExecuteAsync(null);

        vm.ErrorValidation.Should().BeNull();
        var appointment = (await s.Appointments.GetByDay(Today)).Should().ContainSingle().Subject;
        appointment.Time.Should().Be(new TimeOnly(16, 45));
    }

    [Fact] // F-13f
    public async Task The_hour_box_follows_the_time_picked_on_the_grid()
    {
        await using var testDb = new TestDatabase();
        var s = Build(testDb);
        var vm = New(s);
        await vm.Initialization;

        vm.Time = new TimeOnly(9, 5);

        vm.TimeText.Should().Be("09:05");
    }

    [Theory] // F-13g
    [InlineData("0")]
    [InlineData("-30")]
    [InlineData("30,5")]
    [InlineData("mitja hora")]
    [InlineData("")]
    public async Task A_duration_that_is_not_a_whole_positive_number_cannot_be_saved(string typed)
    {
        await using var testDb = new TestDatabase();
        var s = Build(testDb);
        var vm = New(s);
        await vm.Initialization;
        vm.TextClient = "Algú de pas";
        vm.DurationText = typed;

        await vm.SaveCommand.ExecuteAsync(null);

        vm.ErrorValidation.Should().Be(Texts.DurationInvalid);
        (await s.Appointments.GetByDay(Today)).Should().BeEmpty();
    }

    [Fact] // F-13h
    public async Task The_duration_box_opens_filled_and_follows_the_service_picked()
    {
        await using var testDb = new TestDatabase();
        var s = Build(testDb);
        int serviceId = await s.Catalog.CreateService(Make.Service("Tall", duration: 45));
        var service = await s.Catalog.GetService(serviceId);
        var vm = New(s);

        vm.DurationText.Should().Be("30", "an empty box would look like nothing was set");

        vm.Service = service;
        vm.DurationText.Should().Be("45");
    }

    [Fact] // F-13b
    public async Task A_malformed_guest_phone_cannot_be_saved()
    {
        await using var testDb = new TestDatabase();
        var s = Build(testDb);
        var vm = New(s);
        await vm.Initialization;
        vm.TextClient = "Algú de pas";
        vm.GuestPhone = "123";

        await vm.SaveCommand.ExecuteAsync(null);

        vm.ErrorValidation.Should().NotBeNull();
        (await s.Appointments.GetByDay(Today)).Should().BeEmpty();
    }

    [Fact] // F-13c
    public async Task A_guest_phone_with_spaces_saves_once_it_has_nine_digits()
    {
        await using var testDb = new TestDatabase();
        var s = Build(testDb);
        var vm = New(s);
        await vm.Initialization;
        vm.TextClient = "Algú de pas";
        vm.GuestPhone = "61 23 45 678";

        await vm.SaveCommand.ExecuteAsync(null);

        vm.ErrorValidation.Should().BeNull();
        (await s.Appointments.GetByDay(Today)).Should().ContainSingle();
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
        registered.ClientPicker.Text = "joan";
        registered.ClientPicker.PickHighlightedCommand.Execute(null);
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

    [Fact]
    public async Task Saving_with_a_search_typed_but_nobody_picked_says_to_pick_or_switch()
    {
        await using var testDb = new TestDatabase();
        var s = Build(testDb);
        await s.Clients.Create(Make.Client("Joan García"));
        var vm = New(s);
        await vm.Initialization;

        vm.ClientPicker.Text = "joan";
        await vm.SaveCommand.ExecuteAsync(null);

        vm.ErrorValidation.Should().Be(Texts.ClientNotPicked);
        (await s.Appointments.GetByDay(Today)).Should().BeEmpty();
    }

    [Fact]
    public async Task An_appointment_for_a_guest_reopens_on_new_client_with_its_name()
    {
        await using var testDb = new TestDatabase();
        var s = Build(testDb);
        int appointmentId = await s.Appointments.Create(new Appointment
        {
            Date = Today, Time = new TimeOnly(10, 0), DurationMin = 30, GuestName = "Algú"
        });

        var vm = Edit(s, (await s.Appointments.GetById(appointmentId))!);
        await vm.Initialization;

        vm.ClientPicker.IsNewClient.Should().BeTrue();
        vm.ClientPicker.NewName.Should().Be("Algú");
        vm.CanWriteGuest.Should().BeTrue();
    }
}
