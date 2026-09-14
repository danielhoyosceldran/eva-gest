using AwesomeAssertions;
using EvaGest.Helpers;
using EvaGest.Models;
using EvaGest.Services;
using EvaGest.Tests.Infra;
using EvaGest.ViewModels.Dialogs;
using EvaGest.ViewModels.Pages;
using Xunit;

namespace EvaGest.Tests.Services;

/// <summary>
/// Block O: workers and their schedules. Without these rows the availability
/// calculation (block E) counts zero workers and always warns about an overlap, so
/// these tests cover what makes the agenda mean anything at all.
/// </summary>
public class WorkerServiceTests
{
    private static Dictionary<Weekday, List<(TimeOnly start, TimeOnly fi)>> Schedule(
        params (Weekday day, int fromTime, int toTime)[] intervals)
        => intervals
            .GroupBy(f => f.day)
            .ToDictionary(
                g => g.Key,
                g => g.Select(f => (new TimeOnly(f.fromTime, 0), new TimeOnly(f.toTime, 0))).ToList());

    [Fact] // O-01
    public async Task Create_saves_the_worker_with_all_her_intervals()
    {
        await using var testDb = new TestDatabase();
        var service = new WorkerService(new TestFactory(testDb.Options));

        await service.Create(Make.Worker("Marta"),
            Schedule((Weekday.Mon, 9, 14), (Weekday.Mon, 16, 20), (Weekday.Tue, 9, 14)));

        var schedule = await service.GetSchedule((await service.GetAll()).Single().Id);
        schedule[Weekday.Mon].Should().Equal(
            (new TimeOnly(9, 0), new TimeOnly(14, 0)),
            (new TimeOnly(16, 0), new TimeOnly(20, 0)));
        schedule[Weekday.Tue].Should().ContainSingle();
        schedule.Should().NotContainKey(Weekday.Wed);
    }

    [Fact] // O-02
    public async Task Update_replaces_the_schedule_instead_of_adding_to_it()
    {
        await using var testDb = new TestDatabase();
        var service = new WorkerService(new TestFactory(testDb.Options));
        var created = await service.Create(Make.Worker(), Schedule((Weekday.Mon, 9, 14)));

        created.Name = "Marta Puig";
        await service.Update(created, Schedule((Weekday.Fri, 10, 18)));

        await using var db = testDb.Context();
        db.WorkerSchedules.Should().ContainSingle();
        db.WorkerSchedules.Single().Weekday.Should().Be(Weekday.Fri);
        db.Workers.Single().Name.Should().Be("Marta Puig");
    }

    [Fact] // O-03
    public async Task Marking_a_worker_inactive_keeps_her_schedule_but_drops_her_from_availability()
    {
        await using var testDb = new TestDatabase();
        var factory = new TestFactory(testDb.Options);
        var service = new WorkerService(factory);
        var created = await service.Create(Make.Worker(), Schedule((Weekday.Mon, 9, 20)));

        await service.ChangeStatus(created.Id, active: false);

        var schedule = await service.GetSchedule(created.Id);
        schedule[Weekday.Mon].Should().ContainSingle("the schedule is kept for when she comes back");

        var available = await new AvailabilityService(factory)
            .AvailableWorkers(new DateOnly(2026, 9, 7), new TimeOnly(10, 0), 30);
        available.Should().BeEmpty("an inactive worker does not count as available");
    }

    [Fact] // O-04
    public async Task A_worker_with_a_schedule_avoids_the_overlap_notice_on_the_first_appointment()
    {
        await using var testDb = new TestDatabase();
        var factory = new TestFactory(testDb.Options);
        await new WorkerService(factory).Create(Make.Worker(), Schedule((Weekday.Mon, 9, 20)));

        var result = await new AvailabilityService(factory)
            .Check(new DateOnly(2026, 9, 7), new TimeOnly(10, 0), 30, workerId: null);

        result.AvailableWorkers.Should().Be(1);
        result.HasOverlap.Should().BeFalse();
    }

    [Fact] // O-05
    public async Task The_page_lists_the_workers_with_a_summarised_schedule()
    {
        await using var testDb = new TestDatabase();
        var service = new WorkerService(new TestFactory(testDb.Options));
        await service.Create(Make.Worker("Marta"), Schedule((Weekday.Mon, 9, 14), (Weekday.Tue, 9, 14)));

        var vm = new WorkersViewModel(service, new TestDialogService());
        await vm.Load();

        vm.HasNone.Should().BeFalse();
        var row = vm.Rows.Single();
        row.ScheduleSummary.Should().Be("Dl, Dt · 09:00–14:00");
        row.StatusText.Should().Be("Activa");
        row.Days.Should().HaveCount(7);
        row.Days[0].Hex.Should().Be(row.Worker.Color);
        row.Days[2].Hex.Should().Be("#00000000", "dimecres no treballa");
    }

    [Fact] // O-06
    public async Task With_no_worker_the_page_shows_the_empty_state()
    {
        await using var testDb = new TestDatabase();
        var vm = new WorkersViewModel(
            new WorkerService(new TestFactory(testDb.Options)), new TestDialogService());

        await vm.Load();

        vm.HasNone.Should().BeTrue();
        vm.Rows.Should().BeEmpty();
    }

    [Fact] // O-07
    public async Task Creating_from_the_page_saves_what_was_filled_in_the_dialog()
    {
        await using var testDb = new TestDatabase();
        var service = new WorkerService(new TestFactory(testDb.Options));
        var dialogs = new TestDialogService
        {
            ResultDialog = true,
            FillDialog = d =>
            {
                var dialog = (WorkerDialogViewModel)d;
                dialog.Name = "Berta";
                dialog.ScheduleDays[0].WorksMorning = true;
                dialog.ScheduleDays[0].MorningStart = "09:00";
                dialog.ScheduleDays[0].MorningEnd = "14:00";
                dialog.SaveCommand.Execute(null);
                return Task.CompletedTask;
            }
        };

        var vm = new WorkersViewModel(service, dialogs);
        await vm.Load();
        await vm.NewWorkerCommand.ExecuteAsync(null);

        vm.Rows.Should().ContainSingle();
        vm.Rows[0].Worker.Name.Should().Be("Berta");
        (await service.GetSchedule(vm.Rows[0].Worker.Id))[Weekday.Mon].Should().ContainSingle();
    }

    [Fact] // O-08
    public void The_dialog_refuses_to_save_a_worker_with_no_interval()
    {
        var dialog = new WorkerDialogViewModel([]) { Name = "Berta" };

        dialog.SaveCommand.Execute(null);

        dialog.ErrorValidation.Should().Be("Indica com a mínim un dia i un horari.");
    }

    [Fact] // O-09
    public void The_dialog_refuses_to_save_without_a_name()
    {
        var dialog = new WorkerDialogViewModel([]);
        dialog.ScheduleDays[0].WorksMorning = true;
        dialog.ScheduleDays[0].MorningStart = "09:00";
        dialog.ScheduleDays[0].MorningEnd = "14:00";

        dialog.SaveCommand.Execute(null);

        dialog.ErrorValidation.Should().Contain("nom");
    }

    [Fact] // O-10
    public void The_dialog_flags_the_day_whose_hours_run_backwards()
    {
        var dialog = new WorkerDialogViewModel([]) { Name = "Berta" };
        dialog.ScheduleDays[0].WorksMorning = true;
        dialog.ScheduleDays[0].MorningStart = "20:00";
        dialog.ScheduleDays[0].MorningEnd = "09:00";

        dialog.SaveCommand.Execute(null);

        dialog.ErrorValidation.Should().Contain("vermell");
        dialog.ScheduleDays[0].Error.Should().NotBeNull();
    }

    [Fact] // O-11
    public void Copying_monday_fills_tuesday_to_friday_and_not_the_weekend()
    {
        var dialog = new WorkerDialogViewModel([]) { Name = "Berta" };
        dialog.ScheduleDays[0].WorksMorning = true;
        dialog.ScheduleDays[0].MorningStart = "09:00";
        dialog.ScheduleDays[0].MorningEnd = "14:00";

        dialog.CopyToWeekdaysCommand.Execute(null);

        dialog.ScheduleDays.Take(5).Should().OnlyContain(d => d.Open && d.MorningStart == "09:00");
        dialog.ScheduleDays[5].Open.Should().BeFalse();
        dialog.ScheduleDays[6].Open.Should().BeFalse();
    }

    [Fact] // O-12
    public void The_suggested_colour_is_the_first_one_nobody_uses()
    {
        var first = WorkerPalette.Colors[0].Hex;
        var second = WorkerPalette.Colors[1].Hex;

        new WorkerDialogViewModel([]).Color.Hex.Should().Be(first);
        new WorkerDialogViewModel([first]).Color.Hex.Should().Be(second);
    }
}
