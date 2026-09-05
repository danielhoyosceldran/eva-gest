using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using EvaGest.Models;
using EvaGest.Services;

namespace EvaGest.ViewModels.Dialegs;

/// <summary>One row of the chronological history block (pantalles 3.4), with the
/// amount already formatted so the view stays free of conversion logic.</summary>
public record HistorialFila(DateOnly Data, string Tipus, string Concepte, string Estat, string Import);

public partial class FitxaClientViewModel : DialegViewModelBase
{
    private readonly IClientService _clients;
    private readonly IInformesService _informes;
    private readonly IDialogService _dialegs;

    [ObservableProperty] private Client _client;
    [ObservableProperty] private IndicadorsClient? _indicadors;

    public ObservableCollection<HistorialFila> Historial { get; } = [];

    public string TotalGastatText => Indicadors is null ? "—" : Diners.Format((int)Indicadors.TotalGastatCents);
    public string MitjanaText => Indicadors?.MitjanaPerVisitaEuros is decimal m ? m.ToString("C2") : "—";
    public string FrequenciaText => Indicadors?.FrequenciaDies is double f ? f.ToString("0.0") : "—";
    public string AdormirDespertarText => Client.Adormit ? "Despertar" : "Adormir";

    partial void OnIndicadorsChanged(IndicadorsClient? value)
    {
        OnPropertyChanged(nameof(TotalGastatText));
        OnPropertyChanged(nameof(MitjanaText));
        OnPropertyChanged(nameof(FrequenciaText));
    }

    partial void OnClientChanged(Client value) => OnPropertyChanged(nameof(AdormirDespertarText));

    /// <summary>True once the client has actually been deleted, so the host page
    /// knows to remove it from its own list instead of just refreshing.</summary>
    public bool Eliminat { get; private set; }

    public override string Titol => Client.Nom;

    public FitxaClientViewModel(IClientService clients, IInformesService informes, IDialogService dialegs, Client client)
    {
        _clients = clients;
        _informes = informes;
        _dialegs = dialegs;
        _client = client;
    }

    public async Task Carregar()
    {
        Indicadors = await _informes.IndicadorsDeClient(Client.Id);

        Historial.Clear();
        foreach (var fila in await _clients.HistorialDeClient(Client.Id))
        {
            Historial.Add(new HistorialFila(
                fila.Data, fila.Tipus, fila.Concepte,
                Etiquetes(fila.Tipus, fila.Estat),
                fila.ImportCents is int cents ? Diners.Format(cents) : "—"));
        }
    }

    private static string Etiquetes(string tipus, string estatBrut) => tipus == "Cita"
        ? EvaGest.Services.Etiquetes.Text(Enum.Parse<EstatCita>(estatBrut))
        : EvaGest.Services.Etiquetes.Text(Enum.Parse<EstatVenda>(estatBrut));

    [RelayCommand]
    private void TancarFitxa() => SolicitarTancar(true);

    [RelayCommand]
    private async Task AdormirDespertar()
    {
        if (Client.Adormit) await _clients.Despertar(Client.Id);
        else await _clients.Adormir(Client.Id);

        Client = (await _clients.ObtenirPerId(Client.Id))!;
    }

    [RelayCommand]
    private async Task Eliminar()
    {
        var (cites, vendes) = await _clients.ComptarHistorial(Client.Id);

        bool confirmat = await _dialegs.Confirmar(
            $"Vols eliminar {Client.Nom}?",
            $"S'esborrarà la seva fitxa i tot el seu historial: {cites} cites i {vendes} vendes. "
            + "Aquesta acció no es pot desfer.\n\n"
            + "Si només vols que deixi d'aparèixer a les cerques, pots adormir-lo i conservar les dades.",
            "Eliminar definitivament");

        if (!confirmat) return;

        await _clients.Eliminar(Client.Id);
        Eliminat = true;
        SolicitarTancar(true);
    }
}
