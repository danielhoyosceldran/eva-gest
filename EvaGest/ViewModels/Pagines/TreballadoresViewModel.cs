using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.Input;
using EvaGest.Helpers;
using EvaGest.Models;
using EvaGest.Services;
using EvaGest.ViewModels.Dialegs;

namespace EvaGest.ViewModels.Pagines;

/// <summary>
/// Treballadores (pantalles 2.4). Defining who works and when is not cosmetic: the
/// weekly schedules stored here are what the overlap warning counts against, so this
/// page is the one that has to be filled in before the agenda means anything.
/// </summary>
public partial class TreballadoresViewModel(ITreballadoraService treballadores, IDialogService dialegs)
    : PaginaViewModelBase
{
    public override string Titol => "Treballadores";

    /// <summary>One cell of the weekly overview grid. A day she does not work carries a
    /// fully transparent colour rather than no colour, so the grid keeps its shape.</summary>
    public record CellaDia(string Hex, string Detall);

    public record FilaTreballadora(
        Treballadora Treballadora,
        string HorariResum,
        string EstatText,
        string AccioEstatText,
        IReadOnlyList<CellaDia> Dies);

    public ObservableCollection<FilaTreballadora> Files { get; } = [];

    public bool NoHiHaCap => Files.Count == 0;

    public IReadOnlyList<string> NomsDies { get; } =
        [.. Enum.GetValues<DiaSetmana>().Select(d => d.ToString())];

    public async Task Carregar()
    {
        Carregant = true;
        try
        {
            var totes = await treballadores.ObtenirTotes();

            Files.Clear();
            foreach (var t in totes)
            {
                var horari = AgrupatPerDia(t);
                Files.Add(new FilaTreballadora(
                    t,
                    Resumir(horari),
                    t.Actiu ? "Activa" : "Inactiva",
                    t.Actiu ? "Marcar inactiva" : "Reactivar",
                    [.. Enum.GetValues<DiaSetmana>().Select(d => ACella(t, horari, d))]));
            }

            OnPropertyChanged(nameof(NoHiHaCap));
        }
        finally { Carregant = false; }
    }

    [RelayCommand]
    private async Task NovaTreballadora()
    {
        var vm = new TreballadoraDialogViewModel(Files.Select(f => f.Treballadora.Color));
        if (!await dialegs.MostrarDialeg(vm)) return;

        await treballadores.Crear(vm.AModel(), vm.AHorari());
        await Carregar();
    }

    [RelayCommand]
    private async Task EditarTreballadora(Treballadora treballadora)
    {
        var horari = await treballadores.HorariDe(treballadora.Id);
        var vm = new TreballadoraDialogViewModel(treballadora, horari);
        if (!await dialegs.MostrarDialeg(vm)) return;

        await treballadores.Actualitzar(vm.AModel(), vm.AHorari());
        await Carregar();
    }

    /// <summary>No confirmation: it is reversible and happens every holiday (pantalles 2.4).</summary>
    [RelayCommand]
    private async Task CanviarEstat(Treballadora treballadora)
    {
        await treballadores.CanviarEstat(treballadora.Id, !treballadora.Actiu);
        await Carregar();
    }

    /// <summary>
    /// Deleting a worker who has never been booked or charged removes her outright; one
    /// who appears in the history is deactivated instead, because the appointments and
    /// sales that name her are what the per-worker reports are built from.
    /// </summary>
    [RelayCommand]
    private async Task EliminarTreballadora(Treballadora treballadora)
    {
        bool confirmat = await dialegs.Confirmar(
            "Eliminar treballadora?",
            $"S'eliminarà «{treballadora.Nom}» i el seu horari.\n\n"
            + "Si ja té cites o vendes, es marcarà com a inactiva en lloc d'esborrar-se, "
            + "per no perdre l'historial. En tots dos casos deixarà de sortir a l'hora "
            + "d'assignar cites i vendes.",
            "Eliminar");

        if (!confirmat) return;

        var resultat = await treballadores.Eliminar(treballadora.Id);
        await Carregar();

        MostrarAvis(resultat == ResultatEsborrat.Desactivat
            ? $"«{treballadora.Nom}» té cites o vendes registrades, així que s'ha marcat com a inactiva "
              + "en lloc d'eliminar-se. L'historial es manté i ja no es podrà assignar."
            : $"«{treballadora.Nom}» s'ha eliminat.");
    }

    private static Dictionary<DiaSetmana, List<HorariTreballadora>> AgrupatPerDia(Treballadora t)
        => t.Horaris
            .GroupBy(h => h.DiaSetmana)
            .ToDictionary(g => g.Key, g => g.OrderBy(h => h.HoraInici).ToList());

    private static CellaDia ACella(
        Treballadora t, Dictionary<DiaSetmana, List<HorariTreballadora>> horari, DiaSetmana dia)
    {
        if (!horari.TryGetValue(dia, out var franges) || franges.Count == 0)
            return new CellaDia("#00000000", $"{Etiquetes.Text(dia)}: no treballa");

        string detall = string.Join(" · ", franges.Select(FormatFranja));
        return new CellaDia(t.Color, $"{Etiquetes.Text(dia)}: {detall}");
    }

    private static string Resumir(Dictionary<DiaSetmana, List<HorariTreballadora>> horari)
    {
        if (horari.Count == 0) return "Sense horari";

        var dies = horari.Keys.OrderBy(d => d).Select(d => d.ToString());

        // Every day sharing the same ranges is the common case, and reads far better as
        // "Dl, Dt, Dc · 09:00–14:00" than as the same hours repeated once per day.
        var totesLesFranges = horari.Values
            .Select(f => string.Join(" · ", f.Select(FormatFranja)))
            .Distinct()
            .ToList();

        return totesLesFranges.Count == 1
            ? $"{string.Join(", ", dies)} · {totesLesFranges[0]}"
            : string.Join(" · ", horari.OrderBy(p => p.Key)
                .Select(p => $"{p.Key.ToString()} {string.Join(" i ", p.Value.Select(FormatFranja))}"));
    }

    private static string FormatFranja(HorariTreballadora h)
        => $"{HorariHelper.Format(h.HoraInici)}–{HorariHelper.Format(h.HoraFi)}";
}
