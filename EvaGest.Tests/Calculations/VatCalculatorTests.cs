using AwesomeAssertions;
using EvaGest.Models;
using EvaGest.Services;
using Xunit;

namespace EvaGest.Tests.Calculations;

public class VatCalculatorTests
{
    private static SaleLine Line(int amountCents, int vatBp = 2100, int quantity = 1)
        => new()
        {
            Description = "Prova",
            Quantity = quantity,
            UnitPriceCents = amountCents / quantity,
            VatBp = vatBp,
            AmountCents = amountCents
        };

    [Fact] // A-01
    public void A_line_of_15_euros_at_21_percent_vat_included()
    {
        var r = VatCalculator.Compute([Line(1500, 2100)], VatMode.Included);
        r.BaseCents.Should().Be(1240);
        r.VatCents.Should().Be(260);
        r.TotalCents.Should().Be(1500);
    }

    [Fact] // A-02
    public void A_line_of_15_euros_at_21_percent_vat_excluded()
    {
        var r = VatCalculator.Compute([Line(1500, 2100)], VatMode.NotIncluded);
        r.BaseCents.Should().Be(1500);
        r.VatCents.Should().Be(315);
        r.TotalCents.Should().Be(1815);
    }

    [Fact] // A-03
    public void Three_lines_of_10_at_21_percent_included_are_not_rounded_per_line()
    {
        var lines = new[] { Line(1000), Line(1000), Line(1000) };
        var r = VatCalculator.Compute(lines, VatMode.Included);
        r.BaseCents.Should().Be(2479);
        r.VatCents.Should().Be(521);
    }

    [Fact] // A-04
    public void Mixed_rates_are_computed_separately_and_then_summed()
    {
        var lines = new[] { Line(1500, 2100), Line(1000, 1000) };
        var byRate = VatCalculator.ComputeByRate(lines, VatMode.NotIncluded);
        byRate.Should().HaveCount(2);

        var r = VatCalculator.Compute(lines, VatMode.NotIncluded);
        r.BaseCents.Should().Be(byRate.Sum(p => p.BaseCents));
        r.VatCents.Should().Be(byRate.Sum(p => p.VatCents));
    }

    [Fact] // A-05
    public void A_sale_of_zero_does_not_crash()
    {
        var r = VatCalculator.Compute([Line(0, 2100)], VatMode.Included);
        r.BaseCents.Should().Be(0);
        r.VatCents.Should().Be(0);
        r.TotalCents.Should().Be(0);
    }

    [Fact] // A-06
    public void An_amount_of_one_cent_at_21_percent()
    {
        var r = VatCalculator.Compute([Line(1, 2100)], VatMode.Included);
        r.BaseCents.Should().Be(1);
        r.VatCents.Should().Be(0);
    }

    [Fact] // A-07
    public void A_vat_rate_of_zero_percent()
    {
        var r = VatCalculator.Compute([Line(1500, 0)], VatMode.Included);
        r.BaseCents.Should().Be(1500);
        r.VatCents.Should().Be(0);
        r.TotalCents.Should().Be(1500);
    }

    [Fact] // A-08
    public void The_5_2_percent_surcharge_keeps_its_precision()
    {
        var r = VatCalculator.Compute([Line(1500, 520)], VatMode.NotIncluded);
        (r.BaseCents + r.VatCents).Should().Be(r.TotalCents);
    }

    [Fact] // A-09
    public void A_large_amount_of_a_million_euros()
    {
        var r = VatCalculator.Compute([Line(100_000_000, 2100)], VatMode.Included);
        r.BaseCents.Should().Be(82644628);
        r.VatCents.Should().Be(17355372);
    }

    [Fact] // A-10
    public void A_quantity_greater_than_one()
    {
        var line = Line(2700, 2100, quantity: 3);
        line.AmountCents.Should().Be(2700);
        line.UnitPriceCents.Should().Be(900);
    }

    [Fact] // A-11
    public void The_invariant_base_plus_vat_equals_total_holds_for_random_combinations()
    {
        var random = new Random(12345);
        int[] type = [2100, 1000, 400, 520, 0];

        for (int i = 0; i < 1000; i++)
        {
            var mode = i % 2 == 0 ? VatMode.Included : VatMode.NotIncluded;
            var lines = Enumerable.Range(0, random.Next(1, 6))
                .Select(_ => Line(random.Next(0, 500_000), type[random.Next(type.Length)]))
                .ToList();

            var r = VatCalculator.Compute(lines, mode);
            (r.BaseCents + r.VatCents).Should().Be(r.TotalCents);
        }
    }

    [Fact] // A-12
    public void Half_a_cent_always_rounds_up()
    {
        // 21% of 50 = 10.5 -> must round to 11, not banker's (10)
        var r = VatCalculator.Compute([Line(50, 2100)], VatMode.NotIncluded);
        r.VatCents.Should().Be(11);
    }

    [Fact] // A-13
    public void The_order_of_the_lines_does_not_affect_the_result()
    {
        var a = new[] { Line(1500, 2100), Line(900, 1000) };
        var b = new[] { Line(900, 1000), Line(1500, 2100) };

        VatCalculator.Compute(a, VatMode.Included).Should()
            .Be(VatCalculator.Compute(b, VatMode.Included));
    }

    [Fact] // A-14
    public void ToBreakdownRows_generates_one_row_per_rate_present()
    {
        var lines = new[] { Line(1500, 2100), Line(900, 1000) };
        var records = VatCalculator.ToBreakdownRows(lines, VatMode.Included);
        records.Should().HaveCount(2);
    }

    [Fact] // A-15
    public void The_sum_of_the_breakdown_rows_equals_the_sale_totals()
    {
        var lines = new[] { Line(1500, 2100), Line(900, 1000), Line(300, 2100) };
        var totals = VatCalculator.Compute(lines, VatMode.Included);
        var records = VatCalculator.ToBreakdownRows(lines, VatMode.Included);

        records.Sum(r => r.BaseCents).Should().Be(totals.BaseCents);
        records.Sum(r => r.VatCents).Should().Be(totals.VatCents);
        records.Sum(r => r.TotalCents).Should().Be(totals.TotalCents);
    }
}
