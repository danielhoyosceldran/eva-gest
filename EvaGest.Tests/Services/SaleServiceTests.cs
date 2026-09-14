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
}
