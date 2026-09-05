using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using EvaGest.Models;
using EvaGest.Services;
using EvaGest.ViewModels.Dialegs;

namespace EvaGest.ViewModels.Pagines;

/// <summary>
/// The first CRUD page (Fase 3, capa-mvvm 4.7): deliberately the most boring module,
/// so it fixes the View -> ViewModel -> Service pattern that Clients/Agenda/Vendes repeat.
/// </summary>
public partial class CatalegViewModel(ICatalegService cataleg, IDialogService dialegs) : PaginaViewModelBase
{
    public override string Titol => "Catàleg";

    public ObservableCollection<Servei> Serveis { get; } = [];
    public ObservableCollection<Producte> Productes { get; } = [];
    public ObservableCollection<MetodePagament> Metodes { get; } = [];

    public async Task Carregar()
    {
        Carregant = true;
        try
        {
            Serveis.Clear();
            foreach (var s in await cataleg.ObtenirServeis()) Serveis.Add(s);

            Productes.Clear();
            foreach (var p in await cataleg.ObtenirProductes()) Productes.Add(p);

            Metodes.Clear();
            foreach (var m in await cataleg.ObtenirMetodes()) Metodes.Add(m);
        }
        finally { Carregant = false; }
    }

    // --- Serveis ---

    [RelayCommand]
    private async Task NouServei()
    {
        var vm = new ServeiDialogViewModel();
        if (await dialegs.MostrarDialeg(vm))
        {
            await cataleg.CrearServei(vm.AModel());
            await Carregar();
        }
    }

    [RelayCommand]
    private async Task EditarServei(Servei servei)
    {
        var vm = new ServeiDialogViewModel(servei);
        if (await dialegs.MostrarDialeg(vm))
        {
            var actualitzat = vm.AModel();
            actualitzat.Actiu = servei.Actiu;
            await cataleg.ActualitzarServei(actualitzat);
            await Carregar();
        }
    }

    [RelayCommand]
    private async Task CanviarEstatServei(Servei servei)
    {
        await cataleg.CanviarEstatServei(servei.Id, !servei.Actiu);
        await Carregar();
    }

    // --- Productes ---

    [RelayCommand]
    private async Task NouProducte()
    {
        var vm = new ProducteDialogViewModel();
        if (await dialegs.MostrarDialeg(vm))
        {
            await cataleg.CrearProducte(vm.AModel());
            await Carregar();
        }
    }

    [RelayCommand]
    private async Task EditarProducte(Producte producte)
    {
        var vm = new ProducteDialogViewModel(producte);
        if (await dialegs.MostrarDialeg(vm))
        {
            var actualitzat = vm.AModel();
            actualitzat.Actiu = producte.Actiu;
            await cataleg.ActualitzarProducte(actualitzat);
            await Carregar();
        }
    }

    [RelayCommand]
    private async Task CanviarEstatProducte(Producte producte)
    {
        await cataleg.CanviarEstatProducte(producte.Id, !producte.Actiu);
        await Carregar();
    }

    // --- Mètodes de pagament ---

    [RelayCommand]
    private async Task NouMetode()
    {
        var vm = new MetodePagamentDialogViewModel();
        if (await dialegs.MostrarDialeg(vm))
        {
            await cataleg.CrearMetode(vm.AModel().Nom);
            await Carregar();
        }
    }

    [RelayCommand]
    private async Task EditarMetode(MetodePagament metode)
    {
        var vm = new MetodePagamentDialogViewModel(metode);
        if (await dialegs.MostrarDialeg(vm))
        {
            var actualitzat = vm.AModel();
            actualitzat.Actiu = metode.Actiu;
            await cataleg.ActualitzarMetode(actualitzat);
            await Carregar();
        }
    }

    [RelayCommand]
    private async Task CanviarEstatMetode(MetodePagament metode)
    {
        // Deactivating the last active method would make it impossible to take
        // money (pantalles 2.5), so the service is asked first and the UI explains why.
        if (metode.Actiu && !await cataleg.PotDesactivarMetode(metode.Id))
        {
            await dialegs.Informar("No es pot desactivar",
                "Cal mantenir almenys un mètode de pagament actiu per poder cobrar.");
            return;
        }

        await cataleg.CanviarEstatMetode(metode.Id, !metode.Actiu);
        await Carregar();
    }
}
