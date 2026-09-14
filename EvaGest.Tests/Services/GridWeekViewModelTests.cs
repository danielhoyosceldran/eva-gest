using AwesomeAssertions;
using EvaGest.Helpers;
using EvaGest.Models;
using EvaGest.Services;
using EvaGest.Tests.Infra;
using EvaGest.ViewModels.Elements;
using Xunit;

namespace EvaGest.Tests.Services;

public class GridWeekViewModelTests
{
    private static readonly DateOnly Monday = new(2026, 9, 7);

    private sealed class Record
    {
        public List<(DateOnly, TimeOnly)> Slots { get; } = [];
        public List<Appointment> Appointments { get; } = [];
    }

    private static (WeekGridViewModel grid, Record record) Build(
        TestDatabase testDb, ModeGrid mode = ModeGrid.Agenda)
    {
        var factory = new TestFactory(testDb.Options);
        var record = new Record();
        var grid = new WeekGridViewModel(
            new AppointmentService(factory), new AvailabilityService(factory), new SettingsService(factory),
            mode,
            (d, h) => record.Slots.Add((d, h)),
            c => record.Appointments.Add(c));
        return (grid, record);
    }

    private static async Task AddSchedule(TestDatabase testDb, Weekday day, int opens, int closes)
    {
        await using var db = testDb.Context();
        db.ShopSchedule.Add(new ShopSchedule
        {
            Weekday = day,
            OpeningTime = new TimeOnly(opens, 0),
            ClosingTime = new TimeOnly(closes, 0)
        });
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    private static async Task<int> AddAppointment(TestDatabase testDb, DateOnly date, int time, int minute,
        int duration = 30, AppointmentStatus status = AppointmentStatus.Pending, string name = "Convidat")
    {
        await using var db = testDb.Context();
        var appointment = new Appointment
        {
            Date = date, Time = new TimeOnly(time, minute), DurationMin = duration,
            GuestName = name, Status = status
        };
        db.Appointments.Add(appointment);
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        return appointment.Id;
    }

    [Fact] // GV-01
    public async Task A_week_always_has_seven_days_starting_on_monday()
    {
        await using var testDb = new TestDatabase();
        var (grid, _) = Build(testDb);

        await grid.LoadWeek(Monday);

        grid.Days.Should().HaveCount(7);
        grid.Days[0].Date.Should().Be(Monday);
        grid.Days[6].Date.Should().Be(Monday.AddDays(6));
    }

    [Fact] // GV-02
    public async Task Each_appointment_lands_in_the_column_of_its_day()
    {
        await using var testDb = new TestDatabase();
        await AddSchedule(testDb, Weekday.Mon, 9, 20);
        await AddAppointment(testDb, Monday, 10, 0, name: "Anna");
        await AddAppointment(testDb, Monday.AddDays(2), 11, 0, name: "Berta");
        var (grid, _) = Build(testDb);

        await grid.LoadWeek(Monday);

        grid.Days[0].Appointments.Should().ContainSingle().Which.Title.Should().Be("Anna");
        grid.Days[1].Appointments.Should().BeEmpty();
        grid.Days[2].Appointments.Should().ContainSingle().Which.Title.Should().Be("Berta");
    }

    [Fact] // GV-03
    public async Task A_closed_day_is_marked_and_shaded_top_to_bottom()
    {
        await using var testDb = new TestDatabase();
        await AddSchedule(testDb, Weekday.Mon, 9, 20);
        await using (var db = testDb.Context())
        {
            db.ClosedDays.Add(new ClosedDay { Date = Monday, Reason = "Festiu local" });
            await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        }
        var (grid, _) = Build(testDb);

        await grid.LoadWeek(Monday);

        var day = grid.Days[0];
        day.Closed.Should().BeTrue();
        day.ClosedReason.Should().Be("Festiu local");
        day.Bands.Should().ContainSingle();
        day.Bands[0].Type.Should().Be(BandType.ClosedDay);
        day.Bands[0].Height.Should().BeApproximately(grid.TotalHeightPx, 1e-9);
    }

    [Fact] // GV-04
    public async Task A_split_shift_leaves_the_midday_band()
    {
        await using var testDb = new TestDatabase();
        await AddSchedule(testDb, Weekday.Mon, 9, 13);
        await AddSchedule(testDb, Weekday.Mon, 16, 20);
        var (grid, _) = Build(testDb);

        await grid.LoadWeek(Monday);

        grid.GridStartMinute.Should().Be(9 * 60);
        grid.GridEndMinute.Should().Be(20 * 60);
        grid.Days[0].Bands.Should().ContainSingle()
            .Which.Top.Should().BeApproximately(4 * 60 * grid.PixelsPerMinute, 1e-9);
    }

    [Fact] // GV-05
    public async Task Clicking_an_empty_slot_reports_the_day_and_the_snapped_time()
    {
        await using var testDb = new TestDatabase();
        await AddSchedule(testDb, Weekday.Wed, 9, 20);
        var (grid, record) = Build(testDb);
        await grid.LoadWeek(Monday);

        // 90 minutes after the start of the grid, in the wednesday column
        grid.Days[2].ClickAtPosition(90 * grid.PixelsPerMinute);

        record.Slots.Should().ContainSingle()
            .Which.Should().Be((Monday.AddDays(2), new TimeOnly(10, 30)));
    }

    [Fact] // GV-06
    public async Task Cancelled_appointments_take_no_lane()
    {
        await using var testDb = new TestDatabase();
        await AddSchedule(testDb, Weekday.Mon, 9, 20);
        await AddAppointment(testDb, Monday, 10, 0, status: AppointmentStatus.Cancelled, name: "Anul·lada");
        await AddAppointment(testDb, Monday, 10, 0, name: "Vigent");
        var (grid, _) = Build(testDb);

        await grid.LoadWeek(Monday);

        var current = grid.Days[0].Appointments.Single(c => c.Title == "Vigent");
        current.LaneCount.Should().Be(1);
        current.LaneIndex.Should().Be(0);
        grid.Days[0].Appointments.Single(c => c.Title == "Anul·lada").IsCancelled.Should().BeTrue();
    }

    [Fact] // GV-07
    public async Task Two_overlapping_appointments_split_the_column()
    {
        await using var testDb = new TestDatabase();
        await AddSchedule(testDb, Weekday.Mon, 9, 20);
        await AddAppointment(testDb, Monday, 10, 0, duration: 60, name: "Anna");
        await AddAppointment(testDb, Monday, 10, 30, duration: 60, name: "Berta");
        var (grid, _) = Build(testDb);

        await grid.LoadWeek(Monday);

        grid.Days[0].Appointments.Should().OnlyContain(c => c.LaneCount == 2);
        grid.Days[0].Appointments.Select(c => c.LaneIndex).Should().BeEquivalentTo([0, 1]);
    }

    [Fact] // GV-08
    public async Task Appointment_outside_opening_hours_stays_visible()
    {
        await using var testDb = new TestDatabase();
        await AddSchedule(testDb, Weekday.Mon, 9, 20);
        await AddAppointment(testDb, Monday, 8, 30, name: "Matiner");
        var (grid, _) = Build(testDb);

        await grid.LoadWeek(Monday);

        grid.GridStartMinute.Should().Be(8 * 60);
        grid.Days[0].Appointments.Single().Top.Should().BeApproximately(30 * grid.PixelsPerMinute, 1e-9);
    }

    [Fact] // GV-09
    public async Task An_invalid_granularity_in_the_settings_falls_back_to_thirty()
    {
        await using var testDb = new TestDatabase();
        var factory = new TestFactory(testDb.Options);
        await new SettingsService(factory).Save(ConfigKeys.AgendaSlotMinutes, "45");
        var (grid, _) = Build(testDb);

        await grid.LoadWeek(Monday);

        grid.SlotMinutes.Should().Be(30);
    }

    [Fact] // GV-10
    public async Task A_valid_granularity_from_the_settings_is_applied()
    {
        await using var testDb = new TestDatabase();
        var factory = new TestFactory(testDb.Options);
        await new SettingsService(factory).Save(ConfigKeys.AgendaSlotMinutes, "15");
        var (grid, _) = Build(testDb);

        await grid.LoadWeek(Monday);

        grid.SlotMinutes.Should().Be(15);
        grid.PixelsPerMinute.Should().BeApproximately(grid.SlotHeightPx / 15, 1e-9);
    }

    [Fact] // GV-11
    public async Task Clicking_an_appointment_reports_the_whole_model()
    {
        await using var testDb = new TestDatabase();
        await AddSchedule(testDb, Weekday.Mon, 9, 20);
        int id = await AddAppointment(testDb, Monday, 10, 0, name: "Anna");
        var (grid, record) = Build(testDb);
        await grid.LoadWeek(Monday);

        grid.AppointmentClickCommand.Execute(grid.Days[0].Appointments.Single());

        record.Appointments.Should().ContainSingle().Which.Id.Should().Be(id);
    }

    [Fact] // GV-12
    public async Task The_ghost_block_follows_the_date_and_the_time()
    {
        await using var testDb = new TestDatabase();
        await AddSchedule(testDb, Weekday.Mon, 9, 20);
        await AddSchedule(testDb, Weekday.Wed, 9, 20);
        var (grid, _) = Build(testDb, ModeGrid.Selector);
        await grid.LoadWeek(Monday);

        grid.ShowGhost(Monday, new TimeOnly(10, 0), 30);
        grid.Days[0].Appointments.Should().ContainSingle(c => c.IsGhost);

        grid.ShowGhost(Monday.AddDays(2), new TimeOnly(12, 0), 60);
        grid.Days[0].Appointments.Should().NotContain(c => c.IsGhost);
        var ghost = grid.Days[2].Appointments.Should().ContainSingle(c => c.IsGhost).Subject;
        ghost.Top.Should().BeApproximately(3 * 60 * grid.PixelsPerMinute, 1e-9);
        ghost.Height.Should().BeApproximately(60 * grid.PixelsPerMinute - 1, 1e-9);
    }

    [Fact] // GV-13
    public async Task The_picker_grid_is_more_compact_than_the_agenda()
    {
        await using var testDb = new TestDatabase();
        var (agenda, _) = Build(testDb);
        var (selector, _) = Build(testDb, ModeGrid.Selector);

        selector.SlotHeightPx.Should().BeLessThan(agenda.SlotHeightPx);
        selector.RulerWidthPx.Should().BeLessThan(agenda.RulerWidthPx);
    }

    [Fact] // GV-14
    public async Task Without_schedules_or_appointments_the_grid_shows_the_default_range()
    {
        await using var testDb = new TestDatabase();
        var (grid, _) = Build(testDb);

        await grid.LoadWeek(Monday);

        (grid.GridStartMinute, grid.GridEndMinute).Should().Be(GridHelper.DefaultRange);
        grid.Days.Should().OnlyContain(d => d.Bands.Count == 0,
            "ombrejar tota la setmana com a fora d'horari quan encara no hi ha horaris no diu res");
    }

    [Fact] // GV-16
    public async Task With_schedules_configured_the_days_without_one_are_shaded()
    {
        await using var testDb = new TestDatabase();
        await AddSchedule(testDb, Weekday.Mon, 9, 20);   // nameés monday opens
        var (grid, _) = Build(testDb);

        await grid.LoadWeek(Monday);

        grid.Days[0].Bands.Should().BeEmpty("monday is open across the whole visible range");
        grid.Days[1].Bands.Should().ContainSingle("tuesday has no schedule");
    }

    [Fact] // GV-15
    public async Task Changing_week_reuses_the_same_columns()
    {
        await using var testDb = new TestDatabase();
        var (grid, _) = Build(testDb);
        await grid.LoadWeek(Monday);
        var before = grid.Days.ToList();

        await grid.LoadWeek(Monday.AddDays(7));

        grid.Days.Should().BeSameAs(grid.Days);
        grid.Days.Zip(before).Should().OnlyContain(p => ReferenceEquals(p.First, p.Second));
        grid.Days[0].Date.Should().Be(Monday.AddDays(7));
    }
}
