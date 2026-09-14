using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.Input;
using EvaGest.Helpers;
using EvaGest.Models;
using EvaGest.Services;
using EvaGest.ViewModels.Dialogs;
using EvaGest.Resources;

namespace EvaGest.ViewModels.Pages;

/// <summary>
/// Workers (pantalles 2.4). Defining who works and when is not cosmetic: the
/// weekly schedules stored here are what the overlap warning counts against, so this
/// page is the one that has to be filled in before the agenda means anything.
/// </summary>
public partial class WorkersViewModel(IWorkerService workers, IDialogService dialogs)
    : PageViewModelBase
{
    public override string Title => Texts.NavWorkers;

    /// <summary>One cell of the weekly overview grid. A day she does not work carries a
    /// fully transparent colour rather than no colour, so the grid keeps its shape.</summary>
    public record DayCell(string Hex, string Detail);

    public record WorkerRow(
        Worker Worker,
        string ScheduleSummary,
        string StatusText,
        string StatusActionText,
        IReadOnlyList<DayCell> Days);

    public ObservableCollection<WorkerRow> Rows { get; } = [];

    public bool HasNone => Rows.Count == 0;

    public IReadOnlyList<string> DayNames { get; } =
        [.. Enum.GetValues<Weekday>().Select(d => d.ToString())];

    public async Task Load()
    {
        Loading = true;
        try
        {
            var all = await workers.GetAll();

            Rows.Clear();
            foreach (var t in all)
            {
                var schedule = GroupedByDay(t);
                Rows.Add(new WorkerRow(
                    t,
                    Summarize(schedule),
                    t.Active ? Texts.Active : Texts.Inactive,
                    t.Active ? Texts.MarkInactive : Texts.Reactivate,
                    [.. Enum.GetValues<Weekday>().Select(d => ToCell(t, schedule, d))]));
            }

            OnPropertyChanged(nameof(HasNone));
        }
        finally { Loading = false; }
    }

    [RelayCommand]
    private async Task NewWorker()
    {
        var vm = new WorkerDialogViewModel(Rows.Select(f => f.Worker.Color));
        if (!await dialogs.ShowDialog(vm)) return;

        await workers.Create(vm.AModel(), vm.ToSchedule());
        await Load();
    }

    [RelayCommand]
    private async Task EditWorker(Worker worker)
    {
        var schedule = await workers.GetSchedule(worker.Id);
        var vm = new WorkerDialogViewModel(worker, schedule);
        if (!await dialogs.ShowDialog(vm)) return;

        await workers.Update(vm.AModel(), vm.ToSchedule());
        await Load();
    }

    /// <summary>No confirmation: it is reversible and happens every holiday (pantalles 2.4).</summary>
    [RelayCommand]
    private async Task ChangeStatus(Worker worker)
    {
        await workers.ChangeStatus(worker.Id, !worker.Active);
        await Load();
    }

    /// <summary>
    /// Deleting a worker who has never been booked or charged removes her outright; one
    /// who appears in the history is deactivated instead, because the appointments and
    /// sales that name her are what the per-worker reports are built from.
    /// </summary>
    [RelayCommand]
    private async Task DeleteWorker(Worker worker)
    {
        bool confirmed = await dialogs.Confirm(
            Texts.DeleteWorkerTitle,
            string.Format(Texts.DeleteWorkerMessage, worker.Name),
            Texts.Delete);

        if (!confirmed) return;

        var result = await workers.Delete(worker.Id);
        await Load();

        ShowNotice(result == DeleteResult.Deactivated
            ? string.Format(Texts.WorkerDeactivated, worker.Name)
            : string.Format(Texts.WorkerDeleted, worker.Name));
    }

    private static Dictionary<Weekday, List<WorkerSchedule>> GroupedByDay(Worker t)
        => t.Schedules
            .GroupBy(h => h.Weekday)
            .ToDictionary(g => g.Key, g => g.OrderBy(h => h.StartTime).ToList());

    private static DayCell ToCell(
        Worker t, Dictionary<Weekday, List<WorkerSchedule>> schedule, Weekday day)
    {
        if (!schedule.TryGetValue(day, out var intervals) || intervals.Count == 0)
            return new DayCell("#00000000", string.Format(Texts.DayNotWorking, Labels.Text(day)));

        string detail = string.Join(" · ", intervals.Select(FormatInterval));
        return new DayCell(t.Color, string.Format(Texts.DayScheduleCell, Labels.Text(day), detail));
    }

    private static string Summarize(Dictionary<Weekday, List<WorkerSchedule>> schedule)
    {
        if (schedule.Count == 0) return Texts.NoSchedule;

        var days = schedule.Keys.OrderBy(d => d).Select(Labels.TextShort);

        // Every day sharing the same ranges is the common case, and reads far better as
        // "Dl, Dt, Dc · 09:00–14:00" than as the same hours repeated once per day.
        var allIntervals = schedule.Values
            .Select(f => string.Join(" · ", f.Select(FormatInterval)))
            .Distinct()
            .ToList();

        return allIntervals.Count == 1
            ? $"{string.Join(", ", days)} · {allIntervals[0]}"
            : string.Join(" · ", schedule.OrderBy(p => p.Key)
                .Select(p => $"{Labels.TextShort(p.Key)} {string.Join(Texts.ScheduleAndJoiner, p.Value.Select(FormatInterval))}"));
    }

    private static string FormatInterval(WorkerSchedule h)
        => $"{ScheduleHelper.Format(h.StartTime)}–{ScheduleHelper.Format(h.EndTime)}";
}
