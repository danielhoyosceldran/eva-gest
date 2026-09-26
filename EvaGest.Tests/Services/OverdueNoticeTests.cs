using AwesomeAssertions;
using EvaGest.Helpers;
using EvaGest.Models;
using EvaGest.Services;
using EvaGest.Tests.Infra;
using Xunit;

namespace EvaGest.Tests.Services;

/// <summary>
/// The shell's overdue-appointments notice: it must go away as soon as the appointment
/// is closed in any way, and the user can close it for a while without losing it for good.
/// </summary>
public class OverdueNoticeTests
{
    private static readonly DateTime Noon = new(2026, 9, 26, 12, 0, 0);

    [Fact]
    public void A_notice_nobody_closed_is_shown()
    {
        new OverdueNoticeSnooze().ShouldShow([1], Noon).Should().BeTrue();
    }

    [Fact]
    public void Nothing_overdue_shows_nothing()
    {
        new OverdueNoticeSnooze().ShouldShow([], Noon).Should().BeFalse();
    }

    [Fact]
    public void A_closed_notice_stays_hidden_until_the_snooze_runs_out()
    {
        var snooze = new OverdueNoticeSnooze();
        snooze.Dismiss([1], Noon);

        snooze.ShouldShow([1], Noon + OverdueNoticeSnooze.Duration - TimeSpan.FromMinutes(1)).Should().BeFalse();
        snooze.ShouldShow([1], Noon + OverdueNoticeSnooze.Duration).Should().BeTrue();
    }

    [Fact]
    public void A_newly_overdue_appointment_brings_a_closed_notice_back_at_once()
    {
        var snooze = new OverdueNoticeSnooze();
        snooze.Dismiss([1], Noon);

        snooze.ShouldShow([1, 2], Noon.AddMinutes(1)).Should().BeTrue();
    }

    [Fact]
    public void Closing_one_appointment_of_a_closed_notice_keeps_it_hidden()
    {
        var snooze = new OverdueNoticeSnooze();
        snooze.Dismiss([1, 2], Noon);

        snooze.ShouldShow([2], Noon.AddMinutes(1)).Should().BeFalse();
    }

    [Fact]
    public void Once_nothing_is_overdue_the_next_overdue_appointment_is_shown_straight_away()
    {
        var snooze = new OverdueNoticeSnooze();
        snooze.Dismiss([1], Noon);
        snooze.ShouldShow([], Noon.AddMinutes(1));

        snooze.ShouldShow([1], Noon.AddMinutes(2)).Should().BeTrue();
    }

    [Fact]
    public async Task Charging_an_appointment_tells_the_overdue_check_right_away()
    {
        await using var testDb = new TestDatabase();
        var factory = new TestFactory(testDb.Options);
        var config = new SettingsService(factory);
        await new SeedService(factory, config).Seed();

        int appointmentId, methodId;
        await using (var db = testDb.Context())
        {
            var client = Make.Client();
            db.Clients.Add(client);
            await db.SaveChangesAsync();
            var appointment = new Appointment
            {
                Date = DateOnly.FromDateTime(DateTime.Today), Time = new TimeOnly(8, 0), DurationMin = 30,
                ClientId = client.Id, Status = AppointmentStatus.Pending
            };
            db.Appointments.Add(appointment);
            await db.SaveChangesAsync();
            appointmentId = appointment.Id;
            methodId = db.PaymentMethods.First().Id;
        }

        var notifier = new AppointmentChangeNotifier();
        int notified = 0;
        notifier.Changed += () => notified++;
        var sales = new SaleService(factory, config, notifier);

        var sale = await sales.PrepareFromAppointment(appointmentId);
        sale.PaymentMethodId = methodId;
        await sales.Create(sale, [Make.Line(1500)]);

        notified.Should().Be(1);
    }
}
