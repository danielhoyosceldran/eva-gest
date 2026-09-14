using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using EvaGest.Models;
using EvaGest.Services;
using EvaGest.ViewModels.Dialogs;
using EvaGest.Resources;

namespace EvaGest.ViewModels.Pages;

public partial class ClientsViewModel(IClientService clients, IReportsService reports, IDialogService dialogs) : PageViewModelBase
{
    public override string Title => Texts.NavClients;

    [ObservableProperty] private string _searchText = string.Empty;
    [ObservableProperty] private bool _showAsleep;

    public ObservableCollection<Client> Active { get; } = [];
    public ObservableCollection<Client> Asleep { get; } = [];

    public async Task Load()
    {
        Loading = true;
        try
        {
            var result = string.IsNullOrWhiteSpace(SearchText)
                ? await clients.GetActive()
                : await clients.Search(SearchText);

            Active.Clear();
            foreach (var c in result) Active.Add(c);

            Asleep.Clear();
            foreach (var c in await clients.GetAsleep()) Asleep.Add(c);
        }
        finally { Loading = false; }
    }

    partial void OnSearchTextChanged(string value) => _ = Load();

    [RelayCommand]
    private async Task NewClient()
    {
        var vm = new ClientDialogViewModel(clients);
        await OpenClientDialog(vm);
    }

    [RelayCommand]
    private async Task EditClient(Client client)
    {
        var vm = new ClientDialogViewModel(clients, client);
        await OpenClientDialog(vm);
    }

    private async Task OpenClientDialog(ClientDialogViewModel vm)
    {
        if (await dialogs.ShowDialog(vm))
        {
            var model = vm.AModel();
            if (model.Id == 0) await clients.Create(model);
            else await clients.Update(model);
            await Load();
        }
    }

    [RelayCommand]
    private async Task OpenRecord(Client client)
    {
        var vm = new ClientRecordViewModel(clients, reports, dialogs, client);
        await vm.Load();
        await dialogs.ShowDialog(vm);
        await Load();
    }

    [RelayCommand]
    private async Task Wake(Client client)
    {
        await clients.Wake(client.Id);
        await Load();
    }
}
