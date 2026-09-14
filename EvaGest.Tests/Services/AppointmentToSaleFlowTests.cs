using AwesomeAssertions;
using EvaGest.Models;
using EvaGest.Services;
using EvaGest.Tests.Infra;
using EvaGest.ViewModels.Dialogs;
using EvaGest.ViewModels.Pages;
using Xunit;

namespace EvaGest.Tests.Services;

/// <summary>
/// Block R: CU-02, where the agenda and the till meet. Marking an appointment as
/// completed has to open the sale with the data already filled in; otherwise the user
/// retypes what the app already knew.
/// </summary>
public class FluxAppointmentSaleTests
{
    private static readonly DateOnly Today = DateOnly.FromDateTime(DateTime.Today);

    private sealed record Assembly(
        HomeViewModel Start, TestDialogService Dialogs, AppointmentService Appointments, SaleService Sales);

    private static async Task<Assembly> Build(TestDatabase testDb, TestDialogService? dialogs = null)
    {
        var factory = new TestFactory(testDb.Options);
        var config = new SettingsService(factory);
        await new SeedService(factory, config).Seed();

        dialogs ??= new TestDialogService();
        var appointments = new AppointmentService(factory);
        var sales = new SaleService(factory, config);

        var start = new HomeViewModel(
            appointments, sales, new TillService(factory), new ClientService(factory),
            new AvailabilityService(factory), new CatalogService(factory),
            new WorkerService(factory), config, new TestSoundService(), dialogs);

        return new Assembly(start, dialogs, appointments, sales);
    }

    private static async Task<Appointment> AddsAppointment(TestDatabase testDb, bool withService = true)
    {
        await using var db = testDb.Context();

        var client = Make.Client();
        db.Clients.Add(client);

        Service? service = null;
        if (withService)
        {
            service = Make.Service("Tall", priceCents: 1500);
            db.Services.Add(service);
        }

        await db.SaveChangesAsync();

        var appointment = new Appointment
        {
            Date = Today, Time = new TimeOnly(10, 0), DurationMin = 30,
            ClientId = client.Id, ServiceId = service?.Id, Status = AppointmentStatus.Pending
        };
        db.Appointments.Add(appointment);
        await db.SaveChangesAsync();

        return appointment;
    }

    [Fact] // R-01
    public async Task Marking_completed_opens_the_sale_dialog_with_the_appointment_data()
    {
        await using var testDb = new TestDatabase();
        var m = await Build(testDb);
        var appointment = await AddsAppointment(testDb);
        await m.Start.Load();

        await m.Start.MarkCompletedCommand.ExecuteAsync(m.Start.DayAppointments.Single());

        var dialog = m.Dialogs.DialogsDisplayed.OfType<SaleDialogViewModel>().Single();
        dialog.CurrentMode.Should().Be(SaleDialogViewModel.Mode.FromAppointment);
        dialog.SelectedClient!.Id.Should().Be(appointment.ClientId);
        dialog.LinkedAppointmentText.Should().NotBeNull();
        dialog.Lines.Should().ContainSingle("the appointment's service becomes the first line");
        dialog.TotalCents.Should().Be(1500);
    }

    [Fact] // R-02
    public async Task Closing_the_sale_dialog_without_charging_leaves_the_appointment_pending_and_without_sale()
    {
        await using var testDb = new TestDatabase();
        var m = await Build(testDb); // TestDialogService cancels by default
        await AddsAppointment(testDb);
        await m.Start.Load();

        await m.Start.MarkCompletedCommand.ExecuteAsync(m.Start.DayAppointments.Single());

        var appointments = await m.Appointments.GetByDay(Today);
        appointments.Single().Status.Should().Be(AppointmentStatus.Pending,
            "cancel·lar el cobrament no ha de deixar rastre: la cita es pot tornar a cobrar");
        (await m.Sales.Search(new SalesFilter())).Should().BeEmpty();
    }

    [Fact] // R-03
    public async Task Appointment_without_service_opens_the_sale_with_an_empty_form()
    {
        await using var testDb = new TestDatabase();
        var m = await Build(testDb);
        await AddsAppointment(testDb, withService: false);
        await m.Start.Load();

        await m.Start.MarkCompletedCommand.ExecuteAsync(m.Start.DayAppointments.Single());

        m.Dialogs.DialogsDisplayed.OfType<SaleDialogViewModel>().Single()
            .Lines.Should().BeEmpty();
    }

    [Fact] // R-04
    public async Task Cancelling_or_marking_a_no_show_opens_no_sale()
    {
        await using var testDb = new TestDatabase();
        var m = await Build(testDb);
        await AddsAppointment(testDb);
        await m.Start.Load();

        await m.Start.MarkCancelledCommand.ExecuteAsync(m.Start.DayAppointments.Single());

        m.Dialogs.DialogsDisplayed.Should().BeEmpty();
        (await m.Appointments.GetByDay(Today)).Single().Status.Should().Be(AppointmentStatus.Cancelled);
    }

    [Fact] // R-05
    public async Task Charging_from_the_appointment_leaves_the_sale_linked_to_it()
    {
        await using var testDb = new TestDatabase();
        var dialogs = new TestDialogService
        {
            ResultDialog = true,
            FillDialog = async d =>
            {
                if (d is not SaleDialogViewModel sale) return;
                sale.PaymentMethod = sale.ActiveMethods[0];
                await sale.ChargeCommand.ExecuteAsync(null);
            }
        };

        var m = await Build(testDb, dialogs);
        var appointment = await AddsAppointment(testDb);
        await m.Start.Load();

        await m.Start.MarkCompletedCommand.ExecuteAsync(m.Start.DayAppointments.Single());

        var sale = (await m.Sales.Search(new SalesFilter())).Single();
        sale.AppointmentId.Should().Be(appointment.Id);
        sale.TotalCents.Should().Be(1500);
    }
}
