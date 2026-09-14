using CommunityToolkit.Mvvm.ComponentModel;

namespace EvaGest.ViewModels.Elements;

/// <summary>
/// "Shop shut for the day" and "outside opening hours" get two different treatments,
/// so a holiday never reads the same as a lunch break.
/// </summary>
public enum BandType { OutsideSchedule, ClosedDay }

/// <summary>A shaded band on a day column. Never hit-testable: the app only warns
/// about out-of-hours bookings, it never blocks them (casos-us CU-01).</summary>
public partial class GridBandViewModel : ObservableObject
{
    [ObservableProperty] private double _top;
    [ObservableProperty] private double _height;

    public BandType Type { get; init; }
}

/// <summary>One label on the hour ruler.</summary>
public partial class RulerHourViewModel : ObservableObject
{
    [ObservableProperty] private double _top;

    public string Label { get; init; } = string.Empty;
}
