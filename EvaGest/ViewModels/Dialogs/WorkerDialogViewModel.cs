using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using EvaGest.Helpers;
using EvaGest.Models;
using EvaGest.ViewModels.Elements;
using EvaGest.Resources;

namespace EvaGest.ViewModels.Dialogs;

/// <summary>
/// Worker (pantalles 3.6). The weekly schedule is part of the same dialog rather
/// than a separate screen, because a worker without hours is invisible to the overlap
/// calculation — creating one and forgetting the schedule would look like a bug.
///
/// There is no delete action on purpose: a worker who leaves is marked inactive, since
/// deleting her would break the sales history attached to her.
/// </summary>
public partial class WorkerDialogViewModel : DialogViewModelBase
{
    private readonly int? _id;

    [ObservableProperty] private string _name = string.Empty;
    [ObservableProperty] private bool _active = true;
    [ObservableProperty] private WorkerPalette.WorkerColor _color;

    public override string Title => _id is null ? Texts.NewWorkerTitle : Texts.EditWorkerTitle;

    public IReadOnlyList<WorkerPalette.WorkerColor> AvailableColors
        => WorkerPalette.Colors;

    /// <summary>The seven rows of the schedule form, always in weekday order. Same row
    /// ViewModel as the barbershop's opening hours, so both forms parse hours alike.</summary>
    public ObservableCollection<DayScheduleViewModel> ScheduleDays { get; } =
        [.. Enum.GetValues<Weekday>().Select(d => new DayScheduleViewModel { Day = d })];

    /// <summary>New worker: the suggested colour is the first one nobody is using.</summary>
    public WorkerDialogViewModel(IEnumerable<string> usedColors)
    {
        Color = ChooseColor(WorkerPalette.FreeColor(usedColors));
    }

    public WorkerDialogViewModel(
        Worker worker,
        IReadOnlyDictionary<Weekday, List<(TimeOnly start, TimeOnly fi)>> schedule)
    {
        _id = worker.Id;
        Name = worker.Name;
        Active = worker.Active;
        Color = ChooseColor(worker.Color);

        foreach (var day in ScheduleDays) day.Fill(schedule.GetValueOrDefault(day.Day, []));
    }

    /// <summary>Most weeks are the same Monday to Friday; this saves typing it five times.</summary>
    [RelayCommand]
    private void CopyToWeekdays()
    {
        var monday = ScheduleDays[0];
        foreach (var day in ScheduleDays.Skip(1).Take(4)) monday.CopyTo(day);
        ErrorValidation = null;
    }

    [RelayCommand]
    private void Save()
    {
        if (string.IsNullOrWhiteSpace(Name))
        {
            ErrorValidation = Texts.WorkerNameRequired;
            return;
        }

        // Validate every row before reporting, so all the bad days light up at once
        // rather than one per attempt.
        bool hasErrors = false;
        int intervals = 0;

        foreach (var day in ScheduleDays)
        {
            var result = day.Check();
            if (!result.IsValid) { hasErrors = true; continue; }
            intervals += result.Intervals.Count;
        }

        if (hasErrors)
        {
            ErrorValidation = Texts.CheckRedDays;
            return;
        }

        if (intervals == 0)
        {
            ErrorValidation = Texts.AtLeastOneDayRequired;
            return;
        }

        ErrorValidation = null;
        RequestClose(true);
    }

    [RelayCommand]
    private void Cancel() => RequestClose(false);

    public Worker AModel() => new()
    {
        Id = _id ?? 0,
        Name = Name.Trim(),
        Active = Active,
        Color = Color.Hex
    };

    /// <summary>The validated schedule, ready for the service. Only call after Save.</summary>
    public Dictionary<Weekday, List<(TimeOnly start, TimeOnly fi)>> ToSchedule()
    {
        var perDay = new Dictionary<Weekday, List<(TimeOnly start, TimeOnly fi)>>();

        foreach (var day in ScheduleDays)
        {
            var result = day.Check();
            if (result.IsValid && result.Intervals.Count > 0) perDay[day.Day] = result.Intervals;
        }

        return perDay;
    }

    private static WorkerPalette.WorkerColor ChooseColor(string? hex)
        => WorkerPalette.Colors.FirstOrDefault(
               c => string.Equals(c.Hex, hex?.Trim(), StringComparison.OrdinalIgnoreCase))
           ?? WorkerPalette.Colors[0];
}
