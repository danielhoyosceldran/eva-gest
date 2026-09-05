using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using EvaGest.Services;
using EvaGest.ViewModels.Dialegs;
using EvaGest.ViewModels.Pagines;

namespace EvaGest.ViewModels;

public partial class MainWindowViewModel : ObservableObject
{
    private readonly IDialogService _dialegs;

    [ObservableProperty]
    private PaginaViewModelBase _paginaActual;

    // Kept alive for the session so navigating away and back does not lose state.
    private readonly IniciViewModel _inici;
    private readonly AgendaViewModel _agenda;
    private readonly ClientsViewModel _clients;
    private readonly TreballadoresViewModel _treballadores = new();
    private readonly CatalegViewModel _cataleg;
    private readonly VendesViewModel _vendes;
    private readonly CaixaViewModel _caixa;
    private readonly InformesViewModel _informes;
    private readonly ConfiguracioViewModel _configuracio;

    public MainWindowViewModel(IniciViewModel inici, CatalegViewModel cataleg, ClientsViewModel clients,
        AgendaViewModel agenda, VendesViewModel vendes, CaixaViewModel caixa,
        InformesViewModel informes, ConfiguracioViewModel configuracio, IDialogService dialegs)
    {
        _inici = inici;
        _cataleg = cataleg;
        _clients = clients;
        _agenda = agenda;
        _vendes = vendes;
        _caixa = caixa;
        _informes = informes;
        _configuracio = configuracio;
        _dialegs = dialegs;
        _paginaActual = _inici;
    }

    [RelayCommand] private void NavegarInici() => PaginaActual = _inici;
    [RelayCommand] private void NavegarAgenda() => PaginaActual = _agenda;
    [RelayCommand] private void NavegarClients() => PaginaActual = _clients;
    [RelayCommand] private void NavegarTreballadores() => PaginaActual = _treballadores;
    [RelayCommand] private void NavegarCataleg() => PaginaActual = _cataleg;
    [RelayCommand] private void NavegarVendes() => PaginaActual = _vendes;
    [RelayCommand] private void NavegarCaixa() => PaginaActual = _caixa;
    [RelayCommand] private void NavegarInformes() => PaginaActual = _informes;
    [RelayCommand] private void NavegarConfiguracio() => PaginaActual = _configuracio;

    [RelayCommand]
    private async Task ObrirAjuda()
    {
        var vm = new AjudaViewModel();
        await _dialegs.MostrarDialeg(vm);
    }
}
