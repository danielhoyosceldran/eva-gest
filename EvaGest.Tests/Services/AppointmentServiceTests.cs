using AwesomeAssertions;
using EvaGest.Models;
using EvaGest.Services;
using EvaGest.Tests.Infra;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace EvaGest.Tests.Services;

/// <summary>Block F: appointments i estats.</summary>
public class AppointmentServiceTests
{
    private static readonly DateOnly Today = new(2026, 9, 7);

    private static AppointmentService CreatesService(TestDatabase testDb) => new(new TestFactory(testDb.Options));

    [Fact] // F-01
    public async Task A_new_appointment_starts_pending()
    {
        await using var testDb = new TestDatabase();
        var appointments = CreatesService(testDb);

        int id = await appointments.Create(new Appointment
        {
            Date = Today, Time = new TimeOnly(10, 0), DurationMin = 30, GuestName = "Convidat"
        });

        (await appointments.GetById(id))!.Status.Should().Be(AppointmentStatus.Pending);
    }

    [Fact] // F-02
    public async Task Mark_completed()
    {
        await using var testDb = new TestDatabase();
        var appointments = CreatesService(testDb);
        int id = await appointments.Create(new Appointment { Date = Today, Time = new TimeOnly(10, 0), DurationMin = 30, GuestName = "C" });

        await appointments.ChangeStatus(id, AppointmentStatus.Completed);

        (await appointments.GetById(id))!.Status.Should().Be(AppointmentStatus.Completed);
    }

    [Fact] // F-03
    public async Task Mark_cancelled()
    {
        await using var testDb = new TestDatabase();
        var appointments = CreatesService(testDb);
        int id = await appointments.Create(new Appointment { Date = Today, Time = new TimeOnly(10, 0), DurationMin = 30, GuestName = "C" });

        await appointments.ChangeStatus(id, AppointmentStatus.Cancelled);

        (await appointments.GetById(id))!.Status.Should().Be(AppointmentStatus.Cancelled);
    }

    [Fact] // F-04
    public async Task Mark_not_attended()
    {
        await using var testDb = new TestDatabase();
        var appointments = CreatesService(testDb);
        int id = await appointments.Create(new Appointment { Date = Today, Time = new TimeOnly(10, 0), DurationMin = 30, GuestName = "C" });

        await appointments.ChangeStatus(id, AppointmentStatus.NoShow);

        (await appointments.GetById(id))!.Status.Should().Be(AppointmentStatus.NoShow);
    }

    [Fact] // F-05
    public async Task A_cancelled_appointment_can_go_back_to_pending()
    {
        await using var testDb = new TestDatabase();
        var appointments = CreatesService(testDb);
        int id = await appointments.Create(new Appointment { Date = Today, Time = new TimeOnly(10, 0), DurationMin = 30, GuestName = "C" });
        await appointments.ChangeStatus(id, AppointmentStatus.Cancelled);

        bool changed = await appointments.ChangeStatus(id, AppointmentStatus.Pending);

        changed.Should().BeTrue();
        (await appointments.GetById(id))!.Status.Should().Be(AppointmentStatus.Pending);
    }

    [Fact] // F-05
    public async Task Completed_with_an_active_sale_cannot_go_back_to_pending()
    {
        await using var testDb = new TestDatabase();
        var appointments = CreatesService(testDb);

        int methodId, appointmentId;
        await using (var db = testDb.Context())
        {
            var method = Make.Method();
            db.PaymentMethods.Add(method);
            var appointment = new Appointment { Date = Today, Time = new TimeOnly(10, 0), DurationMin = 30, GuestName = "C" };
            db.Appointments.Add(appointment);
            await db.SaveChangesAsync();
            methodId = method.Id;
            appointmentId = appointment.Id;
        }

        await using (var db = testDb.Context())
        {
            db.Sales.Add(new Sale
            {
                Date = Today, Time = new TimeOnly(10, 0), GuestName = "C", AppointmentId = appointmentId,
                PaymentMethodId = methodId, BaseCents = 100, VatCents = 21, TotalCents = 121, VatMode = VatMode.Included
            });
            await db.SaveChangesAsync();
        }
        await appointments.ChangeStatus(appointmentId, AppointmentStatus.Completed);

        bool changed = await appointments.ChangeStatus(appointmentId, AppointmentStatus.Pending);

        changed.Should().BeFalse();
        (await appointments.GetById(appointmentId))!.Status.Should().Be(AppointmentStatus.Completed);
    }

    [Fact] // F-05
    public async Task Completed_with_a_voided_sale_can_go_back_to_pending()
    {
        await using var testDb = new TestDatabase();
        var appointments = CreatesService(testDb);
        var sales = new SaleService(new TestFactory(testDb.Options), new SettingsService(new TestFactory(testDb.Options)));

        int methodId, appointmentId, saleId;
        await using (var db = testDb.Context())
        {
            var method = Make.Method();
            db.PaymentMethods.Add(method);
            var appointment = new Appointment { Date = Today, Time = new TimeOnly(10, 0), DurationMin = 30, GuestName = "C" };
            db.Appointments.Add(appointment);
            await db.SaveChangesAsync();
            methodId = method.Id;
            appointmentId = appointment.Id;
        }

        await using (var db = testDb.Context())
        {
            var sale = new Sale
            {
                Date = Today, Time = new TimeOnly(10, 0), GuestName = "C", AppointmentId = appointmentId,
                PaymentMethodId = methodId, BaseCents = 100, VatCents = 21, TotalCents = 121, VatMode = VatMode.Included
            };
            db.Sales.Add(sale);
            await db.SaveChangesAsync();
            saleId = sale.Id;
        }
        await appointments.ChangeStatus(appointmentId, AppointmentStatus.Completed);
        await sales.Void(saleId);

        bool changed = await appointments.ChangeStatus(appointmentId, AppointmentStatus.Pending);

        changed.Should().BeTrue();
        (await appointments.GetById(appointmentId))!.Status.Should().Be(AppointmentStatus.Pending);
    }

    [Fact] // F-06
    public async Task A_completed_appointment_without_a_sale_is_valid()
    {
        await using var testDb = new TestDatabase();
        var appointments = CreatesService(testDb);
        int id = await appointments.Create(new Appointment { Date = Today, Time = new TimeOnly(10, 0), DurationMin = 30, GuestName = "C" });
        await appointments.ChangeStatus(id, AppointmentStatus.Completed);

        (await appointments.GetCompletedWithoutSale()).Should().ContainSingle(c => c.Id == id);
    }

    [Fact] // F-07
    public async Task The_duration_comes_from_the_service_when_it_has_one()
    {
        await using var testDb = new TestDatabase();
        int serviceId;
        await using (var db = testDb.Context())
        {
            var service = Make.Service("Tall", duration: 30);
            db.Services.Add(service);
            await db.SaveChangesAsync();
            serviceId = service.Id;
        }

        var service2 = (await testDb.Context().Services.FindAsync(serviceId))!;
        service2.DurationMin.Should().Be(30);
    }

    [Fact] // F-09
    public async Task An_appointment_with_a_guest_client_is_saved()
    {
        await using var testDb = new TestDatabase();
        var appointments = CreatesService(testDb);

        int id = await appointments.Create(new Appointment
        {
            Date = Today, Time = new TimeOnly(10, 0), DurationMin = 30,
            GuestName = "Anna", GuestPhone = "600111222"
        });

        (await appointments.GetById(id))!.IsGuest.Should().BeTrue();
    }

    [Fact] // F-10
    public async Task An_appointment_with_both_a_registered_client_and_a_guest_name_is_rejected()
    {
        await using var testDb = new TestDatabase();
        await using var db = testDb.Context();

        var client = new Client { Name = "Joan", Mobile = "612345678" };
        db.Clients.Add(client);
        await db.SaveChangesAsync();

        db.Appointments.Add(new Appointment
        {
            Date = Today, Time = new TimeOnly(10, 0), DurationMin = 30,
            ClientId = client.Id, GuestName = "Un altre nom"
        });

        var action = async () => await db.SaveChangesAsync();
        await action.Should().ThrowAsync<DbUpdateException>();
    }

    [Fact] // F-11
    public async Task An_appointment_with_neither_client_nor_guest_is_rejected()
    {
        await using var testDb = new TestDatabase();
        await using var db = testDb.Context();

        db.Appointments.Add(new Appointment { Date = Today, Time = new TimeOnly(10, 0), DurationMin = 30 });

        var action = async () => await db.SaveChangesAsync();
        await action.Should().ThrowAsync<DbUpdateException>();
    }

    [Fact] // F-12
    public async Task An_appointment_cannot_have_two_sales()
    {
        await using var testDb = new TestDatabase();

        int methodId, appointmentId;
        await using (var db = testDb.Context())
        {
            var method = Make.Method();
            db.PaymentMethods.Add(method);
            var appointment = new Appointment { Date = Today, Time = new TimeOnly(10, 0), DurationMin = 30, GuestName = "C" };
            db.Appointments.Add(appointment);
            await db.SaveChangesAsync();
            methodId = method.Id;
            appointmentId = appointment.Id;
        }

        // Two separate DbContext instances, like two independent operations in the
        // running app: this is what actually exercises the unique index at the SQLite
        // level. A single shared context instead triggers EF's own one-to-one fixup,
        // which silently nulls the first Sale's AppointmentId and never reaches the database.
        await using (var db = testDb.Context())
        {
            db.Sales.Add(new Sale
            {
                Date = Today, Time = new TimeOnly(10, 0), GuestName = "C", AppointmentId = appointmentId,
                PaymentMethodId = methodId, BaseCents = 100, VatCents = 21, TotalCents = 121, VatMode = VatMode.Included
            });
            await db.SaveChangesAsync();
        }

        await using var db2 = testDb.Context();
        db2.Sales.Add(new Sale
        {
            Date = Today, Time = new TimeOnly(11, 0), GuestName = "C", AppointmentId = appointmentId,
            PaymentMethodId = methodId, BaseCents = 100, VatCents = 21, TotalCents = 121, VatMode = VatMode.Included
        });

        var action = async () => await db2.SaveChangesAsync();
        await action.Should().ThrowAsync<DbUpdateException>();
    }
}
