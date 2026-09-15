using AwesomeAssertions;
using EvaGest.Services;
using Xunit;

namespace EvaGest.Tests.Calculations;

public class DateValidatorTests
{
    [Theory]
    [InlineData("01/01/2024")]
    [InlineData("31/12/2023")]
    [InlineData("29/02/2024")] // 2024 is a leap year
    [InlineData("28/02/2023")] // last day of February in a non-leap year
    [InlineData("30/04/2024")] // April has 30 days
    [InlineData("31/01/2024")] // January has 31 days
    [InlineData("29/02/2000")] // divisible by 400: leap year
    public void IsValidDate_Real_calendar_date_in_dd_mm_yyyy_Valid(string text)
    {
        DateValidator.IsValidDate(text, out var date).Should().BeTrue();
        date.ToString("dd/MM/yyyy").Should().Be(text);
    }

    [Theory]
    [InlineData("29/02/2023")] // 2023 is not a leap year
    [InlineData("29/02/1900")] // divisible by 100 but not 400: not a leap year
    [InlineData("31/04/2024")] // April does not have 31 days
    [InlineData("31/06/2024")] // June does not have 31 days
    [InlineData("31/09/2024")] // September does not have 31 days
    [InlineData("31/11/2024")] // November does not have 31 days
    [InlineData("00/01/2024")] // day 0
    [InlineData("32/01/2024")] // day 32
    public void IsValidDate_Day_out_of_range_for_that_month_or_year_Invalid(string text)
        => DateValidator.IsValidDate(text, out _).Should().BeFalse();

    [Theory]
    [InlineData("01/00/2024")] // month 0
    [InlineData("01/13/2024")] // month 13
    public void IsValidDate_Month_out_of_range_Invalid(string text)
        => DateValidator.IsValidDate(text, out _).Should().BeFalse();

    [Theory]
    [InlineData("1/1/2024")]     // missing leading zeros
    [InlineData("01-01-2024")]   // wrong separator
    [InlineData("01.01.2024")]   // wrong separator
    [InlineData("2024/01/01")]   // wrong order
    [InlineData("01/01/24")]     // two-digit year
    [InlineData("01/01/20245")]  // five-digit year
    [InlineData("01/01/2024 ")]  // trailing space
    [InlineData(" 01/01/2024")]  // leading space
    [InlineData("01//01/2024")]  // doubled separator
    [InlineData("aa/bb/cccc")]   // letters
    [InlineData("")]
    public void IsValidDate_Anything_not_exactly_dd_mm_yyyy_Invalid(string text)
        => DateValidator.IsValidDate(text, out _).Should().BeFalse();

    [Fact]
    public void IsValidDate_Null_Invalid()
        => DateValidator.IsValidDate(null!, out _).Should().BeFalse();
}
