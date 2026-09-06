using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using EvaGest.Models;
using EvaGest.Services;
using EvaGest.ViewModels.Elements;

namespace EvaGest.ViewModels.Dialegs;

/// <summary>
/// Venda (nova / des de cita / editar), pantalles 3.2. The most complex dialog, and
/// the one that reuses the same class for all three modes (RF-09).
/// </summary>
public partial class VendaDialogViewModel : DialegViewModelBase
{
    public enum Mode { Nova, DesDeCita, Editar }

    private readonly IVendaService _vendes;
    private readonly ISoundService _so;
    private readonly IClientService _clients;
    private readonly IDialogService _dialegs;
    private int? _id;

    /// <summary>Whether the guest reminder is switched on (RF-05bis).</summary>
    private bool _avisConvidatActivat;

    /// <summary>
    /// The VAT mode the live footer computes in. Read once when the dialog opens and
    /// held, so the totals the user is shown match what VendaService will freeze onto
    /// the sale. Editing an existing sale keeps that sale's own mode (decision 6.4).
    /// </summary>
    private IvaMode _modeIva = IvaMode.Inclos;

    [ObservableProperty] private Mode _modeActual;

    [ObservableProperty] private Client? _clientSeleccionat;
    [ObservableProperty] private string _textClient = string.Empty;
    [ObservableProperty] private string? _telefonConvidat;

    [ObservableProperty] private Treballadora? _treballadora;
    [ObservableProperty] private MetodePagament? _metodePagament;
    [ObservableProperty] private string? _observacions;

    [ObservableProperty] private bool _avisClientNoRegistrat;

    /// <summary>Informational only: set when opened from an appointment (pantalles 3.2).</summary>
    [ObservableProperty] private string? _citaAssociadaText;
    private int? _citaId;

    public ObservableCollection<VendaLiniaViewModel> Linies { get; } = [];

    public ObservableCollection<Client> ClientsActius { get; } = [];
    public ObservableCollection<Servei> ServeisActius { get; } = [];
    public ObservableCollection<Producte> ProductesActius { get; } = [];
    public ObservableCollection<Treballadora> TreballadoresActives { get; } = [];
    public ObservableCollection<MetodePagament> MetodesActius { get; } = [];

    [ObservableProperty] private int _baseCents;
    [ObservableProperty] private int _ivaCents;
    [ObservableProperty] private int _totalCents;
    [ObservableProperty] private List<DesglossamentPerTipus> _desglossament = [];

    public string BaseText => Diners.Format(BaseCents);
    public string IvaText => Diners.Format(IvaCents);
    public string TotalText => Diners.Format(TotalCents);

    public override string Titol => ModeActual switch
    {
        Mode.Editar => "Editar venda",
        Mode.DesDeCita => "Nova venda (des de cita)",
        _ => "Nova venda"
    };

    private VendaDialogViewModel(
        IVendaService vendes, ISoundService so, IClientService clients, IDialogService dialegs)
    {
        _vendes = vendes;
        _so = so;
        _clients = clients;
        _dialegs = dialegs;
    }

    /// <summary>
    /// Shown only while a free-text name is actually typed. A sale for a guest is
    /// perfectly valid; the reminder just says the history will not be kept, and the
    /// user can turn it off entirely in Configuració (RF-05bis).
    /// </summary>
    private void RevisarAvisConvidat()
        => AvisClientNoRegistrat = _avisConvidatActivat
            && ClientSeleccionat is null
            && !string.IsNullOrWhiteSpace(TextClient);

    partial void OnTextClientChanged(string value) => RevisarAvisConvidat();
    partial void OnClientSeleccionatChanged(Client? value) => RevisarAvisConvidat();

    /// <summary>Registers the guest without losing the half-filled sale, and selects
    /// the new client, so the reminder does not simply reappear.</summary>
    [RelayCommand]
    private async Task RegistrarClientAra()
    {
        var dialeg = new ClientDialogViewModel(_clients) { Nom = TextClient.Trim() };
        if (TelefonConvidat is { Length: > 0 } telefon) dialeg.Mobil = telefon;

        if (!await _dialegs.MostrarDialeg(dialeg)) return;

        var creat = dialeg.AModel();
        creat.Id = await _clients.Crear(creat);

        ClientsActius.Add(creat);
        ClientSeleccionat = creat;
        TextClient = string.Empty;
        RevisarAvisConvidat();
    }

    private static async Task<IvaMode> ModeVigent(IConfiguracioService configuracio)
        => Enum.TryParse<IvaMode>(await configuracio.Obtenir(ClausConfig.IvaModeActual), out var mode)
            ? mode
            : IvaMode.Inclos;

    /// <summary>Empty sale, opened from "+ Nova venda".</summary>
    public static async Task<VendaDialogViewModel> Nova(
        IVendaService vendes, IClientService clients, ICatalegService cataleg,
        ITreballadoraService treballadores, ISoundService so, IConfiguracioService configuracio,
        IDialogService dialegs)
    {
        var vm = new VendaDialogViewModel(vendes, so, clients, dialegs) { ModeActual = Mode.Nova };
        vm._modeIva = await ModeVigent(configuracio);
        await vm.CarregarOpcions(clients, cataleg, treballadores, configuracio);
        return vm;
    }

    /// <summary>
    /// Prefilled from a completed appointment (RF-09). Takes the appointment itself and
    /// not just its id because the service it was booked for becomes the first sale
    /// line, and a Venda has no service of its own to read that from.
    /// </summary>
    public static async Task<VendaDialogViewModel> DesDeCita(
        IVendaService vendes, IClientService clients, ICatalegService cataleg,
        ITreballadoraService treballadores, ISoundService so, IConfiguracioService configuracio,
        IDialogService dialegs, Cita cita)
    {
        var vm = new VendaDialogViewModel(vendes, so, clients, dialegs) { ModeActual = Mode.DesDeCita };
        vm._modeIva = await ModeVigent(configuracio);
        await vm.CarregarOpcions(clients, cataleg, treballadores, configuracio);

        var venda = await vendes.PreparaDesDeCita(cita.Id);
        vm._citaId = cita.Id;
        vm.CitaAssociadaText = $"Des de la cita del {venda.Data:dd/MM/yyyy} a les {venda.Hora:HH\\:mm}";
        vm.ClientSeleccionat = vm.ClientsActius.FirstOrDefault(c => c.Id == venda.ClientId);
        vm.TextClient = venda.NomConvidat ?? string.Empty;
        vm.TelefonConvidat = venda.TelefonConvidat;
        vm.Treballadora = vm.TreballadoresActives.FirstOrDefault(t => t.Id == venda.TreballadoraId);

        // The line is a starting point, not a commitment: it stays fully editable, and
        // an appointment with no service simply opens with an empty sale.
        if (cita.ServeiId is int serveiId
            && vm.ServeisActius.FirstOrDefault(s => s.Id == serveiId) is { } servei)
            vm.AfegirServei(servei);

        vm.RevisarAvisConvidat();
        return vm;
    }

    /// <summary>Existing sale, opened for editing.</summary>
    public static async Task<VendaDialogViewModel> Editar(
        IVendaService vendes, IClientService clients, ICatalegService cataleg,
        ITreballadoraService treballadores, ISoundService so, IConfiguracioService configuracio,
        IDialogService dialegs, Venda venda)
    {
        var vm = new VendaDialogViewModel(vendes, so, clients, dialegs) { ModeActual = Mode.Editar };
        vm._modeIva = venda.IvaMode;
        await vm.CarregarOpcions(clients, cataleg, treballadores, configuracio);

        vm.ClientSeleccionat = venda.ClientId is int cid ? vm.ClientsActius.FirstOrDefault(c => c.Id == cid) : null;
        vm.TextClient = venda.NomConvidat ?? string.Empty;
        vm.TelefonConvidat = venda.TelefonConvidat;
        vm.Treballadora = venda.TreballadoraId is int tid ? vm.TreballadoresActives.FirstOrDefault(t => t.Id == tid) : null;
        vm.MetodePagament = vm.MetodesActius.FirstOrDefault(m => m.Id == venda.MetodePagamentId);
        vm.Observacions = venda.Observacions;
        vm._citaId = venda.CitaId;

        foreach (var linia in venda.Linies)
        {
            var liniaVm = new VendaLiniaViewModel
            {
                ServeiId = linia.ServeiId, ProducteId = linia.ProducteId,
                Descripcio = linia.Descripcio, Quantitat = linia.Quantitat,
                PreuText = Diners.FormatExport(linia.PreuUnitariCents), IvaBp = linia.IvaBp
            };
            vm.AfegirLiniaExistent(liniaVm);
        }

        vm._id = venda.Id;
        vm.RevisarAvisConvidat();
        return vm;
    }

    private async Task CarregarOpcions(IClientService clients, ICatalegService cataleg,
        ITreballadoraService treballadores, IConfiguracioService configuracio)
    {
        _avisConvidatActivat = await configuracio.ObtenirBool(ClausConfig.MostrarAvisConvidat, true);

        foreach (var c in await clients.ObtenirActius()) ClientsActius.Add(c);
        foreach (var s in await cataleg.ObtenirServeis(nomesActius: true)) ServeisActius.Add(s);
        foreach (var p in await cataleg.ObtenirProductes(nomesActius: true)) ProductesActius.Add(p);
        foreach (var t in await treballadores.ObtenirTotes(nomesActives: true)) TreballadoresActives.Add(t);
        foreach (var m in await cataleg.ObtenirMetodes(nomesActius: true)) MetodesActius.Add(m);
    }

    [RelayCommand] private void AfegirServei(Servei servei) => AfegirLiniaExistent(VendaLiniaViewModel.DesDeServei(servei));
    [RelayCommand] private void AfegirProducte(Producte producte) => AfegirLiniaExistent(VendaLiniaViewModel.DesDeProducte(producte));
    [RelayCommand] private void AfegirConceptePersonalitzat() => AfegirLiniaExistent(VendaLiniaViewModel.Lliure());

    [RelayCommand]
    private void TreureLinia(VendaLiniaViewModel linia)
    {
        linia.Canviada -= RecalcularTotals;
        Linies.Remove(linia);
        RecalcularTotals();
    }

    private void AfegirLiniaExistent(VendaLiniaViewModel linia)
    {
        linia.Canviada += RecalcularTotals;
        Linies.Add(linia);
        RecalcularTotals();
    }

    private bool PotCobrar() =>
        Linies.Count > 0
        && MetodePagament is not null
        && (ClientSeleccionat is not null || !string.IsNullOrWhiteSpace(TextClient));

    [RelayCommand]
    private void RecalcularTotals()
    {
        var linies = Linies.Select(l => l.AModel()).ToList();
        var d = IvaCalculator.Calcular(linies, _modeIva);
        BaseCents = d.BaseCents;
        IvaCents = d.IvaCents;
        TotalCents = d.TotalCents;
        Desglossament = IvaCalculator.CalcularPerTipus(linies, _modeIva);
        OnPropertyChanged(nameof(BaseText));
        OnPropertyChanged(nameof(IvaText));
        OnPropertyChanged(nameof(TotalText));
    }

    [RelayCommand]
    private async Task Cobrar()
    {
        if (!PotCobrar())
        {
            ErrorValidacio = Linies.Count == 0
                ? "Cal afegir almenys una línia per cobrar."
                : "Cal triar un mètode de pagament.";
            return;
        }

        ErrorValidacio = null;

        var venda = new Venda
        {
            Id = _id ?? 0,
            Data = DateOnly.FromDateTime(DateTime.Now),
            Hora = TimeOnly.FromDateTime(DateTime.Now),
            ClientId = ClientSeleccionat?.Id,
            NomConvidat = ClientSeleccionat is null ? TextClient.Trim() : null,
            TelefonConvidat = ClientSeleccionat is null ? TelefonConvidat : null,
            TreballadoraId = Treballadora?.Id,
            MetodePagamentId = MetodePagament!.Id,
            Observacions = string.IsNullOrWhiteSpace(Observacions) ? null : Observacions.Trim(),
            CitaId = _citaId
        };

        var linies = Linies.Select(l => l.AModel()).ToList();

        if (ModeActual == Mode.Editar) await _vendes.Actualitzar(venda, linies);
        else await _vendes.Crear(venda, linies);

        await _so.ReproduirConfirmacio();
        SolicitarTancar(true);
    }

    [RelayCommand]
    private async Task AnullarVenda()
    {
        if (_id is int id)
        {
            await _vendes.Anullar(id);
            SolicitarTancar(true);
        }
    }

    [RelayCommand]
    private void Cancellar() => SolicitarTancar(false);
}
