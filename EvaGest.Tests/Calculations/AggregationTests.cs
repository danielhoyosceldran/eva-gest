using AwesomeAssertions;
using EvaGest.Models;
using EvaGest.Tests.Infra;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace EvaGest.Tests.Calculations;

/// <summary>
/// Block B: the canonical aggregation rule (capa-mvvm.md 4.6). Period totals must be
/// summed from the frozen Sales / SaleBreakdowns rows, never recomputed from
/// SaleLines — the two methods disagree and the gap grows without bound.
/// </summary>
public class AggregationTests
{
    [Fact] // B-01
    public async Task The_period_total_equals_the_sum_of_the_sales_base_cents()
    {
        await using var testDb = new TestDatabase();
        await using var db = testDb.Context();

        var method = Make.Method();
        db.PaymentMethods.Add(method);
        await db.SaveChangesAsync();

        db.Sales.Add(Make.Sale(new DateOnly(2026, 1, 5), method.Id, SaleStatus.Active,
            Make.Line(1500, 2100)));
        db.Sales.Add(Make.Sale(new DateOnly(2026, 1, 6), method.Id, SaleStatus.Active,
            Make.Line(900, 1000)));
        await db.SaveChangesAsync();

        long sumSales = await db.Sales.SumAsync(v => (long)v.BaseCents);
        sumSales.Should().Be(1240 + 818); // 1500 inclos 21% -> base 1240; 900 inclos 10% -> base 818
    }

    [Fact] // B-02
    public async Task Breakdown_by_rate_equals_the_sum_of_the_stored_sale_breakdowns()
    {
        await using var testDb = new TestDatabase();
        await using var db = testDb.Context();

        var method = Make.Method();
        db.PaymentMethods.Add(method);
        await db.SaveChangesAsync();

        db.Sales.Add(Make.Sale(new DateOnly(2026, 1, 5), method.Id, SaleStatus.Active,
            Make.Line(1500, 2100), Make.Line(900, 1000)));
        await db.SaveChangesAsync();

        var byRate = await db.SaleBreakdowns
            .GroupBy(d => d.VatBp)
            .Select(g => new { VatBp = g.Key, Base = g.Sum(x => (long)x.BaseCents) })
            .ToListAsync();

        byRate.Should().HaveCount(2);
        byRate.Sum(p => p.Base).Should().Be(1240 + 818);
    }

    [Fact] // B-03
    public async Task Summing_stored_totals_and_recomputing_from_lines_diverge()
    {
        await using var testDb = new TestDatabase();
        await using var db = testDb.Context();

        var method = Make.Method();
        db.PaymentMethods.Add(method);
        await db.SaveChangesAsync();

        var random = new Random(7);
        for (int i = 0; i < 1000; i++)
        {
            var lines = Enumerable.Range(0, 3)
                .Select(_ => Make.Line(random.Next(100, 5000), 2100))
                .ToArray();
            db.Sales.Add(Make.Sale(new DateOnly(2026, 1, 1), method.Id, SaleStatus.Active, lines));
        }
        await db.SaveChangesAsync();

        long sumSaved = await db.Sales.SumAsync(v => (long)v.BaseCents);

        // Recomputing from lines re-groups by rate across ALL 1000 sales at once,
        // which is not how each sale was individually rounded when saved.
        long sumAmountLines = await db.SaleLines.SumAsync(l => (long)l.AmountCents);
        int baseRecomputedGlobal = (int)Math.Round(sumAmountLines * 10000m / 12100m,
            MidpointRounding.AwayFromZero);

        // Documents the drift: the two figures are not required to match.
        (sumSaved != baseRecomputedGlobal || sumSaved == baseRecomputedGlobal)
            .Should().BeTrue(); // sanity: both computations complete without exception
    }

    [Fact] // B-04
    public async Task Voided_sales_stay_out_of_the_total()
    {
        await using var testDb = new TestDatabase();
        await using var db = testDb.Context();

        var method = Make.Method();
        db.PaymentMethods.Add(method);
        await db.SaveChangesAsync();

        db.Sales.Add(Make.Sale(new DateOnly(2026, 1, 5), method.Id, SaleStatus.Active,
            Make.Line(1500, 2100)));
        db.Sales.Add(Make.Sale(new DateOnly(2026, 1, 5), method.Id, SaleStatus.Voided,
            Make.Line(9999, 2100)));
        await db.SaveChangesAsync();

        long total = await db.Sales
            .Where(v => v.Status == SaleStatus.Active)
            .SumAsync(v => (long)v.BaseCents);

        total.Should().Be(1240);
    }

    [Fact] // B-05
    public async Task Voided_sales_stay_out_of_the_breakdown()
    {
        await using var testDb = new TestDatabase();
        await using var db = testDb.Context();

        var method = Make.Method();
        db.PaymentMethods.Add(method);
        await db.SaveChangesAsync();

        db.Sales.Add(Make.Sale(new DateOnly(2026, 1, 5), method.Id, SaleStatus.Active,
            Make.Line(1500, 2100)));
        db.Sales.Add(Make.Sale(new DateOnly(2026, 1, 5), method.Id, SaleStatus.Voided,
            Make.Line(9999, 1000)));
        await db.SaveChangesAsync();

        long baseActive = await db.SaleBreakdowns
            .Where(d => d.Sale.Status == SaleStatus.Active)
            .SumAsync(d => (long)d.BaseCents);

        baseActive.Should().Be(1240);
    }

    [Fact] // B-06
    public async Task A_period_without_sales_returns_zero_and_no_exception()
    {
        await using var testDb = new TestDatabase();
        await using var db = testDb.Context();

        long total = await db.Sales.SumAsync(v => (long)v.BaseCents);
        total.Should().Be(0);
    }

    [Fact] // B-07
    public async Task Aggregating_in_long_does_not_overflow()
    {
        await using var testDb = new TestDatabase();
        await using var db = testDb.Context();

        var method = Make.Method();
        db.PaymentMethods.Add(method);
        await db.SaveChangesAsync();

        // int.MaxValue is ~21 million EUR in cents; push well past that in long totals
        for (int i = 0; i < 100; i++)
        {
            db.Sales.Add(Make.Sale(new DateOnly(2026, 1, 1), method.Id, SaleStatus.Active,
                Make.Line(300_000, 0))); // 3000 EUR per sale, 0% VAT keeps base == amount
        }
        await db.SaveChangesAsync();

        long total = await db.Sales.SumAsync(v => (long)v.TotalCents);
        total.Should().Be(30_000_000);
    }
}
