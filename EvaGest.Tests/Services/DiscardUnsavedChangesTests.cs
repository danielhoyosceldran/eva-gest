using System.Windows.Input;
using AwesomeAssertions;
using CommunityToolkit.Mvvm.Input;
using EvaGest.Models;
using EvaGest.Services;
using EvaGest.Tests.Infra;
using EvaGest.ViewModels.Dialogs;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace EvaGest.Tests.Services;

/// <summary>
/// F-02: Cancel (and Esc, and the window's X, which go the same way) closed the sale and
/// appointment dialogs at once, losing a half-entered sale with no question. When the
/// user has changed something since the dialog opened, cancelling must ask first:
/// "keep editing" leaves the dialog open, "discard" closes it with nothing saved. A
/// dialog nobody touched closes without asking.
///
/// In the confirmation, "yes" (the confirm button) is discarding.
/// </summary>
public class DiscardUnsavedChangesTests
{
    private static readonly DateOnly Monday = new(2026, 10, 12);

    /// <summary>Runs a command the way the button does and waits for it to finish.</summary>
    private static async Task Press(ICommand command)
    {
        if (command is IAsyncRelayCommand asyncCommand) await asyncCommand.ExecuteAsync(null);
        else command.Execute(null);
    }

    private sealed record Shop(TestFactory Factory, SettingsService Settings, SaleService Sales,
        ClientService Clients, CatalogService Catalog, WorkerService Workers, AppointmentService Appointments,
        AvailabilityService Availability);

    private static async Task<Shop> Build(TestDatabase testDb)
    {
        var factory = new TestFactory(testDb.Options);
        var settings = new SettingsService(factory);
        await new SeedService(factory, settings).Seed();
        return new Shop(factory, settings, new SaleService(factory, settings), new ClientService(factory),
            new CatalogService(factory), new WorkerService(factory), new AppointmentService(factory),
            new AvailabilityService(factory));
    }

    private static Task<SaleDialogViewModel> NewSale(Shop shop, TestDialogService dialogs)
        => SaleDialogViewModel.New(shop.Sales, shop.Clients, shop.Catalog, shop.Workers,
            new TestSoundService(), shop.Settings, dialogs);

    /// <summary>What the dialog asked its host: null while it stays open.</summary>
    private sealed class CloseWatcher
    {
        public bool? Closed { get; private set; }
        public int Times { get; private set; }

        public CloseWatcher(DialogViewModelBase dialog)
            => dialog.Close += confirmed => { Closed = confirmed; Times++; };
    }

    // ── The sale dialog ──────────────────────────────────────────────────────

    [Fact]
    public async Task An_untouched_new_sale_closes_without_asking()
    {
        await using var testDb = new TestDatabase();
        var shop = await Build(testDb);
        var dialogs = new TestDialogService();
        var vm = await NewSale(shop, dialogs);
        var watcher = new CloseWatcher(vm);

        await Press(vm.CancelCommand);

        dialogs.ConfirmacionsRequested.Should().BeEmpty("nothing would be lost");
        watcher.Closed.Should().BeFalse("cancelled, closed unsaved");
    }

    [Fact]
    public async Task Cancelling_a_new_sale_with_a_line_added_asks_first()
    {
        await using var testDb = new TestDatabase();
        var shop = await Build(testDb);
        var dialogs = new TestDialogService { ResultConfirm = false };
        var vm = await NewSale(shop, dialogs);
        vm.AddCustomConceptCommand.Execute(null);
        vm.Lines[0].Description = "Tall";
        vm.Lines[0].PriceText = "15,00";

        await Press(vm.CancelCommand);

        dialogs.ConfirmacionsRequested.Should().ContainSingle("a half-entered sale must not vanish without a question");
    }

    [Fact]
    public async Task Answering_keep_editing_leaves_the_sale_dialog_open()
    {
        await using var testDb = new TestDatabase();
        var shop = await Build(testDb);
        var dialogs = new TestDialogService { ResultConfirm = false };
        var vm = await NewSale(shop, dialogs);
        var watcher = new CloseWatcher(vm);
        vm.AddCustomConceptCommand.Execute(null);

        await Press(vm.CancelCommand);

        watcher.Times.Should().Be(0, "the user chose to keep editing");
        vm.Lines.Should().ContainSingle("what was typed is still there");
    }

    [Fact]
    public async Task Answering_discard_closes_the_sale_dialog_and_saves_nothing()
    {
        await using var testDb = new TestDatabase();
        var shop = await Build(testDb);
        var dialogs = new TestDialogService { ResultConfirm = true };
        var vm = await NewSale(shop, dialogs);
        var watcher = new CloseWatcher(vm);
        vm.TextClient = "Anna";
        vm.PaymentMethod = vm.ActiveMethods[0];
        vm.AddCustomConceptCommand.Execute(null);
        vm.Lines[0].Description = "Tall";
        vm.Lines[0].PriceText = "15,00";

        await Press(vm.CancelCommand);

        dialogs.ConfirmacionsRequested.Should().ContainSingle();
        watcher.Closed.Should().BeFalse("discarded: closed as cancelled");
        await using var db = testDb.Context();
        (await db.Sales.CountAsync()).Should().Be(0, "a discarded sale is not charged");
    }

    [Fact]
    public async Task A_sale_opened_for_editing_and_not_changed_closes_without_asking()
    {
        await using var testDb = new TestDatabase();
        var shop = await Build(testDb);
        int methodId;
        await using (var db = testDb.Context()) methodId = (await db.PaymentMethods.FirstAsync()).Id;
        int id = await shop.Sales.Create(Make.Sale(Monday, methodId, SaleStatus.Active, Make.Line(2_000)),
            [Make.Line(2_000)]);
        var dialogs = new TestDialogService();
        var vm = await SaleDialogViewModel.Edit(shop.Sales, shop.Clients, shop.Catalog, shop.Workers,
            new TestSoundService(), shop.Settings, dialogs, (await shop.Sales.GetById(id))!);
        var watcher = new CloseWatcher(vm);

        await Press(vm.CancelCommand);

        dialogs.ConfirmacionsRequested.Should().BeEmpty("loading the sale is not a change");
        watcher.Closed.Should().BeFalse();
    }

    [Fact]
    public async Task A_sale_opened_for_editing_with_its_notes_changed_asks_first()
    {
        await using var testDb = new TestDatabase();
        var shop = await Build(testDb);
        int methodId;
        await using (var db = testDb.Context()) methodId = (await db.PaymentMethods.FirstAsync()).Id;
        int id = await shop.Sales.Create(Make.Sale(Monday, methodId, SaleStatus.Active, Make.Line(2_000)),
            [Make.Line(2_000)]);
        var dialogs = new TestDialogService { ResultConfirm = false };
        var vm = await SaleDialogViewModel.Edit(shop.Sales, shop.Clients, shop.Catalog, shop.Workers,
            new TestSoundService(), shop.Settings, dialogs, (await shop.Sales.GetById(id))!);
        var watcher = new CloseWatcher(vm);

        vm.Notes = "pagarà la resta dijous";
        await Press(vm.CancelCommand);

        dialogs.ConfirmacionsRequested.Should().ContainSingle();
        watcher.Times.Should().Be(0);
    }

    [Fact]
    public async Task A_sale_raised_from_an_appointment_and_not_touched_closes_without_asking()
    {
        // The service line the appointment brings in is the dialog's starting point, not
        // something the user typed.
        await using var testDb = new TestDatabase();
        var shop = await Build(testDb);
        Appointment appointment;
        await using (var db = testDb.Context())
        {
            var service = Make.Service("Tall de prova");
            db.Services.Add(service);
            await db.SaveChangesAsync();
            appointment = new Appointment
            {
                Date = DateOnly.FromDateTime(DateTime.Today).AddDays(-1), Time = new TimeOnly(10, 0),
                DurationMin = 30, GuestName = "Pere", ServiceId = service.Id
            };
            db.Appointments.Add(appointment);
            await db.SaveChangesAsync();
        }
        var dialogs = new TestDialogService();
        var vm = await SaleDialogViewModel.FromAppointment(shop.Sales, shop.Clients, shop.Catalog, shop.Workers,
            new TestSoundService(), shop.Settings, dialogs, appointment);
        var watcher = new CloseWatcher(vm);

        await Press(vm.CancelCommand);

        dialogs.ConfirmacionsRequested.Should().BeEmpty();
        watcher.Closed.Should().BeFalse();
    }

    // ── The appointment dialog ───────────────────────────────────────────────

    private static async Task<AppointmentDialogViewModel> NewAppointment(Shop shop, TestDialogService dialogs)
    {
        var vm = new AppointmentDialogViewModel(shop.Appointments, shop.Availability, shop.Clients,
            shop.Catalog, shop.Workers, shop.Settings, dialogs, Monday, new TimeOnly(10, 0));
        await vm.Initialization;
        return vm;
    }

    private static async Task<(AppointmentDialogViewModel vm, int id)> ExistingAppointment(
        Shop shop, TestDialogService dialogs)
    {
        int id = await shop.Appointments.Create(new Appointment
        {
            Date = Monday, Time = new TimeOnly(10, 0), DurationMin = 30, GuestName = "Anna",
            Notes = "primera visita", Status = AppointmentStatus.Pending
        });
        var vm = new AppointmentDialogViewModel(shop.Appointments, shop.Availability, shop.Clients,
            shop.Catalog, shop.Workers, shop.Settings, dialogs, (await shop.Appointments.GetById(id))!);
        await vm.Initialization;
        return (vm, id);
    }

    [Fact]
    public async Task An_untouched_new_appointment_closes_without_asking()
    {
        await using var testDb = new TestDatabase();
        var shop = await Build(testDb);
        var dialogs = new TestDialogService();
        var vm = await NewAppointment(shop, dialogs);
        var watcher = new CloseWatcher(vm);

        await Press(vm.CancelCommand);

        dialogs.ConfirmacionsRequested.Should().BeEmpty();
        watcher.Closed.Should().BeFalse();
    }

    [Fact]
    public async Task Cancelling_a_new_appointment_with_its_time_changed_asks_first()
    {
        await using var testDb = new TestDatabase();
        var shop = await Build(testDb);
        var dialogs = new TestDialogService { ResultConfirm = false };
        var vm = await NewAppointment(shop, dialogs);
        var watcher = new CloseWatcher(vm);

        vm.TimeText = "11:30";
        await Press(vm.CancelCommand);

        dialogs.ConfirmacionsRequested.Should().ContainSingle();
        watcher.Times.Should().Be(0, "the user chose to keep editing");
    }

    [Fact]
    public async Task Cancelling_an_existing_appointment_with_its_notes_changed_asks_first()
    {
        await using var testDb = new TestDatabase();
        var shop = await Build(testDb);
        var dialogs = new TestDialogService { ResultConfirm = false };
        var (vm, _) = await ExistingAppointment(shop, dialogs);
        var watcher = new CloseWatcher(vm);

        vm.Notes = "vol arreglar també la barba";
        await Press(vm.CancelCommand);

        dialogs.ConfirmacionsRequested.Should().ContainSingle();
        watcher.Times.Should().Be(0);
    }

    [Fact]
    public async Task An_existing_appointment_opened_and_not_changed_closes_without_asking()
    {
        await using var testDb = new TestDatabase();
        var shop = await Build(testDb);
        var dialogs = new TestDialogService();
        var (vm, _) = await ExistingAppointment(shop, dialogs);
        var watcher = new CloseWatcher(vm);

        await Press(vm.CancelCommand);

        dialogs.ConfirmacionsRequested.Should().BeEmpty("loading the appointment is not a change");
        watcher.Closed.Should().BeFalse();
    }

    [Fact]
    public async Task Discarding_a_changed_appointment_closes_it_and_saves_nothing()
    {
        await using var testDb = new TestDatabase();
        var shop = await Build(testDb);
        var dialogs = new TestDialogService { ResultConfirm = true };
        var (vm, id) = await ExistingAppointment(shop, dialogs);
        var watcher = new CloseWatcher(vm);

        vm.TimeText = "12:00";
        vm.Notes = "canviat";
        await Press(vm.CancelCommand);

        dialogs.ConfirmacionsRequested.Should().ContainSingle();
        watcher.Closed.Should().BeFalse();
        var stored = await shop.Appointments.GetById(id);
        stored!.Time.Should().Be(new TimeOnly(10, 0));
        stored.Notes.Should().Be("primera visita");
    }
}
