using CommunityToolkit.Mvvm.ComponentModel;
using EvaGest.Models;
using EvaGest.Services;
using EvaGest.Resources;

namespace EvaGest.ViewModels.Elements;

/// <summary>
/// One appointment block placed on the weekly grid. Carries its own geometry
/// (vertical in pixels, horizontal as a lane index) so the view only binds.
/// </summary>
public partial class GridAppointmentViewModel : ObservableObject
{
    /// <summary>Below this height the block cannot fit a legible title.</summary>
    private const double MinTitleHeight = 22;
    private const double MinSubtitleHeight = 40;

    public Appointment? Model { get; init; }

    public int Id => Model?.Id ?? 0;

    [ObservableProperty] private double _top;
    [ObservableProperty] private double _height;
    [ObservableProperty] private int _laneIndex;
    [ObservableProperty] private int _laneCount = 1;
    [ObservableProperty] private bool _isSelected;

    /// <summary>Ghost block: the appointment being edited in the dialog, which moves
    /// live as the hour or duration changes and is not stored yet.</summary>
    public bool IsGhost { get; init; }

    public string ColorHex { get; init; } = string.Empty;
    public string TimeText { get; init; } = string.Empty;
    public string Title { get; init; } = string.Empty;
    public string Subtitle { get; init; } = string.Empty;
    public bool IsCancelled { get; init; }
    public string TextTooltip { get; init; } = string.Empty;

    public bool ShowTitle => Height >= MinTitleHeight;
    public bool ShowSubtitle => Height >= MinSubtitleHeight && Subtitle.Length > 0;

    partial void OnHeightChanged(double value)
    {
        OnPropertyChanged(nameof(ShowTitle));
        OnPropertyChanged(nameof(ShowSubtitle));
    }

    public static GridAppointmentViewModel From(Appointment appointment, double top, double height)
    {
        var fi = Helpers.GridHelper.ATime(
            Helpers.GridHelper.DayMinutes(appointment.Time) + appointment.DurationMin);

        return new GridAppointmentViewModel
        {
            Model = appointment,
            Top = top,
            Height = height,
            ColorHex = appointment.Worker?.Color ?? string.Empty,
            TimeText = $"{appointment.Time:HH\\:mm}–{fi:HH\\:mm}",
            Title = appointment.DisplayName,
            Subtitle = appointment.Service?.Name ?? string.Empty,
            IsCancelled = appointment.Status is AppointmentStatus.Cancelled or AppointmentStatus.NoShow,
            TextTooltip = Tooltip(appointment, fi)
        };
    }

    private static string Tooltip(Appointment appointment, TimeOnly fi)
    {
        var lines = new List<string>
        {
            $"{appointment.Time:HH\\:mm} – {fi:HH\\:mm}  ({appointment.DurationMin} min)",
            appointment.DisplayName
        };
        if (appointment.Service is { Name: var service }) lines.Add(service);
        if (appointment.Worker is { Name: var worker }) lines.Add(string.Format(Texts.WithWorker, worker));
        lines.Add(Labels.Text(appointment.Status));
        if (!string.IsNullOrWhiteSpace(appointment.Notes)) lines.Add(appointment.Notes);

        return string.Join(Environment.NewLine, lines);
    }
}
