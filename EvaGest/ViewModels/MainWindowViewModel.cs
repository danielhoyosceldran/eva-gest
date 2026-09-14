using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using EvaGest.Services;
using EvaGest.ViewModels.Dialogs;
using EvaGest.ViewModels.Pages;

namespace EvaGest.ViewModels;

public partial class MainWindowViewModel : ObservableObject
{
    private readonly IDialogService _dialogs;

    [ObservableProperty]
    private PageViewModelBase _currentPage;

    // Kept alive for the session so navigating away and back does not lose state.
    private readonly HomeViewModel _start;
    private readonly AgendaViewModel _agenda;
    private readonly ClientsViewModel _clients;
    private readonly WorkersViewModel _workers;
    private readonly CatalogViewModel _catalog;
    private readonly SalesViewModel _sales;
    private readonly TillViewModel _till;
    private readonly ReportsViewModel _reports;
    private readonly SettingsViewModel _settings;

    public MainWindowViewModel(HomeViewModel start, CatalogViewModel catalog, ClientsViewModel clients,
        WorkersViewModel workers, AgendaViewModel agenda, SalesViewModel sales,
        TillViewModel till, ReportsViewModel reports, SettingsViewModel settings,
        IDialogService dialogs)
    {
        _start = start;
        _catalog = catalog;
        _clients = clients;
        _workers = workers;
        _agenda = agenda;
        _sales = sales;
        _till = till;
        _reports = reports;
        _settings = settings;
        _dialogs = dialogs;
        _currentPage = _start;
    }

    [RelayCommand] private void NavigateHome() => CurrentPage = _start;
    [RelayCommand] private void NavigateAgenda() => CurrentPage = _agenda;
    [RelayCommand] private void NavigateClients() => CurrentPage = _clients;
    [RelayCommand] private void NavigateWorkers() => CurrentPage = _workers;
    [RelayCommand] private void NavigateCatalog() => CurrentPage = _catalog;
    [RelayCommand] private void NavigateSales() => CurrentPage = _sales;
    [RelayCommand] private void NavigateTill() => CurrentPage = _till;
    [RelayCommand] private void NavigateReports() => CurrentPage = _reports;
    [RelayCommand] private void NavigateSettings() => CurrentPage = _settings;

    [RelayCommand]
    private async Task OpenHelp()
    {
        var vm = new HelpViewModel();
        await _dialogs.ShowDialog(vm);
    }
}
