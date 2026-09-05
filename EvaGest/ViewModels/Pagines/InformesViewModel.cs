using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using EvaGest.Models;
using EvaGest.Services;

namespace EvaGest.ViewModels.Pagines;

/// <summary>
/// Informes (pantalles 2.8): four blocks with a common period selector for the
/// worker-related ones. Reports only make sense once there is real data (Fase 8
/// is deliberately one of the last modules built).
/// </summary>
public partial class InformesViewModel(IInformesService informes, ITreballadoraService treballadores)
    : PaginaViewModelBase
{
    public override string Titol => "Informes";

    [ObservableProperty] private DateOnly _des = new(DateOnly.FromDateTime(DateTime.Today).Year,
        DateOnly.FromDateTime(DateTime.Today).Month, 1);
    [ObservableProperty] private DateOnly _fins = DateOnly.FromDateTime(DateTime.Today);

    [ObservableProperty] private string? _clientDelMesText;
    [ObservableProperty] private DetallTreballadora? _detallSeleccionat;
    [ObservableProperty] private Treballadora? _treballadoraSeleccionada;

    /// <summary>Bar height in DIP for the evolution chart, scaled to the largest month.</summary>
    public record BarraEvolucio(string Etiqueta, double AlcadaDip, string ImportText);

    // Named-property rows for the ranking lists: WPF Binding resolves CLR properties
    // reliably, unlike ValueTuple's Item1/Item2 fields, which are not guaranteed to
    // bind (silently blank instead of throwing — see Fase 7's XAML resource lesson).
    public record FilaClientVisites(Client Client, int Visites, long TotalCents);
    public record FilaClientDespesa(Client Client, long TotalCents);
    public record FilaClientMitjana(Client Client, decimal MitjanaEuros);
    public record FilaClientInactiu(Client Client, DateOnly Ultima, int DiesSense);

    public ObservableCollection<BarraEvolucio> Evolucio { get; } = [];
    public ObservableCollection<FilaClientVisites> TopVisites { get; } = [];
    public ObservableCollection<FilaClientDespesa> TopDespesa { get; } = [];
    public ObservableCollection<FilaClientMitjana> TopMitjana { get; } = [];
    public ObservableCollection<FilaClientInactiu> FaTemps { get; } = [];
    public ObservableCollection<DetallTreballadora> RanquingTreballadores { get; } = [];
    public ObservableCollection<Treballadora> Treballadores { get; } = [];

    public async Task Carregar()
    {
        Carregant = true;
        try
        {
            var clientDelMes = await informes.ClientDelMes();
            ClientDelMesText = clientDelMes is { } cdm
                ? $"{cdm.client.Nom} · {cdm.visites} visites · {Diners.Format((int)cdm.totalCents)}"
                : "Encara no hi ha vendes aquest mes.";

            var mensual = await informes.EvolucioMensual();
            long maxim = Math.Max(1, mensual.Max(m => m.totalCents));
            Evolucio.Clear();
            foreach (var (any, mes, total) in mensual)
            {
                var etiqueta = new DateOnly(any, mes, 1).ToString("MMM", new System.Globalization.CultureInfo("ca-ES"));
                Evolucio.Add(new BarraEvolucio(etiqueta, 4 + (total * 116.0 / maxim), Diners.FormatExport((int)total)));
            }

            TopVisites.Clear();
            foreach (var t in await informes.TopPerVisites()) TopVisites.Add(new(t.client, t.visites, t.totalCents));

            TopDespesa.Clear();
            foreach (var t in await informes.TopPerDespesa()) TopDespesa.Add(new(t.client, t.totalCents));

            TopMitjana.Clear();
            foreach (var t in await informes.TopPerMitjana()) TopMitjana.Add(new(t.client, t.mitjanaEuros));

            FaTemps.Clear();
            foreach (var t in await informes.FaTempsQueNoVenen()) FaTemps.Add(new(t.client, t.ultima, t.diesSense));

            RanquingTreballadores.Clear();
            foreach (var d in await informes.RanquingTreballadores(Des, Fins)) RanquingTreballadores.Add(d);

            if (Treballadores.Count == 0)
                foreach (var t in await treballadores.ObtenirTotes()) Treballadores.Add(t);

            if (TreballadoraSeleccionada is null && Treballadores.Count > 0)
                TreballadoraSeleccionada = Treballadores[0];

            if (TreballadoraSeleccionada is not null)
                DetallSeleccionat = await informes.DetallDeTreballadora(TreballadoraSeleccionada.Id, Des, Fins);
        }
        finally { Carregant = false; }
    }

    partial void OnDesChanged(DateOnly value) => _ = Carregar();
    partial void OnFinsChanged(DateOnly value) => _ = Carregar();
    partial void OnTreballadoraSeleccionadaChanged(Treballadora? value) => _ = Carregar();
}
