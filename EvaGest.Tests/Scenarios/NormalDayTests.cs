using AwesomeAssertions;
using EvaGest.Models;
using EvaGest.Resources;
using EvaGest.Services;
using EvaGest.Tests.Infra;
using EvaGest.ViewModels.Dialogs;
using Xunit;

namespace EvaGest.Tests.Scenarios;

/// <summary>
/// Block W, first part — an ordinary working day, driven only through the pages and
/// dialogs the user clicks, with a <see cref="ScriptedUser"/> doing the typing. The
/// question each story answers is whether every screen tells the same story at the end:
/// Home, Sales, Till and Reports read the same sales through different services, and a
/// figure that disagrees between two of them is exactly what the owner would notice first.
/// </summary>
public class NormalDayTests
{
    [Fact] // W-01
    public async Task A_morning_of_bookings_charges_and_a_no_show_adds_up_on_every_screen()
    {
        await using var shop = await Shop.Open();
        var tall = await shop.AddService("Tall", 1500);
        var barba = await shop.AddService("Barba", 800, durationMin: 20);
        await shop.AddProduct("Cera mat", 1200);
        var marta = await shop.AddWorker("Marta");
        var joan = await shop.AddClient("Joan Garcia", "612000111");
        var anna = await shop.AddClient("Anna Vidal", "612000222");

        // Joan's booking is made at the counter through the dialog
        await shop.Home.Load();
        shop.User.Then<AppointmentDialogViewModel>(async vm =>
        {
            await vm.Initialization;
            vm.ClientPicker.PickClient("garcia");
            vm.ServicePicker.Text = "tall";
            vm.ServicePicker.PickHighlightedCommand.Execute(null);
            vm.Worker = vm.ActiveWorkers.Single(w => w.Id == marta.Id);
            vm.TimeText = "10:00";
            vm.DurationText.Should().Be("30", "the service's duration is proposed");
            await vm.SaveCommand.ExecuteAsync(null);
        });
        await shop.Home.NewAppointmentCommand.ExecuteAsync(null);

        // Two more were booked by phone earlier
        await shop.Book(Shop.Today, new TimeOnly(10, 30), guest: "Pere", service: barba);
        await shop.Book(Shop.Today, new TimeOnly(12, 0), client: anna, service: tall);
        await shop.Home.Load();
        shop.Home.TodayAppointments.Should().Be(3);

        // Joan: the booked haircut plus a wax, by card
        var joanAppointment = shop.Home.DayAppointments.Single(a => a.ClientId == joan.Id);
        shop.User.Then<SaleDialogViewModel>(async vm =>
        {
            vm.Lines.Should().ContainSingle(l => l.Description == "Tall");
            vm.SearchAndAdd("cera");
            vm.PayWith("Targeta");
            vm.TotalText.Should().Be(Money.Format(2700));
            await vm.ChargeCommand.ExecuteAsync(null);
        });
        await shop.Home.MarkCompletedCommand.ExecuteAsync(joanAppointment);

        // Pere, a guest, pays cash
        shop.User.Then<SaleDialogViewModel>(async vm =>
        {
            vm.TextClient.Should().Be("Pere");
            vm.PayWith("Efectiu");
            await vm.ChargeCommand.ExecuteAsync(null);
        });
        await shop.Home.MarkCompletedCommand.ExecuteAsync(
            shop.Home.DayAppointments.Single(a => a.GuestName == "Pere"));

        // Anna never comes
        await shop.Home.MarkNoShowCommand.ExecuteAsync(shop.Home.DayAppointments.Single(a => a.ClientId == anna.Id));

        // Some supplies paid out of the till
        shop.User.Then<MovementDialogViewModel>(async vm =>
        {
            vm.Concept = "Fulles d'afaitar";
            vm.PriceText = "20,00";
            await vm.SaveCommand.ExecuteAsync(null);
        });
        await shop.Home.NewCashOutCommand.ExecuteAsync(null);

        shop.User.ScriptFinished.Should().BeTrue();

        // Home
        shop.Home.TodaySales.Should().Be(2);
        shop.Home.DayAppointments.Select(a => a.Status).Should().BeEquivalentTo(
            [AppointmentStatus.Completed, AppointmentStatus.Completed, AppointmentStatus.NoShow]);

        // Till
        await shop.TillPage.Load();
        var day = shop.TillPage.Summary!;
        day.SalesCents.Should().Be(1500 + 1200 + 800);
        day.CashOutCents.Should().Be(2000);
        day.BalanceCents.Should().Be(3500 - 2000);
        (day.BaseCents + day.VatCents).Should().Be(day.SalesCents);
        shop.TillPage.VatRates.Should().ContainSingle(r => r.RateText == Percentages.Format(2100));

        // Sales: the same total as the till, two lines, both active
        shop.SalesPage.From = Shop.Today;
        shop.SalesPage.To = Shop.Today;
        await shop.SalesPage.Load();
        shop.SalesPage.ActiveCount.Should().Be(2);
        shop.SalesPage.TotalTotalText.Should().Be(shop.TillPage.SalesText);
        shop.SalesPage.TotalVatText.Should().Be(shop.TillPage.VatText);
        shop.SalesPage.Sales.Single(r => r.Sale.ClientId == joan.Id).ConceptText.Should().Be("Tall + Cera mat");

        // Reports: Marta handled one of the two sales
        shop.ReportsPage.From = Shop.Today;
        shop.ReportsPage.To = Shop.Today;
        await shop.ReportsPage.Load();
        shop.ReportsPage.WorkerRanking.Should().ContainSingle(r => r.WorkerId == marta.Id && r.SalesHandled == 1);
        shop.ReportsPage.ClientOfTheMonthText.Should().Contain("Joan Garcia");
        (await shop.Appointments.CountByStatus(Shop.Today, Shop.Today, AppointmentStatus.NoShow)).Should().Be(1);
    }

    [Fact] // W-02
    public async Task Two_services_and_three_products_at_two_vat_rates_agree_between_dialog_till_and_sales()
    {
        await using var shop = await Shop.Open();
        await shop.AddService("Tall", 1500);
        await shop.AddProduct("Xampú", 990, vatBp: 1000);
        var client = await shop.AddClient("Marc Soler");

        int dialogTotal = 0, dialogVat = 0;
        shop.User.Answers(true)    // "Tall again?" — yes, father and son
            .Then<SaleDialogViewModel>(async vm =>
            {
                vm.ClientPicker.PickClient("marc");
                vm.SearchAndAdd("tall");
                vm.SearchAndAdd("tall");
                vm.SearchAndAdd("xamp");
                vm.Lines.Single(l => l.Description == "Xampú").IncreaseCommand.Execute(null);
                vm.Lines.Single(l => l.Description == "Xampú").IncreaseCommand.Execute(null);

                vm.Lines.Should().HaveCount(2, "a second identical item raises the quantity, not the lines");
                vm.Lines.Single(l => l.Description == "Tall").Quantity.Should().Be(2);
                vm.Breakdown.Should().HaveCount(2);

                vm.PayWith("Bizum");
                dialogTotal = vm.TotalCents;
                dialogVat = vm.VatCents;
                await vm.ChargeCommand.ExecuteAsync(null);
            });
        await shop.Home.NewSaleCommand.ExecuteAsync(null);

        dialogTotal.Should().Be(2 * 1500 + 3 * 990);
        shop.User.Asked.Should().ContainSingle(t => t == Texts.DuplicateServiceTitle);

        await shop.TillPage.Load();
        shop.TillPage.Summary!.SalesCents.Should().Be(dialogTotal, "what the dialog showed is what was charged");
        shop.TillPage.Summary.VatCents.Should().Be(dialogVat);
        shop.TillPage.VatRates.Should().HaveCount(2);

        await shop.SalesPage.Load();
        shop.SalesPage.TotalTotalText.Should().Be(Money.Format(dialogTotal));
        shop.SalesPage.TotalBaseText.Should().Be(shop.TillPage.BaseText);
    }

    [Fact] // W-03
    public async Task A_week_of_ordinary_days_matches_the_sum_of_its_days_and_the_month_report()
    {
        await using var shop = await Shop.Open();
        var tall = await shop.AddService("Tall", 1550);
        var marta = await shop.AddWorker("Marta");
        var berta = await shop.AddWorker("Berta");
        var clients = new List<Client>();
        for (int i = 0; i < 6; i++) clients.Add(await shop.AddClient($"Client {i}", $"61200{i:0000}"));

        // A whole week ending today, mixed workers, clients, guests and methods
        var monday = Helpers.WeekHelper.MondayOfWeek(Shop.Today);
        var methods = new[] { "Efectiu", "Targeta", "Bizum" };
        int expected = 0, n = 0;
        for (var date = monday; date <= Shop.Today; date = date.AddDays(1))
            for (int slot = 0; slot < 5; slot++, n++)
            {
                var appointment = await shop.Book(date, new TimeOnly(9 + slot, 0),
                    client: n % 3 == 0 ? null : clients[n % clients.Count],
                    guest: $"Passavolant {n}", service: tall, worker: n % 2 == 0 ? marta : berta);

                shop.User.Then<SaleDialogViewModel>(async vm =>
                {
                    if (n % 4 == 0) vm.AddConcept("Retoc", "3,35");
                    vm.PayWith(methods[n % 3]);
                    await vm.ChargeCommand.ExecuteAsync(null);
                });
                await shop.Home.MarkCompletedCommand.ExecuteAsync(appointment);
                expected += 1550 + (n % 4 == 0 ? 335 : 0);
            }

        var week = await shop.Till.Summary(monday, Shop.Today);
        week.SalesCents.Should().Be(expected);
        (week.BaseCents + week.VatCents).Should().Be(week.SalesCents);

        long daily = 0;
        for (var date = monday; date <= Shop.Today; date = date.AddDays(1))
            daily += (await shop.Till.Summary(date, date)).SalesCents;
        daily.Should().Be(week.SalesCents);

        var ranking = await shop.Reports.WorkerRanking(monday, Shop.Today);
        ranking.Sum(r => r.IncomeCents).Should().Be(week.SalesCents);
        ranking.Sum(r => r.SalesHandled).Should().Be(n);

        // Nothing was left pending or uncharged
        (await shop.Appointments.GetOverduePending(DateTime.Now.AddDays(1))).Should().BeEmpty();
        (await shop.Appointments.GetCompletedWithoutSale()).Should().BeEmpty();
    }
}
