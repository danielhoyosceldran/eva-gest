using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using EvaGest.Models;
using EvaGest.Services;
using EvaGest.ViewModels.Dialegs;

namespace EvaGest.ViewModels.Pagines;

public partial class ClientsViewModel(IClientService clients, IInformesService informes, IDialogService dialegs) : PaginaViewModelBase
{
    public override string Titol => "Clients";

    [ObservableProperty] private string _textCerca = string.Empty;
    [ObservableProperty] private bool _mostrarAdormits;

    public ObservableCollection<Client> Actius { get; } = [];
    public ObservableCollection<Client> Adormits { get; } = [];

    public async Task Carregar()
    {
        Carregant = true;
        try
        {
            var resultat = string.IsNullOrWhiteSpace(TextCerca)
                ? await clients.ObtenirActius()
                : await clients.Cercar(TextCerca);

            Actius.Clear();
            foreach (var c in resultat) Actius.Add(c);

            Adormits.Clear();
            foreach (var c in await clients.ObtenirAdormits()) Adormits.Add(c);
        }
        finally { Carregant = false; }
    }

    partial void OnTextCercaChanged(string value) => _ = Carregar();

    [RelayCommand]
    private async Task NouClient()
    {
        var vm = new ClientDialogViewModel(clients);
        await ObrirDialegClient(vm);
    }

    [RelayCommand]
    private async Task EditarClient(Client client)
    {
        var vm = new ClientDialogViewModel(clients, client);
        await ObrirDialegClient(vm);
    }

    private async Task ObrirDialegClient(ClientDialogViewModel vm)
    {
        if (await dialegs.MostrarDialeg(vm))
        {
            var model = vm.AModel();
            if (model.Id == 0) await clients.Crear(model);
            else await clients.Actualitzar(model);
            await Carregar();
        }
    }

    [RelayCommand]
    private async Task ObrirFitxa(Client client)
    {
        var vm = new FitxaClientViewModel(clients, informes, dialegs, client);
        await vm.Carregar();
        await dialegs.MostrarDialeg(vm);
        await Carregar();
    }

    [RelayCommand]
    private async Task Despertar(Client client)
    {
        await clients.Despertar(client.Id);
        await Carregar();
    }
}
