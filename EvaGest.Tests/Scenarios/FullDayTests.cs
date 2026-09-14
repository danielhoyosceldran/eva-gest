using AwesomeAssertions;
using EvaGest.Models;
using EvaGest.Services;
using EvaGest.Tests.Infra;
using Xunit;

namespace EvaGest.Tests.Scenarios;

/// <summary>
/// Block K: whole simulated days, to check that everything adds up at the end of one.
/// The other blocks each test one piece; this one makes them work together, which is
/// where the one-cent gaps nobody notices until the quarterly return show up.
/// </summary>
public class DayFullTests
{
    private static readonly DateOnly Day = new(2026, 9, 9);

    private sealed record Scenario(
        TestDatabase Bd, AppointmentService Appointments, SaleService Sales, TillService Till,
        ReportsService Reports, CatalogService Catalog, SettingsService Settings);

    private static async Task<Scenario> Build(TestDatabase testDb)
    {
        var factory = new TestFactory(testDb.Options);
        var config = new SettingsService(factory);
        await new SeedService(factory, config).Seed();

        return new Scenario(testDb, new AppointmentService(factory), new SaleService(factory, config),
            new TillService(factory), new ReportsService(factory), new CatalogService(factory), config);
    }

    private static async Task<int> FirstMethod(TestDatabase testDb)
    {
        await using var db = testDb.Context();
        return db.PaymentMethods.OrderBy(m => m.Id).First().Id;
    }

    private static async Task<int> Adds<T>(TestDatabase testDb, T entity) where T : class
    {
        await using var db = testDb.Context();
        db.Add(entity);
        await db.SaveChangesAsync();
        return (int)typeof(T).GetProperty("Id")!.GetValue(entity)!;
    }

    private static SaleLine Line(int amountCents, int vatBp = 2100,
        int? serviceId = null, int? productId = null, string description = "Tall")
        => new()
        {
            Description = description,
            Quantity = 1,
            UnitPriceCents = amountCents,
            VatBp = vatBp,
            AmountCents = amountCents,
            ServiceId = serviceId,
            ProductId = productId
        };

    private static Sale NewSale(DateOnly date, int methodId, int? clientId = null,
        int? workerId = null, string? guest = null)
        => new()
        {
            Date = date,
            Time = new TimeOnly(10, 0),
            ClientId = clientId,
            GuestName = clientId is null ? guest ?? "Client de prova" : null,
            WorkerId = workerId,
            PaymentMethodId = methodId
        };

    [Fact] // K-01
    public async Task An_ordinary_day_adds_up_in_balance_and_vat()
    {
        await using var testDb = new TestDatabase();
        var e = await Build(testDb);
        int methodId = await FirstMethod(testDb);

        // Six appointments: five completed and charged, one no-show
        for (int i = 0; i < 6; i++)
            await e.Appointments.Create(new Appointment
            {
                Date = Day, Time = new TimeOnly(9 + i, 0), DurationMin = 30,
                GuestName = $"Client {i}", Status = AppointmentStatus.Pending
            });

        var appointments = await e.Appointments.GetByDay(Day);
        foreach (var appointment in appointments.Take(5))
        {
            await e.Appointments.ChangeStatus(appointment.Id, AppointmentStatus.Completed);
            await e.Sales.Create(NewSale(Day, methodId), [Line(1500)]);
        }
        await e.Appointments.ChangeStatus(appointments[5].Id, AppointmentStatus.NoShow);

        await e.Till.Create(new CashMovement
        {
            Date = Day, Type = MovementType.In, AmountCents = 5000,
            PaymentMethodId = methodId, Concept = "Aportació"
        });
        await e.Till.Create(new CashMovement
        {
            Date = Day, Type = MovementType.Out, AmountCents = 3000,
            PaymentMethodId = methodId, Concept = "Material"
        });

        var summary = await e.Till.Summary(Day, Day);

        summary.SalesCents.Should().Be(7500);
        summary.CashInCents.Should().Be(5000);
        summary.CashOutCents.Should().Be(3000);
        summary.BalanceCents.Should().Be(9500);

        // The invariant that makes the quarterly return add up
        (summary.BaseCents + summary.VatCents).Should().Be(summary.SalesCents);

        (await e.Appointments.CountByStatus(Day, Day, AppointmentStatus.NoShow)).Should().Be(1);
        (await e.Appointments.CountByStatus(Day, Day, AppointmentStatus.Completed)).Should().Be(5);
    }

    [Fact] // K-02
    public async Task A_day_with_corrections_reflects_the_edit_and_excludes_the_voided_sale()
    {
        await using var testDb = new TestDatabase();
        var e = await Build(testDb);
        int methodId = await FirstMethod(testDb);

        var ids = new List<int>();
        foreach (int amount in new[] { 1000, 2000, 3000, 4000 })
            ids.Add(await e.Sales.Create(NewSale(Day, methodId), [Line(amount)]));

        var second = await e.Sales.GetById(ids[1]);
        await e.Sales.Update(second!, [Line(2500)]);
        await e.Sales.Void(ids[3]);

        var summary = await e.Till.Summary(Day, Day);

        summary.SalesCents.Should().Be(1000 + 2500 + 3000);
        (summary.BaseCents + summary.VatCents).Should().Be(summary.SalesCents);
    }

    [Fact] // K-03
    public async Task A_guest_counts_towards_the_total_but_not_the_client_ranking()
    {
        await using var testDb = new TestDatabase();
        var e = await Build(testDb);
        int methodId = await FirstMethod(testDb);
        int clientId = await Adds(testDb, Make.Client("Joana", "600111222"));

        await e.Sales.Create(NewSale(Day, methodId, clientId), [Line(1000)]);
        await e.Sales.Create(NewSale(Day, methodId, clientId), [Line(2000)]);
        await e.Sales.Create(NewSale(Day, methodId, guest: "Passavolant"), [Line(3000)]);

        var summary = await e.Till.Summary(Day, Day);
        summary.SalesCents.Should().Be(6000, "the guest paid too");

        var ranking = await e.Reports.TopBySpend();
        ranking.Should().ContainSingle();
        ranking[0].client.Id.Should().Be(clientId);
        ranking[0].totalCents.Should().Be(3000);

        await using var db = testDb.Context();
        db.Clients.Should().ContainSingle("a guest leaves no client record");
    }

    [Fact] // K-04
    public async Task With_two_workers_the_work_percentages_add_up_to_a_hundred()
    {
        await using var testDb = new TestDatabase();
        var e = await Build(testDb);
        int methodId = await FirstMethod(testDb);
        int martaId = await Adds(testDb, Make.Worker("Marta"));
        int bertaId = await Adds(testDb, Make.Worker("Berta"));

        for (int i = 0; i < 5; i++)
            await e.Sales.Create(NewSale(Day, methodId, workerId: martaId), [Line(1000)]);
        for (int i = 0; i < 3; i++)
            await e.Sales.Create(NewSale(Day, methodId, workerId: bertaId), [Line(1000)]);

        var ranking = await e.Reports.WorkerRanking(Day, Day);

        ranking.Should().HaveCount(2);
        ranking.Sum(d => d.WorkPercentage ?? 0).Should().Be(100m);
        ranking.Single(d => d.WorkerId == martaId).SalesHandled.Should().Be(5);
    }

    [Fact] // K-05
    public async Task Custom_lines_are_kept_and_counted_as_other()
    {
        await using var testDb = new TestDatabase();
        var e = await Build(testDb);
        int methodId = await FirstMethod(testDb);
        int workerId = await Adds(testDb, Make.Worker("Marta"));
        int serviceId = await Adds(testDb, Make.Service("Tall", priceCents: 1500));

        await e.Sales.Create(NewSale(Day, methodId, workerId: workerId),
            [Line(1500, serviceId: serviceId)]);
        await e.Sales.Create(NewSale(Day, methodId, workerId: workerId),
            [Line(500, description: "Retoc de favor")]);

        var detail = await e.Reports.GetWorkerDetail(workerId, Day, Day);

        detail.OtherConceptsCents.Should().Be(500, "a reduced price is stored exactly as typed");
        detail.Services.Should().ContainSingle(s => s.name == "Tall");
        (await e.Till.Summary(Day, Day)).SalesCents.Should().Be(2000);
    }

    [Fact] // K-06
    public async Task The_weekly_total_is_the_sum_of_five_daily_balances()
    {
        await using var testDb = new TestDatabase();
        var e = await Build(testDb);
        int methodId = await FirstMethod(testDb);

        var monday = new DateOnly(2026, 9, 7);
        for (int d = 0; d < 5; d++)
        {
            var day = monday.AddDays(d);
            for (int i = 0; i < 5; i++)
                await e.Sales.Create(NewSale(day, methodId), [Line(1500)]);

            await e.Till.Create(new CashMovement
            {
                Date = day, Type = MovementType.In, AmountCents = 5000,
                PaymentMethodId = methodId, Concept = "Aportació"
            });
            await e.Till.Create(new CashMovement
            {
                Date = day, Type = MovementType.Out, AmountCents = 3000,
                PaymentMethodId = methodId, Concept = "Material"
            });
        }

        long sumDaily = 0;
        long baseDaily = 0;
        long vatDaily = 0;
        for (int d = 0; d < 5; d++)
        {
            var summaryDay = await e.Till.Summary(monday.AddDays(d), monday.AddDays(d));
            sumDaily += summaryDay.BalanceCents;
            baseDaily += summaryDay.BaseCents;
            vatDaily += summaryDay.VatCents;
        }

        var week = await e.Till.Summary(monday, monday.AddDays(4));

        week.BalanceCents.Should().Be(sumDaily);
        week.BaseCents.Should().Be(baseDaily);
        week.VatCents.Should().Be(vatDaily);
    }

    [Fact] // K-07
    public async Task The_quarter_matches_the_sum_of_the_stored_bases()
    {
        await using var testDb = new TestDatabase();
        var e = await Build(testDb);
        int methodId = await FirstMethod(testDb);

        var start = new DateOnly(2026, 7, 1);
        for (int d = 0; d < 60; d++)
            await e.Sales.Create(NewSale(start.AddDays(d), methodId), [Line(1333)]);

        var summary = await e.Till.Summary(start, start.AddDays(59));

        await using var db = testDb.Context();
        long baseSaved = db.SaleBreakdowns.Sum(d => (long)d.BaseCents);
        long vatSaved = db.SaleBreakdowns.Sum(d => (long)d.VatCents);

        // Summed from the frozen rows, never recomputed from the lines: with 60 tickets
        // the two methods drift apart by cents (decision 6.4).
        summary.BaseCents.Should().Be(baseSaved);
        summary.VatCents.Should().Be(vatSaved);
        (summary.BaseCents + summary.VatCents).Should().Be(summary.SalesCents);
    }

    [Fact] // K-08
    public async Task Closing_and_reopening_the_context_gives_the_same_figures()
    {
        await using var testDb = new TestDatabase();
        var e = await Build(testDb);
        int methodId = await FirstMethod(testDb);

        for (int i = 0; i < 4; i++)
            await e.Sales.Create(NewSale(Day, methodId), [Line(1750)]);

        var before = await e.Till.Summary(Day, Day);

        // A fresh service graph over the same file: nothing may live only in memory
        var other = await Build(testDb);
        var after = await other.Till.Summary(Day, Day);

        after.Should().BeEquivalentTo(before);
    }

    [Fact] // K-09
    public async Task A_day_without_activity_gives_zeros_and_does_not_crash()
    {
        await using var testDb = new TestDatabase();
        var e = await Build(testDb);

        var summary = await e.Till.Summary(Day, Day);

        summary.SalesCents.Should().Be(0);
        summary.BalanceCents.Should().Be(0);
        summary.BaseCents.Should().Be(0);
        summary.VatCents.Should().Be(0);
        summary.VatBreakdown.Should().BeEmpty();

        // No sales in the period means the share of work has no denominator: it must
        // read as "no data", never as 0 % (RF-17).
        int workerId = await Adds(testDb, Make.Worker("Marta"));
        var detail = await e.Reports.GetWorkerDetail(workerId, Day, Day);
        detail.WorkPercentage.Should().BeNull();
        detail.ProductsPercentage.Should().BeNull();
    }

    [Fact] // K-10
    public async Task A_day_with_two_vat_rates_splits_them_in_the_breakdown()
    {
        await using var testDb = new TestDatabase();
        var e = await Build(testDb);
        int methodId = await FirstMethod(testDb);
        int serviceId = await Adds(testDb, Make.Service("Tall", priceCents: 1500));
        int productId = await Adds(testDb, Make.Product("Xampú", priceCents: 900, vatBp: 1000));

        await e.Sales.Create(NewSale(Day, methodId),
        [
            Line(1500, vatBp: 2100, serviceId: serviceId),
            Line(900, vatBp: 1000, productId: productId, description: "Xampú")
        ]);

        var summary = await e.Till.Summary(Day, Day);

        summary.VatBreakdown.Should().HaveCount(2);
        summary.SalesCents.Should().Be(2400);
        (summary.BaseCents + summary.VatCents).Should().Be(summary.SalesCents);

        // Each rate is computed on its own group, then summed (decision 6.4 / CU-03b)
        summary.VatBreakdown.Sum(d => d.TotalCents).Should().Be(2400);
    }
}
