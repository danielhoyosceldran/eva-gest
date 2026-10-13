using AwesomeAssertions;
using EvaGest.Models;
using EvaGest.Resources;
using EvaGest.Services;
using EvaGest.Tests.Infra;
using EvaGest.ViewModels.Dialogs;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace EvaGest.Tests.Scenarios;

/// <summary>
/// Block W, third part — the mistakes a busy counter actually makes, and putting them
/// right: the wrong price, the wrong customer's appointment charged, a sale forgotten, a
/// half-typed form, a cash-out entered twice. The rule each story checks is the same: a
/// mistake can always be undone from the screens the user has, and once it is, every
/// total reads as if it had never happened while the record of it is kept (RF-10, CU-04).
/// </summary>
public class MistakeFlowTests
{
    [Fact] // W-20
    public async Task A_price_typed_wrong_is_corrected_from_the_sales_page_without_moving_the_sale()
    {
        await using var shop = await Shop.Open();

        shop.User.Then<SaleDialogViewModel>(async vm =>
        {
            vm.TextClient = "Pol";
            vm.AddConcept("Tall i barba", "51,00");    // meant 15,00
            vm.PayWith("Efectiu");
            await vm.ChargeCommand.ExecuteAsync(null);
        });
        await shop.Home.NewSaleCommand.ExecuteAsync(null);
        var original = (await shop.Sales.Search(new SalesFilter())).Single();

        await shop.SalesPage.Load();
        shop.User.Then<SaleDialogViewModel>(async vm =>
        {
            vm.CurrentMode.Should().Be(SaleDialogViewModel.Mode.Edit);
            vm.Lines.Single().PriceText = "15,00";
            vm.PayWith("Targeta");                      // and it was by card, too
            await vm.ChargeCommand.ExecuteAsync(null);
        });
        await shop.SalesPage.EditSaleCommand.ExecuteAsync(shop.SalesPage.Sales.Single().Sale);

        var corrected = (await shop.Sales.GetById(original.Id))!;
        corrected.TotalCents.Should().Be(1500);
        corrected.PaymentMethodId.Should().Be(await shop.MethodId("Targeta"));
        corrected.Date.Should().Be(original.Date);
        corrected.Time.Should().Be(original.Time);
        corrected.Status.Should().Be(SaleStatus.Active);

        shop.SalesPage.Sales.Should().ContainSingle("a correction replaces the sale, it does not add one");
        shop.SalesPage.TotalTotalText.Should().Be(Money.Format(1500));
        (await shop.Till.Summary(Shop.Today, Shop.Today)).SalesCents.Should().Be(1500);

        // The wrong figure is not lost: the audit trail keeps what the ticket said before
        await using var db = shop.Db.Context();
        var audit = await db.AuditEntries.Where(a => a.EntityId == original.Id).ToListAsync();
        audit.Should().ContainSingle(a => a.Action == "Update");
    }

    [Fact] // W-21
    public async Task Charging_the_wrong_appointment_is_undone_by_voiding_reopening_and_charging_the_right_one()
    {
        await using var shop = await Shop.Open();
        var tall = await shop.AddService("Tall", 1500);
        var joan = await shop.AddClient("Joan");
        var pere = await shop.AddClient("Pere", "612999888");
        var joanAppointment = await shop.Book(Shop.Today, new TimeOnly(10, 0), client: joan, service: tall);
        var pereAppointment = await shop.Book(Shop.Today, new TimeOnly(10, 30), client: pere, service: tall);

        async Task ChargeFromHome(Appointment appointment)
        {
            shop.User.Then<SaleDialogViewModel>(async vm =>
            {
                vm.PayWith("Efectiu");
                await vm.ChargeCommand.ExecuteAsync(null);
            });
            await shop.Home.MarkCompletedCommand.ExecuteAsync(appointment);
        }

        // Pere is in the chair, but Joan's row is the one clicked
        await shop.Home.Load();
        await ChargeFromHome(joanAppointment);

        // Cancelling Joan's charged appointment is refused, not silently done
        await shop.Home.MarkCancelledCommand.ExecuteAsync(joanAppointment);
        shop.Home.Notice.Should().Be(Texts.AppointmentHasSaleCannotCancel);
        (await shop.Appointments.GetById(joanAppointment.Id))!.Status.Should().Be(AppointmentStatus.Completed);

        // The void needs the owner's PIN: a wrong PIN first, then the right one
        await shop.SalesPage.Load();
        var wrongSale = shop.SalesPage.Sales.Single().Sale;
        shop.User.PinAnswers(false, true);
        await shop.SalesPage.VoidSaleCommand.ExecuteAsync(wrongSale);
        (await shop.Sales.GetById(wrongSale.Id))!.Status.Should().Be(SaleStatus.Active);
        await shop.SalesPage.VoidSaleCommand.ExecuteAsync(wrongSale);
        (await shop.Sales.GetById(wrongSale.Id))!.Status.Should().Be(SaleStatus.Voided);

        // Joan's appointment goes back to pending, and Pere's is charged
        await shop.Home.MarkPendingCommand.ExecuteAsync(joanAppointment);
        shop.Home.Notice.Should().NotBe(Texts.AppointmentHasSaleCannotReopen);
        (await shop.Appointments.GetById(joanAppointment.Id))!.Status.Should().Be(AppointmentStatus.Pending);
        await ChargeFromHome(pereAppointment);

        // Joan turns up after all: the same appointment can be charged again
        await ChargeFromHome(joanAppointment);

        var all = await shop.Sales.Search(new SalesFilter());
        all.Should().HaveCount(3);
        all.Count(s => s.Status == SaleStatus.Active).Should().Be(2);
        all.Count(s => s.AppointmentId == joanAppointment.Id).Should().Be(2, "the voided charge stays linked to its visit");

        await shop.SalesPage.Load();
        shop.SalesPage.ActiveCount.Should().Be(2);
        shop.SalesPage.TotalTotalText.Should().Be(Money.Format(3000));
        (await shop.Till.Summary(Shop.Today, Shop.Today)).SalesCents.Should().Be(3000);
        (await shop.Reports.GetClientIndicators(joan.Id)).Should().NotBeNull();
        (await shop.Appointments.GetCompletedWithoutSale()).Should().BeEmpty();
    }

    [Fact] // W-22
    public async Task An_appointment_left_uncharged_is_flagged_until_it_is_charged()
    {
        await using var shop = await Shop.Open();
        var twoHoursAgo = DateTime.Now.AddHours(-2);
        var appointment = await shop.Book(DateOnly.FromDateTime(twoHoursAgo),
            TimeOnly.FromDateTime(twoHoursAgo), guest: "Marina");

        // "Fet" was pressed and the dialog closed straight away without charging
        shop.User.Then<SaleDialogViewModel>(vm => vm.CancelCommand.Execute(null));
        await shop.Home.MarkCompletedCommand.ExecuteAsync(appointment);

        shop.User.Asked.Should().BeEmpty("nothing had been typed, so closing asks nothing");
        (await shop.Appointments.GetById(appointment.Id))!.Status
            .Should().Be(AppointmentStatus.Pending, "closing without charging leaves it to be charged");

        await shop.Shell.CheckOverdueAppointments();
        shop.Shell.OverdueAppointmentsNotice.Should().Contain("Marina");

        // Charged later; a stray Esc halfway through is caught by the discard question
        shop.User.Answers(false).Then<SaleDialogViewModel>(async vm =>
        {
            vm.AddConcept("Tall", "15,00");
            await vm.CancelCommand.ExecuteAsync(null);
            shop.User.Asked.Should().ContainSingle(t => t == Texts.DiscardChangesTitle);

            vm.PayWith("Bizum");
            await vm.ChargeCommand.ExecuteAsync(null);
        });
        await shop.Home.MarkCompletedCommand.ExecuteAsync(appointment);

        (await shop.Appointments.GetById(appointment.Id))!.Status.Should().Be(AppointmentStatus.Completed);
        shop.Shell.OverdueAppointmentsNotice.Should().BeNull("charging it reaches the notice at once");
        (await shop.Sales.Search(new SalesFilter())).Should().ContainSingle();
    }

    [Fact] // W-23
    public async Task Every_wrong_box_in_a_sale_is_refused_with_its_own_message_and_one_sale_is_recorded()
    {
        await using var shop = await Shop.Open();
        await shop.AddClient("Núria Roca");

        var messages = new List<string?>();
        shop.User.Then<SaleDialogViewModel>(async vm =>
        {
            async Task Try() { await vm.ChargeCommand.ExecuteAsync(null); messages.Add(vm.ErrorValidation); }

            await Try();                                         // nothing at all

            vm.AddCustomConceptCommand.Execute(null);
            vm.ClientPicker.Text = "nuria";                      // searched but never picked
            await Try();

            vm.ClientPicker.PickHighlightedCommand.Execute(null);
            await Try();                                         // no payment method

            vm.PayWith("Efectiu");
            await Try();                                         // the line has no description

            vm.Lines[0].Description = "Tall";
            await Try();                                         // and no price

            vm.Lines[0].PriceText = "quinze";
            await Try();

            vm.Lines[0].PriceText = "-15";
            await Try();

            vm.Lines[0].PriceText = "15,00";
            vm.Lines[0].QuantityText = "0";
            await Try();

            vm.Lines[0].QuantityText = "1";
            vm.Lines[0].VatText = "121";
            await Try();

            vm.Lines[0].VatText = "21";
            await Try();                                         // finally right
        });
        await shop.Home.NewSaleCommand.ExecuteAsync(null);

        messages.Should().Equal(
            Texts.AtLeastOneLineRequired,
            Texts.SaleClientNotPicked,
            Texts.PaymentMethodRequired,
            Texts.LineDescriptionRequired,
            Texts.PriceRequired,
            Texts.PriceInvalid,
            Texts.PriceNegative,
            Texts.QuantityInvalid,
            Texts.VatOutOfRange,
            null);

        var sale = (await shop.Sales.Search(new SalesFilter())).Should().ContainSingle().Subject;
        sale.TotalCents.Should().Be(1500);
        sale.Client!.Name.Should().Be("Núria Roca");
    }

    [Fact] // W-24
    public async Task A_cash_out_typed_wrong_is_refused_then_one_entered_twice_is_deleted()
    {
        await using var shop = await Shop.Open();
        await shop.TillPage.Load();

        var errors = new List<string?>();
        shop.User.Then<MovementDialogViewModel>(async vm =>
        {
            async Task Try() { await vm.SaveCommand.ExecuteAsync(null); errors.Add(vm.ErrorValidation); }

            await Try();                        // empty
            vm.PriceText = "-5";
            await Try();
            vm.PriceText = "0";
            await Try();
            vm.PriceText = "1.200";             // a thousand with a group dot: refused, not
            await Try();                        // guessed at (it could be 1,20)
            vm.PriceText = "1200";
            await Try();                        // ...but no concept
            vm.Concept = "Lloguer";
            await Try();
        });
        await shop.TillPage.NewCashOutCommand.ExecuteAsync(null);

        errors.Should().Equal(Texts.AmountRequired, Texts.AmountMustBePositive, Texts.AmountMustBePositive,
                              Texts.AmountMustBePositive, Texts.ConceptRequired, null);
        shop.TillPage.Summary!.CashOutCents.Should().Be(120000);

        // The same rent entered a second time by mistake
        shop.User.Then<MovementDialogViewModel>(async vm =>
        {
            vm.PriceText = "1200";
            vm.Concept = "Lloguer";
            await vm.SaveCommand.ExecuteAsync(null);
        });
        await shop.TillPage.NewCashOutCommand.ExecuteAsync(null);
        shop.TillPage.Movements.Should().HaveCount(2);

        shop.User.PinAnswers(true);
        await shop.TillPage.DeleteCommand.ExecuteAsync(shop.TillPage.Movements[0]);

        shop.TillPage.Movements.Should().ContainSingle();
        shop.TillPage.Summary.CashOutCents.Should().Be(120000);
        shop.TillPage.BalanceText.Should().Be(Money.Format(-120000));

        await using var db = shop.Db.Context();
        (await db.CashMovements.CountAsync()).Should().Be(2, "a deleted movement is voided, not erased");
    }

    [Fact] // W-25
    public async Task Registering_a_client_twice_is_refused_and_says_who_already_has_the_name()
    {
        await using var shop = await Shop.Open();
        await shop.AddClient("Joan García", "612000111");
        await shop.ClientsPage.Load();

        string? refusal = null;
        shop.User.Then<ClientDialogViewModel>(async vm =>
        {
            vm.Name = "  joan garcia ";
            vm.Mobile = "699000000";
            await vm.SaveCommand.ExecuteAsync(null);
            refusal = vm.ErrorValidation;

            vm.Mobile = "abc";                     // and a phone fumbled on the way
            vm.Name = "Joan García Pons";
            await vm.SaveCommand.ExecuteAsync(null);
            vm.ErrorValidation.Should().Be(Texts.PhoneInvalid);

            vm.Mobile = "699000000";
            await vm.SaveCommand.ExecuteAsync(null);
        });
        await shop.ClientsPage.NewClientCommand.ExecuteAsync(null);

        refusal.Should().Be(string.Format(Texts.ClientNameAlreadyExists, "Joan García", "612000111"));
        shop.ClientsPage.Active.Select(c => c.Name).Should().BeEquivalentTo(["Joan García", "Joan García Pons"]);
    }

    [Fact] // W-26
    public async Task An_appointment_cancelled_by_mistake_is_reopened_and_charged()
    {
        await using var shop = await Shop.Open();
        var anna = await shop.AddClient("Anna");
        var appointment = await shop.Book(Shop.Today, new TimeOnly(16, 0), client: anna);
        await shop.Home.Load();

        await shop.Home.MarkCancelledCommand.ExecuteAsync(appointment);
        (await shop.Appointments.GetById(appointment.Id))!.Status.Should().Be(AppointmentStatus.Cancelled);

        // A cancelled appointment cannot be charged as it is (F-05)...
        var charge = () => SaleDialogViewModel.FromAppointment(shop.Sales, shop.Clients, shop.Catalog,
            shop.Workers, new TestSoundService(), shop.Settings, shop.User, appointment);
        await charge.Should().ThrowAsync<InvalidOperationException>();

        // ...so it is reopened first
        await shop.Home.MarkPendingCommand.ExecuteAsync(appointment);
        shop.User.Then<SaleDialogViewModel>(async vm =>
        {
            vm.AddConcept("Tall", "15,00");
            vm.PayWith("Efectiu");
            await vm.ChargeCommand.ExecuteAsync(null);
        });
        await shop.Home.MarkCompletedCommand.ExecuteAsync(appointment);

        (await shop.Appointments.GetById(appointment.Id))!.Status.Should().Be(AppointmentStatus.Completed);
        (await shop.Appointments.CountByStatus(Shop.Today, Shop.Today, AppointmentStatus.Cancelled)).Should().Be(0);

        // The history shows both steps
        await using var db = shop.Db.Context();
        (await db.AuditEntries.CountAsync(a => a.EntityId == appointment.Id && a.Action == "Status"))
            .Should().Be(2, "cancelled, then reopened");
    }

    [Fact] // W-27
    public async Task A_booking_deleted_by_mistake_while_another_was_charged_cannot_take_the_charged_one_with_it()
    {
        await using var shop = await Shop.Open();
        var charged = await shop.Book(Shop.Today, new TimeOnly(9, 0), guest: "Cobrat");
        var empty = await shop.Book(Shop.Today, new TimeOnly(9, 30), guest: "Duplicat");

        shop.User.Then<SaleDialogViewModel>(async vm =>
        {
            vm.AddConcept("Tall", "15,00");
            vm.PayWith("Efectiu");
            await vm.ChargeCommand.ExecuteAsync(null);
        });
        await shop.Home.MarkCompletedCommand.ExecuteAsync(charged);

        await shop.Agenda.Load();
        await shop.Agenda.SelectDayCommand.ExecuteAsync(Shop.Today);

        // The owner first says no, then deletes the duplicate, then tries the charged one
        shop.User.Answers(false, true);
        await shop.Agenda.DeleteAppointmentCommand.ExecuteAsync(empty);
        shop.Agenda.DayAppointments.Should().HaveCount(2, "\"no\" deletes nothing");
        await shop.Agenda.DeleteAppointmentCommand.ExecuteAsync(empty);
        shop.Agenda.Notice.Should().Be(Texts.AppointmentDeleted);
        int asked = shop.User.Asked.Count;
        await shop.Agenda.DeleteAppointmentCommand.ExecuteAsync(charged);
        shop.Agenda.Notice.Should().Be(Texts.AppointmentNotDeletedHasSale);
        shop.User.Asked.Should().HaveCount(asked, "a delete that cannot happen is not asked about first (B-35)");

        shop.Agenda.DayAppointments.Should().ContainSingle(r => r.Appointment.Id == charged.Id);
        (await shop.Sales.Search(new SalesFilter())).Single().AppointmentId.Should().Be(charged.Id);
    }
}
