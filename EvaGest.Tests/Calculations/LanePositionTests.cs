using System.Globalization;
using AwesomeAssertions;
using EvaGest.Helpers;
using Xunit;

namespace EvaGest.Tests.Calculations;

/// <summary>
/// Horizontal placement of appointment blocks in a day column. Appointments must leave
/// the right third of the column free, so the slot can still be clicked to add another.
/// </summary>
public class LanePositionTests
{
    private const double Column = 304;   // 300 usable after the 4 px right margin

    private static double Place(string what, int lane, int total)
        => (double)new LanePositionConverter().Convert(
            [Column, lane, total], typeof(double), what, CultureInfo.InvariantCulture);

    [Fact]
    public void A_lone_appointment_covers_two_thirds_of_the_column()
    {
        Place("Left", 0, 1).Should().Be(0);
        Place("Width", 0, 1).Should().BeApproximately(198, 0.001);   // 200 minus the 2 px gap
    }

    [Fact]
    public void Overlapping_appointments_share_the_two_thirds_and_leave_the_rest_free()
    {
        Place("Left", 1, 2).Should().BeApproximately(100, 0.001);
        double rightEdge = Place("Left", 1, 2) + Place("Width", 1, 2);
        rightEdge.Should().BeLessThanOrEqualTo(200);
    }

    [Fact]
    public void An_unmeasured_column_places_nothing()
    {
        new LanePositionConverter().Convert([double.NaN, 0, 1], typeof(double), "Width",
            CultureInfo.InvariantCulture).Should().Be(0d);
    }
}
