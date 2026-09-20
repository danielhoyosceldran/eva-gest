using AwesomeAssertions;
using EvaGest.Models;
using EvaGest.Services;
using EvaGest.Tests.Infra;
using EvaGest.Resources;
using EvaGest.ViewModels.Dialogs;
using EvaGest.ViewModels.Pages;
using Xunit;

namespace EvaGest.Tests.Services;

/// <summary>
/// Block T: the Sales page. The RF-15 filters and, above all, that the table footer
/// sums only the active sales: a voided sale stays in the list precisely so it can be
/// seen not to count.
/// </summary>
public class SalesPageTests
{
    private static readonly DateOnly Today = new(2026, 9, 7);

    private static async Task<SalesViewModel> Build(TestDatabase testDb, TestDialogService? dialogs = null)
    {
        var factory = new TestFactory(testDb.Options);
        var config = new SettingsService(factory);
        await new SeedService(factory, config).Seed();

        return new SalesViewModel(
            new SaleService(factory, config), new ClientService(factory), new CatalogService(factory),
            new WorkerService(factory), new TestSoundService(), config,
            new ExportService(factory), dialogs ?? new TestDialogService());
    }

    private sealed record Data(int MethodId, int OtherMethodId, int ServiceId, int ClientId);

    private static async Task<Data> Seed(TestDatabase testDb)
    {
        await using var db = testDb.Context();

        var service = Make.Service("Tall", priceCents: 1500);
        var client = Make.Client("Joana", "600111222");
        var cash = Make.Method("Efectiu");
        var card = Make.Method("Targeta");

        db.Services.Add(service);
        db.Clients.Add(client);
        db.PaymentMethods.AddRange(cash, card);
        await db.SaveChangesAsync();

        return new Data(cash.Id, card.Id, service.Id, client.Id);
    }

    private static async Task<int> AddsSale(
        TestDatabase testDb, Data d, int amountCents, SaleStatus status = SaleStatus.Active,
        DateOnly? date = null, int? methodId = null, int? clientId = null, int? serviceId = null)
    {
        await using var db = testDb.Context();

        var line = Make.Line(amountCents);
        line.ServiceId = serviceId;

        var sale = Make.Sale(date ?? Today, methodId ?? d.MethodId, status, line);
        if (clientId is int id)
        {
            sale.ClientId = id;
            sale.GuestName = null;
        }

        db.Sales.Add(sale);
        await db.SaveChangesAsync();
        return sale.Id;
    }

    [Fact] // T-01
    public async Task The_footer_sums_only_the_active_sales()
    {
        await using var testDb = new TestDatabase();
        var d = await Seed(testDb);
        await AddsSale(testDb, d, 1000);
        await AddsSale(testDb, d, 2000);
        await AddsSale(testDb, d, 5000, SaleStatus.Voided);

        var vm = await Build(testDb);
        await vm.Load();

        vm.Sales.Should().HaveCount(3, "the voided sale stays visible in the history");
        vm.ActiveCount.Should().Be(2);
        vm.TotalTotalText.Should().Be(Money.Format(3000));
    }

    [Fact] // T-02
    public async Task Amounts_are_shown_in_euros_not_in_cents()
    {
        await using var testDb = new TestDatabase();
        var d = await Seed(testDb);
        await AddsSale(testDb, d, 3650);

        var vm = await Build(testDb);
        await vm.Load();

        // The XAML used to bind TotalCents to a {0:0.00} format string, which turned
        // 36,50 € into "3650,00".
        vm.Sales[0].TotalText.Should().Be(Money.Format(3650));
        vm.Sales[0].TotalText.Should().NotContain("3650,00");
        vm.Sales[0].DateText.Should().Be("07/09/2026");
    }

    [Fact] // T-03
    public async Task Filtering_by_status_leaves_only_the_voided_sales()
    {
        await using var testDb = new TestDatabase();
        var d = await Seed(testDb);
        await AddsSale(testDb, d, 1000);
        await AddsSale(testDb, d, 5000, SaleStatus.Voided);

        var vm = await Build(testDb);
        await vm.Load();
        vm.Status = SaleStatus.Voided;
        await vm.Load();

        vm.Sales.Should().ContainSingle();
        vm.ActiveCount.Should().Be(0);
        vm.TotalTotalText.Should().Be(Money.Format(0));
    }

    [Fact] // T-04
    public async Task Filtering_by_date_leaves_out_what_does_not_fall_inside()
    {
        await using var testDb = new TestDatabase();
        var d = await Seed(testDb);
        await AddsSale(testDb, d, 1000, date: Today);
        await AddsSale(testDb, d, 2000, date: Today.AddDays(-10));

        var vm = await Build(testDb);
        await vm.Load();
        vm.From = Today.AddDays(-2);
        await vm.Load();

        vm.Sales.Should().ContainSingle();
        vm.TotalTotalText.Should().Be(Money.Format(1000));
    }

    [Fact] // T-05
    public async Task Filter_by_client_and_by_payment_method()
    {
        await using var testDb = new TestDatabase();
        var d = await Seed(testDb);
        await AddsSale(testDb, d, 1000, clientId: d.ClientId);
        await AddsSale(testDb, d, 2000, methodId: d.OtherMethodId);

        var vm = await Build(testDb);
        await vm.Load();

        vm.Client = vm.FilterClients.Single(c => c.Id == d.ClientId);
        await vm.Load();
        vm.Sales.Should().ContainSingle();

        await vm.ClearFiltersCommand.ExecuteAsync(null);
        vm.PaymentMethod = vm.FilterMethods.Single(m => m.Id == d.OtherMethodId);
        await vm.Load();
        vm.Sales.Should().ContainSingle();
        vm.Sales[0].TotalText.Should().Be(Money.Format(2000));
    }

    [Fact] // T-06
    public async Task Clearing_the_filters_shows_everything_again()
    {
        await using var testDb = new TestDatabase();
        var d = await Seed(testDb);
        await AddsSale(testDb, d, 1000);
        await AddsSale(testDb, d, 2000, SaleStatus.Voided);

        var vm = await Build(testDb);
        await vm.Load();
        vm.Status = SaleStatus.Active;
        await vm.Load();
        vm.Sales.Should().ContainSingle();

        await vm.ClearFiltersCommand.ExecuteAsync(null);

        vm.Sales.Should().HaveCount(2);
        vm.HasNoResults.Should().BeFalse();
    }

    [Fact] // T-07
    public async Task Filters_that_match_nothing_say_so()
    {
        await using var testDb = new TestDatabase();
        var d = await Seed(testDb);
        await AddsSale(testDb, d, 1000);

        var vm = await Build(testDb);
        await vm.Load();
        vm.From = Today.AddDays(30);
        await vm.Load();

        vm.HasNoResults.Should().BeTrue();
        vm.TotalTotalText.Should().Be(Money.Format(0));
    }

    [Fact] // T-08
    public async Task Only_an_active_sale_offers_to_be_voided()
    {
        await using var testDb = new TestDatabase();
        var d = await Seed(testDb);
        await AddsSale(testDb, d, 1000);
        await AddsSale(testDb, d, 2000, SaleStatus.Voided);

        var vm = await Build(testDb);
        await vm.Load();

        vm.Sales.Single(f => f.Sale.Status == SaleStatus.Active).CanVoid.Should().BeTrue();
        vm.Sales.Single(f => f.Sale.Status == SaleStatus.Voided).CanVoid.Should().BeFalse();
    }

    [Fact] // T-09
    public async Task Voiding_asks_for_confirmation_and_takes_the_sale_out_of_the_totals()
    {
        await using var testDb = new TestDatabase();
        var d = await Seed(testDb);
        await AddsSale(testDb, d, 1000);

        var dialogs = new TestDialogService { ResultConfirm = false };
        var vm = await Build(testDb, dialogs);
        await vm.Load();

        await vm.VoidSaleCommand.ExecuteAsync(vm.Sales[0].Sale);
        vm.ActiveCount.Should().Be(1, "nothing may happen without confirmation");

        dialogs.ResultConfirm = true;
        await vm.VoidSaleCommand.ExecuteAsync(vm.Sales[0].Sale);

        vm.ActiveCount.Should().Be(0);
        vm.Sales.Should().ContainSingle("continua a l'historial");
    }

    /// <summary>
    /// The footer used to sum int cents with Enumerable.Sum, which is checked: past
    /// int.MaxValue it threw rather than widening, so clearing the filters on a long
    /// enough history failed the page load outright instead of showing a total.
    /// </summary>
    [Fact]
    public async Task The_footer_totals_survive_a_history_above_int_max()
    {
        await using var testDb = new TestDatabase();
        var d = await Seed(testDb);

        const int huge = 1_500_000_000;
        const long expected = 2L * huge;
        expected.Should().BeGreaterThan(int.MaxValue, "otherwise this test proves nothing");

        await AddsSale(testDb, d, huge);
        await AddsSale(testDb, d, huge);

        var vm = await Build(testDb);
        await vm.Load();

        vm.ActiveCount.Should().Be(2);
        vm.TotalTotalText.Should().Be(Money.Format(expected));
    }

    // ── What the lines come to together ──────────────────────────────────────
    // Each line is bounded on its own, never against its neighbours, and ComputeByRate
    // sums a rate group with a checked Enumerable.Sum. Two individually valid lines
    // could take the group past int.MaxValue and throw — while the user was still
    // typing, because the dialog recomputes its footer on every keystroke.

    private static async Task<SaleDialogViewModel> NewSaleDialog(TestDatabase testDb)
    {
        var factory = new TestFactory(testDb.Options);
        var config = new SettingsService(factory);
        await new SeedService(factory, config).Seed();

        var vm = await SaleDialogViewModel.New(
            new SaleService(factory, config), new ClientService(factory), new CatalogService(factory),
            new WorkerService(factory), new TestSoundService(), config, new TestDialogService());

        vm.TextClient = "Client de prova";
        vm.PaymentMethod = vm.ActiveMethods[0];
        return vm;
    }

    private static void AddLine(SaleDialogViewModel vm, string price)
    {
        vm.AddCustomConceptCommand.Execute(null);
        var line = vm.Lines[^1];
        line.Description = "Concepte";
        line.QuantityText = "1";
        line.VatText = "21";
        line.PriceText = price;
    }

    [Fact]
    public async Task Typing_a_second_over_large_line_does_not_throw_it_flags_the_total()
    {
        await using var testDb = new TestDatabase();
        var vm = await NewSaleDialog(testDb);

        AddLine(vm, "15000000,00");
        vm.TotalTooLarge.Should().BeFalse("one line of this size is representable");

        // This is the keystroke that used to crash the dialog.
        var second = () => AddLine(vm, "15000000,00");
        second.Should().NotThrow<OverflowException>();

        vm.TotalTooLarge.Should().BeTrue();
    }

    [Fact]
    public async Task A_sale_whose_lines_overflow_their_rate_group_is_refused_not_charged()
    {
        await using var testDb = new TestDatabase();
        var d = await Seed(testDb);
        var vm = await NewSaleDialog(testDb);

        AddLine(vm, "15000000,00");
        AddLine(vm, "15000000,00");

        await vm.ChargeCommand.ExecuteAsync(null);

        vm.ErrorValidation.Should().Be(Texts.SaleTotalTooLarge);

        var page = await Build(testDb);
        await page.Load();
        page.Sales.Should().BeEmpty("nothing may reach the database");
    }

    [Fact]
    public async Task An_ordinary_two_line_sale_is_still_charged()
    {
        await using var testDb = new TestDatabase();
        var d = await Seed(testDb);
        var vm = await NewSaleDialog(testDb);

        AddLine(vm, "15,00");
        AddLine(vm, "9,00");

        await vm.ChargeCommand.ExecuteAsync(null);

        vm.ErrorValidation.Should().BeNull();
        vm.TotalTooLarge.Should().BeFalse();

        var page = await Build(testDb);
        await page.Load();
        page.Sales.Should().ContainSingle();
        page.Sales[0].Sale.TotalCents.Should().Be(2400);
    }
}
