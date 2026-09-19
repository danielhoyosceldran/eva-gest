using AwesomeAssertions;
using EvaGest.Services;
using Xunit;

namespace EvaGest.Tests.Calculations;

/// <summary>
/// Block H — hostile input. Everything here is something a real person can type into a
/// real box: a paste that carried a currency symbol, a keyboard set to another locale, a
/// stuck key, an empty field tabbed through. None of it is nonsense for its own sake.
///
/// The rule under test is the same everywhere: a parser either returns false or returns a
/// correct value. It must never throw, and it must never quietly return a number the user
/// did not type — that number would be frozen onto a sale and no later edit could undo it.
/// </summary>
public class HostileInputTests
{
    // ── Money.TryParse ───────────────────────────────────────────────────────

    [Theory]
    [InlineData("")]                 // tabbed through an empty box
    [InlineData("   ")]
    [InlineData("€")]                // pasted the symbol alone
    [InlineData("abc")]
    [InlineData("1,2,3,4")]          // stuck comma key: every separator but the last is a
                                     // group separator, so "1234" would be a guess, not a read
    [InlineData("--5")]
    [InlineData("1e5")]              // scientific notation is not money
    [InlineData("NaN")]
    [InlineData("∞")]
    [InlineData("1/2")]
    [InlineData("12,,50")]
    [InlineData("0x10")]
    public void Nonsense_in_a_price_box_is_refused_and_never_throws(string typed)
    {
        Action parse = () => Money.TryParse(typed, out _);

        parse.Should().NotThrow();
        Money.TryParse(typed, out _).Should().BeFalse($"\"{typed}\" is not an amount");
    }

    [Theory]
    [InlineData("15", 1500)]
    [InlineData("15,50", 1550)]
    [InlineData("15.50", 1550)]      // a keyboard set to another locale
    [InlineData("1.234,56", 123456)] // pasted from a spreadsheet
    [InlineData("1,234.56", 123456)] // the same amount the other way round
    [InlineData("15,50 €", 1550)]    // pasted with the symbol
    [InlineData("  15,50  ", 1550)]
    [InlineData(",50", 50)]          // leading separator
    [InlineData("15,", 1500)]        // trailing separator, mid-typing
    [InlineData("15,999", 1600)]     // more precision than a cent: rounds away from zero
    [InlineData("15,995", 1600)]     // the midpoint must not round to even (1599)
    [InlineData("-15,50", -1550)]    // a correction line is allowed to be negative
    public void A_plausible_amount_is_read_exactly_as_written(string typed, int expectedCents)
    {
        Money.TryParse(typed, out int cents).Should().BeTrue();
        cents.Should().Be(expectedCents);
    }

    [Fact]
    public void An_amount_too_large_for_int_cents_is_refused_rather_than_wrapped()
    {
        // 30 million euros is 3e9 cents, past int.MaxValue. Returning false is the only
        // safe answer: wrapping would freeze a negative total onto the sale.
        Money.TryParse("30000000", out _).Should().BeFalse();
        Money.TryParse("-30000000", out _).Should().BeFalse();
    }

    [Fact]
    public void An_absurdly_long_run_of_digits_is_refused_and_does_not_hang()
    {
        string typed = new('9', 400);

        Action parse = () => Money.TryParse(typed, out _);

        parse.Should().NotThrow();
        Money.TryParse(typed, out _).Should().BeFalse();
    }

    [Fact]
    public void The_largest_amount_that_still_fits_is_accepted()
    {
        // Directly below the int ceiling: the guard must reject what overflows without
        // also rejecting the last legal value.
        Money.TryParse("21474836,47", out int cents).Should().BeTrue();
        cents.Should().Be(int.MaxValue);
    }

    // ── Percentages.TryParse ─────────────────────────────────────────────────

    [Theory]
    [InlineData("")]
    [InlineData("%")]
    [InlineData("abc")]
    [InlineData("-1")]        // a negative VAT rate is not a thing
    [InlineData("101")]       // above 100 % is a typo, and it would be frozen onto sales
    [InlineData("1000")]      // someone typing basis points into a percent box
    [InlineData("2,1,0")]
    public void An_impossible_vat_rate_is_refused(string typed)
    {
        Action parse = () => Percentages.TryParse(typed, out _);

        parse.Should().NotThrow();
        Percentages.TryParse(typed, out _).Should().BeFalse();
    }

    [Theory]
    [InlineData("21", 2100)]
    [InlineData("21 %", 2100)]
    [InlineData("5,2", 520)]
    [InlineData("5.2", 520)]
    [InlineData("0", 0)]      // an exempt line is legitimate
    [InlineData("100", 10000)]
    public void A_real_vat_rate_is_read_as_basis_points(string typed, int expectedBp)
    {
        Percentages.TryParse(typed, out int bp).Should().BeTrue();
        bp.Should().Be(expectedBp);
    }

    [Fact]
    public void A_rate_and_its_formatting_survive_a_round_trip()
    {
        // The settings page writes what it formatted, so format->parse must be lossless
        // or a rate drifts every time the page is saved.
        foreach (int bp in new[] { 0, 400, 520, 1000, 2100, 10000 })
        {
            Percentages.TryParse(Percentages.FormatWithoutUnit(bp), out int back)
                .Should().BeTrue($"{bp} bp formats to something re-readable");
            back.Should().Be(bp);
        }
    }

    // ── Formatting ───────────────────────────────────────────────────────────

    [Theory]
    [InlineData(0)]
    [InlineData(5)]
    [InlineData(1500)]
    [InlineData(-1550)]
    [InlineData(123456789)]
    public void Every_amount_is_shown_with_exactly_two_decimals(int cents)
    {
        // The user reads these to check a till against real coins, so a round amount must
        // still read "15,00 €" and never "15 €".
        string shown = Money.Format(cents);
        string separator = EvaGest.Services.AppLanguage.Culture.NumberFormat.CurrencyDecimalSeparator;

        shown.Should().Contain(separator);
        shown.Split(separator)[^1].TrimEnd(' ', '€', ' ')
             .Should().HaveLength(2, $"\"{shown}\" must end in two decimal digits");
    }

    [Fact]
    public void A_period_total_larger_than_int_cents_still_formats_correctly()
    {
        // Aggregates are summed as long. Formatting used to narrow them back to int,
        // which wrapped to a negative figure past ~21.4 million euros.
        long cents = (long)int.MaxValue + 100_000;

        Money.Format(cents).Should().NotStartWith("-");
        Money.FormatExport(cents).Should().NotStartWith("-");
    }

    [Fact]
    public void Formatting_and_parsing_an_amount_round_trips()
    {
        foreach (int cents in new[] { 0, 1, 99, 100, 1550, 123456, int.MaxValue })
        {
            Money.TryParse(Money.FormatExport(cents), out int back)
                .Should().BeTrue($"{cents} formats to something re-readable");
            back.Should().Be(cents);
        }
    }
}
