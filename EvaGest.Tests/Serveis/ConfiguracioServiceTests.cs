using AwesomeAssertions;
using EvaGest.Models;
using EvaGest.Services;
using EvaGest.Tests.Infra;
using Xunit;

namespace EvaGest.Tests.Serveis;

public class ConfiguracioServiceTests
{
    private static ConfiguracioService Servei(BaseDadesProva bd) => new(new FabricaDeProva(bd.Opcions));

    [Fact] // C-01
    public async Task Una_clau_que_no_existeix_torna_null()
    {
        await using var bd = new BaseDadesProva();
        (await Servei(bd).Obtenir("no_hi_es")).Should().BeNull();
    }

    [Fact] // C-02
    public async Task Guardar_i_tornar_a_llegir_dona_el_mateix_valor()
    {
        await using var bd = new BaseDadesProva();
        var config = Servei(bd);

        await config.Guardar(ClausConfig.MinutsSlotAgenda, "15");

        (await config.Obtenir(ClausConfig.MinutsSlotAgenda)).Should().Be("15");
    }

    [Fact] // C-03
    public async Task Guardar_dues_vegades_la_mateixa_clau_la_sobreescriu()
    {
        await using var bd = new BaseDadesProva();
        var config = Servei(bd);

        await config.Guardar(ClausConfig.MinutsSlotAgenda, "15");
        await config.Guardar(ClausConfig.MinutsSlotAgenda, "60");

        (await config.ObtenirInt(ClausConfig.MinutsSlotAgenda, 30)).Should().Be(60);
        await using var db = bd.Context();
        db.Configuracio.Count(c => c.Clau == ClausConfig.MinutsSlotAgenda).Should().Be(1);
    }

    [Fact] // C-04
    public async Task ObtenirInt_amb_un_valor_no_numeric_cau_al_per_defecte()
    {
        await using var bd = new BaseDadesProva();
        var config = Servei(bd);
        await config.Guardar(ClausConfig.MinutsSlotAgenda, "molt");

        (await config.ObtenirInt(ClausConfig.MinutsSlotAgenda, 30)).Should().Be(30);
    }

    [Fact] // C-05
    public async Task ObtenirBool_llegeix_el_valor_guardat()
    {
        await using var bd = new BaseDadesProva();
        var config = Servei(bd);
        await config.Guardar(ClausConfig.SoConfirmacio, "false");

        (await config.ObtenirBool(ClausConfig.SoConfirmacio, true)).Should().BeFalse();
        (await config.ObtenirBool("clau_absent", true)).Should().BeTrue();
    }

    [Fact] // C-06
    public async Task InvalidarCache_torna_a_llegir_de_la_base_de_dades()
    {
        await using var bd = new BaseDadesProva();
        var config = Servei(bd);
        await config.Obtenir(ClausConfig.MinutsSlotAgenda);   // omple la cache amb el buit

        await using (var db = bd.Context())
        {
            db.Configuracio.Add(new ConfiguracioItem { Clau = ClausConfig.MinutsSlotAgenda, Valor = "60" });
            await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        (await config.Obtenir(ClausConfig.MinutsSlotAgenda)).Should().BeNull("la cache encara és vàlida");
        config.InvalidarCache();
        (await config.Obtenir(ClausConfig.MinutsSlotAgenda)).Should().Be("60");
    }
}
