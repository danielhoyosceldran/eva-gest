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
public partial class HorariDiaViewModel : ObservableObject
{
    // Set while Omplir writes the stored hours in, so the "suggest sensible hours"
    // behaviour of the tick boxes does not fight the values being loaded.
    private bool _omplint;

    public required DiaSetmana Dia { get; init; }

    public string NomDia => Etiquetes.Text(Dia);

    /// <summary>The hours the picker offers; the boxes stay editable for odd times.</summary>
    public IReadOnlyList<string> HoresDisponibles => HorariHelper.Slots;

    [ObservableProperty] private bool _treballaMati;
    [ObservableProperty] private bool _treballaTarda;
    [ObservableProperty] private string _matiInici = string.Empty;
    [ObservableProperty] private string _matiFi = string.Empty;
    [ObservableProperty] private string _tardaInici = string.Empty;
    [ObservableProperty] private string _tardaFi = string.Empty;
    [ObservableProperty] private string? _error;

    /// <summary>True when the day is worked at all, in either shift.</summary>
    public bool Obert => TreballaMati || TreballaTarda;

    public HorariHelper.ResultatHorari Comprovar()
    {
        var resultat = HorariHelper.Comprovar(
            TreballaMati, MatiInici, MatiFi, TreballaTarda, TardaInici, TardaFi);
        Error = resultat.Error;
        return resultat;
    }

    /// <summary>Fills the row from what is stored: no ranges means the day is not worked.
    /// A single stored range lands on the shift its start hour belongs to, so an
    /// afternoon-only day does not come back looking like a morning.</summary>
    public void Omplir(IReadOnlyList<(TimeOnly inici, TimeOnly fi)> franges)
    {
        _omplint = true;
        try
        {
            var ordenades = franges.OrderBy(f => f.inici).ToList();

            (TimeOnly inici, TimeOnly fi)? mati = null, tarda = null;

            if (ordenades.Count == 1 && ordenades[0].inici >= HorariHelper.TallMatiTarda)
            {
                tarda = ordenades[0];
            }
            else
            {
                if (ordenades.Count > 0) mati = ordenades[0];
                if (ordenades.Count > 1) tarda = ordenades[1];
            }

            TreballaMati = mati is not null;
            TreballaTarda = tarda is not null;

            MatiInici = mati is null ? string.Empty : HorariHelper.Format(mati.Value.inici);
            MatiFi = mati is null ? string.Empty : HorariHelper.Format(mati.Value.fi);
            TardaInici = tarda is null ? string.Empty : HorariHelper.Format(tarda.Value.inici);
            TardaFi = tarda is null ? string.Empty : HorariHelper.Format(tarda.Value.fi);
            Error = null;
        }
        finally
        {
            _omplint = false;
        }
    }

    /// <summary>Copies this day's shifts onto another, for "apply to every weekday".</summary>
    public void CopiarA(HorariDiaViewModel altre)
    {
        altre._omplint = true;
        try
        {
            altre.TreballaMati = TreballaMati;
            altre.TreballaTarda = TreballaTarda;
            altre.MatiInici = MatiInici;
            altre.MatiFi = MatiFi;
            altre.TardaInici = TardaInici;
            altre.TardaFi = TardaFi;
            altre.Error = null;
        }
        finally
        {
            altre._omplint = false;
        }
    }

    // Ticking a shift with both boxes empty fills in the usual hours: the common case is
    // then one click instead of two hours typed, and the unusual case is still editable.
    partial void OnTreballaMatiChanged(bool value)
    {
        Error = null;
        OnPropertyChanged(nameof(Obert));
        if (!value || _omplint) return;
        if (string.IsNullOrWhiteSpace(MatiInici) && string.IsNullOrWhiteSpace(MatiFi))
        {
            MatiInici = HorariHelper.Format(HorariHelper.MatiPerDefecte.inici);
            MatiFi = HorariHelper.Format(HorariHelper.MatiPerDefecte.fi);
        }
    }

    partial void OnTreballaTardaChanged(bool value)
    {
        Error = null;
        OnPropertyChanged(nameof(Obert));
        if (!value || _omplint) return;
        if (string.IsNullOrWhiteSpace(TardaInici) && string.IsNullOrWhiteSpace(TardaFi))
        {
            TardaInici = HorariHelper.Format(HorariHelper.TardaPerDefecte.inici);
            TardaFi = HorariHelper.Format(HorariHelper.TardaPerDefecte.fi);
        }
    }

    // Typing anywhere clears the stale message; it comes back on the next save attempt.
    partial void OnMatiIniciChanged(string value) => Error = null;
    partial void OnMatiFiChanged(string value) => Error = null;
    partial void OnTardaIniciChanged(string value) => Error = null;
    partial void OnTardaFiChanged(string value) => Error = null;
}
