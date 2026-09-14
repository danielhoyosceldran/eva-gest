using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using EvaGest.Models;
using EvaGest.Services;
using EvaGest.ViewModels.Dialogs;
using EvaGest.Resources;

namespace EvaGest.ViewModels.Pages;

/// <summary>
/// The first CRUD page (Phase 3, capa-mvvm 4.7): deliberately the most boring module,
/// so it fixes the View -> ViewModel -> Service pattern that Clients/Agenda/Sales repeat.
/// </summary>
public partial class CatalogViewModel(
    ICatalogService catalog, ISettingsService settings, IDialogService dialogs)
    : PageViewModelBase
{
    public override string Title => Texts.NavCatalog;

    // Prices are stored in cents and VAT in basis points. Bound raw to a {0:0.00}
    // format string, a 15,00 € service showed up as "1500,00 €" and 21 % as "2100",
    // so both are formatted here instead.
    public record ServiceRow(Service Service, string PriceText, string VatText, string DurationText);
    public record ProductRow(Product Product, string PriceText, string VatText);

    public ObservableCollection<ServiceRow> Services { get; } = [];
    public ObservableCollection<ProductRow> Products { get; } = [];
    public ObservableCollection<PaymentMethod> Methods { get; } = [];

    public async Task Load()
    {
        Loading = true;
        try
        {
            Services.Clear();
            foreach (var s in await catalog.GetServices())
                Services.Add(new ServiceRow(s, Money.Format(s.PriceCents), Percentages.Format(s.VatBp),
                    s.DurationMin is int d ? string.Format(Texts.MinutesShort, d) : "—"));

            Products.Clear();
            foreach (var p in await catalog.GetProducts())
                Products.Add(new ProductRow(p, Money.Format(p.PriceCents), Percentages.Format(p.VatBp)));

            Methods.Clear();
            foreach (var m in await catalog.GetMethods()) Methods.Add(m);
        }
        finally { Loading = false; }
    }

    /// <summary>The rate proposed for a new catalogue entry (RF-23).</summary>
    private async Task<int> DefaultVat()
        => await settings.GetInt(ConfigKeys.DefaultVatBp, 2100);

    // --- Services ---

    [RelayCommand]
    private async Task NewService()
    {
        var vm = new ServiceDialogViewModel(await DefaultVat());
        if (await dialogs.ShowDialog(vm))
        {
            await catalog.CreateService(vm.AModel());
            await Load();
        }
    }

    [RelayCommand]
    private async Task EditService(Service service)
    {
        var vm = new ServiceDialogViewModel(service);
        if (await dialogs.ShowDialog(vm))
        {
            var updated = vm.AModel();
            updated.Active = service.Active;
            await catalog.UpdateService(updated);
            await Load();
        }
    }

    [RelayCommand]
    private async Task ChangeServiceStatus(Service service)
    {
        await catalog.ChangeServiceStatus(service.Id, !service.Active);
        await Load();
    }

    [RelayCommand]
    private async Task DeleteService(Service service)
    {
        if (!await ConfirmDeletion(Texts.TypeService, service.Name)) return;

        var result = await catalog.DeleteService(service.Id);
        await Load();
        ShowNotice(ResultText(result, Texts.ArticleService, service.Name,
                              Texts.UsedInAppointmentsOrSales));
    }

    // --- Products ---

    [RelayCommand]
    private async Task NewProduct()
    {
        var vm = new ProductDialogViewModel(await DefaultVat());
        if (await dialogs.ShowDialog(vm))
        {
            await catalog.CreateProduct(vm.AModel());
            await Load();
        }
    }

    [RelayCommand]
    private async Task EditProduct(Product product)
    {
        var vm = new ProductDialogViewModel(product);
        if (await dialogs.ShowDialog(vm))
        {
            var updated = vm.AModel();
            updated.Active = product.Active;
            await catalog.UpdateProduct(updated);
            await Load();
        }
    }

    [RelayCommand]
    private async Task ChangeProductStatus(Product product)
    {
        await catalog.ChangeProductStatus(product.Id, !product.Active);
        await Load();
    }

    [RelayCommand]
    private async Task DeleteProduct(Product product)
    {
        if (!await ConfirmDeletion(Texts.TypeProduct, product.Name)) return;

        var result = await catalog.DeleteProduct(product.Id);
        await Load();
        ShowNotice(ResultText(result, Texts.ArticleProduct, product.Name, Texts.UsedInSales));
    }

    // --- Payment methods ---

    [RelayCommand]
    private async Task NewMethod()
    {
        var vm = new PaymentMethodDialogViewModel();
        if (await dialogs.ShowDialog(vm))
        {
            await catalog.CreateMethod(vm.AModel().Name);
            await Load();
        }
    }

    [RelayCommand]
    private async Task EditMethod(PaymentMethod method)
    {
        var vm = new PaymentMethodDialogViewModel(method);
        if (await dialogs.ShowDialog(vm))
        {
            var updated = vm.AModel();
            updated.Active = method.Active;
            await catalog.UpdateMethod(updated);
            await Load();
        }
    }

    [RelayCommand]
    private async Task ChangeMethodStatus(PaymentMethod method)
    {
        // Deactivating the last active method would make it impossible to take
        // money (pantalles 2.5), so the service is asked first and the UI explains why.
        if (method.Active && !await catalog.CanDeactivateMethod(method.Id))
        {
            await dialogs.Inform(Texts.CannotDeactivateTitle, Texts.KeepOneActiveMethod);
            return;
        }

        await catalog.ChangeMethodStatus(method.Id, !method.Active);
        await Load();
    }

    [RelayCommand]
    private async Task DeleteMethod(PaymentMethod method)
    {
        if (!await ConfirmDeletion(Texts.TypePaymentMethod, method.Name)) return;

        var result = await catalog.DeleteMethod(method.Id);
        await Load();

        ShowNotice(result == DeleteResult.Blocked
            ? string.Format(Texts.MethodNotDeletedLastActive, method.Name)
            : ResultText(result, Texts.ArticleMethod, method.Name,
                         Texts.UsedInSalesOrMovements));
    }

    // --- Esborrat ---

    /// <summary>
    /// One confirmation for the three lists. It says up front that a used entry will be
    /// deactivated instead of deleted, so the note afterwards confirms what was announced
    /// rather than surprising the user with it.
    /// </summary>
    private Task<bool> ConfirmDeletion(string type, string name)
        => dialogs.Confirm(
            string.Format(Texts.DeleteCatalogItemTitle, type),
            string.Format(Texts.DeleteCatalogItemMessage, name),
            Texts.Delete);

    private static string ResultText(DeleteResult result, string article, string name, string on)
        => result == DeleteResult.Deactivated
            ? string.Format(Texts.CatalogItemDeactivated, article, name, on)
            : string.Format(Texts.CatalogItemDeleted, article, name);
}
