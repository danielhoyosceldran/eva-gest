using System.Globalization;
using System.Text.RegularExpressions;
using System.IO;
using AwesomeAssertions;
using EvaGest.Models;
using EvaGest.Resources;
using EvaGest.Services;
using Xunit;

namespace EvaGest.Tests.Services;

/// <summary>
/// Block V: the interface language (RF-23). The point of these is that a Spanish
/// session must never fall through to a Catalan string, and the other way round: the
/// user would see half the screen in the wrong language and no way to report it.
///
/// They read the tables through an explicit culture instead of moving the culture of
/// the process, which the rest of the suite shares.
/// </summary>
public class LanguageTests
{
    /// <summary>Catalan is the neutral table, Spanish the only satellite.</summary>
    private static readonly CultureInfo CatalanTable = CultureInfo.InvariantCulture;
    private static readonly CultureInfo SpanishTable = CultureInfo.GetCultureInfo("es");

    private static readonly CultureInfo Catalan = CultureInfo.GetCultureInfo("ca-ES");
    private static readonly CultureInfo Spanish = CultureInfo.GetCultureInfo("es-ES");

    [Fact]
    public void Every_text_answers_in_both_languages()
    {
        Texts.Keys.Should().NotBeEmpty();

        foreach (string key in Texts.Keys)
        {
            Texts.GetExact(key, CatalanTable).Should()
                .NotBeNull($"{key} needs a Catalan wording");
            Texts.GetExact(key, SpanishTable).Should()
                .NotBeNull($"{key} needs a Spanish wording, or the screen comes out half translated");
        }
    }

    [Fact]
    public void The_same_key_gives_a_different_wording_in_each_language()
    {
        Texts.Get(nameof(Texts.NavClients), Catalan).Should().Be("Clients");
        Texts.Get(nameof(Texts.NavClients), Spanish).Should().Be("Clientes");
    }

    [Fact]
    public void An_unknown_language_setting_falls_back_to_Catalan()
    {
        AppLanguage.Parse(null).Should().Be(Language.Catalan);
        AppLanguage.Parse("").Should().Be(Language.Catalan);
        AppLanguage.Parse("Klingon").Should().Be(Language.Catalan);
    }

    [Fact]
    public void A_stored_language_is_read_back()
    {
        AppLanguage.Parse(nameof(Language.Spanish)).Should().Be(Language.Spanish);
        AppLanguage.Parse(nameof(Language.Catalan)).Should().Be(Language.Catalan);
    }

    [Fact]
    public void Placeholders_survive_the_translation()
    {
        // A message whose {0} was lost in translation would print the wrong thing with
        // no error at all, so the arguments have to match key by key.
        foreach (string key in Texts.Keys)
        {
            Placeholders(Texts.Get(key, Spanish)).Should()
                .Be(Placeholders(Texts.Get(key, Catalan)),
                    $"{key} has to take the same arguments in both languages");
        }
    }

    private static int Placeholders(string text)
    {
        int count = 0;
        for (int i = 0; i < 5; i++)
            if (text.Contains($"{{{i}}}")) count++;
        return count;
    }

    /// <summary>
    /// Walks up from the test binaries to the folder holding the solution, so the rule
    /// below reads the real .xaml sources rather than a copy that can drift.
    /// </summary>
    private static DirectoryInfo RepositoryRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "EvaGest.slnx")))
            dir = dir.Parent;

        dir.Should().NotBeNull("the .xaml sources have to be findable for this rule to mean anything");
        return dir!;
    }

    /// <summary>
    /// Every other test here compares the two resx tables against each other, which makes
    /// them structurally blind to a string that never reached a table at all. Two did:
    /// "{0} vendes actives" and "{0} minuts" sat in XAML StringFormat attributes and
    /// stayed Catalan in a Spanish session.
    /// </summary>
    [Fact]
    public void No_user_visible_literal_is_written_straight_into_a_view()
    {
        // A literal value (not a {Binding} or {x:Static}) on an attribute the user reads,
        // and any StringFormat holding words rather than just digits and punctuation.
        var literalAttribute = new Regex(
            @"(?<![\w.])(?:Text|Content|Header|ToolTip)\s*=\s*""(?<value>[^""{][^""]*)""");
        // Stops at the quote that ends the attribute or the comma that starts the next
        // markup-extension argument. Deliberately does NOT stop at "}": that would cut
        // "{0} vendes actives" down to "{0" and let the very bug this rule exists for
        // walk straight through.
        var stringFormat = new Regex(@"StringFormat=\{\}(?<value>[^"",]*)");
        var hasLetters = new Regex(@"\p{L}");

        // Everything inside a {0...} placeholder is a .NET format specifier — "dd/MM/yyyy"
        // is a date pattern, not a word anyone translates. Only what sits AROUND the
        // placeholder is text the user reads.
        var placeholder = new Regex(@"\{\d+(?::[^}]*)?\}");

        var views = Directory
            .GetFiles(Path.Combine(RepositoryRoot().FullName, "EvaGest", "Views"), "*.xaml",
                      SearchOption.AllDirectories)
            .ToList();
        views.Should().NotBeEmpty("otherwise this rule silently checks nothing");

        var offenders = new List<string>();
        foreach (string path in views)
        {
            string xaml = File.ReadAllText(path);
            foreach (var regex in new[] { literalAttribute, stringFormat })
                foreach (Match match in regex.Matches(xaml))
                {
                    string value = match.Groups["value"].Value;
                    if (hasLetters.IsMatch(placeholder.Replace(value, "")))
                        offenders.Add($"{Path.GetFileName(path)}: \"{value.Trim()}\"");
                }
        }

        offenders.Should().BeEmpty(
            "text the user reads belongs in both resx tables, not in a view "
            + "(CLAUDE.md: never a literal in code)");
    }
}
