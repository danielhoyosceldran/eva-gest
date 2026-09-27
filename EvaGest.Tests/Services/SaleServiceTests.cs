using AwesomeAssertions;
using EvaGest.Models;
using EvaGest.Services;
using EvaGest.Tests.Infra;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace EvaGest.Tests.Services;

/// <summary>Block G (sales) + F-05 (a cancelled appointment cannot carry a sale).</summary>
public class SaleServiceTests
{
    private static readonly DateOnly Today = new(2026, 9, 7);

    private static SaleService CreatesService(TestDatabase testDb)
        => new(new TestFactory(testDb.Options), new TestSettings());

    private static async Task<int> AddsMethod(TestDatabase testDb)
    {
        await using var db = testDb.Context();
        var m = Make.Method();
        db.PaymentMethods.Add(m);
        await db.SaveChangesAsync();
        return m.Id;
    }

    private static Sale NewSale(int methodId, params SaleLine[] lines) => new()
    {
        Date = Today, Time = new TimeOnly(10, 0), GuestName = "Client de prova",
        PaymentMethodId = methodId, Lines = [.. lines]
    };

    private static SaleLine Line(int amountCents, int vatBp = 2100, int quantity = 1,
        int? serviceId = null, int? productId = null) => new()
    {
        ServiceId = serviceId, ProductId = productId, Description = "Prova",
        Quantity = quantity, UnitPriceCents = amountCents / quantity,
        VatBp = vatBp, AmountCents = amountCents
    };

    [Fact] // G-01
    public async Task A_standalone_sale_with_one_service_is_saved_with_the_right_totals()
    {
        await using var testDb = new TestDatabase();
        int methodId = await AddsMethod(testDb);
        var sales = CreatesService(testDb);

        int id = await sales.Create(NewSale(methodId), [Line(1500, 2100)]);

        var sale = await sales.GetById(id);
        sale!.BaseCents.Should().Be(1240);
        sale.VatCents.Should().Be(260);
        sale.TotalCents.Should().Be(1500);
    }

    [Fact] // G-02
    public async Task A_sale_from_an_appointment_preloads_client_and_service()
    {
        await using var testDb = new TestDatabase();
        int methodId = await AddsMethod(testDb);
        int appointmentId;
        await using (var db = testDb.Context())
        {
            var service = Make.Service();
            db.Services.Add(service);
            await db.SaveChangesAsync();

            var appointment = new Appointment
            {
                Date = Today, Time = new TimeOnly(10, 0), DurationMin = 30,
                GuestName = "Anna", ServiceId = service.Id
            };
            db.Appointments.Add(appointment);
            await db.SaveChangesAsync();
            appointmentId = appointment.Id;
        }

        var sales = CreatesService(testDb);
        var ready = await sales.PrepareFromAppointment(appointmentId);

        ready.GuestName.Should().Be("Anna");
        ready.AppointmentId.Should().Be(appointmentId);
    }

    [Fact] // G-03
    public async Task A_custom_line_is_of_type_other()
    {
        var line = Line(1000, 2100);
        line.Type.Should().Be(LineType.Other);
    }

    [Fact] // G-04 / G-05
    public async Task Changing_a_service_price_afterwards_does_not_affect_earlier_sales()
    {
        await using var testDb = new TestDatabase();
        int methodId = await AddsMethod(testDb);
        int serviceId;
        await using (var db = testDb.Context())
        {
            var service = Make.Service("Tall", 1500, 2100);
            db.Services.Add(service);
            await db.SaveChangesAsync();
            serviceId = service.Id;
        }

        var sales = CreatesService(testDb);
        int saleId = await sales.Create(NewSale(methodId), [Line(1500, 2100, serviceId: serviceId)]);

        // The catalogue price changes afterwards
        await using (var db = testDb.Context())
        {
            var service = await db.Services.FindAsync(serviceId);
            service!.PriceCents = 2000;
            await db.SaveChangesAsync();
        }

        var sale = await sales.GetById(saleId);
        sale!.TotalCents.Should().Be(1500); // the frozen price is kept
    }

    [Fact] // G-07
    public async Task The_vat_mode_is_frozen_on_the_sale()
    {
        await using var testDb = new TestDatabase();
        int methodId = await AddsMethod(testDb);
        var sales = CreatesService(testDb);

        int id = await sales.Create(NewSale(methodId), [Line(1500, 2100)]);

        (await sales.GetById(id))!.VatMode.Should().Be(VatMode.Included);
    }

    [Fact] // G-10 / G-11
    public async Task Voiding_a_sale_does_not_delete_it_and_it_stays_visible()
    {
        await using var testDb = new TestDatabase();
        int methodId = await AddsMethod(testDb);
        var sales = CreatesService(testDb);

        int id = await sales.Create(NewSale(methodId), [Line(1500, 2100)]);
        await sales.Void(id);

        var sale = await sales.GetById(id);
        sale!.Status.Should().Be(SaleStatus.Voided);
        (await sales.Search(new SalesFilter())).Should().ContainSingle(v => v.Id == id);
    }

    [Fact] // G-12 / G-13
    public async Task Editing_a_sale_recomputes_totals_and_breakdowns()
    {
        await using var testDb = new TestDatabase();
        int methodId = await AddsMethod(testDb);
        var sales = CreatesService(testDb);

        int id = await sales.Create(NewSale(methodId), [Line(1000, 2100)]);

        var updated = new Sale { Id = id, Date = Today, Time = new TimeOnly(11, 0), GuestName = "C", PaymentMethodId = methodId };
        await sales.Update(updated, [Line(2000, 2100), Line(1000, 1000)]);

        var sale = await sales.GetById(id);
        sale!.Lines.Should().HaveCount(2);

        await using var db = testDb.Context();
        var breakdowns = db.SaleBreakdowns.Where(d => d.SaleId == id).ToList();
        breakdowns.Should().HaveCount(2);
    }

    [Fact] // G-14
    public async Task A_sale_with_a_guest_client_is_saved_and_counts_towards_the_totals()
    {
        await using var testDb = new TestDatabase();
        int methodId = await AddsMethod(testDb);
        var sales = CreatesService(testDb);

        await sales.Create(NewSale(methodId), [Line(1500, 2100)]);

        long total = 0;
        await using (var db = testDb.Context())
            total = await db.Sales.SumAsync(v => (long)v.TotalCents);

        total.Should().Be(1500);
    }

    [Fact] // F-05
    public async Task A_cancelled_appointment_cannot_carry_a_sale()
    {
        await using var testDb = new TestDatabase();
        int appointmentId;
        await using (var db = testDb.Context())
        {
            var appointment = new Appointment
            {
                Date = Today, Time = new TimeOnly(10, 0), DurationMin = 30,
                GuestName = "C", Status = AppointmentStatus.Cancelled
            };
            db.Appointments.Add(appointment);
            await db.SaveChangesAsync();
            appointmentId = appointment.Id;
        }

        var sales = CreatesService(testDb);
        var action = async () => await sales.PrepareFromAppointment(appointmentId);

        await action.Should().ThrowAsync<InvalidOperationException>();
    }

    [Fact]
    public async Task Editing_a_sale_keeps_what_it_said_before_and_after_in_the_audit_trail()
    {
        await using var testDb = new TestDatabase();
        int methodId = await AddsMethod(testDb);
        var sales = CreatesService(testDb);

        int id = await sales.Create(NewSale(methodId), [Line(1000, 2100)]);

        var updated = new Sale { Id = id, Date = Today.AddDays(-40), Time = new TimeOnly(11, 0), GuestName = "C", PaymentMethodId = methodId };
        await sales.Update(updated, [Line(2500, 2100)]);

        await using var db = testDb.Context();
        var entry = await db.AuditEntries.SingleAsync();
        entry.Entity.Should().Be("Sale");
        entry.EntityId.Should().Be(id);
        entry.Action.Should().Be("Update");

        var before = System.Text.Json.JsonSerializer.Deserialize<AuditTrail.SaleSnapshot>(entry.Before!)!;
        var after = System.Text.Json.JsonSerializer.Deserialize<AuditTrail.SaleSnapshot>(entry.After!)!;
        before.TotalCents.Should().Be(1000);
        before.Date.Should().Be(Today);
        before.Lines.Should().ContainSingle(l => l.AmountCents == 1000);
        after.TotalCents.Should().Be(2500);
        after.Date.Should().Be(Today.AddDays(-40));
        after.Lines.Should().ContainSingle(l => l.AmountCents == 2500);
    }

    [Fact]
    public async Task Voiding_a_sale_leaves_an_audit_entry()
    {
        await using var testDb = new TestDatabase();
        int methodId = await AddsMethod(testDb);
        var sales = CreatesService(testDb);

        int id = await sales.Create(NewSale(methodId), [Line(1000, 2100)]);
        await sales.Void(id);

        await using var db = testDb.Context();
        var entry = await db.AuditEntries.SingleAsync();
        entry.Action.Should().Be("Void");
        entry.Before.Should().Contain("\"Status\":\"Active\"");
        entry.After.Should().Contain("\"Status\":\"Voided\"");
    }

    [Fact]
    public async Task A_sale_with_a_negative_line_is_refused_and_nothing_is_saved()
    {
        await using var testDb = new TestDatabase();
        int methodId = await AddsMethod(testDb);
        var sales = CreatesService(testDb);

        var act = () => sales.Create(NewSale(methodId), [Line(1500), Line(-500)]);

        await act.Should().ThrowAsync<ArgumentException>();
        await using var db = testDb.Context();
        (await db.Sales.CountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task A_voided_sale_can_not_be_edited()
    {
        await using var testDb = new TestDatabase();
        int methodId = await AddsMethod(testDb);
        var sales = CreatesService(testDb);

        int id = await sales.Create(NewSale(methodId), [Line(1000, 2100)]);
        await sales.Void(id);

        var updated = new Sale { Id = id, Date = Today, Time = new TimeOnly(10, 0), GuestName = "C", PaymentMethodId = methodId };
        var act = () => sales.Update(updated, [Line(9900, 2100)]);

        await act.Should().ThrowAsync<InvalidOperationException>();
        (await sales.GetById(id))!.TotalCents.Should().Be(1000);
    }

    [Fact]
    public async Task Charging_an_appointment_saves_the_sale_and_closes_the_appointment_in_one_save()
    {
        // One SaveChanges is one transaction: the sale can never be recorded while the
        // appointment it charged stays pending (they used to be two separate saves).
        await using var testDb = new TestDatabase();
        int methodId = await AddsMethod(testDb);
        int appointmentId;
        await using (var db = testDb.Context())
        {
            var appointment = new Appointment
            {
                Date = Today, Time = new TimeOnly(10, 0), DurationMin = 30, GuestName = "Pere"
            };
            db.Appointments.Add(appointment);
            await db.SaveChangesAsync();
            appointmentId = appointment.Id;
        }

        var counter = new SaveCounter();
        var options = new DbContextOptionsBuilder<EvaGest.Data.ShopDbContext>(testDb.Options)
            .AddInterceptors(counter).Options;
        var sales = new SaleService(new TestFactory(options), new TestSettings());

        var sale = NewSale(methodId);
        sale.GuestName = "Pere";
        sale.AppointmentId = appointmentId;
        await sales.Create(sale, [Line(1500)]);

        counter.Saves.Should().Be(1);
        await using var check = testDb.Context();
        (await check.Appointments.SingleAsync()).Status.Should().Be(AppointmentStatus.Completed);
    }

    private sealed class SaveCounter : Microsoft.EntityFrameworkCore.Diagnostics.SaveChangesInterceptor
    {
        public int Saves { get; private set; }

        public override ValueTask<Microsoft.EntityFrameworkCore.Diagnostics.InterceptionResult<int>> SavingChangesAsync(
            Microsoft.EntityFrameworkCore.Diagnostics.DbContextEventData eventData,
            Microsoft.EntityFrameworkCore.Diagnostics.InterceptionResult<int> result,
            CancellationToken cancellationToken = default)
        {
            Saves++;
            return base.SavingChangesAsync(eventData, result, cancellationToken);
        }
    }

    [Fact]
    public async Task A_sale_records_when_it_was_entered_and_an_edit_never_moves_that()
    {
        await using var testDb = new TestDatabase();
        int methodId = await AddsMethod(testDb);
        var sales = CreatesService(testDb);

        var before = DateTime.UtcNow;
        int id = await sales.Create(NewSale(methodId), [Line(1000)]);
        var createdAt = (await sales.GetById(id))!.CreatedAtUtc;

        createdAt.Should().NotBeNull();
        createdAt!.Value.Should().BeOnOrAfter(before).And.BeOnOrBefore(DateTime.UtcNow);

        var updated = new Sale { Id = id, Date = Today.AddDays(-3), Time = new TimeOnly(9, 0), GuestName = "C", PaymentMethodId = methodId };
        await sales.Update(updated, [Line(1200)]);

        (await sales.GetById(id))!.CreatedAtUtc.Should().Be(createdAt);
    }

    [Fact]
    public async Task A_line_whose_amount_is_not_price_times_quantity_is_refused()
    {
        await using var testDb = new TestDatabase();
        int methodId = await AddsMethod(testDb);
        var sales = CreatesService(testDb);

        var tampered = new SaleLine { Description = "Tall", Quantity = 2, UnitPriceCents = 1500, VatBp = 2100, AmountCents = 1500 };
        var act = () => sales.Create(NewSale(methodId), [tampered]);

        await act.Should().ThrowAsync<ArgumentException>();
        await using var db = testDb.Context();
        (await db.Sales.CountAsync()).Should().Be(0);
    }
}
