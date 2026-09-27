using AwesomeAssertions;
using EvaGest.Services;
using Xunit;

namespace EvaGest.Tests.Calculations;

/// <summary>
/// A VAT rate is stored in basis points, which hold two decimals of a percent. A third
/// decimal used to be rounded away without a word, then frozen onto every sale.
/// </summary>
public class PercentagePrecisionTests
{
    [Theory]
    [InlineData("21,005")]
    [InlineData("5.255")]
    [InlineData("10,0001")]
    public void A_rate_with_more_than_two_decimals_is_refused(string typed)
        => Percentages.TryParse(typed, out _).Should().BeFalse();

    [Theory]
    [InlineData("21", 2100)]
    [InlineData("5,2", 520)]
    [InlineData("5.25", 525)]
    [InlineData("10,00", 1000)]
    [InlineData("0", 0)]
    public void A_rate_with_up_to_two_decimals_is_read_exactly(string typed, int expectedBp)
    {
        Percentages.TryParse(typed, out int bp).Should().BeTrue();
        bp.Should().Be(expectedBp);
    }
}
