using AwesomeAssertions;
using EvaGest.Services;
using Xunit;

namespace EvaGest.Tests.Calculations;

public class NumberValidatorTests
{
    [Theory]
    [InlineData("0", 0)]
    [InlineData("1", 1)]
    [InlineData("30", 30)]
    [InlineData("  30  ", 30)] // outer whitespace is never what was meant
    public void TryParseWhole_Digits_Valid(string text, int expected)
    {
        NumberValidator.TryParseWhole(text, out int value).Should().BeTrue();
        value.Should().Be(expected);
    }

    [Theory]
    [InlineData("-1")]     // sign
    [InlineData("+1")]     // sign
    [InlineData("1,5")]    // decimal
    [InlineData("1.5")]    // decimal
    [InlineData("3 0")]    // inner space
    [InlineData("trenta")] // letters
    [InlineData("30min")]  // trailing unit
    [InlineData("1e3")]    // exponent
    [InlineData("")]
    [InlineData("   ")]
    public void TryParseWhole_Anything_but_digits_Invalid(string text)
        => NumberValidator.TryParseWhole(text, out _).Should().BeFalse();

    [Fact]
    public void TryParseWhole_Null_Invalid()
        => NumberValidator.TryParseWhole(null, out _).Should().BeFalse();

    [Fact]
    public void TryParseWhole_A_number_too_big_to_be_meant_Invalid()
        => NumberValidator.TryParseWhole("9999999999", out _).Should().BeFalse();

    [Theory]
    [InlineData("0")]
    [InlineData("-1")]
    public void TryParseAtLeast_Below_the_minimum_Invalid(string text)
        => NumberValidator.TryParseAtLeast(text, 1, out _).Should().BeFalse();

    [Fact]
    public void TryParseAtLeast_At_the_minimum_Valid()
    {
        NumberValidator.TryParseAtLeast("1", 1, out int value).Should().BeTrue();
        value.Should().Be(1);
    }
}
