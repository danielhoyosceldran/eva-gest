using System.Globalization;
using AwesomeAssertions;
using EvaGest.Models;
using EvaGest.Services;
using EvaGest.Tests.Infra;
using Xunit;

namespace EvaGest.Tests.Services;

/// <summary>
/// The transactions table of the Reports page: sales and cash movements filtered
/// together. Checked against a real SQLite database, and its totals against the Till
/// page's, which must never disagree with it for the same period.
/// </summary>
public class TransactionsServiceTests
{
    private static readonly DateOnly Monday = new(2026, 9, 7);
    private static readonly DateOnly Sunday = Monday.AddDays(6);

    /// <summary>Ids of the shop <see cref="Seed"/> builds.</summary>
    private record Shop(
        int Cash, int Card, int Marta, int Laia, int Jose, int Anna,
        int Rent, int Cleaning,
        int HaircutSale, int MixedSale, int CustomSale, int VoidedSale, int CardSale,
        int CashIn, int RentOut, int VoidedOut);

    /// <summary>
    /// One week of a small shop:
    ///   Mon  José, haircut, cash, Marta                         15,00
    ///   Tue  Anna, haircut + wax ("Ceres"), cash, Laia          24,00
    ///   Wed  guest, custom line, card, unassigned               10,00
    ///   Wed  José, haircut, voided                              15,00
    ///   Thu  José, haircut, card, Marta                         15,00
    ///   Thu  cash-in 50,00 · cash-out rent 300,00 · voided cash-out 20,00
    ///   and one sale the week after, outside the period.
    /// </summary>
    private static async Task<Shop> Seed(TestDatabase testDb)
    {
        await using var db = testDb.Context();

        var cash = Make.Method("Efectiu");
        var card = Make.Method("Targeta");
        var marta = Make.Worker("Marta");
        var laia = Make.Worker("Laia");
        var jose = Make.Client("José Pérez");
        var anna = Make.Client("Anna Puig", "622222222");
        var haircut = Make.Service("Tall", 1500);
        var wax = Make.Product("Cera mat", 900);
        wax.Category = "Ceres";
        var rent = new ExpenseCategory { Name = "Lloguer" };
        var cleaning = new ExpenseCategory { Name = "Neteja" };
        db.AddRange(cash, card, marta, laia, jose, anna, haircut, wax, rent, cleaning);
        await db.SaveChangesAsync();

        SaleLine Haircut() => new()
        {
            ServiceId = haircut.Id, Description = "Tall", Quantity = 1,
            UnitPriceCents = 1500, VatBp = 2100, AmountCents = 1500
        };
        SaleLine Wax() => new()
        {
            ProductId = wax.Id, Description = "Cera mat", Quantity = 1,
            UnitPriceCents = 900, VatBp = 2100, AmountCents = 900
        };

        Sale ForClient(Sale s, Client c, Worker? w)
        {
            s.GuestName = null;
            s.ClientId = c.Id;
            s.WorkerId = w?.Id;
            return s;
        }

        var haircutSale = ForClient(Make.Sale(Monday, cash.Id, SaleStatus.Active, Haircut()), jose, marta);
        var mixedSale = ForClient(Make.Sale(Monday.AddDays(1), cash.Id, SaleStatus.Active, Haircut(), Wax()), anna, laia);
        var customSale = Make.Sale(Monday.AddDays(2), card.Id, SaleStatus.Active, Make.Line(1000));
        customSale.GuestName = "Passavolant";
        var voidedSale = ForClient(Make.Sale(Monday.AddDays(2), cash.Id, SaleStatus.Voided, Haircut()), jose, marta);
        var cardSale = ForClient(Make.Sale(Monday.AddDays(3), card.Id, SaleStatus.Active, Haircut()), jose, marta);
        cardSale.Notes = "Pagat amb el mòbil";
        var nextWeek = ForClient(Make.Sale(Sunday.AddDays(1), cash.Id, SaleStatus.Active, Haircut()), jose, marta);
        db.Sales.AddRange(haircutSale, mixedSale, customSale, voidedSale, cardSale, nextWeek);

        var cashIn = new CashMovement
        {
            Date = Monday.AddDays(3), Type = MovementType.In, AmountCents = 5000,
            PaymentMethodId = cash.Id, Concept = "Canvi inicial"
        };
        var rentOut = new CashMovement
        {
            Date = Monday.AddDays(3), Type = MovementType.Out, AmountCents = 30000,
            PaymentMethodId = card.Id, CategoryId = rent.Id, Concept = "Lloguer setembre"
        };
        var voidedOut = new CashMovement
        {
            Date = Monday.AddDays(3), Type = MovementType.Out, AmountCents = 2000,
            PaymentMethodId = cash.Id, CategoryId = cleaning.Id, Concept = "Lleixiu",
            Status = MovementStatus.Voided
        };
        db.CashMovements.AddRange(cashIn, rentOut, voidedOut);
        await db.SaveChangesAsync();

        return new Shop(cash.Id, card.Id, marta.Id, laia.Id, jose.Id, anna.Id, rent.Id, cleaning.Id,
            haircutSale.Id, mixedSale.Id, customSale.Id, voidedSale.Id, cardSale.Id,
            cashIn.Id, rentOut.Id, voidedOut.Id);
    }

    private static TransactionFilter Week => new(Monday, Sunday);

    private static async Task<(TransactionsService service, Shop shop, TestDatabase db)> Open()
    {
        var testDb = new TestDatabase();
        var shop = await Seed(testDb);
        return (new TransactionsService(new TestFactory(testDb.Options)), shop, testDb);
    }

    private static IEnumerable<(TransactionKind, int)> Ids(IEnumerable<TransactionEntry> entries)
        => entries.Select(e => (e.Kind, e.Id));

    [Fact]
    public async Task Without_filters_it_lists_the_active_sales_and_movements_of_the_period()
    {
        var (service, shop, db) = await Open();
        await using var _ = db;

        var found = await service.Search(Week);

        Ids(found).Should().BeEquivalentTo(new[]
        {
            (TransactionKind.Sale, shop.HaircutSale), (TransactionKind.Sale, shop.MixedSale),
            (TransactionKind.Sale, shop.CustomSale), (TransactionKind.Sale, shop.CardSale),
            (TransactionKind.CashIn, shop.CashIn), (TransactionKind.CashOut, shop.RentOut),
        });
    }

    [Fact]
    public async Task Rows_come_newest_first_with_a_days_movements_after_its_sales()
    {
        var (service, shop, db) = await Open();
        await using var _ = db;

        var found = await service.Search(Week);

        found.Select(e => e.Date).Should().BeInDescendingOrder();
        found.First().Should().Match<TransactionEntry>(e => e.Kind == TransactionKind.Sale && e.Id == shop.CardSale);
        found.Last().Id.Should().Be(shop.HaircutSale);
    }

    [Fact]
    public async Task Voided_rows_are_listed_only_when_asked_for_and_never_counted()
    {
        var (service, shop, db) = await Open();
        await using var _ = db;

        var withVoided = await service.Search(Week with { IncludeVoided = true });

        Ids(withVoided).Should().Contain((TransactionKind.Sale, shop.VoidedSale))
            .And.Contain((TransactionKind.CashOut, shop.VoidedOut));
        withVoided.Where(e => e.Voided).Should().HaveCount(2);

        var totals = TransactionTotals.From(withVoided);
        totals.Should().Be(TransactionTotals.From(await service.Search(Week)));
    }

    [Theory]
    [InlineData(TransactionKind.Sale, 4)]
    [InlineData(TransactionKind.CashIn, 1)]
    [InlineData(TransactionKind.CashOut, 1)]
    public async Task The_type_filter_keeps_only_that_kind(TransactionKind kind, int expected)
    {
        var (service, _, db) = await Open();
        await using var __ = db;

        var found = await service.Search(Week with { Kind = kind });

        found.Should().HaveCount(expected).And.OnlyContain(e => e.Kind == kind);
    }

    [Fact]
    public async Task Choosing_a_client_leaves_out_the_cash_movements_which_have_none()
    {
        var (service, shop, db) = await Open();
        await using var _ = db;

        var found = await service.Search(Week with { ClientId = shop.Jose });

        Ids(found).Should().BeEquivalentTo(new[]
        {
            (TransactionKind.Sale, shop.HaircutSale), (TransactionKind.Sale, shop.CardSale)
        });
    }

    [Fact]
    public async Task Method_and_worker_filter_sales_and_movements_alike()
    {
        var (service, shop, db) = await Open();
        await using var _ = db;

        var byCard = await service.Search(Week with { PaymentMethodId = shop.Card });
        Ids(byCard).Should().BeEquivalentTo(new[]
        {
            (TransactionKind.Sale, shop.CustomSale), (TransactionKind.Sale, shop.CardSale),
            (TransactionKind.CashOut, shop.RentOut)
        });

        var byLaia = await service.Search(Week with { WorkerId = shop.Laia });
        Ids(byLaia).Should().Equal((TransactionKind.Sale, shop.MixedSale));
    }

    [Fact]
    public async Task A_sale_with_a_service_and_a_product_matches_both_categories()
    {
        var (service, shop, db) = await Open();
        await using var _ = db;

        var services = await service.Search(Week with { Category = TransactionCategory.Of(LineType.Service) });
        var products = await service.Search(Week with { Category = TransactionCategory.Of(LineType.Product) });
        var other = await service.Search(Week with { Category = TransactionCategory.Of(LineType.Other) });

        services.Select(e => e.Id).Should().BeEquivalentTo(new[] { shop.HaircutSale, shop.MixedSale, shop.CardSale });
        products.Select(e => e.Id).Should().Equal(shop.MixedSale);
        other.Select(e => e.Id).Should().Equal(shop.CustomSale);

        // The row is still the whole sale at its frozen total, not the product's share
        products.Single().TotalCents.Should().Be(2400);
    }

    [Fact]
    public async Task A_product_category_matches_ignoring_case_and_blanks()
    {
        var (service, shop, db) = await Open();
        await using var _ = db;

        var found = await service.Search(Week with { Category = TransactionCategory.OfProducts(" ceres ") });

        found.Select(e => e.Id).Should().Equal(shop.MixedSale);
    }

    [Fact]
    public async Task An_expense_category_matches_only_the_movements_filed_under_it()
    {
        var (service, shop, db) = await Open();
        await using var _ = db;

        var rent = await service.Search(Week with { Category = TransactionCategory.OfExpense(shop.Rent) });
        Ids(rent).Should().Equal((TransactionKind.CashOut, shop.RentOut));

        // A voided cleaning expense is the only one in its category, so only shown on request
        (await service.Search(Week with { Category = TransactionCategory.OfExpense(shop.Cleaning) }))
            .Should().BeEmpty();
    }

    [Theory]
    [InlineData("jose")]          // no accent, lower case: finds "José Pérez"
    [InlineData("PÉREZ")]
    public async Task Text_search_ignores_case_and_accents(string text)
    {
        var (service, shop, db) = await Open();
        await using var _ = db;

        var found = await service.Search(Week with { Text = text });

        found.Select(e => e.Id).Should().BeEquivalentTo(new[] { shop.HaircutSale, shop.CardSale });
    }

    [Fact]
    public async Task Text_search_looks_in_lines_notes_and_movement_concepts()
    {
        var (service, shop, db) = await Open();
        await using var _ = db;

        (await service.Search(Week with { Text = "cera" })).Select(e => e.Id).Should().Equal(shop.MixedSale);
        (await service.Search(Week with { Text = "mobil" })).Select(e => e.Id).Should().Equal(shop.CardSale);
        Ids(await service.Search(Week with { Text = "setembre" })).Should().Equal((TransactionKind.CashOut, shop.RentOut));
    }

    [Fact]
    public async Task The_totals_agree_with_the_till_for_the_same_period()
    {
        var (service, _, db) = await Open();
        await using var __ = db;

        var totals = TransactionTotals.From(await service.Search(Week with { IncludeVoided = true }));
        var till = await new TillService(new TestFactory(db.Options)).Summary(Monday, Sunday);

        totals.SalesCents.Should().Be(till.SalesCents);
        totals.CashInCents.Should().Be(till.CashInCents);
        totals.CashOutCents.Should().Be(till.CashOutCents);
        totals.NetCents.Should().Be(till.BalanceCents);
        totals.SalesBaseCents.Should().Be(till.BaseCents);
        totals.SalesVatCents.Should().Be(till.VatCents);
        totals.Count.Should().Be(6);
    }

    [Fact]
    public async Task A_cash_out_counts_negative_in_the_net()
    {
        var (service, _, db) = await Open();
        await using var __ = db;

        var totals = TransactionTotals.From(await service.Search(Week));

        // 15 + 24 + 10 + 15 sold, 50 in, 300 out
        totals.IncomeCents.Should().Be(6400 + 5000);
        totals.NetCents.Should().Be(6400 + 5000 - 30000);
    }

    [Fact]
    public async Task The_filter_options_include_product_categories_once_each()
    {
        await using var testDb = new TestDatabase();
        await Seed(testDb);
        await using (var db = testDb.Context())
        {
            var other = Make.Product("Cera brillant");
            other.Category = "ceres ";
            var blank = Make.Product("Pinta");
            blank.Category = "  ";
            db.Products.AddRange(other, blank);
            await db.SaveChangesAsync();
        }

        var options = await new TransactionsService(new TestFactory(testDb.Options)).GetFilterOptions();

        options.ProductCategories.Should().Equal("Ceres");
        options.ExpenseCategories.Select(c => c.Name).Should().Equal("Lloguer", "Neteja");
        options.Clients.Should().HaveCount(2);
    }

    // ── Export ───────────────────────────────────────────────────────────────

    [Fact]
    public async Task The_exported_table_sums_to_the_net_of_the_period()
    {
        var (service, _, db) = await Open();
        await using var __ = db;
        string folder = Path.Combine(Path.GetTempPath(), "EvaGestTransactionsExport_" + Guid.NewGuid());

        try
        {
            var found = await service.Search(Week);
            string path = await new ExportService(new TestFactory(db.Options))
                .ExportTransactions(found, Monday, Sunday, folder);

            Path.GetFileName(path).Should().Be("transaccions_20260907-20260913.csv");
            var rows = (await File.ReadAllLinesAsync(path)).Skip(1).Where(l => l.Length > 0).ToList();
            rows.Should().HaveCount(found.Count);

            var culture = CultureInfo.GetCultureInfo("ca-ES");
            decimal net = rows.Sum(r => decimal.Parse(r.Split(';')[10], culture));
            net.Should().Be(TransactionTotals.From(found).NetCents / 100m);
        }
        finally
        {
            if (Directory.Exists(folder)) Directory.Delete(folder, recursive: true);
        }
    }
}
