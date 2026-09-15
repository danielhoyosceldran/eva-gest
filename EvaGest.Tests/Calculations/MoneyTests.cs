using AwesomeAssertions;
using EvaGest.Services;
using Xunit;

namespace EvaGest.Tests.Calculations;

public class MoneyTests
{
    [Fact] // C-01
    public void TryParse_integer() => Assert(15, "15", 1500);

    [Fact] // C-02
    public void TryParse_comma_decimal() => Assert(15, "15,50", 1550);

    [Fact] // C-03
    public void TryParse_dot_decimal() => Assert(15, "15.50", 1550);

    [Fact] // C-04
    public void TryParse_separator_thousands()
    {
        Money.TryParse("1.234,56", out int cents).Should().BeTrue();
        cents.Should().Be(123456);
    }

    [Fact] // C-05
    public void TryParse_spaces_and_the_euro_symbol()
    {
        Money.TryParse("  15,00 €  ", out int cents).Should().BeTrue();
        cents.Should().Be(1500);
    }

    [Fact] // C-06
    public void TryParse_decimals_small()
    {
        Money.TryParse("0,05", out int cents).Should().BeTrue();
        cents.Should().Be(5);
    }

    [Fact] // C-07
    public void TryParse_invalid_or_empty_text()
    {
        Money.TryParse("abc", out _).Should().BeFalse();
        Money.TryParse("", out _).Should().BeFalse();
    }

    [Fact] // C-12: a price/amount box that overflows int cents must be rejected, not
           // throw — decimal-to-int is a checked conversion in C#, so this used to
           // raise an unhandled OverflowException instead of showing a validation error.
    public void TryParse_amount_too_large_is_rejected_not_thrown()
    {
        Money.TryParse("99999999999", out _).Should().BeFalse();
        Money.TryParse("-99999999999", out _).Should().BeFalse();
    }

    [Fact] // C-08
    public void Format_uses_a_decimal_comma()
        => Money.Format(1500).Should().Contain("15,00");

    [Fact] // C-09
    public void FormatExport_writes_no_symbol()
        => Money.FormatExport(1500).Should().Be("15,00");

    [Fact] // C-10
    public void Formatting_and_parsing_round_trips()
    {
        var random = new Random(42);
        for (int i = 0; i < 500; i++)
        {
            int original = random.Next(0, 10_000_000);
            Money.TryParse(Money.FormatExport(original), out int recovered).Should().BeTrue();
            recovered.Should().Be(original);
        }
    }

    [Fact] // C-11
    public void Percentages_are_formatted()
    {
        Percentages.Format(2100).Should().Be("21 %");
        Percentages.Format(520).Should().Be("5,2 %");
    }

    private static void Assert(int _, string text, int expected)
    {
        Money.TryParse(text, out int cents).Should().BeTrue();
        cents.Should().Be(expected);
    }
}
