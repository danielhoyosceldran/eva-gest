using AwesomeAssertions;
using EvaGest.Models;
using EvaGest.ViewModels.Dialogs;
using Xunit;

namespace EvaGest.Tests.Services;

/// <summary>
/// The browse-every-client dialog opened from the client box. Newer than
/// pla-proves.md, so no block code.
/// </summary>
public class ClientBrowserDialogViewModelTests
{
    private static List<Client> Many(int count)
        => Enumerable.Range(1, count).Select(i => new Client { Id = i, Name = $"Client {i:00}" }).ToList();

    [Fact]
    public void With_no_search_it_lists_every_client_not_just_the_dropdown_few()
    {
        var vm = new ClientBrowserDialogViewModel(Many(30));

        vm.Results.Should().HaveCount(30);
        vm.IsEmpty.Should().BeFalse();
    }

    [Fact]
    public void The_search_bar_narrows_the_list_as_you_type()
    {
        var vm = new ClientBrowserDialogViewModel(
        [
            new Client { Id = 1, Name = "Joan García" },
            new Client { Id = 2, Name = "Joan Martí" },
            new Client { Id = 3, Name = "Marta Puig" },
        ]);

        vm.SearchText = "joan";
        vm.Results.Select(c => c.Id).Should().Equal(1, 2);

        vm.SearchText = "mart";
        vm.Results.Select(c => c.Id).Should().Equal(3, 2);
    }

    [Fact]
    public void A_single_match_comes_selected_so_Enter_picks_it()
    {
        var vm = new ClientBrowserDialogViewModel(Many(30), "client 07");

        vm.SelectedClient!.Id.Should().Be(7);
        vm.ChooseCommand.CanExecute(null).Should().BeTrue();
    }

    [Fact]
    public void Nothing_can_be_chosen_until_a_row_is()
    {
        var vm = new ClientBrowserDialogViewModel(Many(3));

        vm.ChooseCommand.CanExecute(null).Should().BeFalse();
        vm.SelectedClient = vm.Results[1];
        vm.ChooseCommand.CanExecute(null).Should().BeTrue();
    }

    [Fact]
    public void Choosing_closes_confirmed_and_cancelling_closes_unconfirmed()
    {
        var vm = new ClientBrowserDialogViewModel(Many(3));
        bool? closed = null;
        vm.Close += ok => closed = ok;

        vm.SelectedClient = vm.Results[0];
        vm.ChooseCommand.Execute(null);
        closed.Should().BeTrue();

        vm.CancelCommand.Execute(null);
        closed.Should().BeFalse();
    }

    [Fact]
    public void No_match_shows_the_empty_state()
    {
        var vm = new ClientBrowserDialogViewModel(Many(3));

        vm.SearchText = "zzz";

        vm.IsEmpty.Should().BeTrue();
        vm.SelectedClient.Should().BeNull();
    }
}
