using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using EvaGest.Helpers;
using EvaGest.Models;
using EvaGest.ViewModels.Elements;

namespace EvaGest.ViewModels.Dialegs;

/// <summary>
/// Treballadora (pantalles 3.6). The weekly schedule is part of the same dialog rather
/// than a separate screen, because a worker without hours is invisible to the overlap
/// calculation — creating one and forgetting the schedule would look like a bug.
///
/// There is no delete action on purpose: a worker who leaves is marked inactive, since
/// deleting her would break the sales history attached to her.
/// </summary>
public partial class TreballadoraDialogViewModel : DialegViewModelBase
{
    private readonly int? _id;

    [ObservableProperty] private string _nom = string.Empty;
    [ObservableProperty] private bool _activa = true;
    [ObservableProperty] private PaletaTreballadores.ColorTreballadora _color;

    public override string Titol => _id is null ? "Nova treballadora" : "Editar treballadora";

    public IReadOnlyList<PaletaTreballadores.ColorTreballadora> ColorsDisponibles
        => PaletaTreballadores.Colors;

    /// <summary>The seven rows of the schedule form, always in weekday order. Same row
    /// ViewModel as the barbershop's opening hours, so both forms parse hours alike.</summary>
    public ObservableCollection<HorariDiaViewModel> DiesHorari { get; } =
        [.. Enum.GetValues<DiaSetmana>().Select(d => new HorariDiaViewModel { Dia = d })];

    /// <summary>New worker: the suggested colour is the first one nobody is using.</summary>
    public TreballadoraDialogViewModel(IEnumerable<string> colorsJaUsats)
    {
        Color = TriarColor(PaletaTreballadores.ColorLliure(colorsJaUsats));
    }

    public TreballadoraDialogViewModel(
        Treballadora treballadora,
        IReadOnlyDictionary<DiaSetmana, List<(TimeOnly inici, TimeOnly fi)>> horari)
    {
        _id = treballadora.Id;
        Nom = treballadora.Nom;
        Activa = treballadora.Actiu;
        Color = TriarColor(treballadora.Color);

        foreach (var dia in DiesHorari) dia.Omplir(horari.GetValueOrDefault(dia.Dia, []));
    }

    /// <summary>Most weeks are the same Monday to Friday; this saves typing it five times.</summary>
    [RelayCommand]
    private void CopiarADiesLaborables()
    {
        var dilluns = DiesHorari[0];
        foreach (var dia in DiesHorari.Skip(1).Take(4)) dilluns.CopiarA(dia);
        ErrorValidacio = null;
    }

    [RelayCommand]
    private void Guardar()
    {
        if (string.IsNullOrWhiteSpace(Nom))
        {
            ErrorValidacio = "Cal indicar el nom de la treballadora.";
            return;
        }

        // Validate every row before reporting, so all the bad days light up at once
        // rather than one per attempt.
        bool hiHaErrors = false;
        int franges = 0;

        foreach (var dia in DiesHorari)
        {
            var resultat = dia.Comprovar();
            if (!resultat.EsValid) { hiHaErrors = true; continue; }
            franges += resultat.Franges.Count;
        }

        if (hiHaErrors)
        {
            ErrorValidacio = "Revisa els dies marcats en vermell.";
            return;
        }

        if (franges == 0)
        {
            ErrorValidacio = "Indica com a mínim un dia i un horari.";
            return;
        }

        ErrorValidacio = null;
        SolicitarTancar(true);
    }

    [RelayCommand]
    private void Cancellar() => SolicitarTancar(false);

    public Treballadora AModel() => new()
    {
        Id = _id ?? 0,
        Nom = Nom.Trim(),
        Actiu = Activa,
        Color = Color.Hex
    };

    /// <summary>The validated schedule, ready for the service. Only call after Guardar.</summary>
    public Dictionary<DiaSetmana, List<(TimeOnly inici, TimeOnly fi)>> AHorari()
    {
        var perDia = new Dictionary<DiaSetmana, List<(TimeOnly inici, TimeOnly fi)>>();

        foreach (var dia in DiesHorari)
        {
            var resultat = dia.Comprovar();
            if (resultat.EsValid && resultat.Franges.Count > 0) perDia[dia.Dia] = resultat.Franges;
        }

        return perDia;
    }

    private static PaletaTreballadores.ColorTreballadora TriarColor(string? hex)
        => PaletaTreballadores.Colors.FirstOrDefault(
               c => string.Equals(c.Hex, hex?.Trim(), StringComparison.OrdinalIgnoreCase))
           ?? PaletaTreballadores.Colors[0];
}
