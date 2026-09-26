using AwesomeAssertions;
using EvaGest.Models;
using EvaGest.Services;
using EvaGest.Tests.Infra;
using EvaGest.ViewModels.Pages;
using Xunit;

namespace EvaGest.Tests.Services;

/// <summary>Block M: pantalla d'Start — the counters the user checks every day.</summary>
public class StartViewModelTests
{
    private static readonly DateOnly Today = DateOnly.FromDateTime(DateTime.Today);

    private static HomeViewModel CreatesViewModel(TestDatabase testDb, IOwnerAccessService? owner = null)
    {
        var factory = new TestFactory(testDb.Options);
        return new HomeViewModel(
            new AppointmentService(factory), new SaleService(factory, new SettingsService(factory)), new TillService(factory),
            new ClientService(factory), new AvailabilityService(factory), new CatalogService(factory),
            new WorkerService(factory), new SettingsService(factory),
            new TestSoundService(), new TestDialogService(), owner ?? TestOwner.New());
    }

    private static async Task<int> AddsMethod(TestDatabase testDb)
    {
        await using var db = testDb.Context();
        var m = Make.Method();
        db.PaymentMethods.Add(m);
        await db.SaveChangesAsync();
        return m.Id;
    }

    [Fact] // M-01
    public async Task The_appointment_counter_for_today_counts_every_status()
    {
        await using var testDb = new TestDatabase();
        await using (var db = testDb.Context())
        {
            db.Appointments.Add(new Appointment { Date = Today, Time = new TimeOnly(9, 0), DurationMin = 30, GuestName = "A", Status = AppointmentStatus.Pending });
            db.Appointments.Add(new Appointment { Date = Today, Time = new TimeOnly(10, 0), DurationMin = 30, GuestName = "B", Status = AppointmentStatus.Cancelled });
            await db.SaveChangesAsync();
        }

        var vm = CreatesViewModel(testDb);
        await vm.Load();

        vm.TodayAppointments.Should().Be(2);
    }

    [Fact] // M-02
    public async Task The_sales_counter_for_today_counts_only_active_sales()
    {
        await using var testDb = new TestDatabase();
        int methodId = await AddsMethod(testDb);
        await using (var db = testDb.Context())
        {
            db.Sales.Add(Make.Sale(Today, methodId, SaleStatus.Active, Make.Line(1000, 2100)));
            db.Sales.Add(Make.Sale(Today, methodId, SaleStatus.Voided, Make.Line(9999, 2100)));
            await db.SaveChangesAsync();
        }

        var vm = CreatesViewModel(testDb);
        await vm.Load();

        vm.TodaySales.Should().Be(1);
    }

    [Fact] // M-03 / M-04
    public async Task Charged_today_and_the_balance_match_TillService()
    {
        await using var testDb = new TestDatabase();
        int methodId = await AddsMethod(testDb);
        await using (var db = testDb.Context())
        {
            db.Sales.Add(Make.Sale(Today, methodId, SaleStatus.Active, Make.Line(1500, 2100)));
            db.CashMovements.Add(new CashMovement
            { Date = Today, Type = MovementType.In, AmountCents = 500, PaymentMethodId = methodId, Concept = "E" });
            await db.SaveChangesAsync();
        }

        var till = new TillService(new TestFactory(testDb.Options));
        var summary = await till.Summary(Today, Today);

        var vm = CreatesViewModel(testDb);
        await vm.Load();

        vm.ChargedTodayText.Should().Be(Money.Format((int)summary.SalesCents));
        vm.BalanceText.Should().Be(Money.Format((int)summary.BalanceCents));
    }

    [Fact] // M-05
    public async Task A_day_without_activity_is_all_zeros_and_no_exception()
    {
        await using var testDb = new TestDatabase();
        var vm = CreatesViewModel(testDb);

        await vm.Load();

        vm.TodayAppointments.Should().Be(0);
        vm.TodaySales.Should().Be(0);
        vm.ChargedTodayText.Should().Be(Money.Format(0));
    }

    [Fact] // M-06
    public async Task The_birthday_notice_names_a_client_whose_birthday_is_today()
    {
        await using var testDb = new TestDatabase();
        await using (var db = testDb.Context())
        {
            db.Clients.Add(new Client
            {
                Name = "Anna", Mobile = "600000000", ClientKey = "anna600000000",
                BirthDate = new DateOnly(1990, Today.Month, Today.Day)
            });
            await db.SaveChangesAsync();
        }

        var vm = CreatesViewModel(testDb);
        await vm.Load();

        vm.BirthdayNoticeText.Should().Contain("Anna");
    }

    [Fact] // M-07
    public async Task With_no_birthday_no_notice_is_shown()
    {
        await using var testDb = new TestDatabase();
        await using (var db = testDb.Context())
        {
            var dateNotToday = Today.AddDays(10);
            db.Clients.Add(new Client
            {
                Name = "Anna", Mobile = "600000000", ClientKey = "anna600000000",
                BirthDate = new DateOnly(1990, dateNotToday.Month, dateNotToday.Day)
            });
            await db.SaveChangesAsync();
        }

        var vm = CreatesViewModel(testDb);
        await vm.Load();

        vm.BirthdayNoticeText.Should().BeNull();
    }

    [Fact] // M-08
    public async Task A_sleeping_client_whose_birthday_is_today_is_not_in_the_notice()
    {
        await using var testDb = new TestDatabase();
        await using (var db = testDb.Context())
        {
            db.Clients.Add(new Client
            {
                Name = "Anna", Mobile = "600000000", ClientKey = "anna600000000",
                BirthDate = new DateOnly(1990, Today.Month, Today.Day), Asleep = true
            });
            await db.SaveChangesAsync();
        }

        var vm = CreatesViewModel(testDb);
        await vm.Load();

        vm.BirthdayNoticeText.Should().BeNull();
    }

    [Fact]
    public async Task The_days_money_shows_only_while_owner_mode_is_open()
    {
        await using var testDb = new TestDatabase();
        var owner = await TestOwner.Unlocked();
        var vm = CreatesViewModel(testDb, owner);
        var raised = new List<string?>();
        vm.PropertyChanged += (_, e) => raised.Add(e.PropertyName);

        vm.ShowMoney.Should().BeTrue();

        owner.Lock();
        vm.OwnerModeChanged();

        vm.ShowMoney.Should().BeFalse();
        raised.Should().Contain(nameof(HomeViewModel.ShowMoney));
    }
}
