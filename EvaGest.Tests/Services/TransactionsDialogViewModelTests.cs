using AwesomeAssertions;
using EvaGest.Models;
using EvaGest.Resources;
using EvaGest.Services;
using EvaGest.Tests.Infra;
using EvaGest.ViewModels.Dialogs;
using EvaGest.ViewModels.Pages;
using Xunit;

namespace EvaGest.Tests.Services;

/// <summary>The transactions window: what its filters, shortcuts, footer and export do.</summary>
public class TransactionsDialogViewModelTests
{
    // A Wednesday, so "this week" and "this month" differ from "today"
    private static readonly DateOnly Today = new(2026, 9, 9);

    private static async Task<(TestDatabase db, int methodId, int categoryId)> ShopWithAWeek()
    {
        var testDb = new TestDatabase();
        await using var db = testDb.Context();
        var method = Make.Method();
        var rent = new ExpenseCategory { Name = "Lloguer" };
        db.AddRange(method, rent);
        await db.SaveChangesAsync();

        db.Sales.Add(Make.Sale(Today, method.Id, SaleStatus.Active, Make.Line(2000)));
        db.Sales.Add(Make.Sale(Today.AddDays(-30), method.Id, SaleStatus.Active, Make.Line(4000)));
        db.CashMovements.Add(new CashMovement
        {
            Date = Today, Type = MovementType.Out, AmountCents = 1250, PaymentMethodId = method.Id,
            CategoryId = rent.Id, Concept = "Lloguer"
        });
        await db.SaveChangesAsync();
        return (testDb, method.Id, rent.Id);
    }

    private static TransactionsDialogViewModel Dialog(TestDatabase db, TestDialogService? dialogs = null)
        => new(new TransactionsService(new TestFactory(db.Options)), new ExportService(new TestFactory(db.Options)),
               dialogs ?? new TestDialogService(), Today);

    [Fact]
    public async Task It_opens_on_this_month_with_the_rows_and_totals_of_that_month()
    {
        var (db, _, _) = await ShopWithAWeek();
        await using var _ = db;
        var vm = Dialog(db);

        await vm.Load();

        vm.From.Should().Be(new DateOnly(2026, 9, 1));
        vm.To.Should().Be(Today);
        vm.Rows.Should().HaveCount(2);
        vm.CountText.Should().Be(string.Format(Texts.TransactionsCount, 2));
        vm.NetText.Should().Be(Money.Format(2000 - 1250));
        vm.HasNoResults.Should().BeFalse();
        vm.ErrorValidation.Should().BeNull();
    }

    [Fact]
    public async Task A_cash_out_row_reads_negative_and_is_marked_as_money_out()
    {
        var (db, _, _) = await ShopWithAWeek();
        await using var _ = db;
        var vm = Dialog(db);

        await vm.Load();

        var row = vm.Rows.Single(r => r.Entry.Kind == TransactionKind.CashOut);
        row.IsMoneyOut.Should().BeTrue();
        row.TotalText.Should().Be(Money.Format(-1250));
        row.SignedTotalCents.Should().Be(-1250);
        row.CategoryText.Should().Be("Lloguer");
        row.BaseText.Should().Be("—", "this movement's VAT was never split");
    }

    [Fact]
    public async Task The_category_box_lists_line_types_then_expense_categories()
    {
        var (db, _, rentId) = await ShopWithAWeek();
        await using var _ = db;
        var vm = Dialog(db);

        await vm.Load();

        vm.FilterCategories.Select(c => c.Label).Should().Equal(
            Texts.Services, Texts.Products, Texts.OtherConcepts,
            string.Format(Texts.ExpenseCategoryOption, "Lloguer"));
        vm.FilterCategories.Last().Value.Should().Be(TransactionCategory.OfExpense(rentId));
    }

    [Fact]
    public async Task Choosing_a_type_segment_filters_the_table()
    {
        var (db, _, _) = await ShopWithAWeek();
        await using var _ = db;
        var vm = Dialog(db);
        await vm.Load();

        vm.KindOptions.Single(o => o.Kind == TransactionKind.CashOut).IsSelected = true;
        await vm.Search();

        vm.Kind.Should().Be(TransactionKind.CashOut);
        vm.KindOptions.Count(o => o.IsSelected).Should().Be(1);
        vm.Rows.Should().ContainSingle().Which.Entry.Kind.Should().Be(TransactionKind.CashOut);
    }

    [Fact]
    public async Task The_period_shortcuts_count_from_today()
    {
        var (db, _, _) = await ShopWithAWeek();
        await using var _ = db;
        var vm = Dialog(db);
        await vm.Load();

        await vm.SelectThisYearCommand.ExecuteAsync(null);
        (vm.From, vm.To).Should().Be((new DateOnly(2026, 1, 1), Today));
        vm.Rows.Should().HaveCount(3);

        await vm.SelectThisWeekCommand.ExecuteAsync(null);
        (vm.From, vm.To).Should().Be((new DateOnly(2026, 9, 7), Today));

        await vm.SelectTodayCommand.ExecuteAsync(null);
        (vm.From, vm.To).Should().Be((Today, Today));
    }

    [Fact]
    public async Task Clearing_the_filters_keeps_the_period()
    {
        var (db, methodId, _) = await ShopWithAWeek();
        await using var _ = db;
        var vm = Dialog(db);
        await vm.Load();
        await vm.SelectThisYearCommand.ExecuteAsync(null);

        vm.PaymentMethod = vm.FilterMethods.Single(m => m.Id == methodId);
        vm.SearchText = "nothing matches this";
        vm.KindOptions[1].IsSelected = true;
        await vm.Search();
        vm.HasNoResults.Should().BeTrue();

        await vm.ClearFiltersCommand.ExecuteAsync(null);

        vm.PaymentMethod.Should().BeNull();
        vm.Kind.Should().BeNull();
        vm.SearchText.Should().BeEmpty();
        vm.KindOptions[0].IsSelected.Should().BeTrue();
        vm.From.Should().Be(new DateOnly(2026, 1, 1));
        vm.Rows.Should().HaveCount(3);
    }

    [Fact]
    public async Task Exporting_an_empty_table_says_what_to_do_instead()
    {
        var (db, _, _) = await ShopWithAWeek();
        await using var _ = db;
        var dialogs = new TestDialogService { FolderChosen = Path.GetTempPath() };
        var vm = Dialog(db, dialogs);
        await vm.Load();
        vm.SearchText = "nothing matches this";
        await vm.Search();

        await vm.ExportCommand.ExecuteAsync(null);

        vm.ErrorValidation.Should().Be(Texts.ExportTableEmpty);
        dialogs.InfoDisplayed.Should().BeEmpty();
    }

    [Fact]
    public async Task Exporting_writes_the_rows_on_screen_and_says_where()
    {
        var (db, _, _) = await ShopWithAWeek();
        await using var _ = db;
        string folder = Path.Combine(Path.GetTempPath(), "EvaGestTransactionsDialog_" + Guid.NewGuid());
        var dialogs = new TestDialogService { FolderChosen = folder };
        var vm = Dialog(db, dialogs);
        await vm.Load();

        try
        {
            await vm.ExportCommand.ExecuteAsync(null);

            vm.ErrorValidation.Should().BeNull();
            dialogs.InfoDisplayed.Should().ContainSingle();
            var file = Directory.GetFiles(folder).Should().ContainSingle().Subject;
            File.ReadAllLines(file).Where(l => l.Length > 0).Should().HaveCount(1 + vm.Rows.Count);
        }
        finally
        {
            if (Directory.Exists(folder)) Directory.Delete(folder, recursive: true);
        }
    }

    [Fact]
    public async Task A_search_that_fails_is_said_in_the_window_and_the_next_one_clears_it()
    {
        var (db, _, _) = await ShopWithAWeek();
        await using var _ = db;
        var service = FailingService.Wrap<ITransactionsService>(
            new TransactionsService(new TestFactory(db.Options)), out var failing);
        var vm = new TransactionsDialogViewModel(service, new ExportService(new TestFactory(db.Options)),
            new TestDialogService(), Today);
        await vm.Load();

        failing.Failing = true;
        await vm.Search();
        vm.ErrorValidation.Should().Be(Texts.TransactionsNotLoaded);

        failing.Failing = false;
        await vm.Search();
        vm.ErrorValidation.Should().BeNull();
        vm.Rows.Should().HaveCount(2);
    }

    [Fact]
    public async Task The_reports_page_opens_the_window_already_loaded()
    {
        var (db, _, _) = await ShopWithAWeek();
        await using var _ = db;
        var factory = new TestFactory(db.Options);
        var dialogs = new TestDialogService();
        var page = new ReportsViewModel(new ReportsService(factory), new TransactionsService(factory),
            new ExportService(factory), dialogs);

        await page.ShowTransactionsCommand.ExecuteAsync(null);

        var shown = dialogs.DialogsDisplayed.Should().ContainSingle().Which
            .Should().BeOfType<TransactionsDialogViewModel>().Subject;
        shown.FillsScreen.Should().BeTrue();
        shown.FilterMethods.Should().NotBeEmpty();
    }
}
