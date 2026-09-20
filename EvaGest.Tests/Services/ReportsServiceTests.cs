using AwesomeAssertions;
using EvaGest.Models;
using EvaGest.Services;
using EvaGest.Tests.Infra;
using Xunit;

namespace EvaGest.Tests.Services;

/// <summary>Block I: indicators i reports.</summary>
public class ReportsServiceTests
{
    private static ReportsService CreatesService(TestDatabase testDb) => new(new TestFactory(testDb.Options));

    private static async Task<int> AddsMethod(TestDatabase testDb)
    {
        await using var db = testDb.Context();
        var m = Make.Method();
        db.PaymentMethods.Add(m);
        await db.SaveChangesAsync();
        return m.Id;
    }

    private static async Task<int> AddsClient(TestDatabase testDb, string name = "Joan", string mobile = "612345678")
    {
        await using var db = testDb.Context();
        var c = new Client { Name = name, Mobile = mobile, ClientKey = ClientService.ComputeClientKey(name) };
        db.Clients.Add(c);
        await db.SaveChangesAsync();
        return c.Id;
    }

    [Fact] // I-01
    public async Task GetClientIndicators_without_sales_returns_nulls()
    {
        await using var testDb = new TestDatabase();
        int id = await AddsClient(testDb);
        var reports = CreatesService(testDb);

        var indicators = await reports.GetClientIndicators(id);

        indicators.Visits.Should().Be(0);
        indicators.AveragePerVisitEuros.Should().BeNull();
    }

    [Fact] // I-02
    public async Task GetClientIndicators_with_one_visit_has_a_null_frequency()
    {
        await using var testDb = new TestDatabase();
        int methodId = await AddsMethod(testDb);
        int clientId = await AddsClient(testDb);
        await using (var db = testDb.Context())
        {
            var v = Make.Sale(new DateOnly(2026, 1, 1), methodId, SaleStatus.Active, Make.Line(1000, 2100));
            v.ClientId = clientId; v.GuestName = null;
            db.Sales.Add(v);
            await db.SaveChangesAsync();
        }

        var indicators = await CreatesService(testDb).GetClientIndicators(clientId);
        indicators.FrequencyDays.Should().BeNull();
    }

    [Fact] // I-03
    public async Task GetClientIndicators_counts_only_active_sales()
    {
        await using var testDb = new TestDatabase();
        int methodId = await AddsMethod(testDb);
        int clientId = await AddsClient(testDb);
        await using (var db = testDb.Context())
        {
            var active = Make.Sale(new DateOnly(2026, 1, 1), methodId, SaleStatus.Active, Make.Line(1000, 2100));
            active.ClientId = clientId; active.GuestName = null;
            db.Sales.Add(active);

            var voided = Make.Sale(new DateOnly(2026, 1, 2), methodId, SaleStatus.Voided, Make.Line(9999, 2100));
            voided.ClientId = clientId; voided.GuestName = null;
            db.Sales.Add(voided);
            await db.SaveChangesAsync();
        }

        var indicators = await CreatesService(testDb).GetClientIndicators(clientId);
        indicators.Visits.Should().Be(1);
    }

    [Fact] // I-05
    public async Task WorkerDetail_in_a_period_without_sales_has_a_null_work_percentage()
    {
        await using var testDb = new TestDatabase();
        int workerId;
        await using (var db = testDb.Context())
        {
            var t = Make.Worker();
            db.Workers.Add(t);
            await db.SaveChangesAsync();
            workerId = t.Id;
        }

        var detail = await CreatesService(testDb).GetWorkerDetail(workerId, new DateOnly(2026, 1, 1), new DateOnly(2026, 1, 31));
        detail.WorkPercentage.Should().BeNull();
    }

    [Fact] // I-06
    public async Task WorkerDetail_without_sales_has_a_null_products_percentage()
    {
        await using var testDb = new TestDatabase();
        int workerId;
        await using (var db = testDb.Context())
        {
            var t = Make.Worker();
            db.Workers.Add(t);
            await db.SaveChangesAsync();
            workerId = t.Id;
        }

        var detail = await CreatesService(testDb).GetWorkerDetail(workerId, new DateOnly(2026, 1, 1), new DateOnly(2026, 1, 31));
        detail.ProductsPercentage.Should().BeNull();
    }

    [Fact] // I-07
    public async Task The_work_percentage_of_two_workers_adds_up_to_100()
    {
        await using var testDb = new TestDatabase();
        int methodId = await AddsMethod(testDb);
        int t1, t2;
        await using (var db = testDb.Context())
        {
            var a = Make.Worker("A");
            var b = Make.Worker("B");
            db.Workers.Add(a);
            db.Workers.Add(b);
            await db.SaveChangesAsync();
            t1 = a.Id; t2 = b.Id;
        }
        var date = new DateOnly(2026, 1, 1);
        await using (var db = testDb.Context())
        {
            var v1 = Make.Sale(date, methodId, SaleStatus.Active, Make.Line(6000, 2100));
            v1.WorkerId = t1;
            var v2 = Make.Sale(date, methodId, SaleStatus.Active, Make.Line(4000, 2100));
            v2.WorkerId = t2;
            db.Sales.Add(v1);
            db.Sales.Add(v2);
            await db.SaveChangesAsync();
        }

        var reports = CreatesService(testDb);
        var d1 = await reports.GetWorkerDetail(t1, date, date);
        var d2 = await reports.GetWorkerDetail(t2, date, date);

        (d1.WorkPercentage!.Value + d2.WorkPercentage!.Value).Should().Be(100m);
    }

    [Fact] // I-09 / I-10
    public async Task The_indicators_count_cancelled_and_no_shows_separately()
    {
        await using var testDb = new TestDatabase();
        int clientId = await AddsClient(testDb);
        await using (var db = testDb.Context())
        {
            db.Appointments.Add(new Appointment { Date = new DateOnly(2026, 1, 1), Time = new TimeOnly(10, 0), DurationMin = 30, ClientId = clientId, Status = AppointmentStatus.Cancelled });
            db.Appointments.Add(new Appointment { Date = new DateOnly(2026, 1, 2), Time = new TimeOnly(10, 0), DurationMin = 30, ClientId = clientId, Status = AppointmentStatus.NoShow });
            db.Appointments.Add(new Appointment { Date = new DateOnly(2026, 1, 3), Time = new TimeOnly(10, 0), DurationMin = 30, ClientId = clientId, Status = AppointmentStatus.NoShow });
            await db.SaveChangesAsync();
        }

        var indicators = await CreatesService(testDb).GetClientIndicators(clientId);
        indicators.Cancelled.Should().Be(1);
        indicators.NoShows.Should().Be(2);
    }

    [Fact] // I-13
    public async Task Guest_clients_do_not_appear_in_the_ranking_by_visits()
    {
        await using var testDb = new TestDatabase();
        int methodId = await AddsMethod(testDb);
        int clientId = await AddsClient(testDb);
        await using (var db = testDb.Context())
        {
            var registered = Make.Sale(new DateOnly(2026, 1, 1), methodId, SaleStatus.Active, Make.Line(1000, 2100));
            registered.ClientId = clientId; registered.GuestName = null;
            db.Sales.Add(registered);
            db.Sales.Add(Make.Sale(new DateOnly(2026, 1, 1), methodId, SaleStatus.Active, Make.Line(9999, 2100))); // guest
            await db.SaveChangesAsync();
        }

        var top = await CreatesService(testDb).TopByVisits();
        top.Should().ContainSingle(t => t.client.Id == clientId);
    }

    [Fact] // I-14
    public async Task Guest_clients_count_towards_the_global_till_totals()
    {
        await using var testDb = new TestDatabase();
        int methodId = await AddsMethod(testDb);
        await using (var db = testDb.Context())
        {
            db.Sales.Add(Make.Sale(new DateOnly(2026, 1, 1), methodId, SaleStatus.Active, Make.Line(1000, 2100)));
            await db.SaveChangesAsync();
        }

        var till = new TillService(new TestFactory(testDb.Options));
        var summary = await till.Summary(new DateOnly(2026, 1, 1), new DateOnly(2026, 1, 1));
        summary.SalesCents.Should().Be(1000);
    }

    [Fact] // I-18
    public async Task ClientOfTheMonth_is_the_one_who_spent_most()
    {
        await using var testDb = new TestDatabase();
        int methodId = await AddsMethod(testDb);
        int clientSmall = await AddsClient(testDb, "Petit", "600000001");
        int clientLarge = await AddsClient(testDb, "Gran", "600000002");
        var today = DateOnly.FromDateTime(DateTime.Today);
        var firstOfMonth = new DateOnly(today.Year, today.Month, 1);

        await using (var db = testDb.Context())
        {
            var sale1 = Make.Sale(firstOfMonth, methodId, SaleStatus.Active, Make.Line(1000, 2100));
            sale1.ClientId = clientSmall; sale1.GuestName = null;
            var sale2 = Make.Sale(firstOfMonth, methodId, SaleStatus.Active, Make.Line(9000, 2100));
            sale2.ClientId = clientLarge; sale2.GuestName = null;
            db.Sales.Add(sale1);
            db.Sales.Add(sale2);
            await db.SaveChangesAsync();
        }

        var result = await CreatesService(testDb).ClientOfTheMonth();
        result!.Value.client.Id.Should().Be(clientLarge);
    }

    // ── The worker ranking ───────────────────────────────────────────────────
    // It used to call GetWorkerDetail once per worker, so the two could not disagree by
    // construction. Now it loads the period once and shares a pure Detail(); these pin
    // that the answers are still identical.

    private static async Task<(int busy, int quiet, DateOnly date)> TwoWorkersOneBusy(TestDatabase testDb)
    {
        int methodId = await AddsMethod(testDb);
        int busy, quiet;
        await using (var db = testDb.Context())
        {
            var a = Make.Worker("Marta");
            var b = Make.Worker("Anna");
            db.Workers.AddRange(a, b);
            await db.SaveChangesAsync();
            busy = a.Id; quiet = b.Id;
        }

        var date = new DateOnly(2026, 1, 1);
        await using (var db = testDb.Context())
        {
            var sale = Make.Sale(date, methodId, SaleStatus.Active, Make.Line(6000, 2100));
            sale.WorkerId = busy;

            // No worker at all: still part of the period total the percentages divide by.
            var unassigned = Make.Sale(date, methodId, SaleStatus.Active, Make.Line(4000, 2100));

            db.Sales.AddRange(sale, unassigned);
            await db.SaveChangesAsync();
        }

        return (busy, quiet, date);
    }

    [Fact]
    public async Task The_ranking_gives_each_worker_the_same_figures_as_asking_for_them_one_by_one()
    {
        await using var testDb = new TestDatabase();
        var (busy, quiet, date) = await TwoWorkersOneBusy(testDb);
        var reports = CreatesService(testDb);

        var ranking = await reports.WorkerRanking(date, date);

        foreach (int id in new[] { busy, quiet })
        {
            var one = await reports.GetWorkerDetail(id, date, date);
            var fromRanking = ranking.Single(r => r.WorkerId == id);

            fromRanking.Should().BeEquivalentTo(one);
        }
    }

    [Fact]
    public async Task A_worker_with_no_sales_still_appears_in_the_ranking_on_zero()
    {
        await using var testDb = new TestDatabase();
        var (busy, quiet, date) = await TwoWorkersOneBusy(testDb);

        var ranking = await CreatesService(testDb).WorkerRanking(date, date);

        ranking.Should().HaveCount(2);
        ranking[0].WorkerId.Should().Be(busy, "ranked by takings, highest first");
        ranking[1].WorkerId.Should().Be(quiet);
        ranking[1].IncomeCents.Should().Be(0);
        ranking[1].SalesHandled.Should().Be(0);
    }

    [Fact]
    public async Task The_percentages_divide_by_the_whole_period_including_unassigned_sales()
    {
        await using var testDb = new TestDatabase();
        var (busy, _, date) = await TwoWorkersOneBusy(testDb);

        var ranking = await CreatesService(testDb).WorkerRanking(date, date);

        // 6000 of a 10000 period, not 6000 of the 6000 that has a worker on it.
        ranking.Single(r => r.WorkerId == busy).WorkPercentage.Should().Be(60m);
    }
}
