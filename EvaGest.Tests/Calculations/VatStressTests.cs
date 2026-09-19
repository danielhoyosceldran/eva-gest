using AwesomeAssertions;
using EvaGest.Models;
using EvaGest.Services;
using EvaGest.Tests.Infra;
using Xunit;

namespace EvaGest.Tests.Calculations;

/// <summary>
/// Block H — VAT under stress. The per-sale tests elsewhere check known figures; these
/// attack the invariants with the awkward cases: rates that do not divide cleanly, long
/// tickets, mixed rates, and the exact midpoints where rounding decides.
///
/// Two invariants must hold for every input, because the whole accounting model rests on
/// them: BaseCents + VatCents == TotalCents, and TotalCents is the exact sum of the line
/// amounts when prices include VAT (VAT-included means the customer pays the marked price,
/// so the total can never drift from what was charged).
/// </summary>
public class VatStressTests
{
    private static readonly int[] RealRates = [0, 400, 520, 1000, 2100];

    [Fact]
    public void The_breakdown_always_adds_up_across_a_thousand_random_tickets()
    {
        var random = new Random(20260919);   // fixed seed: a failure must be reproducible

        for (int ticket = 0; ticket < 1000; ticket++)
        {
            var lines = Enumerable.Range(0, random.Next(1, 12))
                .Select(_ => Make.Line(random.Next(1, 500_000), RealRates[random.Next(RealRates.Length)]))
                .ToList();

            var included = VatCalculator.Compute(lines, VatMode.Included);
            (included.BaseCents + included.VatCents).Should().Be(included.TotalCents,
                "a VAT-included breakdown must reconcile");
            included.TotalCents.Should().Be(lines.Sum(l => l.AmountCents),
                "with VAT included the customer pays exactly the marked prices");

            var excluded = VatCalculator.Compute(lines, VatMode.NotIncluded);
            (excluded.BaseCents + excluded.VatCents).Should().Be(excluded.TotalCents,
                "a VAT-excluded breakdown must reconcile");
            excluded.BaseCents.Should().Be(lines.Sum(l => l.AmountCents),
                "with VAT excluded the marked prices are the taxable base");
        }
    }

    [Fact]
    public void The_per_rate_rows_always_sum_back_to_the_sale_total()
    {
        // The quarterly export sums the stored per-rate rows. If those ever disagreed with
        // the sale's own frozen total, the Modelo 303 figures would not match the books.
        var random = new Random(7);

        for (int ticket = 0; ticket < 500; ticket++)
        {
            var lines = Enumerable.Range(0, random.Next(1, 10))
                .Select(_ => Make.Line(random.Next(1, 200_000), RealRates[random.Next(RealRates.Length)]))
                .ToList();

            foreach (var mode in new[] { VatMode.Included, VatMode.NotIncluded })
            {
                var total = VatCalculator.Compute(lines, mode);
                var rows = VatCalculator.ToBreakdownRows(lines, mode);

                rows.Sum(r => r.BaseCents).Should().Be(total.BaseCents);
                rows.Sum(r => r.VatCents).Should().Be(total.VatCents);
                rows.Sum(r => r.TotalCents).Should().Be(total.TotalCents);
            }
        }
    }

    [Fact]
    public void Grouping_per_rate_is_not_the_same_as_rounding_each_line()
    {
        // This is the reason ComputeByRate groups at all. Three lines of 3,33 € at 21 %:
        // rounded individually the bases sum to one cent less than the grouped base.
        // If this test ever stops finding a difference the grouping has been undone.
        var lines = new[] { Make.Line(333), Make.Line(333), Make.Line(333) };

        var grouped = VatCalculator.Compute(lines, VatMode.Included);
        int perLineBases = lines.Sum(l => VatCalculator.Compute([l], VatMode.Included).BaseCents);

        grouped.TotalCents.Should().Be(999);
        perLineBases.Should().NotBe(grouped.BaseCents,
            "per-line rounding loses a cent against per-rate rounding — that is why grouping exists");
    }

    [Fact]
    public void A_zero_rate_line_is_all_base_and_no_quota()
    {
        var result = VatCalculator.Compute([Make.Line(1000, vatBp: 0)], VatMode.Included);

        result.BaseCents.Should().Be(1000);
        result.VatCents.Should().Be(0);
        result.TotalCents.Should().Be(1000);
    }

    [Fact]
    public void A_sale_with_no_lines_is_zero_rather_than_an_error()
    {
        // The dialog computes totals on every keystroke, including before the first line
        // is added. This must not throw.
        var result = VatCalculator.Compute([], VatMode.Included);

        result.Should().Be(new VatBreakdown(0, 0, 0));
        VatCalculator.ToBreakdownRows([], VatMode.Included).Should().BeEmpty();
    }

    [Fact]
    public void A_negative_line_reconciles_like_any_other()
    {
        // A refund or a correction line. The invariant must not depend on the sign.
        var lines = new[] { Make.Line(5000), Make.Line(-1500) };

        var result = VatCalculator.Compute(lines, VatMode.Included);

        result.TotalCents.Should().Be(3500);
        (result.BaseCents + result.VatCents).Should().Be(result.TotalCents);
    }

    [Fact]
    public void A_ticket_of_two_hundred_lines_still_reconciles()
    {
        var lines = Enumerable.Range(1, 200).Select(i => Make.Line(i * 7, RealRates[i % RealRates.Length])).ToList();

        var result = VatCalculator.Compute(lines, VatMode.Included);

        result.TotalCents.Should().Be(lines.Sum(l => l.AmountCents));
        (result.BaseCents + result.VatCents).Should().Be(result.TotalCents);
    }

    [Fact]
    public void Rounding_goes_away_from_zero_at_the_exact_midpoint()
    {
        // 0,05 € at 100 % VAT included: the base is exactly 2,5 cents. Banker's rounding
        // would give 2; Spanish accounting expects 3.
        var result = VatCalculator.Compute([Make.Line(5, vatBp: 10000)], VatMode.Included);

        result.BaseCents.Should().Be(3);
        result.VatCents.Should().Be(2);
        result.TotalCents.Should().Be(5);
    }

    [Fact]
    public void The_smallest_possible_line_does_not_round_away_to_nothing()
    {
        // One cent at 21 % must still be one cent in total: the customer paid a cent.
        var result = VatCalculator.Compute([Make.Line(1)], VatMode.Included);

        result.TotalCents.Should().Be(1);
        (result.BaseCents + result.VatCents).Should().Be(1);
    }

    [Fact]
    public void Mixing_every_real_rate_on_one_ticket_produces_one_row_per_rate()
    {
        var lines = RealRates.Select(bp => Make.Line(10_000, bp)).ToList();

        var rows = VatCalculator.ToBreakdownRows(lines, VatMode.Included);

        rows.Should().HaveCount(RealRates.Length);
        rows.Select(r => r.VatBp).Should().BeInAscendingOrder("the export reports rates in order");
        rows.Sum(r => r.TotalCents).Should().Be(50_000);
    }
}
