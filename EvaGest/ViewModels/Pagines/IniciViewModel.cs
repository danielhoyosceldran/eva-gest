using System.Collections.ObjectModel;
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using EvaGest.Models;
using EvaGest.Services;
using EvaGest.ViewModels.Dialegs;

namespace EvaGest.ViewModels.Pagines;

/// <summary>
/// Inici (pantalles 2.1): the day's state at a glance, and the five quick actions
/// that let the user skip navigating anywhere else for routine work.
/// </summary>
public partial class IniciViewModel(
    ICitaService cites, IVendaService vendes, ICaixaService caixa, IClientService clients,
    IDisponibilitatService disponibilitat, ICatalegService cataleg, ITreballadoraService treballadores,
    IConfiguracioService configuracio, ISoundService so, IDialogService dialegs) : PaginaViewModelBase
{
    public override string Titol => "Inici";

    [ObservableProperty] private string _dataText = string.Empty;
    [ObservableProperty] private string? _avisAniversariText;

    [ObservableProperty] private int _citesAvui;
    [ObservableProperty] private int _vendesAvui;
    [ObservableProperty] private string _cobratAvuiText = "—";
    [ObservableProperty] private string _entradesText = "—";
    [ObservableProperty] private string _sortidesText = "—";
    [ObservableProperty] private string _balancText = "—";

    public ObservableCollection<Cita> CitesDelDia { get; } = [];

    public async Task Carregar()
    {
        Carregant = true;
        try
        {
            var avui = DateOnly.FromDateTime(DateTime.Today);
            DataText = avui.ToString("dddd, d MMMM 'de' yyyy", new CultureInfo("ca-ES"));
            DataText = char.ToUpper(DataText[0], new CultureInfo("ca-ES")) + DataText[1..];

            var aniversaris = await clients.AniversarisAvui();
            AvisAniversariText = aniversaris.Count > 0
                ? "Avui fa anys: " + string.Join(", ", aniversaris.Select(c => c.Nom))
                : null;

            CitesDelDia.Clear();
            foreach (var c in await cites.ObtenirPerDia(avui)) CitesDelDia.Add(c);
            CitesAvui = CitesDelDia.Count;

            var resum = await caixa.Resum(avui, avui);
            VendesAvui = (await vendes.Cercar(new FiltreVendes(avui, avui, Estat: EstatVenda.Activa))).Count;
            CobratAvuiText = Diners.Format((int)resum.VendesCents);
            EntradesText = Diners.Format((int)resum.EntradesCents);
            SortidesText = Diners.Format((int)resum.SortidesCents);
            BalancText = Diners.Format((int)resum.BalancCents);
        }
        finally { Carregant = false; }
    }

    /// <summary>
    /// Marking an appointment as done is where the agenda meets the till (CU-02): the
    /// sale dialog opens straight away with the appointment's client, service and
    /// worker filled in. Closing it without charging is a valid outcome — the client
    /// came but has not paid yet — so the appointment stays Realitzada either way.
    /// </summary>
    [RelayCommand]
    private async Task MarcarRealitzada(Cita cita)
    {
        await cites.CanviarEstat(cita.Id, EstatCita.Realitzada);

        var vm = await VendaDialogViewModel.DesDeCita(
            vendes, clients, cataleg, treballadores, so, configuracio, dialegs, cita);
        await dialegs.MostrarDialeg(vm);

        await Carregar();
    }

    [RelayCommand] private Task MarcarCancellada(Cita cita) => CanviarEstatCita(cita, EstatCita.Cancellada);
    [RelayCommand] private Task MarcarNoAssistida(Cita cita) => CanviarEstatCita(cita, EstatCita.NoAssistida);

    private async Task CanviarEstatCita(Cita cita, EstatCita nouEstat)
    {
        await cites.CanviarEstat(cita.Id, nouEstat);
        await Carregar();
    }

    [RelayCommand]
    private async Task NovaCita()
    {
        var vm = new CitaDialogViewModel(cites, disponibilitat, clients, cataleg, treballadores,
            configuracio, dialegs, DateOnly.FromDateTime(DateTime.Today));
        if (await dialegs.MostrarDialeg(vm)) await Carregar();
    }

    [RelayCommand]
    private async Task NovaVenda()
    {
        var vm = await VendaDialogViewModel.Nova(vendes, clients, cataleg, treballadores, so, configuracio, dialegs);
        if (await dialegs.MostrarDialeg(vm)) await Carregar();
    }

    [RelayCommand]
    private async Task NouClient()
    {
        var vm = new ClientDialogViewModel(clients);
        if (await dialegs.MostrarDialeg(vm))
        {
            var model = vm.AModel();
            if (model.Id == 0) await clients.Crear(model);
            else await clients.Actualitzar(model);
        }
    }

    [RelayCommand]
    private async Task NovaEntrada() => await ObrirDialegMoviment(TipusMoviment.Entrada);

    [RelayCommand]
    private async Task NovaSortida() => await ObrirDialegMoviment(TipusMoviment.Sortida);

    private async Task ObrirDialegMoviment(TipusMoviment tipus)
    {
        var vm = new MovimentDialogViewModel(tipus);
        await vm.CarregarMetodes(cataleg);
        if (await dialegs.MostrarDialeg(vm))
        {
            await caixa.Crear(vm.AModel());
            await Carregar();
        }
    }

    [RelayCommand]
    private async Task ObrirAjuda()
    {
        var vm = new AjudaViewModel("Inici");
        await dialegs.MostrarDialeg(vm);
    }
}
