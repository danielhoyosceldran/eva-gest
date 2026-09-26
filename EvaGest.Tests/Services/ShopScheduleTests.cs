using AwesomeAssertions;
using EvaGest.Models;
using EvaGest.Services;
using EvaGest.Tests.Infra;
using EvaGest.ViewModels.Pages;
using Xunit;

namespace EvaGest.Tests.Services;

/// <summary>Block N: the opening hours, from the form through to the grid.</summary>
public class ScheduleShopTests
{
    private static SettingsViewModel Build(TestDatabase testDb)
    {
        var factory = new TestFactory(testDb.Options);
        var paths = new AppPaths(Path.Combine(Path.GetTempPath(), "eva-prova.db"), Path.GetTempPath());
        return new SettingsViewModel(
            new BackupService(paths, new SettingsService(factory)), new ExportService(factory),
            new SettingsService(factory), new AvailabilityService(factory),
            new TestDialogService(), TestOwner.New());
    }

    [Fact] // N-01
    public async Task Saving_the_schedule_leaves_it_readable_by_the_grid()
    {
        await using var testDb = new TestDatabase();
        var vm = Build(testDb);
        await vm.Load();

        var monday = vm.ScheduleDays[0];
        monday.WorksMorning = true;
        monday.WorksAfternoon = true;
        monday.MorningStart = "09:00";
        monday.MorningEnd = "13:00";
        monday.AfternoonStart = "16:00";
        monday.AfternoonEnd = "20:00";

        await vm.SaveScheduleCommand.ExecuteAsync(null);

        vm.ScheduleError.Should().BeNull();
        var intervals = await new AvailabilityService(new TestFactory(testDb.Options)).WeeklyIntervals();
        intervals[Weekday.Mon].Should().Equal(
            (new TimeOnly(9, 0), new TimeOnly(13, 0)),
            (new TimeOnly(16, 0), new TimeOnly(20, 0)));
        intervals.Should().NotContainKey(Weekday.Tue);
    }

    [Fact] // N-02
    public async Task Loading_again_refills_the_form_with_what_was_saved()
    {
        await using var testDb = new TestDatabase();
        var vm = Build(testDb);
        await vm.Load();
        vm.ScheduleDays[2].WorksMorning = true;
        vm.ScheduleDays[2].MorningStart = "10:00";
        vm.ScheduleDays[2].MorningEnd = "18:00";
        await vm.SaveScheduleCommand.ExecuteAsync(null);

        var other = Build(testDb);
        await other.Load();

        var wednesday = other.ScheduleDays[2];
        wednesday.Open.Should().BeTrue();
        wednesday.MorningStart.Should().Be("10:00");
        wednesday.MorningEnd.Should().Be("18:00");
        wednesday.AfternoonStart.Should().BeEmpty();
        other.ScheduleDays[0].Open.Should().BeFalse();
    }

    [Fact] // N-03
    public async Task A_day_bad_filled_blocks_the_saved_i_is_mark
        () {
        await using var testDb = new TestDatabase();
        var vm = Build(testDb);
        await vm.Load();
        vm.ScheduleDays[0].WorksMorning = true;
        vm.ScheduleDays[0].MorningStart = "20:00";
        vm.ScheduleDays[0].MorningEnd = "09:00";

        await vm.SaveScheduleCommand.ExecuteAsync(null);

        vm.ScheduleError.Should().NotBeNull();
        vm.ScheduleDays[0].Error.Should().NotBeNull();
        var intervals = await new AvailabilityService(new TestFactory(testDb.Options)).WeeklyIntervals();
        intervals.Should().BeEmpty("nothing may be saved while there are errors");
    }

    [Fact] // N-04
    public async Task Saving_replaces_the_previous_schedule_instead_of_adding_to_it()
    {
        await using var testDb = new TestDatabase();
        var vm = Build(testDb);
        await vm.Load();
        vm.ScheduleDays[0].WorksMorning = true;
        vm.ScheduleDays[0].MorningStart = "09:00";
        vm.ScheduleDays[0].MorningEnd = "20:00";
        await vm.SaveScheduleCommand.ExecuteAsync(null);

        vm.ScheduleDays[0].MorningStart = "10:00";
        await vm.SaveScheduleCommand.ExecuteAsync(null);

        await using var db = testDb.Context();
        db.ShopSchedule.Count().Should().Be(1);
        db.ShopSchedule.Single().OpeningTime.Should().Be(new TimeOnly(10, 0));
    }

    [Fact] // N-05
    public async Task Closing_every_day_empties_the_schedule_and_warns()
    {
        await using var testDb = new TestDatabase();
        var vm = Build(testDb);
        await vm.Load();
        vm.ScheduleDays[0].WorksMorning = true;
        vm.ScheduleDays[0].MorningStart = "09:00";
        vm.ScheduleDays[0].MorningEnd = "20:00";
        await vm.SaveScheduleCommand.ExecuteAsync(null);

        vm.ScheduleDays[0].WorksMorning = false;
        await vm.SaveScheduleCommand.ExecuteAsync(null);

        vm.ScheduleError.Should().BeNull();
        vm.ScheduleConfirmation.Should().Contain("tancats");
        await using var db = testDb.Context();
        db.ShopSchedule.Should().BeEmpty();
    }

    [Fact] // N-06
    public async Task Copying_monday_fills_tuesday_to_friday_and_not_the_weekend()
    {
        await using var testDb = new TestDatabase();
        var vm = Build(testDb);
        await vm.Load();
        vm.ScheduleDays[0].WorksMorning = true;
        vm.ScheduleDays[0].MorningStart = "09:00";
        vm.ScheduleDays[0].MorningEnd = "20:00";

        vm.ApplyMondayToRestCommand.Execute(null);

        vm.ScheduleDays.Take(5).Should().OnlyContain(d => d.Open && d.MorningStart == "09:00");
        vm.ScheduleDays[5].Open.Should().BeFalse("saturday must be left alone");
        vm.ScheduleDays[6].Open.Should().BeFalse("sunday must be left alone");
    }

    [Fact] // N-07
    public async Task The_saved_schedule_reaches_the_grid_as_outside_hours_bands()
    {
        await using var testDb = new TestDatabase();
        var vm = Build(testDb);
        await vm.Load();
        foreach (var day in vm.ScheduleDays.Take(5))
        {
            day.WorksMorning = true;
            day.MorningStart = "10:00";
            day.MorningEnd = "18:00";
        }
        await vm.SaveScheduleCommand.ExecuteAsync(null);

        var factory = new TestFactory(testDb.Options);
        await new SettingsService(factory).Save(ConfigKeys.AgendaDays, "7");   // sunday must be on screen
        var grid = new EvaGest.ViewModels.Elements.WeekGridViewModel(
            new AppointmentService(factory), new AvailabilityService(factory), new SettingsService(factory),
            EvaGest.ViewModels.Elements.ModeGrid.Agenda, (_, _) => { });
        await grid.LoadRange(new DateOnly(2026, 9, 7));

        (grid.GridStartMinute, grid.GridEndMinute).Should().Be((9 * 60, 19 * 60));   // plus the padding hours
        grid.Days[0].Bands.Should().HaveCount(2, "only the padding hours fall outside monday's schedule");
        grid.Days[6].Bands.Should().ContainSingle("sunday is closed all day");
    }

    [Fact] // N-08
    public async Task Ticking_a_shift_proposes_the_usual_hours()
    {
        await using var testDb = new TestDatabase();
        var vm = Build(testDb);
        await vm.Load();

        var monday = vm.ScheduleDays[0];
        monday.WorksAfternoon = true;

        monday.AfternoonStart.Should().Be("16:00");
        monday.AfternoonEnd.Should().Be("20:00");
        monday.MorningStart.Should().BeEmpty("the morning was not ticked");
    }

    [Fact] // N-09
    public async Task An_afternoon_only_day_saves_and_reloads_into_the_afternoon_row()
    {
        await using var testDb = new TestDatabase();
        var vm = Build(testDb);
        await vm.Load();
        vm.ScheduleDays[1].WorksAfternoon = true;
        vm.ScheduleDays[1].AfternoonStart = "16:00";
        vm.ScheduleDays[1].AfternoonEnd = "20:00";
        await vm.SaveScheduleCommand.ExecuteAsync(null);

        var other = Build(testDb);
        await other.Load();

        var tuesday = other.ScheduleDays[1];
        tuesday.WorksAfternoon.Should().BeTrue();
        tuesday.WorksMorning.Should().BeFalse();
        tuesday.AfternoonStart.Should().Be("16:00");
        tuesday.MorningStart.Should().BeEmpty();
    }

    [Fact] // N-10
    public async Task Unticking_a_shift_does_not_force_its_hours_to_be_cleared()
    {
        await using var testDb = new TestDatabase();
        var vm = Build(testDb);
        await vm.Load();
        var monday = vm.ScheduleDays[0];
        monday.WorksMorning = true;
        monday.WorksAfternoon = true;
        monday.AfternoonStart = "16:00";
        monday.AfternoonEnd = "14:00"; // invalid, but about to be switched off
        monday.WorksAfternoon = false;

        await vm.SaveScheduleCommand.ExecuteAsync(null);

        vm.ScheduleError.Should().BeNull();
        var intervals = await new AvailabilityService(new TestFactory(testDb.Options)).WeeklyIntervals();
        intervals[Weekday.Mon].Should().ContainSingle();
    }
}
