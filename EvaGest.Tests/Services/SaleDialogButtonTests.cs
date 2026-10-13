using AwesomeAssertions;
using EvaGest.Resources;
using EvaGest.Services;
using EvaGest.Tests.Infra;
using EvaGest.ViewModels.Dialogs;
using Xunit;

namespace EvaGest.Tests.Services;

/// <summary>
/// B-36: editing a sale already charged still offered "Cobrar", as if charging again,
/// when all the button does then is save the corrected sale.
/// </summary>
public class SaleDialogButtonTests
{
    [Fact]
    public async Task A_new_sale_is_charged_and_an_edited_one_is_saved()
    {
        await using var testDb = new TestDatabase();
        var factory = new TestFactory(testDb.Options);
        var settings = new SettingsService(factory);
        await new SeedService(factory, settings).Seed();
        var sales = new SaleService(factory, settings);
        var clients = new ClientService(factory);
        var catalog = new CatalogService(factory);
        var workers = new WorkerService(factory);

        var fresh = await SaleDialogViewModel.New(sales, clients, catalog, workers,
            new TestSoundService(), settings, new TestDialogService());
        fresh.ChargeText.Should().Be(Texts.Charge);

        fresh.TextClient = "Client de prova";
        fresh.PaymentMethod = fresh.ActiveMethods[0];
        fresh.AddCustomConceptCommand.Execute(null);
        var line = fresh.Lines[^1];
        line.Description = "Tall";
        line.QuantityText = "1";
        line.VatText = "21";
        line.PriceText = "15,00";
        await fresh.ChargeCommand.ExecuteAsync(null);
        var stored = (await sales.Search(new SalesFilter())).Single();

        var edit = await SaleDialogViewModel.Edit(sales, clients, catalog, workers,
            new TestSoundService(), settings, new TestDialogService(), (await sales.GetById(stored.Id))!);

        edit.ChargeText.Should().Be(Texts.SaveSale);
    }
}
