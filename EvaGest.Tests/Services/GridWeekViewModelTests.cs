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

    /// <summary>Most of these tests are about a whole week, so the fixture stores the
    /// week view unless a test asks for the three-day one (the app's default).</summary>
    private static async Task<(WeekGridViewModel grid, Record record)> Build(
        TestDatabase testDb, ModeGrid mode = ModeGrid.Agenda, int days = WeekGridViewModel.WholeWeek)
    {
        var factory = new TestFactory(testDb.Options);
        await new SettingsService(factory).Save(ConfigKeys.AgendaDays, days.ToString());
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
        var (grid, _) = await Build(testDb);

        await grid.LoadRange(Monday);

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
        var (grid, _) = await Build(testDb);

        await grid.LoadRange(Monday);

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
        var (grid, _) = await Build(testDb);

        await grid.LoadRange(Monday);

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
        var (grid, _) = await Build(testDb);

        await grid.LoadRange(Monday);

        // One padding hour either side of 09:00-20:00.
        grid.GridStartMinute.Should().Be(8 * 60);
        grid.GridEndMinute.Should().Be(21 * 60);
        // Before opening, the lunch gap, after closing: the lunch gap starts at 13:00.
        grid.Days[0].Bands.Should().HaveCount(3);
        grid.Days[0].Bands[1].Top.Should().BeApproximately(5 * 60 * grid.PixelsPerMinute, 1e-9);
    }

    [Fact] // GV-05
    public async Task Clicking_an_empty_slot_reports_the_day_and_the_snapped_time()
    {
        await using var testDb = new TestDatabase();
        await AddSchedule(testDb, Weekday.Wed, 9, 20);
        var (grid, record) = await Build(testDb);
        await grid.LoadRange(Monday);

        // 150 minutes after the start of the grid (08:00, the padding hour), in the wednesday column
        grid.Days[2].ClickAtPosition(150 * grid.PixelsPerMinute);

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
        var (grid, _) = await Build(testDb);

        await grid.LoadRange(Monday);

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
        var (grid, _) = await Build(testDb);

        await grid.LoadRange(Monday);

        grid.Days[0].Appointments.Should().OnlyContain(c => c.LaneCount == 2);
        grid.Days[0].Appointments.Select(c => c.LaneIndex).Should().BeEquivalentTo([0, 1]);
    }

    [Fact] // GV-08
    public async Task Appointment_outside_opening_hours_stays_visible()
    {
        await using var testDb = new TestDatabase();
        await AddSchedule(testDb, Weekday.Mon, 9, 20);
        await AddAppointment(testDb, Monday, 8, 30, name: "Matiner");
        var (grid, _) = await Build(testDb);

        await grid.LoadRange(Monday);

        grid.GridStartMinute.Should().Be(8 * 60);
        grid.Days[0].Appointments.Single().Top.Should().BeApproximately(30 * grid.PixelsPerMinute, 1e-9);
    }

    [Fact] // GV-09
    public async Task An_invalid_granularity_in_the_settings_falls_back_to_thirty()
    {
        await using var testDb = new TestDatabase();
        var factory = new TestFactory(testDb.Options);
        await new SettingsService(factory).Save(ConfigKeys.AgendaSlotMinutes, "45");
        var (grid, _) = await Build(testDb);

        await grid.LoadRange(Monday);

        grid.SlotMinutes.Should().Be(30);
    }

    [Fact] // GV-10
    public async Task A_valid_granularity_from_the_settings_is_applied()
    {
        await using var testDb = new TestDatabase();
        var factory = new TestFactory(testDb.Options);
        await new SettingsService(factory).Save(ConfigKeys.AgendaSlotMinutes, "15");
        var (grid, _) = await Build(testDb);

        await grid.LoadRange(Monday);

        grid.SlotMinutes.Should().Be(15);
        grid.PixelsPerMinute.Should().BeApproximately(grid.SlotHeightPx / 15, 1e-9);
    }

    [Fact] // GV-11
    public async Task Clicking_an_appointment_reports_the_whole_model()
    {
        await using var testDb = new TestDatabase();
        await AddSchedule(testDb, Weekday.Mon, 9, 20);
        int id = await AddAppointment(testDb, Monday, 10, 0, name: "Anna");
        var (grid, record) = await Build(testDb);
        await grid.LoadRange(Monday);

        grid.AppointmentClickCommand.Execute(grid.Days[0].Appointments.Single());

        record.Appointments.Should().ContainSingle().Which.Id.Should().Be(id);
    }

    [Fact] // GV-12
    public async Task The_ghost_block_follows_the_date_and_the_time()
    {
        await using var testDb = new TestDatabase();
        await AddSchedule(testDb, Weekday.Mon, 9, 20);
        await AddSchedule(testDb, Weekday.Wed, 9, 20);
        var (grid, _) = await Build(testDb, ModeGrid.Selector);
        await grid.LoadRange(Monday);

        grid.ShowGhost(Monday, new TimeOnly(10, 0), 30);
        grid.Days[0].Appointments.Should().ContainSingle(c => c.IsGhost);

        grid.ShowGhost(Monday.AddDays(2), new TimeOnly(12, 0), 60);
        grid.Days[0].Appointments.Should().NotContain(c => c.IsGhost);
        var ghost = grid.Days[2].Appointments.Should().ContainSingle(c => c.IsGhost).Subject;
        ghost.Top.Should().BeApproximately(4 * 60 * grid.PixelsPerMinute, 1e-9);   // grid starts at 08:00
        ghost.Height.Should().BeApproximately(60 * grid.PixelsPerMinute - 1, 1e-9);
    }

    [Fact] // GV-13
    public async Task The_picker_grid_is_more_compact_than_the_agenda()
    {
        await using var testDb = new TestDatabase();
        var (agenda, _) = await Build(testDb);
        var (selector, _) = await Build(testDb, ModeGrid.Selector);

        selector.SlotHeightPx.Should().BeLessThan(agenda.SlotHeightPx);
        selector.RulerWidthPx.Should().BeLessThan(agenda.RulerWidthPx);
    }

    [Fact] // GV-14
    public async Task Without_schedules_or_appointments_the_grid_shows_the_default_range()
    {
        await using var testDb = new TestDatabase();
        var (grid, _) = await Build(testDb);

        await grid.LoadRange(Monday);

        (grid.GridStartMinute, grid.GridEndMinute).Should().Be(GridHelper.DefaultRange);
        grid.Days.Should().OnlyContain(d => d.Bands.Count == 0,
            "ombrejar tota la setmana com a fora d'horari quan encara no hi ha horaris no diu res");
    }

    [Fact] // GV-16
    public async Task With_schedules_configured_the_days_without_one_are_shaded()
    {
        await using var testDb = new TestDatabase();
        await AddSchedule(testDb, Weekday.Mon, 9, 20);   // nameés monday opens
        var (grid, _) = await Build(testDb);

        await grid.LoadRange(Monday);

        grid.Days[0].Bands.Should().HaveCount(2, "only the padding hours fall outside monday's schedule");
        grid.Days[1].Bands.Should().ContainSingle("tuesday has no schedule");
    }

    [Fact] // GV-15
    public async Task Changing_week_reuses_the_same_columns()
    {
        await using var testDb = new TestDatabase();
        var (grid, _) = await Build(testDb);
        await grid.LoadRange(Monday);
        var before = grid.Days.ToList();

        await grid.LoadRange(Monday.AddDays(7));

        grid.Days.Should().BeSameAs(grid.Days);
        grid.Days.Zip(before).Should().OnlyContain(p => ReferenceEquals(p.First, p.Second));
        grid.Days[0].Date.Should().Be(Monday.AddDays(7));
    }

    // ---------- Three-day view ----------

    private static readonly DateOnly Wednesday = Monday.AddDays(2);

    [Fact] // GV-17
    public async Task The_three_day_view_starts_on_the_given_date_not_on_monday()
    {
        await using var testDb = new TestDatabase();
        var (grid, _) = await Build(testDb, days: WeekGridViewModel.ThreeDays);

        await grid.LoadRange(Wednesday);

        grid.IsThreeDayView.Should().BeTrue();
        grid.Days.Select(d => d.Date).Should().Equal(Wednesday, Wednesday.AddDays(1), Wednesday.AddDays(2));
        grid.RangeStart.Should().Be(Wednesday);
    }

    [Fact] // GV-18
    public async Task Without_a_stored_view_the_grid_shows_three_days()
    {
        await using var testDb = new TestDatabase();
        var factory = new TestFactory(testDb.Options);
        var grid = new WeekGridViewModel(
            new AppointmentService(factory), new AvailabilityService(factory), new SettingsService(factory),
            ModeGrid.Agenda, (_, _) => { });

        await grid.LoadRange(Wednesday);

        grid.Days.Should().HaveCount(3);
    }

    [Theory] // GV-19
    [InlineData("5")]
    [InlineData("0")]
    [InlineData("abc")]
    public async Task A_stored_day_count_other_than_seven_means_three(string stored)
    {
        await using var testDb = new TestDatabase();
        var factory = new TestFactory(testDb.Options);
        await new SettingsService(factory).Save(ConfigKeys.AgendaDays, stored);
        var grid = new WeekGridViewModel(
            new AppointmentService(factory), new AvailabilityService(factory), new SettingsService(factory),
            ModeGrid.Agenda, (_, _) => { });

        await grid.LoadRange(Wednesday);

        grid.Days.Should().HaveCount(3);
    }

    [Fact] // GV-20
    public async Task In_three_days_the_arrows_move_one_day_and_the_double_arrows_three()
    {
        await using var testDb = new TestDatabase();
        var (grid, _) = await Build(testDb, days: WeekGridViewModel.ThreeDays);
        await grid.LoadRange(Wednesday);

        await grid.StepForwardCommand.ExecuteAsync(null);
        grid.RangeStart.Should().Be(Wednesday.AddDays(1));

        await grid.StepBackCommand.ExecuteAsync(null);
        grid.RangeStart.Should().Be(Wednesday);

        await grid.JumpForwardCommand.ExecuteAsync(null);
        grid.RangeStart.Should().Be(Wednesday.AddDays(3));

        await grid.JumpBackCommand.ExecuteAsync(null);
        grid.RangeStart.Should().Be(Wednesday);
    }

    [Fact] // GV-21
    public async Task In_the_week_view_the_arrows_still_move_a_whole_week()
    {
        await using var testDb = new TestDatabase();
        var (grid, _) = await Build(testDb);
        await grid.LoadRange(Wednesday);

        grid.RangeStart.Should().Be(Monday, "the week view always starts on monday");

        await grid.StepForwardCommand.ExecuteAsync(null);
        grid.RangeStart.Should().Be(Monday.AddDays(7));
    }

    [Fact] // GV-22
    public async Task Switching_to_the_week_snaps_to_monday_and_is_remembered()
    {
        await using var testDb = new TestDatabase();
        var (grid, _) = await Build(testDb, days: WeekGridViewModel.ThreeDays);
        await grid.LoadRange(Wednesday);

        await grid.ToggleViewCommand.ExecuteAsync(null);

        grid.IsThreeDayView.Should().BeFalse();
        grid.Days.Should().HaveCount(7);
        grid.RangeStart.Should().Be(Monday);
        (await new SettingsService(new TestFactory(testDb.Options)).GetInt(ConfigKeys.AgendaDays, 0))
            .Should().Be(7, "the dialog's picker and the next session must open on the same view");
    }

    [Fact] // GV-23
    public async Task Switching_to_three_days_keeps_today_on_screen()
    {
        var today = DateOnly.FromDateTime(DateTime.Today);
        await using var testDb = new TestDatabase();
        var (grid, _) = await Build(testDb);
        await grid.LoadRange(today);

        await grid.ToggleViewCommand.ExecuteAsync(null);

        grid.IsThreeDayView.Should().BeTrue();
        grid.RangeStart.Should().Be(today);
    }

    [Fact] // GV-24
    public async Task Switching_to_three_days_away_from_today_starts_on_the_first_day_shown()
    {
        await using var testDb = new TestDatabase();
        var (grid, _) = await Build(testDb);
        await grid.LoadRange(Monday);   // a week in the past, today is not in it

        await grid.ToggleViewCommand.ExecuteAsync(null);

        grid.RangeStart.Should().Be(Monday);
        grid.Days.Should().HaveCount(3);
    }

    [Fact] // GV-25
    public async Task Appointments_land_in_the_right_column_of_the_three_day_view()
    {
        await using var testDb = new TestDatabase();
        await AddAppointment(testDb, Wednesday.AddDays(1), 10, 0, name: "Anna");
        await AddAppointment(testDb, Monday, 10, 0, name: "Fora");   // before the range
        var (grid, _) = await Build(testDb, days: WeekGridViewModel.ThreeDays);

        await grid.LoadRange(Wednesday);

        grid.Days[0].Appointments.Should().BeEmpty();
        grid.Days[1].Appointments.Should().ContainSingle().Which.Title.Should().Be("Anna");
        grid.Days.SelectMany(d => d.Appointments).Should().NotContain(a => a.Title == "Fora");
    }

    [Fact] // GV-26
    public async Task The_three_day_view_fits_its_hours_to_the_days_shown()
    {
        await using var testDb = new TestDatabase();
        await AddSchedule(testDb, Weekday.Wed, 10, 14);
        await AddSchedule(testDb, Weekday.Sat, 7, 22);   // not on screen from wednesday
        var (grid, _) = await Build(testDb, days: WeekGridViewModel.ThreeDays);

        await grid.LoadRange(Wednesday);

        // One padding hour either side of wednesday's 10:00-14:00, saturday ignored
        (grid.GridStartMinute, grid.GridEndMinute).Should().Be((9 * 60, 15 * 60));
    }
}
