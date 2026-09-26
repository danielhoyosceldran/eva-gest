using AwesomeAssertions;
using EvaGest.Models;
using EvaGest.Services;
using EvaGest.Tests.Infra;
using EvaGest.ViewModels.Dialogs;
using EvaGest.ViewModels.Pages;
using Xunit;

namespace EvaGest.Tests.Services;

/// <summary>
/// A cash movement belongs to the day the money moved, and the dialog must behave the
/// same whichever page opened it. Both used to be wrong in opposite ways.
/// </summary>
public class MovementDateAndDefaultsTests
{
    private static (ICatalogService catalog, IWorkerService workers) Services(TestDatabase testDb)
        => (new CatalogService(new TestFactory(testDb.Options)),
            new WorkerService(new TestFactory(testDb.Options)));

    [Fact]
    public void A_movement_keeps_the_date_it_was_given_instead_of_today()
    {
        var lastMonth = new DateOnly(2026, 2, 17);
        var vm = new MovementDialogViewModel(MovementType.Out, lastMonth)
        {
            PriceText = "15,00",
            Concept = "Prod",
            Method = new PaymentMethod { Id = 1, Name = "Efectiu" }
        };

        vm.AModel().Date.Should().Be(lastMonth,
            "AModel used to stamp DateTime.Today over whatever period was on screen");
    }

    [Fact]
    public async Task The_till_dates_a_new_movement_inside_the_period_being_looked_at()
    {
        await using var testDb = new TestDatabase();
        var (catalog, workers) = Services(testDb);
        var settings = new TestSettings();

        // A period that ended before today: the movement must land in it, not today.
        var vm = await MovementDialogViewModel.New(
            MovementType.Out, new DateOnly(2026, 2, 28), catalog, workers, settings);

        vm.Date.Should().Be(new DateOnly(2026, 2, 28));
    }

    [Fact]
    public async Task The_dialog_follows_the_till_vat_settings_whichever_page_opened_it()
    {
        await using var testDb = new TestDatabase();
        var (catalog, workers) = Services(testDb);
        var settings = new TestSettings(
            (ConfigKeys.ApplyVatToTill, "1"),
            (ConfigKeys.DefaultVatBp, "1000"));

        // The Start page built this by hand and skipped LoadDefaults, so the split was
        // never offered there and the rate box kept its placeholder 21.
        var vm = await MovementDialogViewModel.New(
            MovementType.Out, DateOnly.FromDateTime(DateTime.Today), catalog, workers, settings);

        vm.ShowVatSplit.Should().BeTrue("the shop switched the VAT split on");
        vm.VatText.Should().Be("10", "the configured default rate, not the literal 21");
    }

    [Fact]
    public void The_work_percentage_column_is_formatted_in_the_interface_culture()
    {
        // Bound through a XAML StringFormat this printed "42.5%": a binding formats with
        // its own culture, which is en-US unless something sets it, and nothing does.
        var row = new ReportsViewModel.WorkerRankingRow(1, "Eva", 3, "1.250,00 €", 42.5m);

        row.WorkPercentText.Should().Be(
            42.5m.ToString("0.0", AppLanguage.Culture) + " %");
        row.WorkPercentText.Should().NotContain(".");
    }

    [Fact]
    public void A_worker_with_no_sales_shows_a_dash_rather_than_a_percentage()
    {
        new ReportsViewModel.WorkerRankingRow(1, "Eva", 0, "0,00 €", null)
            .WorkPercentText.Should().Be("-");
    }
}
