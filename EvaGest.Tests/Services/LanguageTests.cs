using System.Globalization;
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
}
