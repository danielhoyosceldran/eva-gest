using AwesomeAssertions;
using EvaGest.Models;
using EvaGest.Tests.Infra;
using EvaGest.ViewModels.Dialogs;
using EvaGest.ViewModels.Elements;
using Xunit;

namespace EvaGest.Tests.Services;

/// <summary>
/// The client box shared by the appointment and sale dialogs: what typing, the arrow
/// keys and picking do to the selection. Newer than pla-proves.md, so no block code.
/// </summary>
public class ClientPickerViewModelTests
{
    private static ClientPickerViewModel Picker()
    {
        var picker = new ClientPickerViewModel();
        picker.SetClients(
        [
            new Client { Id = 1, Name = "Joan García", Mobile = "612345678" },
            new Client { Id = 2, Name = "Joan Martí", Mobile = "699112233" },
            new Client { Id = 3, Name = "Marta Puig", Mobile = "611000002" },
        ]);
        return picker;
    }

    [Fact]
    public void Typing_opens_the_list_with_the_matches_and_highlights_the_first()
    {
        var picker = Picker();

        picker.Text = "joan";

        picker.IsDropdownOpen.Should().BeTrue();
        picker.Matches.Select(c => c.Id).Should().Equal(1, 2);
        picker.Highlighted!.Id.Should().Be(1);
        picker.SelectedClient.Should().BeNull("typing alone never picks anyone");
    }

    [Fact]
    public void Text_nobody_matches_in_the_search_is_not_a_guest_name()
    {
        // In "existing client" only a pick counts; a guest's name goes in "new client"
        var picker = Picker();

        picker.Text = "Algú de pas";

        picker.IsDropdownOpen.Should().BeFalse();
        picker.GuestName.Should().BeEmpty();
        picker.IsRegistered.Should().BeFalse();
    }

    [Fact]
    public void Arrows_move_the_highlight_and_Enter_picks_it()
    {
        var picker = Picker();
        picker.Text = "joan";

        picker.MoveDownCommand.Execute(null);
        picker.PickHighlightedCommand.Execute(null);

        picker.SelectedClient!.Id.Should().Be(2);
        picker.Text.Should().Be("Joan Martí");
        picker.GuestName.Should().BeEmpty();
        picker.IsDropdownOpen.Should().BeFalse();
    }

    [Fact]
    public void The_highlight_stops_at_both_ends_of_the_list()
    {
        var picker = Picker();
        picker.Text = "joan";

        picker.MoveUpCommand.Execute(null);
        picker.Highlighted!.Id.Should().Be(1);

        for (int i = 0; i < 5; i++) picker.MoveDownCommand.Execute(null);
        picker.Highlighted!.Id.Should().Be(2);
    }

    [Fact]
    public void Enter_and_Escape_are_left_to_the_dialog_while_the_list_is_closed()
    {
        // KeyBindings only mark the key handled when the command can run; with the list
        // closed, Enter must still reach Guardar and Escape Cancel·lar.
        var picker = Picker();

        picker.PickHighlightedCommand.CanExecute(null).Should().BeFalse();
        picker.CloseDropdownCommand.CanExecute(null).Should().BeFalse();

        picker.Text = "joan";
        picker.PickHighlightedCommand.CanExecute(null).Should().BeTrue();
        picker.CloseDropdownCommand.CanExecute(null).Should().BeTrue();

        picker.CloseDropdownCommand.Execute(null);
        picker.PickHighlightedCommand.CanExecute(null).Should().BeFalse();
        picker.Text.Should().Be("joan", "closing the list keeps what was typed");
    }

    [Fact]
    public void Down_on_an_empty_box_opens_the_whole_list_to_browse()
    {
        var picker = Picker();

        picker.MoveDownCommand.Execute(null);

        picker.IsDropdownOpen.Should().BeTrue();
        picker.Matches.Should().HaveCount(3);
    }

    [Fact]
    public void Editing_the_text_after_a_pick_drops_the_pick()
    {
        var picker = Picker();
        picker.Text = "marta";
        picker.PickHighlightedCommand.Execute(null);
        picker.IsRegistered.Should().BeTrue();

        picker.Text = "Marta Pui";

        picker.SelectedClient.Should().BeNull("the box no longer says who was picked");
        picker.IsNewClient.Should().BeFalse("it is still a search, not a guest");
    }

    [Fact]
    public void A_click_on_a_match_picks_that_one()
    {
        var picker = Picker();
        picker.Text = "joan";

        picker.PickCommand.Execute(picker.Matches[1]);

        picker.SelectedClient!.Id.Should().Be(2);
    }

    [Fact]
    public void Picking_from_code_does_not_open_the_list()
    {
        var picker = Picker();

        picker.Select(new Client { Id = 9, Name = "Joan Nou" });
        picker.IsDropdownOpen.Should().BeFalse();

        picker.SetGuestName("Joan");
        picker.IsDropdownOpen.Should().BeFalse("a saved guest name reloading is not the user typing");
        picker.SelectedClient.Should().BeNull();
        picker.IsNewClient.Should().BeTrue();
        picker.GuestName.Should().Be("Joan");
    }

    [Fact]
    public void Selecting_null_drops_the_pick_keeping_the_text()
    {
        var picker = Picker();
        picker.Text = "joan";
        picker.PickHighlightedCommand.Execute(null);

        picker.Select(null);

        picker.SelectedClient.Should().BeNull();
        picker.Text.Should().Be("Joan García");
    }

    [Fact]
    public void An_added_client_becomes_searchable_once()
    {
        var picker = Picker();
        var created = new Client { Id = 4, Name = "Pere Nou" };

        picker.Add(created);
        picker.Add(created);
        picker.Text = "pere";

        picker.Matches.Should().ContainSingle().Which.Id.Should().Be(4);
    }

    [Fact]
    public void A_new_box_starts_on_existing_client_with_nothing_picked()
    {
        var picker = Picker();

        picker.IsExistingClient.Should().BeTrue();
        picker.IsNewClient.Should().BeFalse();
        picker.SelectedClient.Should().BeNull();
        picker.GuestName.Should().BeEmpty();
    }

    [Fact]
    public void New_client_mode_saves_its_own_name_as_the_guest_and_picks_nobody()
    {
        var picker = Picker();
        picker.Text = "joan";
        picker.PickHighlightedCommand.Execute(null);

        picker.IsNewClient = true;
        picker.NewName = "Algú de pas";

        picker.SelectedClient.Should().BeNull("a guest is nobody registered");
        picker.GuestName.Should().Be("Algú de pas");
    }

    [Fact]
    public void Flipping_the_toggle_back_and_forth_loses_neither_the_pick_nor_the_new_name()
    {
        var picker = Picker();
        picker.Text = "marta";
        picker.PickHighlightedCommand.Execute(null);
        picker.IsNewClient = true;
        picker.NewName = "Pere";

        picker.IsExistingClient = true;

        picker.SelectedClient!.Id.Should().Be(3, "a misclick on the toggle must not lose who was picked");
        picker.GuestName.Should().BeEmpty();

        picker.IsNewClient = true;
        picker.GuestName.Should().Be("Pere");
    }

    [Fact]
    public void Picking_a_client_switches_back_to_existing_client()
    {
        var picker = Picker();
        picker.SetGuestName("Algú");

        picker.Select(new Client { Id = 1, Name = "Joan García" });

        picker.IsExistingClient.Should().BeTrue();
        picker.GuestName.Should().BeEmpty();
    }

    [Fact]
    public async Task Browse_opens_the_list_filtered_by_the_search_and_picks_what_was_chosen()
    {
        var dialogs = new TestDialogService
        {
            ResultDialog = true,
            FillDialog = d =>
            {
                var browser = (ClientBrowserDialogViewModel)d;
                browser.SearchText.Should().Be("joan", "the list opens where the user was looking");
                browser.Results.Should().HaveCount(2);
                browser.SelectedClient = browser.Results[1];
                return Task.CompletedTask;
            }
        };
        var picker = new ClientPickerViewModel(dialogs);
        picker.SetClients([
            new Client { Id = 1, Name = "Joan García" },
            new Client { Id = 2, Name = "Joan Martí" },
            new Client { Id = 3, Name = "Marta Puig" }]);
        picker.Text = "joan";

        await picker.BrowseCommand.ExecuteAsync(null);

        picker.SelectedClient!.Id.Should().Be(2);
        picker.Text.Should().Be("Joan Martí");
        picker.IsDropdownOpen.Should().BeFalse();
    }

    [Fact]
    public async Task Cancelling_the_list_changes_nothing()
    {
        var dialogs = new TestDialogService
        {
            ResultDialog = false,
            FillDialog = d =>
            {
                ((ClientBrowserDialogViewModel)d).SelectedClient = ((ClientBrowserDialogViewModel)d).Results[0];
                return Task.CompletedTask;
            }
        };
        var picker = new ClientPickerViewModel(dialogs);
        picker.SetClients([new Client { Id = 1, Name = "Joan García" }]);

        await picker.BrowseCommand.ExecuteAsync(null);

        picker.SelectedClient.Should().BeNull();
    }
}
