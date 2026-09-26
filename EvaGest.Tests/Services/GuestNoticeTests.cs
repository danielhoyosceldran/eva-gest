using AwesomeAssertions;
using EvaGest.Models;
using EvaGest.Services;
using EvaGest.Tests.Infra;
using EvaGest.ViewModels.Dialogs;
using Xunit;

namespace EvaGest.Tests.Services;

/// <summary>
/// Block S: the unregistered-client notice (RF-05bis). It never blocks: it only offers
/// to register them, and it can be turned off in Settings.
/// </summary>
public class NoticeGuestTests
{
    private static readonly DateOnly Today = DateOnly.FromDateTime(DateTime.Today);

    private static async Task<AppointmentDialogViewModel> DialogAppointment(
        TestDatabase testDb, TestDialogService dialogs, bool noticeActive = true)
    {
        var factory = new TestFactory(testDb.Options);
        var config = new SettingsService(factory);
        await new SeedService(factory, config).Seed();
        await config.SaveBool(ConfigKeys.ShowGuestNotice, noticeActive);

        var vm = new AppointmentDialogViewModel(
            new AppointmentService(factory), new AvailabilityService(factory), new ClientService(factory),
            new CatalogService(factory), new WorkerService(factory), config, dialogs, Today);
        await vm.Initialization;
        return vm;
    }

    [Fact] // S-01
    public async Task Typing_a_free_name_turns_the_notice_on()
    {
        await using var testDb = new TestDatabase();
        var vm = await DialogAppointment(testDb, new TestDialogService());

        vm.UnregisteredClientNotice.Should().BeFalse("an empty field is not a guest yet");

        vm.TextClient = "Pere";

        vm.UnregisteredClientNotice.Should().BeTrue();
    }

    [Fact] // S-02
    public async Task With_the_notice_turned_off_it_never_appears()
    {
        await using var testDb = new TestDatabase();
        var vm = await DialogAppointment(testDb, new TestDialogService(), noticeActive: false);

        vm.TextClient = "Pere";

        vm.UnregisteredClientNotice.Should().BeFalse();
    }

    [Fact] // S-03
    public async Task Registering_on_the_spot_creates_the_client_and_selects_them()
    {
        await using var testDb = new TestDatabase();
        var dialogs = new TestDialogService
        {
            ResultDialog = true,
            FillDialog = async d =>
            {
                if (d is not ClientDialogViewModel client) return;
                client.Mobile = "600111222";
                await client.SaveCommand.ExecuteAsync(null);
            }
        };

        var vm = await DialogAppointment(testDb, dialogs);
        vm.TextClient = "Pere";

        await vm.RegisterClientNowCommand.ExecuteAsync(null);

        vm.SelectedClient.Should().NotBeNull();
        vm.SelectedClient!.Name.Should().Be("Pere");
        vm.TextClient.Should().BeEmpty();
        vm.UnregisteredClientNotice.Should().BeFalse("no longer a guest");

        await using var db = testDb.Context();
        db.Clients.Should().ContainSingle(c => c.Name == "Pere");
    }

    [Fact] // S-04
    public async Task Cancelling_the_dialog_leaves_the_appointment_as_it_was()
    {
        await using var testDb = new TestDatabase();
        var vm = await DialogAppointment(testDb, new TestDialogService()); // cancels by default
        vm.TextClient = "Pere";

        await vm.RegisterClientNowCommand.ExecuteAsync(null);

        vm.SelectedClient.Should().BeNull();
        vm.TextClient.Should().Be("Pere", "what had been typed is not lost");
        vm.UnregisteredClientNotice.Should().BeTrue();
    }

    [Fact] // S-05
    public async Task Choosing_a_registered_client_turns_the_notice_off()
    {
        await using var testDb = new TestDatabase();
        await using (var db = testDb.Context())
        {
            db.Clients.Add(Make.Client("Joana"));
            await db.SaveChangesAsync();
        }

        var vm = await DialogAppointment(testDb, new TestDialogService());
        vm.TextClient = "Pere";
        vm.UnregisteredClientNotice.Should().BeTrue();

        vm.ClientPicker.Text = "joana";
        vm.ClientPicker.PickHighlightedCommand.Execute(null);

        vm.UnregisteredClientNotice.Should().BeFalse();
    }

    [Fact] // S-06
    public async Task New_appointment_takes_the_default_duration_from_settings()
    {
        await using var testDb = new TestDatabase();
        var factory = new TestFactory(testDb.Options);
        var config = new SettingsService(factory);
        await config.Save(ConfigKeys.DefaultAppointmentDurationMin, "45");

        var vm = new AppointmentDialogViewModel(
            new AppointmentService(factory), new AvailabilityService(factory), new ClientService(factory),
            new CatalogService(factory), new WorkerService(factory), config,
            new TestDialogService(), Today);
        await vm.Initialization;

        vm.DurationMin.Should().Be(45);
    }

    [Fact] // S-07
    public async Task Editing_an_appointment_keeps_its_duration_and_not_the_default()
    {
        await using var testDb = new TestDatabase();
        var factory = new TestFactory(testDb.Options);
        var config = new SettingsService(factory);
        await config.Save(ConfigKeys.DefaultAppointmentDurationMin, "45");

        var appointment = new Appointment
        {
            Date = Today, Time = new TimeOnly(10, 0), DurationMin = 90,
            GuestName = "Pere", Status = AppointmentStatus.Pending
        };
        await using (var db = testDb.Context())
        {
            db.Appointments.Add(appointment);
            await db.SaveChangesAsync();
        }

        var vm = new AppointmentDialogViewModel(
            new AppointmentService(factory), new AvailabilityService(factory), new ClientService(factory),
            new CatalogService(factory), new WorkerService(factory), config,
            new TestDialogService(), appointment);
        await vm.Initialization;

        vm.DurationMin.Should().Be(90);
    }
}
