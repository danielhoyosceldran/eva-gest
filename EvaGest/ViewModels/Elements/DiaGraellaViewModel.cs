using System.Collections.ObjectModel;
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using EvaGest.Helpers;

namespace EvaGest.ViewModels.Elements;

/// <summary>
/// One day column of the weekly grid. Instances are reused across week changes so the
/// scroll offset and keyboard focus survive navigating with the arrows.
/// </summary>
public partial class DiaGraellaViewModel(GraellaSetmanaViewModel arrel) : ObservableObject
{
    private static readonly CultureInfo Cultura = new("ca-ES");

    [ObservableProperty] private DateOnly _data;
    [ObservableProperty] private bool _esAvui;
    [ObservableProperty] private bool _tancat;
    [ObservableProperty] private string? _motiuTancat;

    [ObservableProperty] private double _slotSobrevolatTop;
    [ObservableProperty] private double _slotSobrevolatAlcada;
    [ObservableProperty] private string _slotSobrevolatText = string.Empty;
    [ObservableProperty] private bool _mostrarSobrevolat;

    public ObservableCollection<CitaGraellaViewModel> Cites { get; } = [];
    public ObservableCollection<BandaGraellaViewModel> Bandes { get; } = [];

    public string CapcaleraDia => Data.ToString("ddd", Cultura).TrimEnd('.');
    public string CapcaleraNumero => Data.Day.ToString(CultureInfo.InvariantCulture);
    public string CapcaleraCompleta => Data.ToString("dddd d 'de' MMMM", Cultura);

    partial void OnDataChanged(DateOnly value)
    {
        OnPropertyChanged(nameof(CapcaleraDia));
        OnPropertyChanged(nameof(CapcaleraNumero));
        OnPropertyChanged(nameof(CapcaleraCompleta));
    }

    /// <summary>Called by the attached behaviour with the click's Y inside the column.</summary>
    public void ClicarAPosicio(double y) => arrel.ActivarSlot(Data, HoraA(y));

    public void SobrevolarAPosicio(double y) => Sobrevolar(HoraA(y));

    /// <summary>Same highlight driven by the keyboard instead of the pointer.</summary>
    public void SobrevolarAMinut(int minut) => Sobrevolar(GraellaHelper.AHora(minut));

    private void Sobrevolar(TimeOnly hora)
    {
        SlotSobrevolatTop = GraellaHelper.Top(hora, arrel.MinutIniciGraella, arrel.PixelsPerMinut);
        SlotSobrevolatAlcada = Math.Max(1, arrel.AlcadaSlotPx - GraellaHelper.SeparacioVerticalPx);
        SlotSobrevolatText = $"{hora:HH\\:mm} · Nova cita";
        MostrarSobrevolat = true;
    }

    public void DeixarDeSobrevolar() => MostrarSobrevolat = false;

    private TimeOnly HoraA(double y)
        => GraellaHelper.SlotDesDeY(y, arrel.MinutIniciGraella, arrel.PixelsPerMinut, arrel.MinutsSlot);
}
