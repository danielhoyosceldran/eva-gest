using CommunityToolkit.Mvvm.ComponentModel;
using EvaGest.Models;
using EvaGest.Services;

namespace EvaGest.ViewModels.Elements;

/// <summary>
/// One appointment block placed on the weekly grid. Carries its own geometry
/// (vertical in pixels, horizontal as a lane index) so the view only binds.
/// </summary>
public partial class CitaGraellaViewModel : ObservableObject
{
    /// <summary>Below this height the block cannot fit a legible title.</summary>
    private const double AlcadaMinimaTitol = 22;
    private const double AlcadaMinimaSubtitol = 40;

    public Cita? Model { get; init; }

    public int Id => Model?.Id ?? 0;

    [ObservableProperty] private double _top;
    [ObservableProperty] private double _alcada;
    [ObservableProperty] private int _carrilIndex;
    [ObservableProperty] private int _nombreCarrils = 1;
    [ObservableProperty] private bool _esSeleccionada;

    /// <summary>Ghost block: the appointment being edited in the dialog, which moves
    /// live as the hour or duration changes and is not stored yet.</summary>
    public bool EsFantasma { get; init; }

    public string ColorHex { get; init; } = string.Empty;
    public string HoraText { get; init; } = string.Empty;
    public string Titol { get; init; } = string.Empty;
    public string Subtitol { get; init; } = string.Empty;
    public bool EsCancellada { get; init; }
    public string TextTooltip { get; init; } = string.Empty;

    public bool MostrarTitol => Alcada >= AlcadaMinimaTitol;
    public bool MostrarSubtitol => Alcada >= AlcadaMinimaSubtitol && Subtitol.Length > 0;

    partial void OnAlcadaChanged(double value)
    {
        OnPropertyChanged(nameof(MostrarTitol));
        OnPropertyChanged(nameof(MostrarSubtitol));
    }

    public static CitaGraellaViewModel Des(Cita cita, double top, double alcada)
    {
        var fi = Helpers.GraellaHelper.AHora(
            Helpers.GraellaHelper.MinutsDelDia(cita.Hora) + cita.DuradaMin);

        return new CitaGraellaViewModel
        {
            Model = cita,
            Top = top,
            Alcada = alcada,
            ColorHex = cita.Treballadora?.Color ?? string.Empty,
            HoraText = $"{cita.Hora:HH\\:mm}–{fi:HH\\:mm}",
            Titol = cita.NomMostrat,
            Subtitol = cita.Servei?.Nom ?? string.Empty,
            EsCancellada = cita.Estat is EstatCita.Cancellada or EstatCita.NoAssistida,
            TextTooltip = Tooltip(cita, fi)
        };
    }

    private static string Tooltip(Cita cita, TimeOnly fi)
    {
        var linies = new List<string>
        {
            $"{cita.Hora:HH\\:mm} – {fi:HH\\:mm}  ({cita.DuradaMin} min)",
            cita.NomMostrat
        };
        if (cita.Servei is { Nom: var servei }) linies.Add(servei);
        if (cita.Treballadora is { Nom: var treballadora }) linies.Add($"Amb {treballadora}");
        linies.Add(Etiquetes.Text(cita.Estat));
        if (!string.IsNullOrWhiteSpace(cita.Observacions)) linies.Add(cita.Observacions);

        return string.Join(Environment.NewLine, linies);
    }
}
