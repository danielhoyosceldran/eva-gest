using AwesomeAssertions;
using EvaGest.Data;
using EvaGest.Models;
using EvaGest.Services;
using EvaGest.Tests.Infra;
using EvaGest.ViewModels.Dialogs;
using EvaGest.ViewModels.Pages;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace EvaGest.Tests.Scenarios;

/// <summary>
/// Block H — attacks on the flows the user actually drives: the dialogs, the filters and
/// the schema itself. The seam between a ViewModel and its service is where the damage
/// happens, because the service can be perfectly correct about a value the dialog handed
/// it wrong.
/// </summary>
public class HostileFlowTests
{
    private static readonly DateOnly March = new(2026, 3, 12);

    private static async Task<(TestFactory factory, SettingsService settings, int methodId)> Build(TestDatabase testDb)
    {
        var factory = new TestFactory(testDb.Options);
        var settings = new SettingsService(factory);
        await new SeedService(factory, settings).Seed();

        await using var db = testDb.Context();
        int methodId = (await db.PaymentMethods.FirstAsync()).Id;
        return (factory, settings, methodId);
    }

    // ── The sale dialog must not move a sale it is only editing ──────────────

    [Fact]
    public async Task Reopening_an_old_sale_and_charging_it_again_leaves_its_date_alone()
    {
        // Driven through the dialog, not the service: the service always honoured the date
        // it was given, and the dialog was the layer stamping DateTime.Now over it.
        await using var testDb = new TestDatabase();
        var (factory, settings, methodId) = await Build(testDb);

        var sales = new SaleService(factory, settings);
        var clients = new ClientService(factory);
        var catalog = new CatalogService(factory);
        var workers = new WorkerService(factory);
        var dialogs = new TestDialogService();

        var march = Make.Sale(March, methodId, SaleStatus.Active, Make.Line(5_000));
        int id = await sales.Create(march, [Make.Line(5_000)]);

        var stored = await sales.GetById(id);
        var vm = await SaleDialogViewModel.Edit(
            sales, clients, catalog, workers, new TestSoundService(), settings, dialogs, stored!);

        vm.Notes = "corrected months later";
        vm.ChargeCommand.Execute(null);

        var reloaded = await sales.GetById(id);
        reloaded!.Date.Should().Be(March, "editing a sale is not re-charging it today");
        reloaded.Time.Should().Be(stored!.Time);
    }

    [Fact]
    public async Task A_sale_raised_from_yesterdays_appointment_belongs_to_yesterday()
    {
        // The shop closes without charging and the sale is taken the next morning. The
        // takings belong to the day the work happened, which is the day the appointment
        // was booked for.
        await using var testDb = new TestDatabase();
        var (factory, settings, _) = await Build(testDb);

        var yesterday = DateOnly.FromDateTime(DateTime.Today).AddDays(-1);

        Appointment appointment;
        await using (var db = testDb.Context())
        {
            appointment = new Appointment
            {
                Date = yesterday, Time = new TimeOnly(19, 30), DurationMin = 30, GuestName = "Pere"
            };
            db.Appointments.Add(appointment);
            await db.SaveChangesAsync();
        }

        var vm = await SaleDialogViewModel.FromAppointment(
            new SaleService(factory, settings), new ClientService(factory), new CatalogService(factory),
            new WorkerService(factory), new TestSoundService(), settings, new TestDialogService(), appointment);

        vm.AddCustomConceptCommand.Execute(null);
        vm.Lines[0].Description = "Tall";
        vm.Lines[0].PriceText = "15,00";
        vm.PaymentMethod = vm.ActiveMethods[0];
        vm.ChargeCommand.Execute(null);

        await using var check = testDb.Context();
        var sale = await check.Sales.FirstAsync();
        sale.Date.Should().Be(yesterday, "the sale belongs to the slot, not to the till session");
    }

    // ── The sale dialog must refuse to freeze a value it cannot trust ────────

    [Fact]
    public async Task A_half_typed_line_can_not_be_charged()
    {
        // Every field is text precisely so a malformed value sits in the box instead of
        // being coerced to zero. Charge has to catch it before it is frozen onto the sale.
        await using var testDb = new TestDatabase();
        var (factory, settings, _) = await Build(testDb);

        var vm = await SaleDialogViewModel.New(
            new SaleService(factory, settings), new ClientService(factory), new CatalogService(factory),
            new WorkerService(factory), new TestSoundService(), settings, new TestDialogService());

        vm.TextClient = "Anna";
        vm.PaymentMethod = vm.ActiveMethods[0];
        vm.AddCustomConceptCommand.Execute(null);
        vm.Lines[0].Description = "Tall";
        vm.Lines[0].PriceText = "quinze euros";      // never a number

        vm.ChargeCommand.Execute(null);

        vm.ErrorValidation.Should().NotBeNull("the user has to be told which box is wrong");

        await using var check = testDb.Context();
        (await check.Sales.CountAsync()).Should().Be(0, "nothing may be recorded from an invalid line");
    }

    [Fact]
    public async Task A_line_whose_total_overflows_int_cents_can_not_be_charged()
    {
        // Price and quantity each fit on their own; their product does not. Unchecked it
        // would wrap to a negative amount and be frozen onto the sale.
        await using var testDb = new TestDatabase();
        var (factory, settings, _) = await Build(testDb);

        var vm = await SaleDialogViewModel.New(
            new SaleService(factory, settings), new ClientService(factory), new CatalogService(factory),
            new WorkerService(factory), new TestSoundService(), settings, new TestDialogService());

        vm.TextClient = "Anna";
        vm.PaymentMethod = vm.ActiveMethods[0];
        vm.AddCustomConceptCommand.Execute(null);
        vm.Lines[0].Description = "Molts talls";
        vm.Lines[0].PriceText = "200000,00";
        vm.Lines[0].QuantityText = "500000";

        vm.ChargeCommand.Execute(null);

        vm.ErrorValidation.Should().NotBeNull();

        await using var check = testDb.Context();
        (await check.Sales.CountAsync()).Should().Be(0);
    }

    // ── Availability at the edges of the day ─────────────────────────────────

    [Fact]
    public async Task An_appointment_running_past_midnight_is_clamped_rather_than_wrapped()
    {
        // 23:30 + 90 min would read back as 01:00 through TimeOnly.AddMinutes, which made
        // a late appointment look like it ended before it started — so it overlapped
        // nothing and sat outside every opening-hours check.
        await using var testDb = new TestDatabase();
        var factory = new TestFactory(testDb.Options);

        await using (var db = testDb.Context())
        {
            db.Appointments.Add(new Appointment
            {
                Date = March, Time = new TimeOnly(23, 30), DurationMin = 90, GuestName = "Late"
            });
            await db.SaveChangesAsync();
        }

        var result = await new AvailabilityService(factory)
            .Check(March, new TimeOnly(23, 45), 30, workerId: null);

        result.ExistingAppointments.Should().Be(1, "23:45 falls inside a booking that started at 23:30");
    }

    [Fact]
    public async Task Two_appointments_that_only_touch_do_not_overlap()
    {
        // 10:00–10:30 and 10:30–11:00 are back to back, which is the normal way a busy
        // morning is booked. Warning about it would train the user to ignore warnings.
        await using var testDb = new TestDatabase();
        var factory = new TestFactory(testDb.Options);

        await using (var db = testDb.Context())
        {
            db.Appointments.Add(new Appointment
            {
                Date = March, Time = new TimeOnly(10, 0), DurationMin = 30, GuestName = "First"
            });
            await db.SaveChangesAsync();
        }

        var availability = new AvailabilityService(factory);

        (await availability.Check(March, new TimeOnly(10, 30), 30, null))
            .ExistingAppointments.Should().Be(0, "touching is not overlapping");

        (await availability.Check(March, new TimeOnly(10, 29), 30, null))
            .ExistingAppointments.Should().Be(1, "one minute of genuine overlap still counts");
    }

    [Fact]
    public async Task A_cancelled_appointment_frees_its_slot()
    {
        await using var testDb = new TestDatabase();
        var factory = new TestFactory(testDb.Options);

        await using (var db = testDb.Context())
        {
            db.Appointments.Add(new Appointment
            {
                Date = March, Time = new TimeOnly(10, 0), DurationMin = 30,
                GuestName = "Cancelled", Status = AppointmentStatus.Cancelled
            });
            db.Appointments.Add(new Appointment
            {
                Date = March, Time = new TimeOnly(10, 0), DurationMin = 30,
                GuestName = "No show", Status = AppointmentStatus.NoShow
            });
            await db.SaveChangesAsync();
        }

        var result = await new AvailabilityService(factory).Check(March, new TimeOnly(10, 0), 30, null);

        result.ExistingAppointments.Should().Be(0, "neither status occupies the chair");
    }

    // ── Filter storms ────────────────────────────────────────────────────────

    [Fact]
    public async Task Clearing_every_filter_at_once_leaves_the_list_consistent_with_the_filters()
    {
        // Each filter property kicks off its own reload, so clearing eight of them used to
        // start nine overlapping loads over one ObservableCollection. Whatever the
        // scheduling, the list that ends up on screen must match the filters that are set.
        await using var testDb = new TestDatabase();
        var (factory, settings, methodId) = await Build(testDb);

        await using (var db = testDb.Context())
        {
            for (int i = 0; i < 20; i++)
                db.Sales.Add(Make.Sale(March.AddDays(i % 5), methodId, SaleStatus.Active, Make.Line(1_000 + i)));
            await db.SaveChangesAsync();
        }

        var page = new SalesViewModel(
            new SaleService(factory, settings), new ClientService(factory), new CatalogService(factory),
            new WorkerService(factory), new TestSoundService(), settings,
            new ExportService(factory), new TestDialogService());

        await page.Load();
        page.Sales.Should().HaveCount(20);

        page.From = March;
        page.To = March;
        await page.Load();
        page.Sales.Should().HaveCount(4, "four of the twenty fall on the first day");

        await page.ClearFiltersCommand.ExecuteAsync(null);

        page.Sales.Should().HaveCount(20, "with no filters every sale is listed");
        page.Sales.Should().OnlyHaveUniqueItems("an interleaved reload must not duplicate rows");
    }

    // ── The schema the user's real database is upgraded with ─────────────────

    [Fact]
    public async Task The_migrations_build_the_schema_the_model_expects()
    {
        // Every other test creates its schema with EnsureCreated, which builds from the
        // model and never runs a migration. The real database is only ever built and
        // upgraded by MigrateAsync, so without this the migration path ships untested and
        // a drift between the two only surfaces on the one machine that has the data.
        string file = Path.Combine(Path.GetTempPath(), $"EvaGestMigrate_{Guid.NewGuid():N}.db");
        var options = new DbContextOptionsBuilder<ShopDbContext>()
            .UseSqlite($"Data Source={file}")
            .UseSnakeCaseNamingConvention()
            .Options;

        try
        {
            await using (var db = new ShopDbContext(options))
            {
                await db.Database.MigrateAsync();

                (await db.Database.GetPendingMigrationsAsync())
                    .Should().BeEmpty("MigrateAsync must leave nothing outstanding");

                // Reading every mapped set is the drift detector: each query names every
                // column the model declares, so a migration that is behind the model fails
                // here with "no such table/column" instead of on the user's machine.
                await ReadsEverySet(db);

                // And the schema is not merely present, it works: a sale with its frozen
                // breakdown round-trips through the migrated tables.
                var method = Make.Method();
                db.PaymentMethods.Add(method);
                await db.SaveChangesAsync();

                db.Sales.Add(Make.Sale(March, method.Id, SaleStatus.Active, Make.Line(1_500)));
                await db.SaveChangesAsync();
            }

            await using (var reopened = new ShopDbContext(options))
            {
                var sale = await reopened.Sales.Include(v => v.Breakdowns).FirstAsync();
                sale.TotalCents.Should().Be(1_500);
                sale.Breakdowns.Should().ContainSingle();
            }
        }
        finally
        {
            Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
            if (File.Exists(file)) File.Delete(file);
        }
    }

    /// <summary>Issues one SELECT per mapped entity type. Empty results are fine; the
    /// point is that every table and column the model declares must exist.</summary>
    private static async Task ReadsEverySet(ShopDbContext db)
    {
        await db.Clients.ToListAsync();
        await db.Workers.ToListAsync();
        await db.WorkerSchedules.ToListAsync();
        await db.Services.ToListAsync();
        await db.Products.ToListAsync();
        await db.PaymentMethods.ToListAsync();
        await db.ExpenseCategories.ToListAsync();
        await db.Appointments.ToListAsync();
        await db.Sales.ToListAsync();
        await db.SaleLines.ToListAsync();
        await db.SaleBreakdowns.ToListAsync();
        await db.CashMovements.ToListAsync();
        await db.ShopSchedule.ToListAsync();
        await db.ClosedDays.ToListAsync();
        await db.Settings.ToListAsync();
    }
}
