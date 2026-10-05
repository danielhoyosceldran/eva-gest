using AwesomeAssertions;
using EvaGest.Models;
using EvaGest.Services;
using EvaGest.Tests.Infra;
using EvaGest.ViewModels.Dialogs;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace EvaGest.Tests.Services;

/// <summary>
/// An edit of an appointment's details must never change its status. The edit dialog
/// reads the appointment when it opens; if the appointment is charged or cancelled while
/// the dialog sits open, saving the edit used to write the old Pending status back,
/// leaving a "pending" appointment with an active sale attached.
/// </summary>
public class AppointmentEditStatusTests
{
    private static readonly DateOnly Day = new(2026, 10, 12);

    private sealed record Shop(
        TestFactory Factory, AppointmentService Appointments, SaleService Sales, int MethodId);

    private static async Task<Shop> Build(TestDatabase testDb)
    {
        var factory = new TestFactory(testDb.Options);
        var config = new SettingsService(factory);
        await new SeedService(factory, config).Seed();

        await using var db = testDb.Context();
        int methodId = (await db.PaymentMethods.FirstAsync()).Id;
        return new Shop(factory, new AppointmentService(factory), new SaleService(factory, config), methodId);
    }

    private static async Task<int> Book(Shop shop)
        => await shop.Appointments.Create(new Appointment
        {
            Date = Day, Time = new TimeOnly(10, 0), DurationMin = 30,
            GuestName = "Pere", Status = AppointmentStatus.Pending
        });

    private static async Task Charge(Shop shop, int appointmentId)
    {
        var sale = await shop.Sales.PrepareFromAppointment(appointmentId);
        sale.PaymentMethodId = shop.MethodId;
        await shop.Sales.Create(sale, [Make.Line(1500)]);
    }

    [Fact]
    public async Task Saving_an_edit_opened_before_the_appointment_was_charged_keeps_it_completed()
    {
        await using var testDb = new TestDatabase();
        var shop = await Build(testDb);
        int id = await Book(shop);

        var opened = (await shop.Appointments.GetById(id))!;   // read while still Pending
        await Charge(shop, id);                                // meanwhile, charged

        opened.Notes = "Vol el tall més curt";
        opened.Time = new TimeOnly(10, 15);
        await shop.Appointments.Update(opened);

        var saved = (await shop.Appointments.GetById(id))!;
        saved.Status.Should().Be(AppointmentStatus.Completed, "it was charged, and an edit of the details cannot undo that");
        saved.Notes.Should().Be("Vol el tall més curt");
        saved.Time.Should().Be(new TimeOnly(10, 15));
    }

    [Fact]
    public async Task Saving_an_edit_opened_before_the_appointment_was_cancelled_keeps_it_cancelled()
    {
        await using var testDb = new TestDatabase();
        var shop = await Build(testDb);
        int id = await Book(shop);

        var opened = (await shop.Appointments.GetById(id))!;
        (await shop.Appointments.ChangeStatus(id, AppointmentStatus.Cancelled)).Should().BeTrue();

        opened.Notes = "Trucarà per tornar a demanar hora";
        await shop.Appointments.Update(opened);

        var saved = (await shop.Appointments.GetById(id))!;
        saved.Status.Should().Be(AppointmentStatus.Cancelled);
        saved.Notes.Should().Be("Trucarà per tornar a demanar hora");
    }

    [Fact]
    public async Task A_charged_appointment_edited_from_a_stale_copy_is_not_offered_to_be_charged_again()
    {
        // What the user would actually run into: back at Pending with its sale still
        // active, the appointment shows up as one still to charge.
        await using var testDb = new TestDatabase();
        var shop = await Build(testDb);
        int id = await Book(shop);

        var opened = (await shop.Appointments.GetById(id))!;
        await Charge(shop, id);
        opened.Notes = "Canvi de nota";
        await shop.Appointments.Update(opened);

        await using var db = testDb.Context();
        var appointment = await db.Appointments.Include(a => a.Sales).SingleAsync(a => a.Id == id);
        appointment.Sales.Should().ContainSingle(s => s.Status == SaleStatus.Active);
        appointment.Status.Should().NotBe(AppointmentStatus.Pending);
    }

    [Fact]
    public async Task Saving_the_edit_dialog_after_the_appointment_was_charged_keeps_it_completed()
    {
        await using var testDb = new TestDatabase();
        var shop = await Build(testDb);
        int id = await Book(shop);

        // The dialog opens on the appointment while it is still Pending...
        var vm = new AppointmentDialogViewModel(
            shop.Appointments, new AvailabilityService(shop.Factory), new ClientService(shop.Factory),
            new CatalogService(shop.Factory), new WorkerService(shop.Factory), new SettingsService(shop.Factory),
            new TestDialogService(), (await shop.Appointments.GetById(id))!);
        await vm.Initialization;

        // ...someone charges it from the Home page...
        await Charge(shop, id);

        // ...and then the open dialog is saved with a new note.
        vm.Notes = "Porta el seu propi raspall";
        await vm.SaveCommand.ExecuteAsync(null);

        vm.ErrorValidation.Should().BeNull();
        var saved = (await shop.Appointments.GetById(id))!;
        saved.Status.Should().Be(AppointmentStatus.Completed);
        saved.Notes.Should().Be("Porta el seu propi raspall");
    }
}
