using AwesomeAssertions;
using EvaGest.Models;
using Xunit;

namespace EvaGest.Tests.Calculations;

/// <summary>Appointment.EndTime used TimeOnly.AddMinutes, which wraps silently past
/// midnight (23:00 + 90 min reads back as 00:30) instead of clamping to the end of
/// the day, making a late or very long appointment look like it ends before it starts.</summary>
public class AppointmentEndTimeTests
{
    [Fact]
    public void A_duration_that_stays_within_the_day_ends_normally()
    {
        var appointment = new Appointment { Time = new TimeOnly(10, 0), DurationMin = 30 };

        appointment.EndTime.Should().Be(new TimeOnly(10, 30));
    }

    [Fact]
    public void A_duration_that_would_cross_midnight_clamps_to_the_end_of_the_day()
    {
        var appointment = new Appointment { Time = new TimeOnly(23, 0), DurationMin = 90 };

        appointment.EndTime.Should().Be(new TimeOnly(23, 59, 59));
    }

    [Fact]
    public void An_appointment_ending_exactly_at_midnight_clamps_too()
    {
        var appointment = new Appointment { Time = new TimeOnly(22, 0), DurationMin = 120 };

        appointment.EndTime.Should().Be(new TimeOnly(23, 59, 59));
    }
}
