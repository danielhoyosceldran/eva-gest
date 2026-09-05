using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using EvaGest.Models;
using EvaGest.Services;

namespace EvaGest.ViewModels.Dialegs;

/// <summary>
/// Cita (nova / editar), pantalles 3.1. What is special: warnings recompute live as
/// data/hora/treballadora change, and NONE of them ever blocks saving (casos-us CU-01).
/// </summary>
public partial class CitaDialogViewModel : DialegViewModelBase
{
    private readonly ICitaService _cites;
    private readonly IDisponibilitatService _disponibilitat;
    private readonly int? _id;
    private readonly int? _clientIdOriginal;
    private readonly EstatCita _estatOriginal = EstatCita.Pendent;

    [ObservableProperty] private DateOnly _data;
    [ObservableProperty] private TimeOnly _hora;
    [ObservableProperty] private int _duradaMin = 30;

    [ObservableProperty] private Client? _clientSeleccionat;
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

    public ObservableCollection<Client> ClientsActius { get; } = [];
    public ObservableCollection<Servei> ServeisActius { get; } = [];
    public ObservableCollection<Treballadora> TreballadoresActives { get; } = [];

    public override string Titol => _id is null ? "Nova cita" : "Editar cita";

    public CitaDialogViewModel(ICitaService cites, IDisponibilitatService disponibilitat,
        IClientService clients, ICatalegService cataleg, ITreballadoraService treballadores,
        DateOnly dataInicial)
    {
        _cites = cites;
        _disponibilitat = disponibilitat;
        Data = dataInicial;
        Hora = new TimeOnly(10, 0);

        _ = Inicialitzar(clients, cataleg, treballadores);
    }

    public CitaDialogViewModel(ICitaService cites, IDisponibilitatService disponibilitat,
        IClientService clients, ICatalegService cataleg, ITreballadoraService treballadores,
        Cita cita) : this(cites, disponibilitat, clients, cataleg, treballadores, cita.Data)
    {
        _id = cita.Id;
        _clientIdOriginal = cita.ClientId;
        _estatOriginal = cita.Estat;
        Hora = cita.Hora;
        DuradaMin = cita.DuradaMin;
        ClientSeleccionat = cita.Client;
        TextClient = cita.NomConvidat ?? string.Empty;
        TelefonConvidat = cita.TelefonConvidat;
        Servei = cita.Servei;
        Treballadora = cita.Treballadora;
        Observacions = cita.Observacions;
    }

    private async Task Inicialitzar(IClientService clients, ICatalegService cataleg, ITreballadoraService treballadores)
    {
        foreach (var c in await clients.ObtenirActius()) ClientsActius.Add(c);
        foreach (var s in await cataleg.ObtenirServeis(nomesActius: true)) ServeisActius.Add(s);
        foreach (var t in await treballadores.ObtenirTotes(nomesActives: true)) TreballadoresActives.Add(t);
        await RevisarAvisos();
    }

    partial void OnServeiChanged(Servei? value)
    {
        if (value?.DuradaMin is int d) DuradaMin = d;
    }

    partial void OnDataChanged(DateOnly value) => _ = RevisarAvisos();
    partial void OnHoraChanged(TimeOnly value) => _ = RevisarAvisos();
    partial void OnDuradaMinChanged(int value) => _ = RevisarAvisos();
    partial void OnTreballadoraChanged(Treballadora? value) => _ = RevisarAvisos();

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
        bool teClient = ClientSeleccionat is not null || !string.IsNullOrWhiteSpace(TextClient);
        if (!teClient)
        {
            ErrorValidacio = "Cal indicar el nom del client per guardar la cita.";
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
            ClientId = ClientSeleccionat?.Id,
            NomConvidat = ClientSeleccionat is null ? TextClient.Trim() : null,
            TelefonConvidat = ClientSeleccionat is null ? TelefonConvidat : null,
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
