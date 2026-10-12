using AwesomeAssertions;
using EvaGest.Helpers;
using Xunit;

namespace EvaGest.Tests.Calculations;

/// <summary>
/// The keystroke rules of the hour input (two boxes, hours and minutes, with a fixed ":"),
/// used for the appointment's hour and every opening-hours row. Newer than
/// pla-proves.md, so no block code.
/// </summary>
public class TimeEntryTests
{
    [Theory]
    [InlineData("1", "1", false)]   // could still be 10–19
    [InlineData("2", "2", false)]   // could still be 20–23
    [InlineData("3", "03", true)]   // can only be 03
    [InlineData("9", "09", true)]
    [InlineData("14", "14", true)]  // two digits: on to the minutes
    [InlineData("23", "23", true)]
    [InlineData("00", "00", true)]
    public void Hours_jump_to_the_minutes_once_they_can_only_be_one_hour(string typed, string kept, bool jump)
    {
        var r = TimeEntry.TypeHours(typed);

        r.Accepted.Should().BeTrue();
        r.Text.Should().Be(kept);
        r.JumpToMinutes.Should().Be(jump);
    }

    [Theory]
    [InlineData("24")]
    [InlineData("99")]
    [InlineData("123")]
    [InlineData("1:")]
    [InlineData("a")]
    public void Anything_that_is_not_an_hour_is_refused(string typed)
        => TimeEntry.TypeHours(typed).Accepted.Should().BeFalse();

    [Theory]
    [InlineData("3", "3")]
    [InlineData("7", "07")]   // can only be 07
    [InlineData("30", "30")]
    [InlineData("59", "59")]
    public void Minutes_keep_what_can_still_be_a_minute(string typed, string kept)
        => TimeEntry.TypeMinutes(typed).Should().Be(kept);

    [Theory]
    [InlineData("60")]
    [InlineData("123")]
    [InlineData("x")]
    public void Anything_that_is_not_a_minute_is_refused(string typed)
        => TimeEntry.TypeMinutes(typed).Should().BeNull();

    [Theory]
    [InlineData(":")]
    [InlineData(".")]
    [InlineData(",")]
    public void The_separators_people_type_out_of_habit_are_recognised(string key)
        => TimeEntry.IsSeparator(key).Should().BeTrue();

    [Theory]
    [InlineData("9", "30", "09:30")]
    [InlineData("9", "", "09:00")]    // an hour alone is on the hour
    [InlineData("14", "5", "14:05")]
    [InlineData("", "", "")]          // no time at all
    [InlineData("", "30", "")]        // minutes alone are not a time yet
    public void The_two_boxes_join_into_the_one_shape_the_app_stores(string hours, string minutes, string time)
    {
        TimeEntry.Join(hours, minutes).Should().Be(time);
        if (time.Length > 0) ScheduleHelper.Analyze(time).Should().NotBeNull("the rest of the app must accept it");
    }

    [Fact]
    public void A_stored_time_splits_back_into_the_boxes()
    {
        TimeEntry.Split("09:30").Should().Be(("09", "30"));
        TimeEntry.Split("").Should().Be(("", ""));
        TimeEntry.Split("not a time").Should().Be(("", ""));
    }

    [Theory]
    [InlineData("9:30", "09", "30")]
    [InlineData("09.30", "09", "30")]
    [InlineData("0930", "09", "30")]
    [InlineData("930", "09", "30")]
    [InlineData("17", "17", "00")]
    [InlineData(" 20:15 ", "20", "15")]
    public void Pasted_times_are_read_loosely(string pasted, string hours, string minutes)
        => TimeEntry.Parse(pasted).Should().Be((hours, minutes));

    [Theory]
    [InlineData("25:00")]
    [InlineData("10:75")]
    [InlineData("hola")]
    [InlineData("12345")]
    public void Pasted_text_that_is_not_a_time_is_ignored(string pasted)
        => TimeEntry.Parse(pasted).Should().BeNull();

    [Theory]
    [InlineData("09", 1, "10")]
    [InlineData("23", 1, "00")]   // wraps round midnight
    [InlineData("00", -1, "23")]
    [InlineData("", 1, "00")]
    public void Arrows_step_the_hours_by_one(string hours, int delta, string expected)
        => TimeEntry.StepHours(hours, delta).Should().Be(expected);

    [Theory]
    [InlineData("00", 1, "15")]
    [InlineData("45", 1, "00")]
    [InlineData("10", 1, "15")]   // an odd minute lands on the next quarter
    [InlineData("10", -1, "00")]  // or the previous one
    [InlineData("30", -1, "15")]
    [InlineData("00", -1, "45")]
    public void Arrows_step_the_minutes_by_quarter_hours(string minutes, int delta, string expected)
        => TimeEntry.StepMinutes(minutes, delta).Should().Be(expected);
}
