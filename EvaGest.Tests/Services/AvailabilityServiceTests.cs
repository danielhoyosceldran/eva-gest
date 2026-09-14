using AwesomeAssertions;
using EvaGest.Models;
using EvaGest.Services;
using EvaGest.Tests.Infra;
using Xunit;

namespace EvaGest.Tests.Services;

/// <summary>Block E: availability i solapament (casos-us CU-01b). The least obvious
/// calculation in the project, so it gets the widest test coverage.</summary>
public class AvailabilityServiceTests
{
    private static readonly DateOnly Monday = new(2026, 9, 7); // a Monday

    private static AvailabilityService CreatesService(TestDatabase testDb) => new(new TestFactory(testDb.Options));

    private static async Task<int> AddsWorker(TestDatabase testDb, bool active = true,
        TimeOnly? start = null, TimeOnly? fi = null)
    {
        await using var db = testDb.Context();
        var t = new Worker { Name = "Marta", Active = active, Color = "#0F766E" };
        t.Schedules.Add(new WorkerSchedule
        {
            Weekday = Weekday.Mon,
            StartTime = start ?? new TimeOnly(9, 0),
            EndTime = fi ?? new TimeOnly(18, 0)
        });
        db.Workers.Add(t);
        await db.SaveChangesAsync();
        return t.Id;
    }

    private static async Task AddsAppointment(TestDatabase testDb, TimeOnly time, int durationMin,
        int? workerId = null, AppointmentStatus status = AppointmentStatus.Pending)
    {
        await using var db = testDb.Context();
        db.Appointments.Add(new Appointment
        {
            Date = Monday, Time = time, DurationMin = durationMin,
            GuestName = "Convidat", WorkerId = workerId, Status = status
        });
        await db.SaveChangesAsync();
    }

    [Fact] // E-01
    public async Task One_worker_and_no_appointment_gives_no_notice()
    {
        await using var testDb = new TestDatabase();
        int t = await AddsWorker(testDb);
        var avail = CreatesService(testDb);

        var r = await avail.Check(Monday, new TimeOnly(10, 0), 30, t);
        r.HasOverlap.Should().BeFalse();
    }

    [Fact] // E-02
    public async Task One_worker_already_busy_warns()
    {
        await using var testDb = new TestDatabase();
        int t = await AddsWorker(testDb);
        await AddsAppointment(testDb, new TimeOnly(10, 0), 30, t);
        var avail = CreatesService(testDb);

        var r = await avail.Check(Monday, new TimeOnly(10, 0), 30, t);
        r.HasOverlap.Should().BeTrue();
    }

    [Fact] // E-03
    public async Task Two_workers_and_one_appointment_give_no_notice()
    {
        await using var testDb = new TestDatabase();
        await AddsWorker(testDb);
        await AddsWorker(testDb);
        await AddsAppointment(testDb, new TimeOnly(10, 0), 30);
        var avail = CreatesService(testDb);

        var r = await avail.Check(Monday, new TimeOnly(10, 0), 30, workerId: null);
        r.HasOverlap.Should().BeFalse();
    }

    [Fact] // E-04
    public async Task Two_workers_and_two_appointments_warn()
    {
        await using var testDb = new TestDatabase();
        await AddsWorker(testDb);
        await AddsWorker(testDb);
        await AddsAppointment(testDb, new TimeOnly(10, 0), 30);
        await AddsAppointment(testDb, new TimeOnly(10, 0), 30);
        var avail = CreatesService(testDb);

        var r = await avail.Check(Monday, new TimeOnly(10, 0), 30, workerId: null);
        r.HasOverlap.Should().BeTrue();
    }

    [Fact] // E-05
    public async Task An_inactive_worker_does_not_count_as_available()
    {
        await using var testDb = new TestDatabase();
        await AddsWorker(testDb, active: false);
        await AddsAppointment(testDb, new TimeOnly(10, 0), 30);
        var avail = CreatesService(testDb);

        var r = await avail.Check(Monday, new TimeOnly(10, 0), 30, workerId: null);
        r.HasOverlap.Should().BeTrue();
    }

    [Fact] // E-06
    public async Task A_cancelled_appointment_does_not_count()
    {
        await using var testDb = new TestDatabase();
        int t = await AddsWorker(testDb);
        await AddsAppointment(testDb, new TimeOnly(10, 0), 30, t, AppointmentStatus.Cancelled);
        var avail = CreatesService(testDb);

        var r = await avail.Check(Monday, new TimeOnly(10, 0), 30, t);
        r.HasOverlap.Should().BeFalse();
    }

    [Fact] // E-07
    public async Task A_no_show_appointment_does_not_count()
    {
        await using var testDb = new TestDatabase();
        int t = await AddsWorker(testDb);
        await AddsAppointment(testDb, new TimeOnly(10, 0), 30, t, AppointmentStatus.NoShow);
        var avail = CreatesService(testDb);

        var r = await avail.Check(Monday, new TimeOnly(10, 0), 30, t);
        r.HasOverlap.Should().BeFalse();
    }

    [Fact] // E-08
    public async Task Consecutive_appointments_without_overlap_give_no_notice()
    {
        await using var testDb = new TestDatabase();
        int t = await AddsWorker(testDb);
        await AddsAppointment(testDb, new TimeOnly(10, 0), 30, t);
        var avail = CreatesService(testDb);

        var r = await avail.Check(Monday, new TimeOnly(10, 30), 30, t);
        r.HasOverlap.Should().BeFalse();
    }

    [Fact] // E-09
    public async Task A_partial_overlap_warns()
    {
        await using var testDb = new TestDatabase();
        int t = await AddsWorker(testDb);
        await AddsAppointment(testDb, new TimeOnly(10, 0), 45, t);
        var avail = CreatesService(testDb);

        var r = await avail.Check(Monday, new TimeOnly(10, 30), 30, t);
        r.HasOverlap.Should().BeTrue();
    }

    [Fact] // E-10
    public async Task The_assigned_worker_free_and_the_other_busy_gives_no_notice()
    {
        await using var testDb = new TestDatabase();
        int t1 = await AddsWorker(testDb);
        int t2 = await AddsWorker(testDb);
        await AddsAppointment(testDb, new TimeOnly(10, 0), 30, t2);
        var avail = CreatesService(testDb);

        var r = await avail.Check(Monday, new TimeOnly(10, 0), 30, t1);
        r.HasOverlap.Should().BeFalse();
    }

    [Fact] // E-11
    public async Task Editing_an_appointment_does_not_clash_with_itself()
    {
        await using var testDb = new TestDatabase();
        int t = await AddsWorker(testDb);
        int appointmentId;
        await using (var db = testDb.Context())
        {
            var c = new Appointment
            {
                Date = Monday, Time = new TimeOnly(10, 0), DurationMin = 30,
                GuestName = "Convidat", WorkerId = t
            };
            db.Appointments.Add(c);
            await db.SaveChangesAsync();
            appointmentId = c.Id;
        }
        var avail = CreatesService(testDb);

        var r = await avail.Check(Monday, new TimeOnly(10, 0), 30, t, excludedAppointmentId: appointmentId);
        r.HasOverlap.Should().BeFalse();
    }

    [Fact] // E-12
    public async Task A_time_outside_the_shop_hours_warns()
    {
        await using var testDb = new TestDatabase();
        await using (var db = testDb.Context())
        {
            db.ShopSchedule.Add(new ShopSchedule
            {
                Weekday = Weekday.Mon, OpeningTime = new TimeOnly(9, 0), ClosingTime = new TimeOnly(14, 0)
            });
            await db.SaveChangesAsync();
        }
        var avail = CreatesService(testDb);

        var r = await avail.Check(Monday, new TimeOnly(20, 0), 30, workerId: null);
        r.OutsideSchedule.Should().BeTrue();
    }

    [Fact] // E-13
    public async Task A_date_among_the_closed_days_warns_with_the_reason()
    {
        await using var testDb = new TestDatabase();
        await using (var db = testDb.Context())
        {
            db.ClosedDays.Add(new ClosedDay { Date = Monday, Reason = "Festiu local" });
            await db.SaveChangesAsync();
        }
        var avail = CreatesService(testDb);

        var r = await avail.Check(Monday, new TimeOnly(10, 0), 30, workerId: null);
        r.ClosedDay.Should().BeTrue();
        r.ClosedDayReason.Should().Be("Festiu local");
    }

    [Fact] // E-14
    public async Task No_notice_blocks_saving()
    {
        // Check never throws and never blocks: it only reports (RF-06)
        await using var testDb = new TestDatabase();
        int t = await AddsWorker(testDb);
        await AddsAppointment(testDb, new TimeOnly(10, 0), 30, t);
        var avail = CreatesService(testDb);

        var r = await avail.Check(Monday, new TimeOnly(10, 0), 30, t);
        r.HasOverlap.Should().BeTrue(); // still just information, nothing thrown
    }

    [Fact] // E-20
    public async Task GetByRange_includes_both_ends()
    {
        await using var testDb = new TestDatabase();
        var sunday = Monday.AddDays(6);
        await using (var db = testDb.Context())
        {
            db.Appointments.Add(new Appointment { Date = Monday, Time = new TimeOnly(9, 0), DurationMin = 30, GuestName = "A" });
            db.Appointments.Add(new Appointment { Date = sunday, Time = new TimeOnly(9, 0), DurationMin = 30, GuestName = "B" });
            await db.SaveChangesAsync();
        }

        var appointments = new AppointmentService(new TestFactory(testDb.Options));
        var result = await appointments.GetByRange(Monday, sunday);

        result.Should().HaveCount(2);
    }
}
