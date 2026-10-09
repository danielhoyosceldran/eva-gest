using AwesomeAssertions;
using EvaGest.Models;
using EvaGest.Services;
using EvaGest.Tests.Infra;
using EvaGest.ViewModels.Dialogs;
using Xunit;

namespace EvaGest.Tests.Services;

/// <summary>
/// E-07. After a sale was stored the dialog still played the chime before closing, and
/// the chime read its on/off setting outside its own try. A failure there left the dialog
/// open on a sale already charged, and Cobrar again charged it a second time.
/// </summary>
public class DoubleChargeTests
{
    private sealed class BrokenSound : ISoundService
    {
        public Task PlayConfirmation() => Task.FromException(new InvalidOperationException("no sound"));
    }

    [Fact]
    public async Task The_chime_never_throws_even_when_its_setting_cannot_be_read()
    {
        var sound = new SoundService(FailingService.Create<ISettingsService>());

        await sound.Invoking(s => s.PlayConfirmation()).Should().NotThrowAsync();
    }

    [Fact]
    public async Task The_sale_dialog_closes_once_the_sale_is_stored_whatever_happens_after()
    {
        await using var testDb = new TestDatabase();
        var factory = new TestFactory(testDb.Options);
        var settings = new SettingsService(factory);
        await new SeedService(factory, settings).Seed();
        var sales = new SaleService(factory, settings);

        var vm = await SaleDialogViewModel.New(sales, new ClientService(factory), new CatalogService(factory),
            new WorkerService(factory), new BrokenSound(), settings, new TestDialogService());
        vm.TextClient = "Client de prova";
        vm.PaymentMethod = vm.ActiveMethods[0];
        vm.AddCustomConceptCommand.Execute(null);
        var line = vm.Lines[^1];
        line.Description = "Tall";
        line.QuantityText = "1";
        line.VatText = "21";
        line.PriceText = "15,00";

        bool closed = false;
        vm.Close += _ => closed = true;

        try { await vm.ChargeCommand.ExecuteAsync(null); }
        catch (InvalidOperationException) { /* the broken chime, after the sale */ }

        closed.Should().BeTrue("a dialog left open on a stored sale invites charging it again");
        (await sales.Search(new SalesFilter())).Should().ContainSingle();
    }
}
