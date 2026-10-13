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

    private static HomeViewModel CreatesViewModel(TestDatabase testDb)
    {
        var factory = new TestFactory(testDb.Options);
        return new HomeViewModel(
            new AppointmentService(factory), new SaleService(factory, new SettingsService(factory)), new TillService(factory),
            new ClientService(factory), new AvailabilityService(factory), new CatalogService(factory),
            new WorkerService(factory), new SettingsService(factory),
            new TestSoundService(), new TestDialogService());
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

    [Fact] // M-05
    public async Task A_day_without_activity_is_all_zeros_and_no_exception()
    {
        await using var testDb = new TestDatabase();
        var vm = CreatesViewModel(testDb);

        await vm.Load();

        vm.TodayAppointments.Should().Be(0);
        vm.TodaySales.Should().Be(0);
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
    public async Task Home_shows_no_money_even_with_sales_today()
    {
        // Home is the screen left open at the counter, so it never shows the day's
        // figures, not even in owner mode: no public text on it may carry an amount.
        await using var testDb = new TestDatabase();
        int methodId = await AddsMethod(testDb);
        await using (var db = testDb.Context())
        {
            db.Sales.Add(Make.Sale(Today, methodId, SaleStatus.Active, Make.Line(1500, 2100)));
            await db.SaveChangesAsync();
        }

        var vm = CreatesViewModel(testDb);
        await vm.Load();

        var texts = typeof(HomeViewModel).GetProperties()
            .Where(p => p.PropertyType == typeof(string) && p.GetIndexParameters().Length == 0)
            .Select(p => (string?)p.GetValue(vm));
        texts.Should().NotContain(t => t != null && t.Contains(Money.Format(1500)));
        vm.TodaySales.Should().Be(1);
    }
}
