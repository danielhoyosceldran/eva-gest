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
public partial class CatalegViewModel(
    ICatalegService cataleg, IConfiguracioService configuracio, IDialogService dialegs)
    : PaginaViewModelBase
{
    public override string Titol => "Catàleg";

    // Prices are stored in cents and VAT in basis points. Bound raw to a {0:0.00}
    // format string, a 15,00 € service showed up as "1500,00 €" and 21 % as "2100",
    // so both are formatted here instead.
    public record FilaServei(Servei Servei, string PreuText, string IvaText, string DuradaText);
    public record FilaProducte(Producte Producte, string PreuText, string IvaText);

    public ObservableCollection<FilaServei> Serveis { get; } = [];
    public ObservableCollection<FilaProducte> Productes { get; } = [];
    public ObservableCollection<MetodePagament> Metodes { get; } = [];

    public async Task Carregar()
    {
        Carregant = true;
        try
        {
            Serveis.Clear();
            foreach (var s in await cataleg.ObtenirServeis())
                Serveis.Add(new FilaServei(s, Diners.Format(s.PreuCents), Percentatges.Format(s.IvaBp),
                    s.DuradaMin is int d ? $"{d} min" : "—"));

            Productes.Clear();
            foreach (var p in await cataleg.ObtenirProductes())
                Productes.Add(new FilaProducte(p, Diners.Format(p.PreuCents), Percentatges.Format(p.IvaBp)));

            Metodes.Clear();
            foreach (var m in await cataleg.ObtenirMetodes()) Metodes.Add(m);
        }
        finally { Carregant = false; }
    }

    /// <summary>The rate proposed for a new catalogue entry (RF-23).</summary>
    private async Task<int> IvaPerDefecte()
        => await configuracio.ObtenirInt(ClausConfig.IvaBpDefecte, 2100);

    // --- Serveis ---

    [RelayCommand]
    private async Task NouServei()
    {
        var vm = new ServeiDialogViewModel(await IvaPerDefecte());
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

    [RelayCommand]
    private async Task EliminarServei(Servei servei)
    {
        if (!await ConfirmarEsborrat("servei", servei.Nom)) return;

        var resultat = await cataleg.EliminarServei(servei.Id);
        await Carregar();
        MostrarAvis(TextResultat(resultat, "El servei", servei.Nom, "cites o vendes"));
    }

    // --- Productes ---

    [RelayCommand]
    private async Task NouProducte()
    {
        var vm = new ProducteDialogViewModel(await IvaPerDefecte());
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

    [RelayCommand]
    private async Task EliminarProducte(Producte producte)
    {
        if (!await ConfirmarEsborrat("producte", producte.Nom)) return;

        var resultat = await cataleg.EliminarProducte(producte.Id);
        await Carregar();
        MostrarAvis(TextResultat(resultat, "El producte", producte.Nom, "vendes"));
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

    [RelayCommand]
    private async Task EliminarMetode(MetodePagament metode)
    {
        if (!await ConfirmarEsborrat("mètode de pagament", metode.Nom)) return;

        var resultat = await cataleg.EliminarMetode(metode.Id);
        await Carregar();

        MostrarAvis(resultat == ResultatEsborrat.Bloquejat
            ? $"«{metode.Nom}» no s'ha eliminat: cal mantenir almenys un mètode de pagament actiu per poder cobrar."
            : TextResultat(resultat, "El mètode", metode.Nom, "vendes o moviments de caixa"));
    }

    // --- Esborrat ---

    /// <summary>
    /// One confirmation for the three lists. It says up front that a used entry will be
    /// deactivated instead of deleted, so the note afterwards confirms what was announced
    /// rather than surprising the user with it.
    /// </summary>
    private Task<bool> ConfirmarEsborrat(string tipus, string nom)
        => dialegs.Confirmar(
            $"Eliminar {tipus}?",
            $"S'eliminarà «{nom}» del catàleg.\n\n"
            + "Si ja s'ha fet servir, es desactivarà en lloc d'esborrar-se, per no tocar l'historial. "
            + "En tots dos casos deixarà de sortir a l'hora de registrar cites i vendes.",
            "Eliminar");

    private static string TextResultat(ResultatEsborrat resultat, string article, string nom, string on)
        => resultat == ResultatEsborrat.Desactivat
            ? $"{article} «{nom}» ja apareix en {on}, així que s'ha desactivat en lloc d'eliminar-se. "
              + "L'historial es manté i ja no es podrà triar de nou."
            : $"{article} «{nom}» s'ha eliminat.";
}
