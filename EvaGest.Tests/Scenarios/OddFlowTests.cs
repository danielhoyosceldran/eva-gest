using AwesomeAssertions;
using EvaGest.Models;
using EvaGest.Resources;
using EvaGest.Services;
using EvaGest.Tests.Infra;
using EvaGest.ViewModels.Dialogs;
using EvaGest.ViewModels.Pages;
using Xunit;

namespace EvaGest.Tests.Scenarios;

/// <summary>
/// Block W, second part — days that are not wrong but are not routine either: a sale for
/// nobody, a guest who decides to register halfway through paying, a closed day that still
/// gets a booking, a worker who leaves, a VAT setting changed between two customers. Each
/// one is legitimate, so the app must take it without complaint and without bending any
/// figure that was already recorded.
/// </summary>
public class OddFlowTests
{
    [Fact] // W-10
    public async Task A_walk_in_charged_to_nobody_and_a_guest_who_registers_mid_sale()
    {
        await using var shop = await Shop.Open();
        await shop.AddProduct("Cera", 900);

        // A bottle of wax sold over the counter to nobody in particular
        shop.User.Then<SaleDialogViewModel>(async vm =>
        {
            vm.ClientPicker.IsNoClient = true;
            vm.SearchAndAdd("cera");
            vm.PayWith("Efectiu");
            await vm.ChargeCommand.ExecuteAsync(null);
        });
        await shop.Home.NewSaleCommand.ExecuteAsync(null);

        // A guest who, once the reminder shows, agrees to be registered before paying
        shop.User.Then<SaleDialogViewModel>(async vm =>
            {
                vm.TextClient = "Laia Ferrer";
                vm.GuestPhone = "699 111 222";
                vm.UnregisteredClientNotice.Should().BeTrue();
                vm.AddConcept("Tall", "14,00");
                vm.PayWith("Targeta");

                await vm.RegisterClientNowCommand.ExecuteAsync(null);

                vm.SelectedClient.Should().NotBeNull();
                vm.UnregisteredClientNotice.Should().BeFalse();
                vm.Lines.Should().ContainSingle("registering must not lose the half-made sale");
                await vm.ChargeCommand.ExecuteAsync(null);
            })
            .Then<ClientDialogViewModel>(async vm =>
            {
                vm.Name.Should().Be("Laia Ferrer");
                vm.Mobile.Should().Be("699 111 222", "the phone typed for the guest is carried over");
                await vm.SaveCommand.ExecuteAsync(null);
            });
        await shop.Home.NewSaleCommand.ExecuteAsync(null);

        // Someone typing the reserved marker as a name is refused, not silently "nobody"
        shop.User.Then<SaleDialogViewModel>(async vm =>
        {
            vm.TextClient = Sale.NoClientName;
            vm.AddConcept("Tall", "14,00");
            vm.PayWith("Efectiu");
            await vm.ChargeCommand.ExecuteAsync(null);
            vm.ErrorValidation.Should().Be(Texts.GuestNameReserved);
        });
        await shop.Home.NewSaleCommand.ExecuteAsync(null);

        var sales = await shop.Sales.Search(new SalesFilter());
        sales.Should().HaveCount(2);
        sales.Should().ContainSingle(s => s.IsNoClient);
        var laia = sales.Single(s => s.ClientId is not null);
        laia.Client!.Name.Should().Be("Laia Ferrer");
        laia.GuestName.Should().BeNull();

        await shop.ClientsPage.Load();
        shop.ClientsPage.Active.Should().ContainSingle(c => c.Name == "Laia Ferrer");

        var ranking = await shop.Reports.TopBySpend();
        ranking.Should().ContainSingle("the sale to nobody is in the till but belongs to no client")
            .Which.totalCents.Should().Be(1400);
        (await shop.Till.Summary(Shop.Today, Shop.Today)).SalesCents.Should().Be(2300);
    }

    [Fact] // W-11
    public async Task Booking_on_a_closed_day_out_of_hours_and_over_another_booking_warns_but_saves()
    {
        await using var shop = await Shop.Open();
        var holiday = Shop.Today.AddDays(7);
        await shop.SettingsPage.Load();
        shop.SettingsPage.NewClosedDate = holiday;
        shop.SettingsPage.NewClosedReason = "Festa major";
        await shop.SettingsPage.AddClosedDayCommand.ExecuteAsync(null);
        await shop.Book(holiday, new TimeOnly(22, 0), guest: "Primer");

        var dialog = new AppointmentDialogViewModel(shop.Appointments, shop.Availability, shop.Clients,
            shop.Catalog, shop.Workers, shop.Settings, shop.User, holiday, new TimeOnly(22, 0));
        await dialog.Initialization;
        dialog.TextClient = "Segon";
        dialog.TimeText = "22:15";
        await WaitForNotices(() => dialog.ClosedDayNotice && dialog.OverlapNotice);

        dialog.ClosedDayReason.Should().Be("Festa major");
        dialog.OutsideScheduleNotice.Should().BeTrue("no opening hours were ever set");

        // First the owner backs off at the overlap question, then saves anyway
        bool closed = false;
        dialog.Close += ok => closed = ok;
        shop.User.Answers(false, true);
        await dialog.SaveCommand.ExecuteAsync(null);
        closed.Should().BeFalse("\"change the time\" keeps the dialog open");
        await dialog.SaveCommand.ExecuteAsync(null);
        closed.Should().BeTrue();

        (await shop.Appointments.GetByDay(holiday)).Should().HaveCount(2, "none of the warnings blocks saving (CU-01)");
    }

    [Fact] // W-12
    public async Task A_worker_who_leaves_keeps_her_history_but_is_no_longer_offered()
    {
        await using var shop = await Shop.Open();
        var marta = await shop.AddWorker("Marta");
        await shop.AddWorker("Berta");

        shop.User.Then<SaleDialogViewModel>(async vm =>
        {
            vm.TextClient = "Pau";
            vm.AddConcept("Tall", "15,00");
            vm.Worker = vm.ActiveWorkers.Single(w => w.Name == "Marta");
            vm.PayWith("Efectiu");
            await vm.ChargeCommand.ExecuteAsync(null);
        });
        await shop.SalesPage.NewSaleCommand.ExecuteAsync(null);

        // She leaves: the owner deletes her, which only deactivates someone with history
        await shop.WorkersPage.Load();
        shop.User.PinAnswers(true);
        await shop.WorkersPage.DeleteWorkerCommand.ExecuteAsync(marta);
        shop.WorkersPage.Notice.Should().Be(string.Format(Texts.WorkerDeactivated, "Marta"));

        // A new sale no longer offers her...
        shop.User.Then<SaleDialogViewModel>(vm =>
        {
            vm.ActiveWorkers.Select(w => w.Name).Should().BeEquivalentTo(["Berta"]);
            vm.CancelCommand.Execute(null);
        });
        await shop.SalesPage.NewSaleCommand.ExecuteAsync(null);

        // ...but correcting her old sale keeps her on it
        await shop.SalesPage.Load();
        var row = shop.SalesPage.Sales.Single();
        row.WorkerText.Should().Be("Marta");
        shop.User.Then<SaleDialogViewModel>(async vm =>
        {
            vm.Worker!.Name.Should().Be("Marta");
            vm.Notes = "Pagat amb monedes";
            await vm.ChargeCommand.ExecuteAsync(null);
        });
        await shop.SalesPage.EditSaleCommand.ExecuteAsync(row.Sale);

        (await shop.Sales.GetById(row.Sale.Id))!.WorkerId.Should().Be(marta.Id);
        var ranking = await shop.Reports.WorkerRanking(Shop.Today, Shop.Today);
        ranking.Should().Contain(r => r.WorkerId == marta.Id && r.IncomeCents == 1500,
            "her month's income is still hers in the report");
    }

    [Fact] // W-13
    public async Task Switching_vat_mode_between_two_customers_leaves_the_first_sale_alone()
    {
        await using var shop = await Shop.Open();
        await shop.AddService("Tall", 1500);

        async Task Charge()
        {
            shop.User.Then<SaleDialogViewModel>(async vm =>
            {
                vm.TextClient = "Client";
                vm.SearchAndAdd("tall");
                vm.PayWith("Efectiu");
                await vm.ChargeCommand.ExecuteAsync(null);
            });
            await shop.Home.NewSaleCommand.ExecuteAsync(null);
        }

        await Charge();

        await shop.SettingsPage.Load();
        shop.User.Answers(false);   // first thought better of it
        shop.SettingsPage.ModeVat = VatMode.NotIncluded;
        await shop.SettingsPage.VatModeChange;
        shop.SettingsPage.ModeVat.Should().Be(VatMode.Included, "declining puts the picker back");

        shop.User.Answers(true);
        shop.SettingsPage.ModeVat = VatMode.NotIncluded;
        await shop.SettingsPage.VatModeChange;

        await Charge();

        var sales = (await shop.Sales.Search(new SalesFilter())).OrderBy(s => s.Id).ToList();
        sales[0].TotalCents.Should().Be(1500, "taken VAT-included");
        sales[1].TotalCents.Should().Be(1815, "15,00 plus 21 % on top");

        // Correcting the first sale afterwards must not reinterpret it in the new mode
        await shop.SalesPage.Load();
        shop.User.Then<SaleDialogViewModel>(async vm =>
        {
            vm.TotalCents.Should().Be(1500);
            vm.Notes = "corregit";
            await vm.ChargeCommand.ExecuteAsync(null);
        });
        await shop.SalesPage.EditSaleCommand.ExecuteAsync(shop.SalesPage.Sales.Single(r => r.Sale.Id == sales[0].Id).Sale);

        (await shop.Sales.GetById(sales[0].Id))!.TotalCents.Should().Be(1500);
        var day = await shop.Till.Summary(Shop.Today, Shop.Today);
        day.SalesCents.Should().Be(3315);
        (day.BaseCents + day.VatCents).Should().Be(day.SalesCents);
    }

    [Fact] // W-14
    public async Task A_client_who_calls_to_move_their_appointment_leaves_today_and_lands_on_the_new_day()
    {
        await using var shop = await Shop.Open();
        var tall = await shop.AddService("Tall", 1500, durationMin: 45);
        var joan = await shop.AddClient("Joan");
        await shop.Book(Shop.Today, new TimeOnly(17, 0), client: joan, service: tall, durationMin: 45);

        await shop.Home.Load();
        var tomorrow = Shop.Today.AddDays(1);
        shop.User.Answers(true).Then<AppointmentDialogViewModel>(async vm =>
        {
            await vm.Initialization;
            vm.SelectedClient!.Id.Should().Be(joan.Id);
            vm.Date = tomorrow;
            vm.TimeText = "18:30";
            await vm.SaveCommand.ExecuteAsync(null);
        });
        await shop.Home.EditAppointmentCommand.ExecuteAsync(shop.Home.DayAppointments.Single());

        shop.Home.DayAppointments.Should().BeEmpty();
        var moved = (await shop.Appointments.GetByDay(tomorrow)).Single();
        moved.Time.Should().Be(new TimeOnly(18, 30));
        moved.DurationMin.Should().Be(45, "moving keeps the length it was booked with");
        moved.ServiceId.Should().Be(tall.Id);
        moved.Status.Should().Be(AppointmentStatus.Pending);
    }

    [Fact] // W-15
    public async Task An_asleep_client_who_comes_back_is_charged_and_woken_by_hand()
    {
        await using var shop = await Shop.Open();
        var rosa = await shop.AddClient("Rosa Puig");
        await shop.Clients.Sleep(rosa.Id);
        await shop.Book(Shop.Today, new TimeOnly(9, 0), client: rosa);

        // Her old appointment still opens with her, though she is not in the search
        await shop.Home.Load();
        shop.User.Then<SaleDialogViewModel>(async vm =>
        {
            vm.SelectedClient!.Id.Should().Be(rosa.Id);
            vm.AddConcept("Tall", "15,00");
            vm.PayWith("Efectiu");
            await vm.ChargeCommand.ExecuteAsync(null);
        });
        await shop.Home.MarkCompletedCommand.ExecuteAsync(shop.Home.DayAppointments.Single());

        (await shop.Sales.Search(new SalesFilter(ClientId: rosa.Id))).Should().ContainSingle();

        await shop.ClientsPage.Load();
        shop.ClientsPage.Asleep.Should().ContainSingle(c => c.Id == rosa.Id);
        await shop.ClientsPage.WakeCommand.ExecuteAsync(shop.ClientsPage.Asleep.Single());
        shop.ClientsPage.Active.Should().ContainSingle(c => c.Id == rosa.Id);
        shop.ClientsPage.Asleep.Should().BeEmpty();
    }

    /// <summary>The appointment dialog refreshes its warnings in the background as the
    /// boxes change; this waits for them the way the user's eye would.</summary>
    private static async Task WaitForNotices(Func<bool> ready)
    {
        for (int i = 0; i < 100 && !ready(); i++) await Task.Delay(20);
        ready().Should().BeTrue("the warnings should have caught up with the boxes");
    }
}
