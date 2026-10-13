using AwesomeAssertions;
using EvaGest.Helpers;
using EvaGest.Models;
using EvaGest.Resources;
using EvaGest.Services;
using EvaGest.Tests.Infra;
using EvaGest.ViewModels.Dialogs;
using EvaGest.ViewModels.Elements;
using EvaGest.ViewModels.Pages;
using Xunit;

namespace EvaGest.Tests.Services;

/// <summary>
/// What a page says when a list has nothing to show (disseny-ui 9: empty states invite an
/// action instead of a blank table). Found on a fresh install in manual test block A:
/// several pages showed bare table headers, or blamed filters nobody had set.
/// </summary>
public class EmptyStateTests
{
    private static async Task<ClientsViewModel> ClientsPage(TestDatabase db)
    {
        var factory = new TestFactory(db.Options);
        var vm = new ClientsViewModel(new ClientService(factory), new ReportsService(factory),
            new TestDialogService(), await TestOwner.Unlocked());
        await vm.Load();
        return vm;
    }

    [Fact]
    public async Task A_shop_with_no_clients_is_invited_to_add_the_first_one()
    {
        await using var db = new TestDatabase();

        var vm = await ClientsPage(db);

        vm.EmptyText.Should().Be(Texts.NoClientsYet);
    }

    [Fact]
    public async Task With_every_client_asleep_the_page_points_to_the_asleep_list()
    {
        await using var db = new TestDatabase();
        await using (var ctx = db.Context())
        {
            var client = Make.Client();
            client.Asleep = true;
            ctx.Clients.Add(client);
            await ctx.SaveChangesAsync();
        }

        var vm = await ClientsPage(db);

        vm.EmptyText.Should().Be(Texts.NoActiveClients);
    }

    [Fact]
    public async Task A_search_that_finds_nobody_says_so_and_names_what_was_searched()
    {
        await using var db = new TestDatabase();
        await using (var ctx = db.Context())
        {
            ctx.Clients.Add(Make.Client("Joan García"));
            await ctx.SaveChangesAsync();
        }
        var vm = await ClientsPage(db);
        vm.EmptyText.Should().BeNull("the table has a row to show");

        vm.SearchText = "Pere";
        await vm.Load();

        vm.EmptyText.Should().Be(string.Format(Texts.NoClientMatchesSearch, "Pere"));
    }

    [Fact]
    public async Task The_list_of_an_empty_catalogue_says_where_to_create_services_not_to_search_again()
    {
        var dialogs = new RecordingDialogService();
        var adder = new CatalogAdderViewModel(Array.Empty<Service>(), Array.Empty<Product>(), _ => { }, dialogs);

        await adder.BrowseCommand.ExecuteAsync(null);

        var browser = dialogs.DialogsShown.OfType<OptionBrowserDialogViewModel>().Single();
        browser.IsEmpty.Should().BeTrue();
        browser.EmptyText.Should().Be(Texts.CatalogBrowserNothingYet,
            "nothing was typed, so \"nothing matches the search\" would send the user hunting for a typo");
    }

    [Fact]
    public void A_search_that_matches_no_option_still_says_so()
    {
        var browser = new OptionBrowserDialogViewModel("Triar treballadora",
            [new PickerOption(new object(), "Laia")], initialSearch: "Pere");

        browser.IsEmpty.Should().BeTrue();
        browser.EmptyText.Should().Be(Texts.OptionBrowserEmpty);
    }

    [Fact]
    public void A_list_with_no_options_at_all_says_it_is_still_empty()
    {
        var browser = new OptionBrowserDialogViewModel("Triar treballadora", []);

        browser.EmptyText.Should().Be(Texts.OptionBrowserNothingYet);
    }
}
