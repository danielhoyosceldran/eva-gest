using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using EvaGest.Models;
using EvaGest.Services;
using EvaGest.ViewModels.Dialegs;

namespace EvaGest.ViewModels.Pagines;

public partial class VendesViewModel(
    IVendaService vendes, IClientService clients, ICatalegService cataleg,
    ITreballadoraService treballadores, ISoundService so, IDialogService dialegs) : PaginaViewModelBase
{
    public override string Titol => "Vendes";

    public ObservableCollection<Venda> Vendes { get; } = [];

    public async Task Carregar()
    {
        Carregant = true;
        try
        {
            Vendes.Clear();
            foreach (var v in await vendes.Cercar(new FiltreVendes())) Vendes.Add(v);
        }
        finally { Carregant = false; }
    }

    [RelayCommand]
    private async Task NovaVenda()
    {
        var vm = await VendaDialogViewModel.Nova(vendes, clients, cataleg, treballadores, so);
        if (await dialegs.MostrarDialeg(vm)) await Carregar();
    }

    [RelayCommand]
    private async Task EditarVenda(Venda venda)
    {
        var vm = await VendaDialogViewModel.Editar(vendes, clients, cataleg, treballadores, so, venda);
        if (await dialegs.MostrarDialeg(vm)) await Carregar();
    }

    [RelayCommand]
    private async Task AnullarVenda(Venda venda)
    {
        bool confirmat = await dialegs.Confirmar(
            "Anul·lar venda?",
            $"La venda de {Diners.Format(venda.TotalCents)} passarà a l'estat Anul·lada. "
            + "Es manté visible a l'historial però queda exclosa dels totals.",
            "Anul·lar");

        if (!confirmat) return;

        await vendes.Anullar(venda.Id);
        await Carregar();
    }
}
