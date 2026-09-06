using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using EvaGest.Helpers;
using EvaGest.Models;
using EvaGest.Services;
using EvaGest.ViewModels.Elements;

namespace EvaGest.ViewModels.Dialegs;

/// <summary>
/// One entry of the client picker. The first one is always "Convidat", so choosing a
/// registered client is undone by picking it again rather than by an empty combo box.
/// </summary>
public sealed record OpcioClient(Client? Client)
{
    public string Nom => Client?.Nom ?? "Convidat (nom lliure)";
    public bool EsConvidat => Client is null;
}

/// <summary>
/// Cita (nova / editar), pantalles 3.1. What is special: warnings recompute live as
/// data/hora/treballadora change, and NONE of them ever blocks saving (casos-us CU-01).
/// The hour is picked on the same weekly grid the Agenda page uses, with a ghost block
/// showing where this appointment would land.
/// </summary>
public partial class CitaDialogViewModel : DialegViewModelBase
{
    private readonly ICitaService _cites;
    private readonly IDisponibilitatService _disponibilitat;
    private readonly IClientService _clients;
    private readonly IDialogService _dialegs;
    private readonly int? _id;

    /// <summary>Whether the guest reminder is switched on (RF-05bis). Read once when
    /// the dialog opens; off by default until the settings have loaded, so the warning
    /// never flashes up before it is known to be wanted.</summary>
    private bool _avisConvidatActivat;
    private readonly int? _clientIdOriginal;
    private readonly int? _serveiIdOriginal;
    private readonly int? _treballadoraIdOriginal;
    private readonly EstatCita _estatOriginal = EstatCita.Pendent;

    [ObservableProperty] private DateOnly _data;
    [ObservableProperty] private TimeOnly _hora;
    [ObservableProperty] private int _duradaMin = 30;

    [ObservableProperty] private OpcioClient? _opcioSeleccionada;
    [ObservableProperty] private string _textClient = string.Empty;
    [ObservableProperty] private string? _telefonConvidat;

    [ObservableProperty] private Servei? _servei;
    [ObservableProperty] private Treballadora? _treballadora;
    [ObservableProperty] private string? _observacions;

    // Non-blocking inline warnings (pantalles 3.1)
    [ObservableProperty] private bool _avisSolapament;
    [ObservableProperty] private bool _avisForaHorari;
    [ObservableProperty] private bool _avisDiaTancat;
    [ObservableProperty] private string? _motiuDiaTancat;
    [ObservableProperty] private bool _avisClientNoRegistrat;

    [ObservableProperty] private GraellaSetmanaViewModel? _graella;

    public ObservableCollection<OpcioClient> OpcionsClient { get; } = [];
    public ObservableCollection<Servei> ServeisActius { get; } = [];
    public ObservableCollection<Treballadora> TreballadoresActives { get; } = [];

    /// <summary>The picker itself is never disabled: deselecting is picking "Convidat".</summary>
    public bool PotEscriureConvidat => OpcioSeleccionada?.EsConvidat ?? true;

    public Client? ClientSeleccionat => OpcioSeleccionada?.Client;

    public override string Titol => _id is null ? "Nova cita" : "Editar cita";

    /// <summary>Awaited by the tests; the views let it run in the background.</summary>
    public Task Inicialitzacio { get; }

    public CitaDialogViewModel(ICitaService cites, IDisponibilitatService disponibilitat,
        IClientService clients, ICatalegService cataleg, ITreballadoraService treballadores,
        IConfiguracioService configuracio, IDialogService dialegs,
        DateOnly dataInicial, TimeOnly? horaInicial = null)
        : this(cites, disponibilitat, clients, cataleg, treballadores, configuracio, dialegs,
               null, dataInicial, horaInicial) { }

    public CitaDialogViewModel(ICitaService cites, IDisponibilitatService disponibilitat,
        IClientService clients, ICatalegService cataleg, ITreballadoraService treballadores,
        IConfiguracioService configuracio, IDialogService dialegs, Cita cita)
        : this(cites, disponibilitat, clients, cataleg, treballadores, configuracio, dialegs,
               cita, cita.Data, cita.Hora) { }

    /// <summary>
    /// Both modes share one body on purpose. Chaining the edit constructor to the new one
    /// used to leave the appointment's fields unset while the background load was already
    /// running, so the client could not be matched by the time the list arrived.
    /// </summary>
    private CitaDialogViewModel(ICitaService cites, IDisponibilitatService disponibilitat,
        IClientService clients, ICatalegService cataleg, ITreballadoraService treballadores,
        IConfiguracioService configuracio, IDialogService dialegs,
        Cita? cita, DateOnly dataInicial, TimeOnly? horaInicial)
    {
        _cites = cites;
        _disponibilitat = disponibilitat;
        _clients = clients;
        _dialegs = dialegs;
        Data = dataInicial;
        Hora = horaInicial ?? new TimeOnly(10, 0);

        OpcionsClient.Add(new OpcioClient(null));
        OpcioSeleccionada = OpcionsClient[0];

        if (cita is not null)
        {
            _id = cita.Id;
            _clientIdOriginal = cita.ClientId;
            _estatOriginal = cita.Estat;
            _serveiIdOriginal = cita.ServeiId;
            _treballadoraIdOriginal = cita.TreballadoraId;
            TextClient = cita.NomConvidat ?? string.Empty;
            if (cita.Client is null) TelefonConvidat = cita.TelefonConvidat;
            Observacions = cita.Observacions;
            // Last, and after the service: picking a service overwrites the duration,
            // and an edited appointment must keep the duration it was saved with.
            DuradaMin = cita.DuradaMin;
        }

        Inicialitzacio = Inicialitzar(clients, cataleg, treballadores, configuracio);
    }

    private async Task Inicialitzar(IClientService clients, ICatalegService cataleg,
        ITreballadoraService treballadores, IConfiguracioService configuracio)
    {
        _avisConvidatActivat = await configuracio.ObtenirBool(ClausConfig.MostrarAvisConvidat, true);

        // A new appointment starts at the configured default; an edited one keeps the
        // duration it was saved with, and a service overrides both further down.
        if (_id is null)
            DuradaMin = await configuracio.ObtenirInt(ClausConfig.DuradaDefecteCitaMin, 30);

        foreach (var c in await clients.ObtenirActius()) OpcionsClient.Add(new OpcioClient(c));
        foreach (var s in await cataleg.ObtenirServeis(nomesActius: true)) ServeisActius.Add(s);
        foreach (var t in await treballadores.ObtenirTotes(nomesActives: true)) TreballadoresActives.Add(t);

        // Resolve every selection by id, never by reference: the appointment's related
        // entities come from a different AsNoTracking query than these lists, so the
        // instances are not equal and the combo boxes would render empty.
        if (_clientIdOriginal is int idClient)
            OpcioSeleccionada = OpcionsClient.FirstOrDefault(o => o.Client?.Id == idClient) ?? OpcionsClient[0];

        int duradaGuardada = DuradaMin;
        if (_serveiIdOriginal is int idServei)
            Servei = ServeisActius.FirstOrDefault(s => s.Id == idServei);
        if (_treballadoraIdOriginal is int idTreballadora)
            Treballadora = TreballadoresActives.FirstOrDefault(t => t.Id == idTreballadora);
        DuradaMin = duradaGuardada;   // picking the service above resets it

        Graella = new GraellaSetmanaViewModel(_cites, _disponibilitat, configuracio, ModeGraella.Selector,
            alClicarSlot: (data, hora) => { Data = data; Hora = hora; },
            alClicarCita: null)
        {
            CitaSeleccionadaId = _id
        };
        await Graella.CarregarSetmana(SetmanaHelper.DilluIrsDeLaSetmana(Data));
        Graella.MostrarFantasma(Data, Hora, DuradaMin);

        RevisarAvisConvidat();
        await RevisarAvisos();
    }

    /// <summary>
    /// Shown only while a free-text name is actually typed: an empty guest field is not
    /// a guest yet. Never blocks saving — it offers to register, nothing more (CU-01).
    /// </summary>
    private void RevisarAvisConvidat()
        => AvisClientNoRegistrat = _avisConvidatActivat
            && ClientSeleccionat is null
            && !string.IsNullOrWhiteSpace(TextClient);

    partial void OnTextClientChanged(string value) => RevisarAvisConvidat();

    /// <summary>
    /// Registers the guest without losing the half-filled appointment, and selects the
    /// new client straight away, so the reminder does not simply reappear.
    /// </summary>
    [RelayCommand]
    private async Task RegistrarClientAra()
    {
        var dialeg = new ClientDialogViewModel(_clients) { Nom = TextClient.Trim() };
        if (TelefonConvidat is { Length: > 0 } telefon) dialeg.Mobil = telefon;

        if (!await _dialegs.MostrarDialeg(dialeg)) return;

        var creat = dialeg.AModel();
        creat.Id = await _clients.Crear(creat);

        var opcio = new OpcioClient(creat);
        OpcionsClient.Add(opcio);
        OpcioSeleccionada = opcio;
        TextClient = string.Empty;
        RevisarAvisConvidat();
    }

    partial void OnServeiChanged(Servei? value)
    {
        if (value?.DuradaMin is int d) DuradaMin = d;
    }

    partial void OnOpcioSeleccionadaChanged(OpcioClient? value)
    {
        if (value?.Client is { } client)
        {
            TextClient = string.Empty;
            TelefonConvidat = client.Mobil;
        }
        else if (ClientSeleccionatAbans is not null)
        {
            // Going back to "Convidat" must not leave the previous client's phone behind
            TelefonConvidat = null;
        }

        ClientSeleccionatAbans = value?.Client;
        OnPropertyChanged(nameof(PotEscriureConvidat));
        OnPropertyChanged(nameof(ClientSeleccionat));
        RevisarAvisConvidat();
    }

    private Client? ClientSeleccionatAbans { get; set; }

    partial void OnDataChanged(DateOnly value)
    {
        _ = SincronitzarGraella(value);
        _ = RevisarAvisos();
    }

    partial void OnHoraChanged(TimeOnly value)
    {
        Graella?.MostrarFantasma(Data, value, DuradaMin);
        _ = RevisarAvisos();
    }

    partial void OnDuradaMinChanged(int value)
    {
        Graella?.MostrarFantasma(Data, Hora, Math.Max(1, value));
        _ = RevisarAvisos();
    }

    partial void OnTreballadoraChanged(Treballadora? value) => _ = RevisarAvisos();

    private async Task SincronitzarGraella(DateOnly data)
    {
        if (Graella is not { } graella) return;

        var dilluns = SetmanaHelper.DilluIrsDeLaSetmana(data);
        if (graella.InicSetmana != dilluns) await graella.CarregarSetmana(dilluns);
        graella.MostrarFantasma(data, Hora, DuradaMin);
    }

    private async Task RevisarAvisos()
    {
        var r = await _disponibilitat.Comprovar(Data, Hora, DuradaMin, Treballadora?.Id, _id);
        AvisSolapament = r.HiHaSolapament;
        AvisForaHorari = r.ForaHorari;
        AvisDiaTancat = r.DiaTancat;
        MotiuDiaTancat = r.MotiuDiaTancat;
    }

    [RelayCommand]
    private async Task Guardar()
    {
        var client = ClientSeleccionat;
        if (client is null && string.IsNullOrWhiteSpace(TextClient))
        {
            ErrorValidacio = "Cal indicar el nom del convidat o triar un client registrat.";
            return;
        }
        if (DuradaMin <= 0)
        {
            ErrorValidacio = "La durada ha de ser més gran que zero.";
            return;
        }

        ErrorValidacio = null;

        var cita = new Cita
        {
            Id = _id ?? 0,
            Data = Data,
            Hora = Hora,
            DuradaMin = DuradaMin,
            ClientId = client?.Id,
            NomConvidat = client is null ? TextClient.Trim() : null,
            TelefonConvidat = client is null ? TelefonConvidat : null,
            ServeiId = Servei?.Id,
            TreballadoraId = Treballadora?.Id,
            Observacions = string.IsNullOrWhiteSpace(Observacions) ? null : Observacions.Trim(),
            Estat = _estatOriginal
        };

        if (_id is null) await _cites.Crear(cita);
        else await _cites.Actualitzar(cita);

        SolicitarTancar(true);
    }

    [RelayCommand]
    private void Cancellar() => SolicitarTancar(false);
}
