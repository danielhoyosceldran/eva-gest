using AwesomeAssertions;
using EvaGest.Models;
using EvaGest.Services;
using EvaGest.Tests.Infra;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace EvaGest.Tests.Services;

/// <summary>
/// The one rule every delete in the app follows: what nothing points at is removed, what
/// the history already names is deactivated instead, and the caller is told which of the
/// two happened. These tests exist because the difference is invisible from the UI —
/// both look like the row disappearing from the pickers.
/// </summary>
public class DeletionTests
{
    private static readonly DateOnly Today = new(2026, 9, 7);

    private static CatalogService Catalog(TestDatabase testDb) => new(new TestFactory(testDb.Options));
    private static AppointmentService Appointments(TestDatabase testDb) => new(new TestFactory(testDb.Options));
    private static WorkerService Workers(TestDatabase testDb) => new(new TestFactory(testDb.Options));

    private static SaleService Sales(TestDatabase testDb)
        => new(new TestFactory(testDb.Options), new TestSettings());

    // ── Services ─────────────────────────────────────────────────────────────

    [Fact]
    public async Task An_unused_service_is_deleted_outright()
    {
        await using var testDb = new TestDatabase();
        var catalog = Catalog(testDb);
        int id = await catalog.CreateService(Make.Service("Tall"));

        var result = await catalog.DeleteService(id);

        result.Should().Be(DeleteResult.Deleted);
        (await catalog.GetService(id)).Should().BeNull();
    }

    [Fact]
    public async Task A_service_used_by_an_appointment_is_deactivated_and_the_appointment_keeps_it()
    {
        await using var testDb = new TestDatabase();
        var catalog = Catalog(testDb);
        int id = await catalog.CreateService(Make.Service("Tall"));

        await using (var db = testDb.Context())
        {
            db.Appointments.Add(new Appointment
            {
                Date = Today, Time = new TimeOnly(10, 0), DurationMin = 30,
                GuestName = "Convidat", ServiceId = id
            });
            await db.SaveChangesAsync();
        }

        var result = await catalog.DeleteService(id);

        result.Should().Be(DeleteResult.Deactivated);
        (await catalog.GetService(id))!.Active.Should().BeFalse();

        // The appointment still says what it was booked for...
        await using var check = testDb.Context();
        (await check.Appointments.SingleAsync()).ServiceId.Should().Be(id);

        // ...but nobody can pick it again for a new one.
        (await catalog.GetServices(onlyActive: true)).Should().BeEmpty();
    }

    [Fact]
    public async Task Service_sold_is_deactivates_i_the_line_of_sale_is_keepsé()
    {
        await using var testDb = new TestDatabase();
        var catalog = Catalog(testDb);
        int method = await catalog.CreateMethod("Efectiu");
        int id = await catalog.CreateService(Make.Service("Tall"));

        var line = Make.Line(1500);
        line.ServiceId = id;
        await Sales(testDb).Create(Make.Sale(Today, method, SaleStatus.Active), [line]);

        var result = await catalog.DeleteService(id);

        result.Should().Be(DeleteResult.Deactivated);
        await using var db = testDb.Context();
        (await db.SaleLines.SingleAsync()).ServiceId.Should().Be(id);
    }

    // ── Products ───────────────────────────────────────────────────────────

    [Fact]
    public async Task An_unsold_product_is_deleted_and_a_sold_one_is_deactivated()
    {
        await using var testDb = new TestDatabase();
        var catalog = Catalog(testDb);
        int method = await catalog.CreateMethod("Efectiu");
        int never = await catalog.CreateProduct(Make.Product("Cera"));
        int sold = await catalog.CreateProduct(Make.Product("Xampú"));

        var line = Make.Line(900);
        line.ProductId = sold;
        await Sales(testDb).Create(Make.Sale(Today, method, SaleStatus.Active), [line]);

        (await catalog.DeleteProduct(never)).Should().Be(DeleteResult.Deleted);
        (await catalog.DeleteProduct(sold)).Should().Be(DeleteResult.Deactivated);

        (await catalog.GetProduct(never)).Should().BeNull();
        (await catalog.GetProduct(sold))!.Active.Should().BeFalse();
    }

    // ── Payment methods ─────────────────────────────────────────────────

    [Fact]
    public async Task The_last_active_payment_method_is_not_deleted()
    {
        await using var testDb = new TestDatabase();
        var catalog = Catalog(testDb);
        int cash = await catalog.CreateMethod("Efectiu");

        (await catalog.DeleteMethod(cash)).Should().Be(DeleteResult.Blocked);
        (await catalog.GetMethods()).Should().ContainSingle();
    }

    /// <summary>
    /// The sharp edge this whole feature exists for: the sale's foreign key cascades, so
    /// removing a used method would delete the sales along with it.
    /// </summary>
    [Fact]
    public async Task A_payment_method_used_by_a_sale_is_deactivated_and_does_not_drag_the_sale_with_it()
    {
        await using var testDb = new TestDatabase();
        var catalog = Catalog(testDb);
        int cash = await catalog.CreateMethod("Efectiu");
        await catalog.CreateMethod("Targeta");

        await Sales(testDb).Create(Make.Sale(Today, cash, SaleStatus.Active), [Make.Line(1500)]);

        var result = await catalog.DeleteMethod(cash);

        result.Should().Be(DeleteResult.Deactivated);
        await using var db = testDb.Context();
        (await db.Sales.CountAsync()).Should().Be(1);
        (await db.PaymentMethods.SingleAsync(m => m.Id == cash)).Active.Should().BeFalse();
    }

    [Fact]
    public async Task An_unused_payment_method_is_deleted_when_another_active_one_remains()
    {
        await using var testDb = new TestDatabase();
        var catalog = Catalog(testDb);
        await catalog.CreateMethod("Efectiu");
        int card = await catalog.CreateMethod("Targeta");

        (await catalog.DeleteMethod(card)).Should().Be(DeleteResult.Deleted);
        (await catalog.GetMethods()).Should().ContainSingle(m => m.Name == "Efectiu");
    }

    [Fact]
    public async Task A_payment_method_used_by_a_till_movement_is_deactivated()
    {
        await using var testDb = new TestDatabase();
        var catalog = Catalog(testDb);
        int cash = await catalog.CreateMethod("Efectiu");
        await catalog.CreateMethod("Targeta");

        var till = new TillService(new TestFactory(testDb.Options));
        await till.Create(new CashMovement
        {
            Date = Today, Type = MovementType.Out, AmountCents = 2000,
            PaymentMethodId = cash, Concept = "Compra material"
        });

        (await catalog.DeleteMethod(cash)).Should().Be(DeleteResult.Deactivated);

        await using var db = testDb.Context();
        (await db.CashMovements.CountAsync()).Should().Be(1);
    }

    // ── Workers ───────────────────────────────────────────────────────

    [Fact]
    public async Task A_worker_without_history_is_deleted_along_with_her_schedule()
    {
        await using var testDb = new TestDatabase();
        var workers = Workers(testDb);

        var created = await workers.Create(Make.Worker("Marta"),
            new Dictionary<Weekday, List<(TimeOnly, TimeOnly)>>
            {
                [Weekday.Mon] = [(new TimeOnly(9, 0), new TimeOnly(14, 0))]
            });

        (await workers.Delete(created.Id)).Should().Be(DeleteResult.Deleted);

        await using var db = testDb.Context();
        (await db.Workers.CountAsync()).Should().Be(0);
        (await db.WorkerSchedules.CountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task A_worker_with_appointments_is_deactivated()
    {
        await using var testDb = new TestDatabase();
        var workers = Workers(testDb);

        var created = await workers.Create(Make.Worker("Marta"),
            new Dictionary<Weekday, List<(TimeOnly, TimeOnly)>>());

        await Appointments(testDb).Create(new Appointment
        {
            Date = Today, Time = new TimeOnly(10, 0), DurationMin = 30,
            GuestName = "Convidat", WorkerId = created.Id
        });

        (await workers.Delete(created.Id)).Should().Be(DeleteResult.Deactivated);

        (await workers.GetAll()).Should().ContainSingle(t => !t.Active);
        (await workers.GetAll(onlyActive: true)).Should().BeEmpty();
    }

    // ── Appointments ───────────────────────────────────────────────────────────────

    [Fact]
    public async Task An_appointment_without_a_sale_is_deleted()
    {
        await using var testDb = new TestDatabase();
        var appointments = Appointments(testDb);
        int id = await appointments.Create(new Appointment
        {
            Date = Today, Time = new TimeOnly(10, 0), DurationMin = 30, GuestName = "Convidat"
        });

        (await appointments.Delete(id)).Should().Be(DeleteResult.Deleted);
        (await appointments.GetById(id)).Should().BeNull();
    }

    [Fact]
    public async Task An_appointment_with_a_linked_sale_is_not_deleted()
    {
        await using var testDb = new TestDatabase();
        var appointments = Appointments(testDb);
        var catalog = Catalog(testDb);
        int method = await catalog.CreateMethod("Efectiu");

        int appointmentId = await appointments.Create(new Appointment
        {
            Date = Today, Time = new TimeOnly(10, 0), DurationMin = 30,
            GuestName = "Convidat", Status = AppointmentStatus.Completed
        });

        var sale = Make.Sale(Today, method, SaleStatus.Active);
        sale.AppointmentId = appointmentId;
        await Sales(testDb).Create(sale, [Make.Line(1500)]);

        (await appointments.Delete(appointmentId)).Should().Be(DeleteResult.Blocked);
        (await appointments.GetById(appointmentId)).Should().NotBeNull();
    }

    // ── Sales ──────────────────────────────────────────────────────────────

    [Fact]
    public async Task Deleting_an_active_sale_voids_it_and_deleting_the_voided_one_removes_it()
    {
        await using var testDb = new TestDatabase();
        var catalog = Catalog(testDb);
        var sales = Sales(testDb);
        int method = await catalog.CreateMethod("Efectiu");

        int id = await sales.Create(Make.Sale(Today, method, SaleStatus.Active), [Make.Line(1500)]);

        (await sales.Delete(id)).Should().Be(DeleteResult.Deactivated);
        (await sales.GetById(id))!.Status.Should().Be(SaleStatus.Voided);

        (await sales.Delete(id)).Should().Be(DeleteResult.Deleted);
        (await sales.GetById(id)).Should().BeNull();

        await using var db = testDb.Context();
        (await db.SaleLines.CountAsync()).Should().Be(0);
        (await db.SaleBreakdowns.CountAsync()).Should().Be(0);
    }
}
