using CommunityToolkit.Mvvm.ComponentModel;
using EvaGest.Helpers;
using EvaGest.Models;
using EvaGest.Services;

namespace EvaGest.ViewModels.Elements;

/// <summary>
/// One row of the opening-hours form: a day with a morning shift and an afternoon shift
/// that are ticked independently. Holds the raw text the user picked or typed, so a
/// half-typed hour is never silently discarded, and reports its own validation message.
/// </summary>
public partial class DayScheduleViewModel : ObservableObject
{
    // Set while Fill writes the stored hours in, so the "suggest sensible hours"
    // behaviour of the tick boxes does not fight the values being loaded.
    private bool _filling;

    public required Weekday Day { get; init; }

    public string DayName => Labels.Text(Day);

    /// <summary>The hours the picker offers; the boxes stay editable for odd times.</summary>
    public IReadOnlyList<string> AvailableTimes => ScheduleHelper.Slots;

    [ObservableProperty] private bool _worksMorning;
    [ObservableProperty] private bool _worksAfternoon;
    [ObservableProperty] private string _morningStart = string.Empty;
    [ObservableProperty] private string _morningEnd = string.Empty;
    [ObservableProperty] private string _afternoonStart = string.Empty;
    [ObservableProperty] private string _afternoonEnd = string.Empty;
    [ObservableProperty] private string? _error;

    /// <summary>True when the day is worked at all, in either shift.</summary>
    public bool Open => WorksMorning || WorksAfternoon;

    public ScheduleHelper.ScheduleResult Check()
    {
        var result = ScheduleHelper.Check(
            WorksMorning, MorningStart, MorningEnd, WorksAfternoon, AfternoonStart, AfternoonEnd);
        Error = result.Error;
        return result;
    }

    /// <summary>Fills the row from what is stored: no ranges means the day is not worked.
    /// A single stored range lands on the shift its start hour belongs to, so an
    /// afternoon-only day does not come back looking like a morning.</summary>
    public void Fill(IReadOnlyList<(TimeOnly start, TimeOnly fi)> intervals)
    {
        _filling = true;
        try
        {
            var ordered = intervals.OrderBy(f => f.start).ToList();

            (TimeOnly start, TimeOnly fi)? morning = null, afternoon = null;

            if (ordered.Count == 1 && ordered[0].start >= ScheduleHelper.MorningAfternoonSplit)
            {
                afternoon = ordered[0];
            }
            else
            {
                if (ordered.Count > 0) morning = ordered[0];
                if (ordered.Count > 1) afternoon = ordered[1];
            }

            WorksMorning = morning is not null;
            WorksAfternoon = afternoon is not null;

            MorningStart = morning is null ? string.Empty : ScheduleHelper.Format(morning.Value.start);
            MorningEnd = morning is null ? string.Empty : ScheduleHelper.Format(morning.Value.fi);
            AfternoonStart = afternoon is null ? string.Empty : ScheduleHelper.Format(afternoon.Value.start);
            AfternoonEnd = afternoon is null ? string.Empty : ScheduleHelper.Format(afternoon.Value.fi);
            Error = null;
        }
        finally
        {
            _filling = false;
        }
    }

    /// <summary>Backups this day's shifts onto another, for "apply to every weekday".</summary>
    public void CopyTo(DayScheduleViewModel other)
    {
        other._filling = true;
        try
        {
            other.WorksMorning = WorksMorning;
            other.WorksAfternoon = WorksAfternoon;
            other.MorningStart = MorningStart;
            other.MorningEnd = MorningEnd;
            other.AfternoonStart = AfternoonStart;
            other.AfternoonEnd = AfternoonEnd;
            other.Error = null;
        }
        finally
        {
            other._filling = false;
        }
    }

    // Ticking a shift with both boxes empty fills in the usual hours: the common case is
    // then one click instead of two hours typed, and the unusual case is still editable.
    partial void OnWorksMorningChanged(bool value)
    {
        Error = null;
        OnPropertyChanged(nameof(Open));
        if (!value || _filling) return;
        if (string.IsNullOrWhiteSpace(MorningStart) && string.IsNullOrWhiteSpace(MorningEnd))
        {
            MorningStart = ScheduleHelper.Format(ScheduleHelper.DefaultMorning.start);
            MorningEnd = ScheduleHelper.Format(ScheduleHelper.DefaultMorning.fi);
        }
    }

    partial void OnWorksAfternoonChanged(bool value)
    {
        Error = null;
        OnPropertyChanged(nameof(Open));
        if (!value || _filling) return;
        if (string.IsNullOrWhiteSpace(AfternoonStart) && string.IsNullOrWhiteSpace(AfternoonEnd))
        {
            AfternoonStart = ScheduleHelper.Format(ScheduleHelper.DefaultAfternoon.start);
            AfternoonEnd = ScheduleHelper.Format(ScheduleHelper.DefaultAfternoon.fi);
        }
    }

    // Typing anywhere clears the stale message; it comes back on the next save attempt.
    partial void OnMorningStartChanged(string value) => Error = null;
    partial void OnMorningEndChanged(string value) => Error = null;
    partial void OnAfternoonStartChanged(string value) => Error = null;
    partial void OnAfternoonEndChanged(string value) => Error = null;
}
