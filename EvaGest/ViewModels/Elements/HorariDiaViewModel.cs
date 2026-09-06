using CommunityToolkit.Mvvm.ComponentModel;
using EvaGest.Helpers;
using EvaGest.Models;
using EvaGest.Services;

namespace EvaGest.ViewModels.Elements;

/// <summary>
/// One row of the opening-hours form. Holds the raw text the user typed, so a half-typed
/// hour is never silently discarded, and reports its own validation message.
/// </summary>
public partial class HorariDiaViewModel : ObservableObject
{
    public required DiaSetmana Dia { get; init; }

    public string NomDia => Etiquetes.Text(Dia);

    [ObservableProperty] private bool _obert;
    [ObservableProperty] private string _matiInici = string.Empty;
    [ObservableProperty] private string _matiFi = string.Empty;
    [ObservableProperty] private string _tardaInici = string.Empty;
    [ObservableProperty] private string _tardaFi = string.Empty;
    [ObservableProperty] private string? _error;

    public HorariHelper.ResultatHorari Comprovar()
    {
        var resultat = HorariHelper.Comprovar(Obert, MatiInici, MatiFi, TardaInici, TardaFi);
        Error = resultat.Error;
        return resultat;
    }

    /// <summary>Fills the row from what is stored: no ranges means the day is closed.</summary>
    public void Omplir(IReadOnlyList<(TimeOnly inici, TimeOnly fi)> franges)
    {
        Obert = franges.Count > 0;
        var ordenades = franges.OrderBy(f => f.inici).ToList();

        MatiInici = ordenades.Count > 0 ? HorariHelper.Format(ordenades[0].inici) : string.Empty;
        MatiFi = ordenades.Count > 0 ? HorariHelper.Format(ordenades[0].fi) : string.Empty;
        TardaInici = ordenades.Count > 1 ? HorariHelper.Format(ordenades[1].inici) : string.Empty;
        TardaFi = ordenades.Count > 1 ? HorariHelper.Format(ordenades[1].fi) : string.Empty;
        Error = null;
    }

    /// <summary>Copies this day's hours onto another, for "apply to every weekday".</summary>
    public void CopiarA(HorariDiaViewModel altre)
    {
        altre.Obert = Obert;
        altre.MatiInici = MatiInici;
        altre.MatiFi = MatiFi;
        altre.TardaInici = TardaInici;
        altre.TardaFi = TardaFi;
        altre.Error = null;
    }

    // Typing anywhere clears the stale message; it comes back on the next save attempt.
    partial void OnObertChanged(bool value) => Error = null;
    partial void OnMatiIniciChanged(string value) => Error = null;
    partial void OnMatiFiChanged(string value) => Error = null;
    partial void OnTardaIniciChanged(string value) => Error = null;
    partial void OnTardaFiChanged(string value) => Error = null;
}
