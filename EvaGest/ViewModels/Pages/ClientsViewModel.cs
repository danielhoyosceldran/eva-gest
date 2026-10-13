using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using EvaGest.Models;
using EvaGest.Services;
using EvaGest.ViewModels.Dialogs;
using EvaGest.Resources;

namespace EvaGest.ViewModels.Pages;

public partial class ClientsViewModel(IClientService clients, IReportsService reports, IDialogService dialogs,
    IOwnerAccessService owner) : PageViewModelBase
{
    public override string Title => Texts.NavClients;

    [ObservableProperty] private string _searchText = string.Empty;
    [ObservableProperty] private bool _showAsleep;

    public ObservableCollection<Client> Active { get; } = [];
    public ObservableCollection<Client> Asleep { get; } = [];

    /// <summary>
    /// Shown in place of the table when it has no rows, so the page says what to do
    /// instead of showing bare headers: add the first client, look among the asleep
    /// ones, or try another search. Null while the table has rows (or before loading).
    /// </summary>
    [ObservableProperty] private string? _emptyText;

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

            EmptyText = Active.Count > 0 ? null
                : !string.IsNullOrWhiteSpace(SearchText) ? string.Format(Texts.NoClientMatchesSearch, SearchText.Trim())
                : Asleep.Count > 0 ? Texts.NoActiveClients
                : Texts.NoClientsYet;
        }
        finally { Loading = false; }
    }

    partial void OnSearchTextChanged(string value) => RunInBackground(Load);

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
        vm.Persist = () =>
        {
            var model = vm.AModel();
            return model.Id == 0 ? clients.Create(model) : clients.Update(model);
        };
        if (await dialogs.ShowDialog(vm)) await Load();
    }

    [RelayCommand]
    private async Task OpenRecord(Client client)
    {
        var vm = new ClientRecordViewModel(clients, reports, dialogs, client, showAmounts: owner.IsUnlocked);
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
