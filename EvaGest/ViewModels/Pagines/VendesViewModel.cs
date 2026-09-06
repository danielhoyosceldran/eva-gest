using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using EvaGest.Models;
using EvaGest.Services;
using EvaGest.ViewModels.Dialegs;

namespace EvaGest.ViewModels.Pagines;

/// <summary>
/// Vendes (pantalles 2.6): the sales history, and the only place a mistake gets fixed.
/// Cancelled sales stay listed but are excluded from the footer totals, which is the
/// whole point of cancelling rather than deleting (RF-10).
/// </summary>
public partial class VendesViewModel(
    IVendaService vendes, IClientService clients, ICatalegService cataleg,
    ITreballadoraService treballadores, ISoundService so, IConfiguracioService configuracio,
    IExportService export, IDialogService dialegs) : PaginaViewModelBase
{
    public override string Titol => "Vendes";

    /// <summary>Money is formatted here rather than in XAML: binding a *Cents field
    /// straight to a {0:0.00} format string showed 36,50 € as "3650,00".</summary>
    public record FilaVenda(
        Venda Venda, string DataText, string ClientText, string TreballadoraText,
        string ConcepteText, string BaseText, string IvaText, string TotalText,
        string MetodeText, string EstatText, bool PotAnullar);

    public ObservableCollection<FilaVenda> Vendes { get; } = [];

    // ── Filtres (RF-15) ─────────────────────────────────────────────────────
    [ObservableProperty] private DateOnly? _des;
    [ObservableProperty] private DateOnly? _fins;
    [ObservableProperty] private Client? _client;
    [ObservableProperty] private Servei? _servei;
    [ObservableProperty] private Producte? _producte;
    [ObservableProperty] private MetodePagament? _metodePagament;
    [ObservableProperty] private Treballadora? _treballadora;
    [ObservableProperty] private EstatVenda? _estat;

    public ObservableCollection<Client> ClientsFiltre { get; } = [];
    public ObservableCollection<Servei> ServeisFiltre { get; } = [];
    public ObservableCollection<Producte> ProductesFiltre { get; } = [];
    public ObservableCollection<MetodePagament> MetodesFiltre { get; } = [];
    public ObservableCollection<Treballadora> TreballadoresFiltre { get; } = [];

    public IReadOnlyList<EstatVenda> EstatsFiltre { get; } = [EstatVenda.Activa, EstatVenda.Anullada];

    // ── Peu de taula ────────────────────────────────────────────────────────
    [ObservableProperty] private string _totalBaseText = "—";
    [ObservableProperty] private string _totalIvaText = "—";
    [ObservableProperty] private string _totalTotalText = "—";
    [ObservableProperty] private int _comptadorActives;

    public bool NoHiHaResultats => Vendes.Count == 0;

    private bool _opcionsCarregades;

    public async Task Carregar()
    {
        Carregant = true;
        try
        {
            if (!_opcionsCarregades) await CarregarOpcionsDeFiltre();

            var trobades = await vendes.Cercar(new FiltreVendes(
                Des, Fins, Client?.Id, Servei?.Id, Producte?.Id,
                MetodePagament?.Id, Treballadora?.Id, Estat));

            Vendes.Clear();
            foreach (var v in trobades) Vendes.Add(AFila(v));

            // Only active sales count: a cancelled one is still on screen precisely so
            // the user can see it does not add up (RF-10).
            var actives = trobades.Where(v => v.Estat == EstatVenda.Activa).ToList();
            ComptadorActives = actives.Count;
            TotalBaseText = Diners.Format(actives.Sum(v => v.BaseCents));
            TotalIvaText = Diners.Format(actives.Sum(v => v.IvaCents));
            TotalTotalText = Diners.Format(actives.Sum(v => v.TotalCents));

            OnPropertyChanged(nameof(NoHiHaResultats));
        }
        finally { Carregant = false; }
    }

    private async Task CarregarOpcionsDeFiltre()
    {
        foreach (var c in await clients.ObtenirActius()) ClientsFiltre.Add(c);
        foreach (var s in await cataleg.ObtenirServeis()) ServeisFiltre.Add(s);
        foreach (var p in await cataleg.ObtenirProductes()) ProductesFiltre.Add(p);
        foreach (var m in await cataleg.ObtenirMetodes()) MetodesFiltre.Add(m);
        foreach (var t in await treballadores.ObtenirTotes()) TreballadoresFiltre.Add(t);
        _opcionsCarregades = true;
    }

    private static FilaVenda AFila(Venda v) => new(
        v,
        v.Data.ToString("dd/MM/yyyy"),
        v.NomMostrat,
        v.Treballadora?.Nom ?? "Sense assignar",
        Resumir(v.Linies),
        Diners.Format(v.BaseCents),
        Diners.Format(v.IvaCents),
        Diners.Format(v.TotalCents),
        v.MetodePagament?.Nom ?? "—",
        Etiquetes.Text(v.Estat),
        v.Estat == EstatVenda.Activa);

    /// <summary>"Tall + Cera" (pantalles 2.6). Past three items it stops naming them,
    /// because a full list would push every other column off the row.</summary>
    private static string Resumir(List<VendaLinia> linies)
    {
        if (linies.Count == 0) return "—";
        if (linies.Count <= 3) return string.Join(" + ", linies.Select(l => l.Descripcio));

        return string.Join(" + ", linies.Take(2).Select(l => l.Descripcio))
               + $" + {linies.Count - 2} més";
    }

    partial void OnDesChanged(DateOnly? value) => _ = Carregar();
    partial void OnFinsChanged(DateOnly? value) => _ = Carregar();
    partial void OnClientChanged(Client? value) => _ = Carregar();
    partial void OnServeiChanged(Servei? value) => _ = Carregar();
    partial void OnProducteChanged(Producte? value) => _ = Carregar();
    partial void OnMetodePagamentChanged(MetodePagament? value) => _ = Carregar();
    partial void OnTreballadoraChanged(Treballadora? value) => _ = Carregar();
    partial void OnEstatChanged(EstatVenda? value) => _ = Carregar();

    [RelayCommand]
    private async Task NetejarFiltres()
    {
        Des = null;
        Fins = null;
        Client = null;
        Servei = null;
        Producte = null;
        MetodePagament = null;
        Treballadora = null;
        Estat = null;
        await Carregar();
    }

    [RelayCommand]
    private async Task NovaVenda()
    {
        var vm = await VendaDialogViewModel.Nova(vendes, clients, cataleg, treballadores, so, configuracio, dialegs);
        if (await dialegs.MostrarDialeg(vm)) await Carregar();
    }

    [RelayCommand]
    private async Task EditarVenda(Venda venda)
    {
        var vm = await VendaDialogViewModel.Editar(vendes, clients, cataleg, treballadores, so, configuracio, dialegs, venda);
        if (await dialegs.MostrarDialeg(vm)) await Carregar();
    }

    [RelayCommand]
    private async Task AnullarVenda(Venda venda)
    {
        bool confirmat = await dialegs.Confirmar(
            "Anul·lar venda?",
            $"La venda de {Diners.Format(venda.TotalCents)} passarà a l'estat Anul·lada. "
            + "Es manté visible a l'historial però queda exclosa dels totals.",
            "Anul·lar");

        if (!confirmat) return;

        await vendes.Anullar(venda.Id);
        await Carregar();
    }

    /// <summary>
    /// An active sale is the accounting record, so "eliminar" cancels it and says so
    /// (RF-10). Repeating it on an already cancelled sale wipes it for good: by then the
    /// user has seen it sitting outside the totals and confirmed twice.
    /// </summary>
    [RelayCommand]
    private async Task EliminarVenda(Venda venda)
    {
        bool anullada = venda.Estat == EstatVenda.Anullada;

        bool confirmat = await dialegs.Confirmar(
            anullada ? "Esborrar definitivament?" : "Eliminar venda?",
            anullada
                ? $"La venda de {Diners.Format(venda.TotalCents)} ja està anul·lada. "
                  + "S'esborrarà del tot, amb les seves línies i el desglossament d'IVA. "
                  + "Això no es pot desfer."
                : $"La venda de {Diners.Format(venda.TotalCents)} forma part de l'historial, "
                  + "així que passarà a Anul·lada en lloc d'esborrar-se: es manté visible "
                  + "però queda fora dels totals.",
            anullada ? "Esborrar" : "Anul·lar");

        if (!confirmat) return;

        var resultat = await vendes.Eliminar(venda.Id);
        await Carregar();

        MostrarAvis(resultat == ResultatEsborrat.Desactivat
            ? "La venda s'ha anul·lat en lloc d'esborrar-se, per no perdre l'historial. "
              + "Si la vols treure del tot, torna a eliminar-la ara que està anul·lada."
            : "La venda s'ha esborrat definitivament.");
    }

    [RelayCommand]
    private async Task ExportarPeriode()
    {
        var vm = new ExportDialogViewModel(export, dialegs);
        await dialegs.MostrarDialeg(vm);
    }
}
