using AwesomeAssertions;
using EvaGest.Helpers;
using EvaGest.Services;
using Xunit;

namespace EvaGest.Tests.Calculations;

/// <summary>
/// What a money box shows once the user leaves it: the amount typed, in the app's own
/// shape. The amount itself never changes; a box that does not parse is left alone.
/// </summary>
public class MoneyEntryTests
{
    [Theory]
    [InlineData("0", "0,00")]
    [InlineData("15", "15,00")]
    [InlineData("15,5", "15,50")]
    [InlineData("15.5", "15,50")]
    [InlineData(",50", "0,50")]
    [InlineData("1200", "1200,00")]
    [InlineData("1.200,00", "1200,00")]
    [InlineData(" 15 € ", "15,00")]
    public void A_typed_amount_is_shown_with_two_decimals_once_the_box_is_left(string typed, string shown)
        => MoneyEntry.Tidy(typed).Should().Be(shown);

    [Theory]
    [InlineData("")]
    [InlineData("quinze")]
    [InlineData("15,999")]
    [InlineData("1.200")]
    public void A_box_that_does_not_hold_an_amount_is_left_as_typed(string typed)
        => MoneyEntry.Tidy(typed).Should().Be(typed);

    [Theory]
    [InlineData("0")]
    [InlineData("15,5")]
    [InlineData("1.234,56")]
    [InlineData("99999,99")]
    public void Tidying_never_changes_the_amount(string typed)
    {
        Money.TryParse(typed, out int before).Should().BeTrue();
        Money.TryParse(MoneyEntry.Tidy(typed), out int after).Should().BeTrue();

        after.Should().Be(before);
    }
}
