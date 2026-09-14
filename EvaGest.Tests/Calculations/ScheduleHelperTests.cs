using AwesomeAssertions;
using EvaGest.Helpers;
using Xunit;

namespace EvaGest.Tests.Calculations;

public class ScheduleHelperTests
{
    [Theory] // H-01
    [InlineData("09:00", 9, 0)]
    [InlineData("9:00", 9, 0)]
    [InlineData("9", 9, 0)]
    [InlineData("0930", 9, 30)]
    [InlineData("9.30", 9, 30)]
    [InlineData("  09:30  ", 9, 30)]
    [InlineData("20:15", 20, 15)]
    public void Parse_accepts_the_shapes_people_actually_write(string text, int time, int minute)
        => ScheduleHelper.Analyze(text).Should().Be(new TimeOnly(time, minute));

    [Theory] // H-02
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    [InlineData("25:00")]
    [InlineData("09:70")]
    [InlineData("matí")]
    public void Parse_rejects_what_is_not_a_time(string? text)
        => ScheduleHelper.Analyze(text).Should().BeNull();

    [Fact] // H-03
    public void A_day_with_no_shift_gives_no_interval_and_no_error()
    {
        var r = ScheduleHelper.Check(false, "qualsevol", "cosa", false, "", "");
        r.IsValid.Should().BeTrue();
        r.Intervals.Should().BeEmpty();
    }

    [Fact] // H-04
    public void Only_a_morning_gives_a_single_interval()
    {
        var r = ScheduleHelper.Check(true, "09:00", "20:00", false, "", "");
        r.IsValid.Should().BeTrue();
        r.Intervals.Should().ContainSingle()
            .Which.Should().Be((new TimeOnly(9, 0), new TimeOnly(20, 0)));
    }

    [Fact] // H-05
    public void Morning_and_afternoon_give_two_intervals()
    {
        var r = ScheduleHelper.Check(true, "09:00", "13:00", true, "16:00", "20:00");
        r.IsValid.Should().BeTrue();
        r.Intervals.Should().Equal(
            (new TimeOnly(9, 0), new TimeOnly(13, 0)),
            (new TimeOnly(16, 0), new TimeOnly(20, 0)));
    }

    [Fact] // H-06
    public void A_ticked_shift_without_hours_is_an_error()
    {
        ScheduleHelper.Check(true, "", "", false, "", "").Error.Should().NotBeNull();
        ScheduleHelper.Check(false, "", "", true, "", "").Error.Should().NotBeNull();
    }

    [Fact] // H-07
    public void An_interval_cannot_end_before_it_starts()
    {
        ScheduleHelper.Check(true, "20:00", "09:00", false, "", "").Error.Should().NotBeNull();
        ScheduleHelper.Check(true, "09:00", "09:00", false, "", "").Error.Should().NotBeNull();
        ScheduleHelper.Check(false, "", "", true, "20:00", "16:00").Error.Should().NotBeNull();
    }

    [Fact] // H-08
    public void Half_filled_afternoon_is_an_error_not_silently_ignored()
    {
        ScheduleHelper.Check(true, "09:00", "13:00", true, "16:00", "").Error.Should().NotBeNull();
        ScheduleHelper.Check(true, "09:00", "13:00", true, "", "20:00").Error.Should().NotBeNull();
    }

    [Fact] // H-09
    public void The_afternoon_cannot_overlap_the_morning()
        => ScheduleHelper.Check(true, "09:00", "14:00", true, "13:00", "20:00").Error.Should().NotBeNull();

    [Fact] // H-10
    public void Intervals_may_touch_without_being_an_error()
        => ScheduleHelper.Check(true, "09:00", "14:00", true, "14:00", "20:00").IsValid.Should().BeTrue();

    [Fact] // H-11
    public void Format_always_returns_two_digit_hours()
        => ScheduleHelper.Format(new TimeOnly(9, 5)).Should().Be("09:05");

    [Fact] // H-12
    public void Only_an_afternoon_gives_the_afternoon_interval_alone()
    {
        var r = ScheduleHelper.Check(false, "", "", true, "16:00", "20:00");
        r.IsValid.Should().BeTrue();
        r.Intervals.Should().ContainSingle()
            .Which.Should().Be((new TimeOnly(16, 0), new TimeOnly(20, 0)));
    }

    [Fact] // H-13
    public void The_hours_of_an_unticked_shift_are_not_validated()
    {
        // Unticking a shift has to be enough; nobody should have to clear its boxes too.
        var r = ScheduleHelper.Check(true, "09:00", "14:00", false, "20:00", "16:00");
        r.IsValid.Should().BeTrue();
        r.Intervals.Should().ContainSingle();
    }

    [Fact] // H-14
    public void The_picker_offers_the_quarter_hours_of_the_working_day()
    {
        ScheduleHelper.Slots.Should().StartWith(["06:00", "06:15", "06:30", "06:45", "07:00"]);
        ScheduleHelper.Slots.Should().EndWith(["23:00"]);
        ScheduleHelper.Slots.Should().Contain("09:00").And.Contain("20:30");
    }
}
