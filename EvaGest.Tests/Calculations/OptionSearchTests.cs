using AwesomeAssertions;
using EvaGest.Helpers;
using Xunit;

namespace EvaGest.Tests.Calculations;

/// <summary>
/// The matching behind the search boxes that replaced the long combo boxes. Newer than
/// pla-proves.md, so no block code.
/// </summary>
public class OptionSearchTests
{
    private static readonly PickerOption[] Workers =
    [
        new("all", "Totes"),
        new("none", "Sense assignar"),
        new(1, "Marta Puig"),
        new(2, "Àngels Martí"),
        new(3, "Laia", "612 34 56 78"),
    ];

    [Fact]
    public void An_empty_search_keeps_the_options_in_their_own_order()
        => OptionSearch.Find(Workers, "").Select(o => o.Label)
            .Should().Equal("Totes", "Sense assignar", "Marta Puig", "Àngels Martí", "Laia");

    [Fact]
    public void A_search_ignores_case_and_accents()
        => OptionSearch.Find(Workers, "angels").Select(o => o.Value).Should().Equal(2);

    [Fact]
    public void A_label_starting_with_the_search_comes_before_one_containing_it()
        // "Marta Puig" starts with it; "Àngels Martí" only has a later word that does
        => OptionSearch.Find(Workers, "mart").Select(o => o.Value).Should().Equal(1, 2);

    [Fact]
    public void A_word_starting_the_label_beats_one_starting_a_later_word()
    {
        PickerOption[] options = [new(1, "Tall i Barba"), new(2, "Barba")];

        OptionSearch.Find(options, "barba").Select(o => o.Value).Should().Equal(2, 1);
    }

    [Fact]
    public void Digits_also_search_the_detail_however_it_was_typed()
        => OptionSearch.Find(Workers, "3456").Select(o => o.Value).Should().Equal(3);

    [Fact]
    public void Every_word_must_match()
        => OptionSearch.Find(Workers, "marta marti").Should().BeEmpty();

    [Fact]
    public void Exact_finds_the_one_option_named_in_full()
    {
        OptionSearch.Exact(Workers, "  marta PUIG ")!.Value.Should().Be(1);
        OptionSearch.Exact(Workers, "marta").Should().BeNull("a part of a name is not the name");
    }
}
