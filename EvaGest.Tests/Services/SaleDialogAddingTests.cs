using AwesomeAssertions;
using EvaGest.Models;
using EvaGest.Services;
using EvaGest.Tests.Infra;
using EvaGest.ViewModels.Dialogs;
using EvaGest.ViewModels.Elements;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace EvaGest.Tests.Services;

/// <summary>
/// How lines get into a sale: the quick buttons, the one search box over services and
/// products, and the − / + of each line. The old dialog had a box per kind, each with
/// its own "+" button to press after picking; a service left picked but not added was
/// the most common way to be told the sale was empty.
/// </summary>
public class SaleDialogAddingTests
{
    private record Shop(TestFactory Factory, SettingsService Settings, CatalogService Catalog, int MethodId);

    private static async Task<Shop> AShop(TestDatabase testDb)
    {
        var factory = new TestFactory(testDb.Options);
        var settings = new SettingsService(factory);
        await new SeedService(factory, settings).Seed();
        var catalog = new CatalogService(factory);
        await catalog.CreateService(Make.Service("Tall", 1500, 2100));
        await catalog.CreateService(Make.Service("Barba", 800, 2100));
        await catalog.CreateProduct(Make.Product("Cera mat", 1250, 2100));

        await using var db = testDb.Context();
        return new(factory, settings, catalog, (await db.PaymentMethods.FirstAsync()).Id);
    }

    /// <param name="dialogs">Answers the "already in the sale" question; left out, it
    /// says yes, so the tests about merging add the second cut.</param>
    private static Task<SaleDialogViewModel> ANewSale(Shop shop, TestDialogService? dialogs = null)
        => SaleDialogViewModel.New(new SaleService(shop.Factory, shop.Settings), new ClientService(shop.Factory),
            shop.Catalog, new WorkerService(shop.Factory), new TestSoundService(), shop.Settings,
            dialogs ?? new TestDialogService { ResultConfirm = true });

    /// <summary>Types in the search box and presses Enter, as the user would.</summary>
    private static void SearchAndPick(SaleDialogViewModel vm, string typed)
    {
        vm.CatalogAdder.Text = typed;
        vm.CatalogAdder.PickHighlightedCommand.Execute(null);
    }

    [Fact]
    public async Task Picking_a_service_in_the_search_box_adds_it_straight_away_and_empties_the_box()
    {
        await using var testDb = new TestDatabase();
        var vm = await ANewSale(await AShop(testDb));

        SearchAndPick(vm, "tall");

        var line = vm.Lines.Should().ContainSingle().Subject;
        line.Description.Should().Be("Tall");
        line.ServiceId.Should().NotBeNull();
        line.AmountCents.Should().Be(1500);
        vm.TotalCents.Should().Be(1500);
        vm.CatalogAdder.Text.Should().BeEmpty("the box is ready for the next item");
        vm.CatalogAdder.IsDropdownOpen.Should().BeFalse();
    }

    [Fact]
    public async Task Services_and_products_are_found_in_the_same_box()
    {
        await using var testDb = new TestDatabase();
        var vm = await ANewSale(await AShop(testDb));

        SearchAndPick(vm, "cera");
        SearchAndPick(vm, "barba");

        vm.Lines.Select(l => l.Description).Should().Equal("Cera mat", "Barba");
        vm.Lines[0].ProductId.Should().NotBeNull("a product keeps its product link for the reports");
        vm.Lines[1].ServiceId.Should().NotBeNull();
    }

    [Fact]
    public async Task Each_match_says_whether_it_is_a_service_or_a_product_and_its_price()
    {
        await using var testDb = new TestDatabase();
        var vm = await ANewSale(await AShop(testDb));

        vm.CatalogAdder.Text = "a";

        vm.CatalogAdder.Matches.Should().Contain(m => m.Label == "Cera mat"
            && m.Detail!.Contains(EvaGest.Resources.Texts.Product) && m.Detail.Contains(Money.Format(1250)));
        vm.CatalogAdder.Matches.Should().Contain(m => m.Label == "Barba"
            && m.Detail!.Contains(EvaGest.Resources.Texts.Service));
    }

    [Fact]
    public async Task Text_that_is_not_in_the_catalogue_can_be_added_as_a_custom_concept()
    {
        await using var testDb = new TestDatabase();
        var vm = await ANewSale(await AShop(testDb));

        vm.CatalogAdder.Text = "Tint especial";

        var offer = vm.CatalogAdder.Matches.Should().ContainSingle().Subject;
        offer.Value.Should().BeOfType<CatalogAdderViewModel.FreeConcept>();
        vm.CatalogAdder.PickHighlightedCommand.Execute(null);

        var line = vm.Lines.Should().ContainSingle().Subject;
        line.Description.Should().Be("Tint especial");
        line.ServiceId.Should().BeNull();
        line.ProductId.Should().BeNull();
        line.IsExpanded.Should().BeTrue("its price still has to be typed, so its boxes open");
    }

    [Fact]
    public async Task The_custom_concept_offer_comes_after_the_real_matches()
    {
        // Enter picks the first match: a real catalogue item must win over the offer
        await using var testDb = new TestDatabase();
        var vm = await ANewSale(await AShop(testDb));

        vm.CatalogAdder.Text = "tall";

        vm.CatalogAdder.Matches.Should().HaveCount(2);
        vm.CatalogAdder.Matches[0].Value.Should().BeOfType<Service>();
        vm.CatalogAdder.Matches[1].Value.Should().BeOfType<CatalogAdderViewModel.FreeConcept>();
    }

    [Fact]
    public async Task Adding_the_same_item_again_raises_its_quantity_instead_of_adding_a_line()
    {
        await using var testDb = new TestDatabase();
        var vm = await ANewSale(await AShop(testDb));

        SearchAndPick(vm, "tall");
        vm.AddQuickServiceCommand.Execute(vm.QuickServices.First(q => q.Name == "Tall"));
        SearchAndPick(vm, "tall");

        var line = vm.Lines.Should().ContainSingle().Subject;
        line.Quantity.Should().Be(3);
        vm.TotalCents.Should().Be(4500);
    }

    [Fact]
    public async Task Adding_a_service_already_in_the_sale_asks_first_and_a_no_leaves_the_sale_alone()
    {
        // Two haircuts for one client is far more often a double click than a sale
        await using var testDb = new TestDatabase();
        var dialogs = new TestDialogService { ResultConfirm = false };
        var vm = await ANewSale(await AShop(testDb), dialogs);

        SearchAndPick(vm, "tall");
        dialogs.ConfirmacionsRequested.Should().BeEmpty("the first cut is not a repeat");

        vm.AddQuickServiceCommand.Execute(vm.QuickServices.First(q => q.Name == "Tall"));
        SearchAndPick(vm, "tall");

        dialogs.ConfirmacionsRequested.Should().Equal(
            EvaGest.Resources.Texts.DuplicateServiceTitle, EvaGest.Resources.Texts.DuplicateServiceTitle);
        vm.Lines.Should().ContainSingle().Which.Quantity.Should().Be(1);
        vm.TotalCents.Should().Be(1500);
    }

    [Fact]
    public async Task A_service_whose_price_was_changed_by_hand_still_counts_as_already_in_the_sale()
    {
        await using var testDb = new TestDatabase();
        var dialogs = new TestDialogService { ResultConfirm = false };
        var vm = await ANewSale(await AShop(testDb), dialogs);

        SearchAndPick(vm, "tall");
        vm.Lines[0].PriceText = "12,00";
        SearchAndPick(vm, "tall");

        dialogs.ConfirmacionsRequested.Should().ContainSingle();
        vm.Lines.Should().ContainSingle();
    }

    [Fact]
    public async Task Repeating_a_product_or_pressing_plus_on_a_line_never_asks()
    {
        // Two bottles of the same wax are normal, and + is a deliberate "one more"
        await using var testDb = new TestDatabase();
        var dialogs = new TestDialogService { ResultConfirm = false };
        var vm = await ANewSale(await AShop(testDb), dialogs);

        SearchAndPick(vm, "cera");
        SearchAndPick(vm, "cera");
        SearchAndPick(vm, "barba");
        vm.Lines.Single(l => l.Description == "Barba").IncreaseCommand.Execute(null);

        dialogs.ConfirmacionsRequested.Should().BeEmpty();
        vm.Lines.Select(l => l.Quantity).Should().Equal(2, 2);
    }

    [Fact]
    public async Task A_line_whose_price_was_changed_by_hand_is_not_merged_with_a_new_one()
    {
        // A discount typed on one cut must not be handed to the next one without asking
        await using var testDb = new TestDatabase();
        var vm = await ANewSale(await AShop(testDb));

        SearchAndPick(vm, "tall");
        vm.Lines[0].PriceText = "12,00";
        SearchAndPick(vm, "tall");

        vm.Lines.Should().HaveCount(2);
        vm.Lines.Select(l => l.AmountCents).Should().Equal(1200, 1500);
        vm.TotalCents.Should().Be(2700);
    }

    [Fact]
    public async Task Custom_concepts_are_never_merged()
    {
        await using var testDb = new TestDatabase();
        var vm = await ANewSale(await AShop(testDb));

        vm.AddCustomConceptCommand.Execute(null);
        vm.AddCustomConceptCommand.Execute(null);

        vm.Lines.Should().HaveCount(2);
    }

    [Fact]
    public async Task The_quick_buttons_offer_the_active_services_and_add_one_per_click()
    {
        await using var testDb = new TestDatabase();
        var vm = await ANewSale(await AShop(testDb));

        vm.QuickServices.Select(q => q.Name).Should().Equal("Barba", "Tall");
        vm.QuickServices[0].PriceText.Should().Be(Money.Format(800));

        vm.AddQuickServiceCommand.Execute(vm.QuickServices[0]);

        vm.Lines.Should().ContainSingle().Which.Description.Should().Be("Barba");
    }

    [Fact]
    public async Task Plus_and_minus_change_the_quantity_and_minus_stops_at_one()
    {
        await using var testDb = new TestDatabase();
        var vm = await ANewSale(await AShop(testDb));
        SearchAndPick(vm, "barba");
        var line = vm.Lines[0];

        line.DecreaseCommand.CanExecute(null).Should().BeFalse("removing a line is the ✕ button's job");
        line.IncreaseCommand.Execute(null);
        line.IncreaseCommand.Execute(null);

        line.QuantityText.Should().Be("3");
        vm.TotalCents.Should().Be(2400);

        line.DecreaseCommand.Execute(null);
        line.Quantity.Should().Be(2);
        vm.TotalCents.Should().Be(1600);
    }

    [Fact]
    public async Task A_line_refused_at_charge_time_opens_its_boxes()
    {
        // Price and VAT are behind the pencil button; the message about them must not
        // point at something the user cannot see.
        await using var testDb = new TestDatabase();
        var vm = await ANewSale(await AShop(testDb));
        SearchAndPick(vm, "tall");
        vm.TextClient = "Pere";
        vm.PaymentMethod = vm.ActiveMethods[0];
        vm.Lines[0].IsExpanded.Should().BeFalse("a catalogue line starts closed");

        vm.Lines[0].PriceText = "quinze";
        await vm.ChargeCommand.ExecuteAsync(null);

        vm.ErrorValidation.Should().NotBeNull();
        vm.Lines[0].IsExpanded.Should().BeTrue();
    }

    [Fact]
    public async Task The_empty_state_shows_until_the_first_line_and_again_after_the_last_is_removed()
    {
        await using var testDb = new TestDatabase();
        var vm = await ANewSale(await AShop(testDb));
        vm.HasLines.Should().BeFalse();

        SearchAndPick(vm, "tall");
        vm.HasLines.Should().BeTrue();

        vm.RemoveLineCommand.Execute(vm.Lines[0]);
        vm.HasLines.Should().BeFalse();
    }
}
