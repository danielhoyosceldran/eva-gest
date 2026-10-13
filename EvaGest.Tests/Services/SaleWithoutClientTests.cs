using AwesomeAssertions;
using EvaGest.Models;
using EvaGest.Resources;
using EvaGest.Services;
using EvaGest.Tests.Infra;
using EvaGest.ViewModels.Dialogs;
using EvaGest.ViewModels.Elements;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace EvaGest.Tests.Services;

/// <summary>
/// The "no client" option of the sale dialog: a sale charged to nobody, on purpose.
/// It is saved with the fixed <see cref="Sale.NoClientName"/> marker as its guest name
/// (ck_sales_client_xor wants one), shown translated, and never reached by accident:
/// an empty client box is still refused. Newer than pla-proves.md, so no block code.
/// </summary>
public class SaleWithoutClientTests
{
    private record Shop(TestFactory Factory, SettingsService Settings);

    private static async Task<Shop> AShop(TestDatabase testDb)
    {
        var factory = new TestFactory(testDb.Options);
        var settings = new SettingsService(factory);
        await new SeedService(factory, settings).Seed();
        return new(factory, settings);
    }

    private static async Task<SaleDialogViewModel> ANewSale(Shop shop)
    {
        var vm = await SaleDialogViewModel.New(
            new SaleService(shop.Factory, shop.Settings), new ClientService(shop.Factory),
            new CatalogService(shop.Factory), new WorkerService(shop.Factory), new TestSoundService(),
            shop.Settings, new TestDialogService());
        vm.PaymentMethod = vm.ActiveMethods[0];
        vm.AddCustomConceptCommand.Execute(null);
        var line = vm.Lines[^1];
        line.Description = "Concepte";
        line.QuantityText = "1";
        line.VatText = "21";
        line.PriceText = "15,00";
        return vm;
    }

    private static Task<SaleDialogViewModel> EditSale(Shop shop, Sale sale)
        => SaleDialogViewModel.Edit(
            new SaleService(shop.Factory, shop.Settings), new ClientService(shop.Factory),
            new CatalogService(shop.Factory), new WorkerService(shop.Factory), new TestSoundService(),
            shop.Settings, new TestDialogService(), sale);

    private static async Task<Sale> TheOnlySale(TestDatabase testDb)
    {
        await using var db = testDb.Context();
        return await db.Sales.Include(s => s.Lines).SingleAsync();
    }

    [Fact]
    public async Task Choosing_no_client_saves_the_sale_with_the_fixed_marker()
    {
        await using var testDb = new TestDatabase();
        var vm = await ANewSale(await AShop(testDb));

        vm.ClientPicker.IsNoClient = true;
        await vm.ChargeCommand.ExecuteAsync(null);

        vm.ErrorValidation.Should().BeNull();
        var sale = await TheOnlySale(testDb);
        sale.ClientId.Should().BeNull();
        sale.GuestName.Should().Be(Sale.NoClientName);
        sale.GuestPhone.Should().BeNull();
        sale.IsNoClient.Should().BeTrue();
    }

    [Fact]
    public async Task An_empty_client_box_is_still_refused_no_client_has_to_be_chosen()
    {
        await using var testDb = new TestDatabase();
        var vm = await ANewSale(await AShop(testDb));

        vm.ClientPicker.IsNewClient = true;
        await vm.ChargeCommand.ExecuteAsync(null);

        vm.ErrorValidation.Should().Be(Texts.GuestNameOrClientRequired);
        await using var db = testDb.Context();
        (await db.Sales.CountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task Typing_the_marker_as_a_guest_name_is_refused()
    {
        await using var testDb = new TestDatabase();
        var vm = await ANewSale(await AShop(testDb));

        vm.TextClient = " NO_REGISTERED_CLIENT ";
        await vm.ChargeCommand.ExecuteAsync(null);

        vm.ErrorValidation.Should().Be(Texts.GuestNameReserved);
        await using var db = testDb.Context();
        (await db.Sales.CountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task A_sale_with_no_client_reopens_on_the_no_client_option_not_as_a_typed_name()
    {
        await using var testDb = new TestDatabase();
        var shop = await AShop(testDb);
        var vm = await ANewSale(shop);
        vm.ClientPicker.IsNoClient = true;
        await vm.ChargeCommand.ExecuteAsync(null);

        var edit = await EditSale(shop, await TheOnlySale(testDb));

        edit.ClientPicker.IsNoClient.Should().BeTrue();
        edit.ClientPicker.NewName.Should().BeEmpty("the marker must never appear in the name box");
        edit.TextClient.Should().BeEmpty();
    }

    [Fact]
    public void The_marker_is_shown_translated_never_as_stored()
    {
        var sale = new Sale { GuestName = Sale.NoClientName };

        sale.DisplayName.Should().Be(Texts.NoClient);
        sale.DisplayName.Should().NotBe(Sale.NoClientName);
    }

    [Fact]
    public void Only_a_sale_picker_offers_no_client()
    {
        var appointmentPicker = new ClientPickerViewModel();

        appointmentPicker.IsNoClient = true;

        appointmentPicker.AllowsNoClient.Should().BeFalse();
        appointmentPicker.IsNoClient.Should().BeFalse("an appointment is always booked for somebody");
    }

    [Fact]
    public void Leaving_no_client_and_back_to_existing_keeps_the_client_picked_before()
    {
        var picker = new ClientPickerViewModel(allowsNoClient: true);
        var marta = new Client { Id = 3, Name = "Marta Puig" };
        picker.SetClients([marta]);
        picker.Select(marta);

        picker.IsNoClient = true;
        picker.SelectedClient.Should().BeNull("no client is nobody registered");
        picker.GuestName.Should().BeEmpty();

        picker.IsExistingClient = true;
        picker.SelectedClient!.Id.Should().Be(3, "a misclick on the toggle must not lose who was picked");
    }
}
