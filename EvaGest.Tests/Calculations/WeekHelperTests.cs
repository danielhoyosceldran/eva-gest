using AwesomeAssertions;
using EvaGest.Helpers;
using Xunit;

namespace EvaGest.Tests.Calculations;

public class WeekHelperTests
{
    [Fact] // E-15
    public void A_monday_returns_itself()
    {
        var monday = new DateOnly(2026, 9, 7);
        for (int i = 0; i < 7; i++)
            WeekHelper.MondayOfWeek(monday.AddDays(i)).Should().Be(monday);
    }

    [Fact] // E-16
    public void Sunday_belongs_to_the_week_starting_the_monday_before()
    {
        var sunday = new DateOnly(2026, 9, 13);
        WeekHelper.MondayOfWeek(sunday).Should().Be(new DateOnly(2026, 9, 7));
    }
}
