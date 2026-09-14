using AwesomeAssertions;
using EvaGest.Services;
using Xunit;

namespace EvaGest.Tests.Calculations;

public class IndicatorsTests
{
    [Fact] // I-01
    public void AveragePerVisit_with_zero_visits_is_null()
        => Indicators.AveragePerVisit(0, 0).Should().BeNull();

    [Fact] // I-02
    public void FrequencyDays_with_one_visit_is_null()
        => Indicators.FrequencyDays([new DateOnly(2026, 1, 1)]).Should().BeNull();

    [Fact] // I-03
    public void FrequencyDays_with_two_visits_21_days_apart()
    {
        var dates = new[] { new DateOnly(2026, 1, 1), new DateOnly(2026, 1, 22) };
        Indicators.FrequencyDays(dates).Should().Be(21.0);
    }

    [Fact] // I-04
    public void FrequencyDays_with_three_visits_21_and_21_days_apart()
    {
        var dates = new[]
        {
            new DateOnly(2026, 1, 1),
            new DateOnly(2026, 1, 22),
            new DateOnly(2026, 2, 12)
        };
        Indicators.FrequencyDays(dates).Should().Be(21.0);
    }

    [Fact] // I-05
    public void WorkPercentage_in_a_period_without_sales_is_null()
        => Indicators.WorkPercentage(0, 0).Should().BeNull();

    [Fact] // I-06
    public void ProductsPercentage_of_a_worker_without_sales_is_null()
        => Indicators.ProductsPercentage(500, 0).Should().BeNull();

    [Fact] // I-07
    public void WorkPercentage_of_two_workers_adds_up_to_100()
    {
        int total = 10_000;
        var p1 = Indicators.WorkPercentage(6_000, total)!.Value;
        var p2 = Indicators.WorkPercentage(4_000, total)!.Value;
        (p1 + p2).Should().Be(100m);
    }
}
