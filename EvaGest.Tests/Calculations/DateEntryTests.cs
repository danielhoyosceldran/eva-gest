using AwesomeAssertions;
using EvaGest.Helpers;
using Xunit;

namespace EvaGest.Tests.Calculations;

/// <summary>
/// The keystroke rules of the date input (three boxes, day / month / year, with fixed
/// "/"), used for every date the user types. Newer than pla-proves.md, so no block code.
/// </summary>
public class DateEntryTests
{
    private static readonly DateOnly Today = new(2026, 10, 13);

    [Theory]
    [InlineData("1", "1", false)]    // could still be 10–19
    [InlineData("3", "3", false)]    // could still be 30 or 31
    [InlineData("0", "0", false)]
    [InlineData("4", "04", true)]    // can only be 04
    [InlineData("9", "09", true)]
    [InlineData("15", "15", true)]   // two digits: on to the month
    [InlineData("31", "31", true)]
    public void A_day_jumps_on_once_it_can_only_be_one_day(string typed, string kept, bool jump)
        => DateEntry.TypeDay(typed).Should().Be(new DateEntry.PartInput(true, kept, jump));

    [Theory]
    [InlineData("32")]
    [InlineData("00")]
    [InlineData("123")]
    [InlineData("a")]
    public void Anything_that_is_not_a_day_is_refused(string typed)
        => DateEntry.TypeDay(typed).Accepted.Should().BeFalse();

    [Theory]
    [InlineData("1", "1", false)]    // could still be 10–12
    [InlineData("2", "02", true)]    // can only be 02
    [InlineData("12", "12", true)]
    public void A_month_jumps_on_once_it_can_only_be_one_month(string typed, string kept, bool jump)
        => DateEntry.TypeMonth(typed).Should().Be(new DateEntry.PartInput(true, kept, jump));

    [Theory]
    [InlineData("13")]
    [InlineData("00")]
    public void Anything_that_is_not_a_month_is_refused(string typed)
        => DateEntry.TypeMonth(typed).Accepted.Should().BeFalse();

    [Fact]
    public void The_year_takes_up_to_four_digits_and_nothing_else()
    {
        DateEntry.TypeYear("2026").Should().Be("2026");
        DateEntry.TypeYear("20260").Should().BeNull();
        DateEntry.TypeYear("20a").Should().BeNull();
    }

    [Theory]
    [InlineData("/")]
    [InlineData("-")]
    [InlineData(".")]
    public void The_separators_people_type_out_of_habit_are_recognised(string key)
        => DateEntry.IsSeparator(key).Should().BeTrue();

    [Fact]
    public void Only_a_complete_real_date_goes_out_while_typing()
    {
        DateEntry.Complete("13", "10", "2026").Should().Be(new DateOnly(2026, 10, 13));
        DateEntry.Complete("13", "10", "202").Should().BeNull("a half-typed year is not a year yet");
        DateEntry.Complete("13", "10", "20").Should().BeNull("2020 is not on its way to 2026");
        DateEntry.Complete("13", "", "2026").Should().BeNull();
    }

    [Theory]
    [InlineData("31", "02", "2026")]
    [InlineData("29", "02", "2026")]   // 2026 is not a leap year
    [InlineData("31", "04", "2026")]
    public void An_impossible_day_is_flagged(string day, string month, string year)
    {
        DateEntry.IsImpossible(day, month, year).Should().BeTrue();
        DateEntry.Complete(day, month, year).Should().BeNull();
    }

    [Fact]
    public void A_leap_day_in_a_leap_year_is_a_real_date()
        => DateEntry.IsImpossible("29", "02", "2028").Should().BeFalse();

    [Theory]
    [InlineData("4", "3", "", "04", "03", "2026")]       // no year: this year
    [InlineData("4", "3", "27", "04", "03", "2027")]     // two digits: this century
    [InlineData("15", "10", "2025", "15", "10", "2025")]
    public void Leaving_fills_in_what_a_person_leaves_out(
        string day, string month, string year, string d, string m, string y)
    {
        var tidy = DateEntry.Tidy(day, month, year, Today);

        (tidy.Day, tidy.Month, tidy.Year).Should().Be((d, m, y));
        tidy.Date.Should().NotBeNull();
    }

    [Fact]
    public void Leaving_with_a_part_missing_or_an_impossible_day_makes_no_date()
    {
        DateEntry.Tidy("15", "", "", Today).Date.Should().BeNull();
        DateEntry.Tidy("31", "2", "", Today).Date.Should().BeNull();
        DateEntry.Tidy("", "", "", Today).Should().Be(("", "", "", (DateOnly?)null));
    }

    [Fact]
    public void A_date_splits_into_the_three_boxes()
    {
        DateEntry.Split(new DateOnly(2026, 3, 4)).Should().Be(("04", "03", "2026"));
        DateEntry.Split(null).Should().Be(("", "", ""));
    }

    [Theory]
    [InlineData("13/10/2026")]
    [InlineData("13-10-26")]
    [InlineData("13.10.2026")]
    [InlineData("2026-10-13")]
    [InlineData(" 13/10/2026 ")]
    public void Pasted_dates_are_read_loosely(string pasted)
        => DateEntry.Parse(pasted).Should().Be(new DateOnly(2026, 10, 13));

    [Theory]
    [InlineData("31/02/2026")]
    [InlineData("13/10")]
    [InlineData("hola")]
    [InlineData("13/10/202")]
    public void Pasted_text_that_is_not_a_date_is_ignored(string pasted)
        => DateEntry.Parse(pasted).Should().BeNull();

    [Theory]
    [InlineData("15", 1, "16")]
    [InlineData("31", 1, "01")]   // round, leaving the month alone
    [InlineData("01", -1, "31")]
    [InlineData("", 1, "13")]     // an empty box starts on today
    public void Arrows_step_the_day(string day, int delta, string expected)
        => DateEntry.StepDay(day, delta, Today).Should().Be(expected);

    [Theory]
    [InlineData("12", 1, "01")]
    [InlineData("01", -1, "12")]
    [InlineData("", -1, "10")]
    public void Arrows_step_the_month(string month, int delta, string expected)
        => DateEntry.StepMonth(month, delta, Today).Should().Be(expected);

    [Theory]
    [InlineData("2026", 1, "2027")]
    [InlineData("2026", -1, "2025")]
    [InlineData("", 1, "2026")]
    [InlineData("20", -1, "2026")]  // not a year yet: start on this one
    public void Arrows_step_the_year(string year, int delta, string expected)
        => DateEntry.StepYear(year, delta, Today).Should().Be(expected);
}
