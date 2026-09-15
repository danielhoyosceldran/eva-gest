using AwesomeAssertions;
using EvaGest.Services;
using Xunit;

namespace EvaGest.Tests.Calculations;

public class TimeValidatorTests
{
    [Theory]
    [InlineData("00:00")]
    [InlineData("23:59")]
    [InlineData("09:05")]
    [InlineData("12:30")]
    public void IsValidTime_hh_mm_within_range_Valid(string text)
    {
        TimeValidator.IsValidTime(text, out var time).Should().BeTrue();
        time.ToString("HH:mm").Should().Be(text);
    }

    [Theory]
    [InlineData("24:00")]  // hour out of range
    [InlineData("23:60")]  // minute out of range
    [InlineData("25:99")]  // both out of range
    public void IsValidTime_Hour_or_minute_out_of_range_Invalid(string text)
        => TimeValidator.IsValidTime(text, out _).Should().BeFalse();

    [Theory]
    [InlineData("9:05")]     // missing leading zero on hour
    [InlineData("09:5")]     // missing leading zero on minute
    [InlineData("09-05")]    // wrong separator
    [InlineData("09.05")]    // wrong separator
    [InlineData("09:05:00")] // seconds included
    [InlineData("9h05")]     // letters
    [InlineData(" 09:05")]   // leading space
    [InlineData("09:05 ")]   // trailing space
    [InlineData("")]
    public void IsValidTime_Anything_not_exactly_hh_mm_Invalid(string text)
        => TimeValidator.IsValidTime(text, out _).Should().BeFalse();

    [Fact]
    public void IsValidTime_Null_Invalid()
        => TimeValidator.IsValidTime(null!, out _).Should().BeFalse();
}
