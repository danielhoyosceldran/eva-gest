using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using AwesomeAssertions;
using EvaGest.Models;
using EvaGest.Services;
using EvaGest.Tests.Infra;
using EvaGest.ViewModels.Dialogs;
using EvaGest.ViewModels.Pages;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace EvaGest.Tests.Views;

/// <summary>
/// Block H — what the tables actually look like once laid out. A header sitting over the
/// wrong column and an amount printed in cents both render perfectly happily; only a
/// person reading the screen notices. A real Arrange pass is the only way to catch the
/// first, and reading the laid-out text is the only way to catch the second.
/// </summary>
[Collection(WpfCollection.Name)]
public class TableAlignmentTests(ApplicationWpf app)
{
    private static readonly DateOnly Today = new(2026, 9, 19);

    private static async Task<SaleDialogViewModel> ASaleDialog(TestDatabase testDb)
    {
        var factory = new TestFactory(testDb.Options);
        var settings = new SettingsService(factory);
        await new SeedService(factory, settings).Seed();

        var vm = await SaleDialogViewModel.New(
            new SaleService(factory, settings), new ClientService(factory), new CatalogService(factory),
            new WorkerService(factory), new TestSoundService(), settings, new TestDialogService());

        vm.AddCustomConceptCommand.Execute(null);
        vm.Lines[0].Description = "Tall";
        vm.Lines[0].PriceText = "15,00";
        vm.Lines[0].VatText = "21";
        return vm;
    }

    [Fact]
    public async Task The_sale_line_headers_sit_over_the_columns_they_name()
    {
        // The header grid and the row grid are separate Grids, so nothing but this makes
        // them agree. They used to declare 7 and 11 columns, which left every header a
        // little left of its box and drifting further across the row.
        await using var testDb = new TestDatabase();
        var vm = await ASaleDialog(testDb);

        app.Runs(() =>
        {
            var view = new EvaGest.Views.Dialogs.SaleDialogView { DataContext = vm };
            view.Measure(new Size(760, 1400));
            view.Arrange(new Rect(0, 0, 760, 1400));
            view.UpdateLayout();

            var headers = (Grid)view.FindName("LineHeaders")!;
            var rows = (ItemsControl)view.FindName("LineRows")!;
            var firstRow = Descendants<Grid>(rows).First(g => g.ColumnDefinitions.Count > 1);

            headers.ColumnDefinitions.Should().HaveSameCount(firstRow.ColumnDefinitions,
                "the two grids must divide the row the same way");

            for (int i = 0; i < headers.ColumnDefinitions.Count; i++)
                headers.ColumnDefinitions[i].ActualWidth.Should().BeApproximately(
                    firstRow.ColumnDefinitions[i].ActualWidth, 0.5,
                    $"column {i} must be the same width in the header and in the row");
        });
    }

    [Fact]
    public async Task A_sale_line_shows_its_amount_as_money_not_as_cents()
    {
        // 15,00 € used to render as "1500".
        await using var testDb = new TestDatabase();
        var vm = await ASaleDialog(testDb);

        app.Runs(() =>
        {
            var view = new EvaGest.Views.Dialogs.SaleDialogView { DataContext = vm };
            view.Measure(new Size(760, 1400));
            view.Arrange(new Rect(0, 0, 760, 1400));
            view.UpdateLayout();

            var texts = Descendants<TextBlock>(view).Select(t => t.Text).ToList();

            texts.Should().Contain(Money.Format(1500));
            texts.Should().NotContain("1500", "cents must never reach the screen unformatted");
        });
    }

    [Fact]
    public async Task The_till_shows_every_figure_as_money_and_names_the_vat_rate()
    {
        // The per-rate table bound the raw *Cents properties through a {0:0.00} format
        // string, so a 121,00 € period read "12100,00", and the rate column read "2100 bp".
        await using var testDb = new TestDatabase();
        var factory = new TestFactory(testDb.Options);
        var settings = new SettingsService(factory);
        await new SeedService(factory, settings).Seed();

        int methodId;
        await using (var db = testDb.Context())
        {
            methodId = (await db.PaymentMethods.FirstAsync()).Id;
            db.Sales.Add(Make.Sale(Today, methodId, SaleStatus.Active, Make.Line(12_100)));
            db.CashMovements.Add(new CashMovement
            {
                Date = Today, Type = MovementType.Out, AmountCents = 2_500,
                PaymentMethodId = methodId, Concept = "Material"
            });
            await db.SaveChangesAsync();
        }

        var vm = new TillViewModel(new TillService(factory), new CatalogService(factory),
            new WorkerService(factory), new TestDialogService());
        await vm.Load();

        vm.SalesText.Should().Be(Money.Format(12_100));
        vm.CashOutText.Should().Be(Money.Format(2_500));
        vm.BalanceText.Should().Be(Money.Format(12_100 - 2_500));

        // The VAT block's total is base + quota, not the till balance. Those were the
        // same binding, so this block silently reported the balance.
        vm.VatTotalText.Should().Be(Money.Format(12_100));
        vm.VatTotalText.Should().NotBe(vm.BalanceText);

        vm.ShowRateBreakdown.Should().BeTrue("a single rate is still worth naming");
        vm.VatRates.Should().ContainSingle();
        vm.VatRates[0].RateText.Should().Be(Percentages.Format(2100));
        vm.VatRates[0].TotalText.Should().Be(Money.Format(12_100));

        vm.Movements.Should().ContainSingle();
        vm.Movements[0].AmountText.Should().Be(Money.Format(-2_500), "a cash-out reads as negative");
        vm.Movements[0].TypeText.Should().Be(Labels.Text(MovementType.Out),
            "the type is shown in the user's language, not as the stored enum name");

        app.Runs(() =>
        {
            var view = new EvaGest.Views.Pages.TillView { DataContext = vm };
            view.Measure(new Size(1200, 1600));
            view.Arrange(new Rect(0, 0, 1200, 1600));
            view.UpdateLayout();

            view.ActualWidth.Should().BeGreaterThan(0);
            Descendants<TextBlock>(view).Select(t => t.Text)
                .Should().NotContain("12100,00", "that is cents printed as if they were euros");
        });
    }

    private static IEnumerable<T> Descendants<T>(DependencyObject root) where T : DependencyObject
    {
        int count = VisualTreeHelper.GetChildrenCount(root);
        for (int i = 0; i < count; i++)
        {
            var child = VisualTreeHelper.GetChild(root, i);
            if (child is T match) yield return match;
            foreach (var deeper in Descendants<T>(child)) yield return deeper;
        }
    }
}
