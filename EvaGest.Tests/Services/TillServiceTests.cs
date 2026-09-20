using AwesomeAssertions;
using EvaGest.Models;
using EvaGest.Services;
using EvaGest.Tests.Infra;
using Xunit;

namespace EvaGest.Tests.Services;

/// <summary>Block H: till i balanç. Canonical rule: everything is summed from the
/// frozen Sales / SaleBreakdowns rows, never recomputed from SaleLines.</summary>
public class TillServiceTests
{
    private static readonly DateOnly Today = new(2026, 9, 7);

    private static TillService CreatesService(TestDatabase testDb) => new(new TestFactory(testDb.Options));

    private static async Task<int> AddsMethod(TestDatabase testDb)
    {
        await using var db = testDb.Context();
        var m = Make.Method();
        db.PaymentMethods.Add(m);
        await db.SaveChangesAsync();
        return m.Id;
    }

    [Fact] // H-01
    public async Task Sales_plus_cash_in_minus_cash_out_equals_the_balance()
    {
        await using var testDb = new TestDatabase();
        int methodId = await AddsMethod(testDb);
        await using (var db = testDb.Context())
        {
            db.Sales.Add(Make.Sale(Today, methodId, SaleStatus.Active, Make.Line(1500, 2100)));
            db.CashMovements.Add(new CashMovement
            {
                Date = Today, Type = MovementType.In, AmountCents = 500, PaymentMethodId = methodId, Concept = "Entrada"
            });
            db.CashMovements.Add(new CashMovement
            {
                Date = Today, Type = MovementType.Out, AmountCents = 300, PaymentMethodId = methodId, Concept = "Sortida"
            });
            await db.SaveChangesAsync();
        }

        var till = CreatesService(testDb);
        var summary = await till.Summary(Today, Today);

        summary.BalanceCents.Should().Be(summary.SalesCents + summary.CashInCents - summary.CashOutCents);
        summary.BalanceCents.Should().Be(1500 + 500 - 300);
    }

    [Fact] // H-02
    public async Task Today_with_three_sales_and_one_outgoing_movement()
    {
        await using var testDb = new TestDatabase();
        int methodId = await AddsMethod(testDb);
        await using (var db = testDb.Context())
        {
            db.Sales.Add(Make.Sale(Today, methodId, SaleStatus.Active, Make.Line(1000, 2100)));
            db.Sales.Add(Make.Sale(Today, methodId, SaleStatus.Active, Make.Line(1000, 2100)));
            db.Sales.Add(Make.Sale(Today, methodId, SaleStatus.Active, Make.Line(1000, 2100)));
            db.CashMovements.Add(new CashMovement
            {
                Date = Today, Type = MovementType.Out, AmountCents = 500, PaymentMethodId = methodId, Concept = "Sortida"
            });
            await db.SaveChangesAsync();
        }

        var till = CreatesService(testDb);
        var summary = await till.Summary(Today, Today);

        summary.SalesCents.Should().Be(3000);
        summary.CashOutCents.Should().Be(500);
        summary.BalanceCents.Should().Be(2500);
    }

    [Fact] // H-04
    public async Task A_custom_period_includes_both_ends()
    {
        await using var testDb = new TestDatabase();
        int methodId = await AddsMethod(testDb);
        var to = Today.AddDays(5);
        await using (var db = testDb.Context())
        {
            db.Sales.Add(Make.Sale(Today, methodId, SaleStatus.Active, Make.Line(1000, 2100)));
            db.Sales.Add(Make.Sale(to, methodId, SaleStatus.Active, Make.Line(1000, 2100)));
            db.Sales.Add(Make.Sale(to.AddDays(1), methodId, SaleStatus.Active, Make.Line(9999, 2100)));
            await db.SaveChangesAsync();
        }

        var till = CreatesService(testDb);
        var summary = await till.Summary(Today, to);

        summary.SalesCents.Should().Be(2000);
    }

    [Fact] // H-05
    public async Task A_movement_carries_no_vat_when_the_setting_is_off()
    {
        await using var testDb = new TestDatabase();
        int methodId = await AddsMethod(testDb);
        await using var db = testDb.Context();
        db.CashMovements.Add(new CashMovement
        {
            Date = Today, Type = MovementType.In, AmountCents = 1000, PaymentMethodId = methodId, Concept = "Propina"
        });
        await db.SaveChangesAsync();

        var movement = db.CashMovements.Single();
        movement.BaseCents.Should().BeNull();
        movement.VatCents.Should().BeNull();
    }

    [Fact] // H-06
    public void A_movement_with_vat_saves_the_computed_base_and_quota()
    {
        var vm = new EvaGest.ViewModels.Dialogs.MovementDialogViewModel(MovementType.In)
        {
            PriceText = "15,00", VatText = "21", SplitVat = true,
            Method = new PaymentMethod { Id = 1, Name = "Efectiu" }
        };

        var model = vm.AModel();
        model.BaseCents.Should().Be(1240);
        model.VatCents.Should().Be(260);
    }

    [Fact] // H-08
    public async Task An_outgoing_movement_subtracts_from_the_balance()
    {
        await using var testDb = new TestDatabase();
        int methodId = await AddsMethod(testDb);
        await using (var db = testDb.Context())
        {
            db.CashMovements.Add(new CashMovement
            {
                Date = Today, Type = MovementType.In, AmountCents = 1000, PaymentMethodId = methodId, Concept = "E"
            });
            db.CashMovements.Add(new CashMovement
            {
                Date = Today, Type = MovementType.Out, AmountCents = 400, PaymentMethodId = methodId, Concept = "S"
            });
            await db.SaveChangesAsync();
        }

        var till = CreatesService(testDb);
        var summary = await till.Summary(Today, Today);

        summary.BalanceCents.Should().Be(1000 - 400);
    }

    [Fact] // H-09
    public async Task Breakdown_by_rate_has_one_row_per_rate_present()
    {
        await using var testDb = new TestDatabase();
        int methodId = await AddsMethod(testDb);
        await using (var db = testDb.Context())
        {
            db.Sales.Add(Make.Sale(Today, methodId, SaleStatus.Active, Make.Line(1500, 2100)));
            db.Sales.Add(Make.Sale(Today, methodId, SaleStatus.Active, Make.Line(900, 1000)));
            await db.SaveChangesAsync();
        }

        var till = CreatesService(testDb);
        var summary = await till.Summary(Today, Today);

        summary.VatBreakdown.Should().HaveCount(2);
    }

    [Fact] // B-04 reinforced: voided sales are excluded from the till balance
    public async Task Voided_sales_are_excluded_from_the_till_summary()
    {
        await using var testDb = new TestDatabase();
        int methodId = await AddsMethod(testDb);
        await using (var db = testDb.Context())
        {
            db.Sales.Add(Make.Sale(Today, methodId, SaleStatus.Active, Make.Line(1500, 2100)));
            db.Sales.Add(Make.Sale(Today, methodId, SaleStatus.Voided, Make.Line(9999, 2100)));
            await db.SaveChangesAsync();
        }

        var till = CreatesService(testDb);
        var summary = await till.Summary(Today, Today);

        summary.SalesCents.Should().Be(1500);
    }

    /// <summary>
    /// The aggregate types are long precisely so a period total does not depend on
    /// turnover staying under int.MaxValue (21.474.836,47 EUR). The per-rate rows used
    /// to be summed as long and then cast straight back to int on the way out, which
    /// put that ceiling back at the one point the figure reached the screen.
    /// </summary>
    [Fact] // H-09 companion: a period total above int.MaxValue survives intact
    public async Task A_period_total_above_int_max_is_not_truncated()
    {
        await using var testDb = new TestDatabase();
        int methodId = await AddsMethod(testDb);

        // Two sales that each fit in int cents but whose sum does not.
        const int huge = 1_500_000_000;
        const long expected = 2L * huge;
        expected.Should().BeGreaterThan(int.MaxValue, "otherwise this test proves nothing");

        await using (var db = testDb.Context())
        {
            db.Sales.Add(Make.Sale(Today, methodId, SaleStatus.Active, Make.Line(huge, 2100)));
            db.Sales.Add(Make.Sale(Today, methodId, SaleStatus.Active, Make.Line(huge, 2100)));
            await db.SaveChangesAsync();
        }

        var till = CreatesService(testDb);
        var summary = await till.Summary(Today, Today);

        summary.SalesCents.Should().Be(expected);
        summary.BalanceCents.Should().Be(expected);

        // The single 21% row carries the whole period, so this is where the cast bit.
        summary.VatBreakdown.Should().ContainSingle()
            .Which.TotalCents.Should().Be(expected);
        summary.VatBreakdown.Sum(r => r.BaseCents + r.VatCents).Should().Be(expected);
    }

    [Fact] // H-03/H-06 companion: a period with neither movements nor sales returns zero
    public async Task An_empty_period_returns_zero_and_no_exception()
    {
        await using var testDb = new TestDatabase();
        var till = CreatesService(testDb);

        var summary = await till.Summary(Today, Today);

        summary.SalesCents.Should().Be(0);
        summary.BalanceCents.Should().Be(0);
        summary.VatBreakdown.Should().BeEmpty();
    }
}
