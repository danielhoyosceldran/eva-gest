using CommunityToolkit.Mvvm.ComponentModel;

namespace EvaGest.ViewModels.Elements;

/// <summary>
/// "Shop shut for the day" and "outside opening hours" get two different treatments,
/// so a holiday never reads the same as a lunch break.
/// </summary>
public enum TipusBanda { ForaHorari, DiaTancat }

/// <summary>A shaded band on a day column. Never hit-testable: the app only warns
/// about out-of-hours bookings, it never blocks them (casos-us CU-01).</summary>
public partial class BandaGraellaViewModel : ObservableObject
{
    [ObservableProperty] private double _top;
    [ObservableProperty] private double _alcada;

    public TipusBanda Tipus { get; init; }
}

/// <summary>One label on the hour ruler.</summary>
public partial class HoraReglaViewModel : ObservableObject
{
    [ObservableProperty] private double _top;

    public string Etiqueta { get; init; } = string.Empty;
}
