using System.Collections.ObjectModel;
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using EvaGest.Helpers;
using EvaGest.Resources;
using EvaGest.Services;

namespace EvaGest.ViewModels.Elements;

/// <summary>
/// One day column of the weekly grid. Instances are reused across week changes so the
/// scroll offset and keyboard focus survive navigating with the arrows.
/// </summary>
public partial class GridDayViewModel(WeekGridViewModel root) : ObservableObject
{

    [ObservableProperty] private DateOnly _date;
    [ObservableProperty] private bool _isToday;
    [ObservableProperty] private bool _closed;
    [ObservableProperty] private string? _closedReason;

    [ObservableProperty] private double _hoveredSlotTop;
    [ObservableProperty] private double _hoveredSlotHeight;
    [ObservableProperty] private string _hoveredSlotText = string.Empty;
    [ObservableProperty] private bool _showHovered;

    public ObservableCollection<GridAppointmentViewModel> Appointments { get; } = [];
    public ObservableCollection<GridBandViewModel> Bands { get; } = [];

    public string DayHeader => Date.ToString("ddd", AppLanguage.Culture).TrimEnd('.');
    public string NumberHeader => Date.Day.ToString(CultureInfo.InvariantCulture);
    public string FullHeader => Date.ToString(Texts.DayMonthFormat, AppLanguage.Culture);

    partial void OnDateChanged(DateOnly value)
    {
        OnPropertyChanged(nameof(DayHeader));
        OnPropertyChanged(nameof(NumberHeader));
        OnPropertyChanged(nameof(FullHeader));
    }

    /// <summary>Called by the attached behaviour with the click's Y inside the column.</summary>
    public void ClickAtPosition(double y) => root.ActivateSlot(Date, TimeAt(y));

    public void HoverAtPosition(double y) => Hover(TimeAt(y));

    /// <summary>Same highlight driven by the keyboard instead of the pointer.</summary>
    public void HoverAtMinute(int minute) => Hover(GridHelper.ATime(minute));

    private void Hover(TimeOnly time)
    {
        HoveredSlotTop = GridHelper.Top(time, root.GridStartMinute, root.PixelsPerMinute);
        HoveredSlotHeight = Math.Max(1, root.SlotHeightPx - GridHelper.VerticalGapPx);
        HoveredSlotText = string.Format(Texts.NewAppointmentAtTime, time.ToString("HH\\:mm"));
        ShowHovered = true;
    }

    public void StopHover() => ShowHovered = false;

    private TimeOnly TimeAt(double y)
        => GridHelper.SlotFromY(y, root.GridStartMinute, root.PixelsPerMinute, root.SlotMinutes);
}
