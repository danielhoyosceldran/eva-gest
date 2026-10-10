using AwesomeAssertions;
using EvaGest.Models;
using EvaGest.Services;
using EvaGest.Tests.Infra;
using EvaGest.ViewModels.Pages;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace EvaGest.Tests.Services;

/// <summary>
/// B-01..B-07 from bugs.csv: a page kept for the whole session that froze "today" or its
/// filter lists, history lost or miscounted, and client search and editing edge cases.
/// </summary>
public class ReviewFindingsTests
{
    private static readonly DateOnly Day = new(2026, 10, 9);

    // ── B-01: the Till's periods follow the date ─────────────────────────────

    [Fact]
    public async Task The_till_today_period_moves_to_the_new_day_when_the_app_stays_open_overnight()
    {
        await using var testDb = new TestDatabase();
        var factory = new TestFactory(testDb.Options);
        var settings = new SettingsService(factory);
        await new SeedService(factory, settings).Seed();

        var clock = Day.ToDateTime(new TimeOnly(19, 0));
        var vm = new TillViewModel(new TillService(factory), new CatalogService(factory),
            new WorkerService(factory), settings, new TestDialogService(), () => clock);

        await vm.Load();
        (vm.From, vm.To).Should().Be((Day, Day));

        clock = Day.AddDays(1).ToDateTime(new TimeOnly(9, 0));
        await vm.Load();

        (vm.From, vm.To).Should().Be((Day.AddDays(1), Day.AddDays(1)));
    }

    [Fact]
    public async Task A_custom_till_range_is_left_alone_on_reload()
    {
        await using var testDb = new TestDatabase();
        var factory = new TestFactory(testDb.Options);
        var settings = new SettingsService(factory);
        await new SeedService(factory, settings).Seed();

        var clock = Day.ToDateTime(new TimeOnly(19, 0));
        var vm = new TillViewModel(new TillService(factory), new CatalogService(factory),
            new WorkerService(factory), settings, new TestDialogService(), () => clock)
        {
            Period = TillPeriod.Custom
        };
        vm.From = new DateOnly(2026, 1, 1);
        vm.To = new DateOnly(2026, 1, 31);

        clock = Day.AddDays(1).ToDateTime(new TimeOnly(9, 0));
        await vm.Load();

        (vm.From, vm.To).Should().Be((new DateOnly(2026, 1, 1), new DateOnly(2026, 1, 31)));
    }

    // ── B-02: the Sales page's filter lists are refilled ─────────────────────

    [Fact]
    public async Task A_client_created_after_the_sales_page_loaded_can_be_filtered_on()
    {
        await using var testDb = new TestDatabase();
        var factory = new TestFactory(testDb.Options);
        var settings = new SettingsService(factory);
        await new SeedService(factory, settings).Seed();
        var clients = new ClientService(factory);

        var vm = new SalesViewModel(new SaleService(factory, settings), clients, new CatalogService(factory),
            new WorkerService(factory), new TestSoundService(), settings,
            new ExportService(factory), new TestDialogService());

        int joanId = await clients.Create(Make.Client("Joan"));
        await vm.Load();
        vm.Client = vm.FilterClients.Single(c => c.Id == joanId);

        int annaId = await clients.Create(Make.Client("Anna"));
        int asleepId = await clients.Create(Make.Client("Pere"));
        await clients.Sleep(asleepId);
        await vm.Load();

        vm.FilterClients.Select(c => c.Id).Should().Contain([annaId, asleepId]);
        // The filter the user picked survives the refill, on the new instance.
        vm.Client!.Id.Should().Be(joanId);
        vm.FilterClients.Should().Contain(vm.Client);
    }

    // ── B-03: a worker paid through the till is never deleted ────────────────

    [Fact]
    public async Task A_worker_named_only_on_a_cash_out_is_deactivated_not_deleted()
    {
        await using var testDb = new TestDatabase();
        int workerId, movementId;
        await using (var db = testDb.Context())
        {
            var worker = Make.Worker();
            var method = Make.Method();
            db.Workers.Add(worker);
            db.PaymentMethods.Add(method);
            await db.SaveChangesAsync();

            var movement = new CashMovement
            {
                Date = Day, Type = MovementType.Out, AmountCents = 120000,
                PaymentMethodId = method.Id, WorkerId = worker.Id, Concept = "Nòmina"
            };
            db.CashMovements.Add(movement);
            await db.SaveChangesAsync();
            (workerId, movementId) = (worker.Id, movement.Id);
        }

        var result = await new WorkerService(new TestFactory(testDb.Options)).Delete(workerId);

        result.Should().Be(DeleteResult.Deactivated);
        await using var check = testDb.Context();
        (await check.CashMovements.SingleAsync(m => m.Id == movementId)).WorkerId.Should().Be(workerId);
        (await check.Workers.SingleAsync(w => w.Id == workerId)).Active.Should().BeFalse();
    }

    // ── B-04: services are counted in units ──────────────────────────────────

    [Fact]
    public async Task A_service_line_with_quantity_two_counts_as_two_services_for_the_worker()
    {
        await using var testDb = new TestDatabase();
        int workerId;
        await using (var db = testDb.Context())
        {
            var worker = Make.Worker();
            var method = Make.Method();
            var service = Make.Service("Tall");
            db.AddRange(worker, method, service);
            await db.SaveChangesAsync();

            var line = Make.Line(3000, quantity: 2);
            line.ServiceId = service.Id;
            line.Description = "Tall";
            var sale = Make.Sale(Day, method.Id, SaleStatus.Active, line);
            sale.WorkerId = worker.Id;
            db.Sales.Add(sale);
            await db.SaveChangesAsync();
            workerId = worker.Id;
        }

        var detail = await new ReportsService(new TestFactory(testDb.Options))
            .GetWorkerDetail(workerId, Day, Day);

        detail.Services.Should().Equal(("Tall", 2));
    }

    // ── B-05: leap-day birthdays ─────────────────────────────────────────────

    [Theory]
    [InlineData(2027, 2, 28, true)]   // no 29 February this year: greeted the day before
    [InlineData(2028, 2, 28, false)]  // leap year: wait for the real day
    [InlineData(2028, 2, 29, true)]
    [InlineData(2027, 3, 1, false)]
    public void Someone_born_on_29_february_has_a_birthday_every_year(int year, int month, int day, bool expected)
        => ClientService.IsBirthday(new DateOnly(2000, 2, 29), new DateOnly(year, month, day))
            .Should().Be(expected);

    [Fact]
    public void An_ordinary_birthday_is_matched_on_day_and_month_only()
    {
        ClientService.IsBirthday(new DateOnly(1990, 10, 9), Day).Should().BeTrue();
        ClientService.IsBirthday(new DateOnly(1990, 10, 8), Day).Should().BeFalse();
    }

    // ── B-06: editing a client keeps them asleep ─────────────────────────────

    [Fact]
    public async Task Editing_an_asleep_client_does_not_wake_them()
    {
        await using var testDb = new TestDatabase();
        var clients = new ClientService(new TestFactory(testDb.Options));
        int id = await clients.Create(Make.Client("Joan"));
        await clients.Sleep(id);

        // What the edit dialog builds: the typed fields only, Asleep left at its default.
        await clients.Update(new Client { Id = id, Name = "Joan García", Mobile = "612345678" });

        var updated = await clients.GetById(id);
        updated!.Asleep.Should().BeTrue();
        updated.Name.Should().Be("Joan García");
        updated.ClientKey.Should().Be(ClientService.ComputeClientKey("Joan García"));
    }

    // ── B-07: search text is literal ─────────────────────────────────────────

    [Theory]
    [InlineData("_")]
    [InlineData("%")]
    public async Task A_wildcard_character_in_the_search_matches_only_itself(string typed)
    {
        await using var testDb = new TestDatabase();
        var clients = new ClientService(new TestFactory(testDb.Options));
        await clients.Create(Make.Client("Joan"));
        await clients.Create(Make.Client($"Anna{typed}B"));

        var found = await clients.Search(typed);

        found.Select(c => c.Name).Should().Equal($"Anna{typed}B");
    }

    [Fact]
    public async Task Ordinary_search_still_matches_part_of_a_name()
    {
        await using var testDb = new TestDatabase();
        var clients = new ClientService(new TestFactory(testDb.Options));
        await clients.Create(Make.Client("Joan García"));

        (await clients.Search("garc")).Should().ContainSingle();
    }
}
