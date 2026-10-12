using AwesomeAssertions;
using EvaGest.Models;
using EvaGest.Tests.Infra;
using EvaGest.ViewModels.Dialogs;
using EvaGest.ViewModels.Elements;
using Xunit;

namespace EvaGest.Tests.Services;

/// <summary>
/// The search box that replaced the combo boxes whose lists grow long (workers,
/// services, products, categories, clients in a filter): what typing, picking, leaving
/// the box and the owner's own property do to each other. Newer than pla-proves.md, so
/// no block code.
/// </summary>
public class SearchPickerViewModelTests
{
    /// <summary>Stands in for a dialog or page: a typed property the picker writes, which
    /// mirrors itself back into the picker the way the real owners' OnXChanged do.</summary>
    private sealed class Owner
    {
        public List<Worker> Workers { get; } =
        [
            new Worker { Id = 1, Name = "Marta Puig" },
            new Worker { Id = 2, Name = "Martina Soler" },
            new Worker { Id = 3, Name = "Laia Vidal" },
        ];

        public SearchPickerViewModel<Worker> Picker { get; }
        public int Changes { get; private set; }

        private Worker? _worker;
        public Worker? Worker
        {
            get => _worker;
            set
            {
                if (ReferenceEquals(_worker, value)) return;
                _worker = value;
                Changes++;
                Picker.Show(value);
            }
        }

        public Owner(TestDialogService? dialogs = null)
            => Picker = new(Workers, w => w.Name, w => Worker = w, dialogs, "Triar treballadora");
    }

    [Fact]
    public void Typing_opens_the_list_with_the_matches_and_picks_nothing()
    {
        var owner = new Owner();

        owner.Picker.Text = "mart";

        owner.Picker.IsDropdownOpen.Should().BeTrue();
        owner.Picker.Matches.Select(o => o.Label).Should().Equal("Marta Puig", "Martina Soler");
        owner.Picker.Highlighted!.Label.Should().Be("Marta Puig");
        owner.Worker.Should().BeNull("typing alone never picks anyone");
    }

    [Fact]
    public void Arrows_move_the_highlight_and_Enter_picks_it_for_the_owner()
    {
        var owner = new Owner();
        owner.Picker.Text = "mart";

        owner.Picker.MoveDownCommand.Execute(null);
        owner.Picker.PickHighlightedCommand.Execute(null);

        owner.Worker!.Id.Should().Be(2);
        owner.Picker.Text.Should().Be("Martina Soler");
        owner.Picker.IsDropdownOpen.Should().BeFalse();
        owner.Changes.Should().Be(1, "one pick is one change, not an echo back and forth");
    }

    [Fact]
    public void Setting_the_owner_property_shows_it_in_the_box()
    {
        var owner = new Owner();

        owner.Worker = owner.Workers[2];

        owner.Picker.Text.Should().Be("Laia Vidal");
        owner.Picker.Selected.Should().BeSameAs(owner.Workers[2]);
        owner.Picker.IsDropdownOpen.Should().BeFalse("text written by code does not open the list");
    }

    [Fact]
    public void Editing_the_text_of_a_pick_drops_it_but_keeps_what_is_being_typed()
    {
        var owner = new Owner();
        owner.Worker = owner.Workers[0];

        owner.Picker.Text = "Marta Pu";

        owner.Worker.Should().BeNull();
        owner.Picker.Text.Should().Be("Marta Pu", "the owner hearing about it must not wipe the box");
    }

    [Fact]
    public void Leaving_the_box_picks_the_one_option_the_text_still_matches()
    {
        var owner = new Owner();
        owner.Picker.Text = "laia";

        owner.Picker.Commit();

        owner.Worker!.Id.Should().Be(3);
        owner.Picker.Text.Should().Be("Laia Vidal");
    }

    [Fact]
    public void Leaving_the_box_with_text_that_names_nothing_empties_it()
    {
        // Otherwise the box would show a name that is not what gets saved
        var owner = new Owner();
        owner.Picker.Text = "mart";

        owner.Picker.Commit();

        owner.Worker.Should().BeNull();
        owner.Picker.Text.Should().BeEmpty();
    }

    [Fact]
    public void Leaving_the_box_keeps_a_pick()
    {
        var owner = new Owner();
        owner.Worker = owner.Workers[1];

        owner.Picker.Commit();

        owner.Worker.Should().BeSameAs(owner.Workers[1]);
        owner.Picker.Text.Should().Be("Martina Soler");
    }

    [Fact]
    public void Clear_empties_half_typed_text_too()
    {
        var owner = new Owner();
        owner.Picker.Text = "mar";

        owner.Picker.Clear();

        owner.Picker.Text.Should().BeEmpty();
        owner.Worker.Should().BeNull();
    }

    [Fact]
    public void An_option_the_owner_adds_later_is_searchable_at_once()
    {
        // The pickers read the owner's list live, as a combo bound to it did
        var owner = new Owner();
        owner.Workers.Add(new Worker { Id = 4, Name = "Núria Roca" });

        owner.Picker.Text = "nuria";

        owner.Picker.Matches.Should().ContainSingle().Which.Label.Should().Be("Núria Roca");
    }

    [Fact]
    public async Task Browse_opens_the_whole_list_filtered_by_the_search_and_picks_what_was_chosen()
    {
        var dialogs = new TestDialogService
        {
            ResultDialog = true,
            FillDialog = d =>
            {
                var browser = (OptionBrowserDialogViewModel)d;
                browser.Title.Should().Be("Triar treballadora");
                browser.SearchText.Should().Be("mart", "the list opens where the user was looking");
                browser.Results.Should().HaveCount(2);
                browser.SelectedOption = browser.Results[1];
                return Task.CompletedTask;
            }
        };
        var owner = new Owner(dialogs);
        owner.Picker.Text = "mart";

        await owner.Picker.BrowseCommand.ExecuteAsync(null);

        owner.Worker!.Id.Should().Be(2);
        owner.Picker.Text.Should().Be("Martina Soler");
    }

    [Fact]
    public async Task Cancelling_the_list_changes_nothing()
    {
        var dialogs = new TestDialogService
        {
            ResultDialog = false,
            FillDialog = d =>
            {
                var browser = (OptionBrowserDialogViewModel)d;
                browser.SelectedOption = browser.Results[0];
                return Task.CompletedTask;
            }
        };
        var owner = new Owner(dialogs);

        await owner.Picker.BrowseCommand.ExecuteAsync(null);

        owner.Worker.Should().BeNull();
    }

    [Fact]
    public void A_dialog_property_and_its_box_stay_in_step()
    {
        // The real wiring, on the movement dialog: its box writes Category, and setting
        // Category from code shows it in the box.
        var vm = new MovementDialogViewModel(MovementType.Out, new DateOnly(2026, 3, 2));
        vm.ActiveCategories.Add(new ExpenseCategory { Id = 1, Name = "Lloguer" });
        vm.ActiveCategories.Add(new ExpenseCategory { Id = 2, Name = "Material" });

        vm.CategoryPicker.Text = "mat";
        vm.CategoryPicker.PickHighlightedCommand.Execute(null);
        vm.Category!.Id.Should().Be(2);

        vm.Category = vm.ActiveCategories[0];
        vm.CategoryPicker.Text.Should().Be("Lloguer");
    }
}
