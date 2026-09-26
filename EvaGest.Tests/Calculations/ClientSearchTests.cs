using AwesomeAssertions;
using EvaGest.Helpers;
using EvaGest.Models;
using Xunit;

namespace EvaGest.Tests.Calculations;

/// <summary>
/// The matching behind the client picker. Newer than pla-proves.md, so these carry no
/// block code.
/// </summary>
public class ClientSearchTests
{
    private static Client C(int id, string name, string mobile = "")
        => new() { Id = id, Name = name, Mobile = mobile };

    private static readonly List<Client> Clients =
    [
        C(1, "Joan García", "612 34 56 78"),
        C(2, "Joan Martí", "699 11 22 33"),
        C(3, "Anna Joanet", "600 00 00 01"),
        C(4, "Marta Puig", "611 00 00 02"),
        C(5, "Núria Ferrer"),
    ];

    private static List<int> Ids(string query) => ClientSearch.Find(Clients, query).Select(c => c.Id).ToList();

    [Fact]
    public void A_word_in_the_middle_of_the_name_matches()
        => Ids("garcia").Should().Equal(1);

    [Fact]
    public void Case_and_accents_are_ignored_both_ways()
    {
        Ids("MARTI").Should().Equal(2);
        Ids("nuria").Should().Equal(5);
        Ids("Núria").Should().Equal(5);
    }

    [Fact]
    public void Every_word_typed_must_match_so_a_second_word_narrows_namesakes()
        => Ids("joan mar").Should().Equal(2);

    [Fact]
    public void Digits_match_the_phone_whatever_spacing_it_was_saved_with()
    {
        Ids("3456").Should().Equal(1);
        Ids("joan 699").Should().Equal(2);
    }

    [Fact]
    public void Digits_do_not_match_a_client_with_no_phone()
        => Ids("0").Should().NotContain(5);

    [Fact]
    public void A_name_starting_with_the_query_ranks_before_one_that_only_contains_it()
    {
        // "Anna Joanet" contains "joan" in a later word; both Joans start with it
        Ids("joan").Should().Equal(1, 2, 3);
    }

    [Fact]
    public void A_later_word_starting_with_the_query_ranks_before_a_match_inside_a_word()
    {
        // "Pere Arnau" has a word starting with "ar"; in "Carla" it is only inside one,
        // so Pere comes first even though Carla is first alphabetically.
        ClientSearch.Find([C(1, "Carla"), C(2, "Pere Arnau")], "ar").Select(c => c.Id).Should().Equal(2, 1);
    }

    [Fact]
    public void An_empty_query_lists_everyone_alphabetically_up_to_the_limit()
    {
        var many = Enumerable.Range(1, 20).Select(i => C(i, $"Client {i:00}")).ToList();

        var found = ClientSearch.Find(many, "   ");

        found.Should().HaveCount(ClientSearch.MaxResults);
        found[0].Name.Should().Be("Client 01");
    }

    [Fact]
    public void No_match_returns_an_empty_list()
        => Ids("zzz").Should().BeEmpty();

    [Fact]
    public void Normalize_collapses_spaces_and_strips_accents_and_cedillas()
        => ClientSearch.Normalize("  Françesc   Pàmies ").Should().Be("francesc pamies");
}
