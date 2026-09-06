using AwesomeAssertions;
using EvaGest.Models;
using EvaGest.Services;
using EvaGest.Tests.Infra;
using Xunit;

namespace EvaGest.Tests.Serveis;

/// <summary>
/// Bloc P: el contingut inicial. Sense mètode de pagament no es pot desar cap venda,
/// així que una base de dades acabada de crear sense seed és una aplicació que
/// s'instal·la i després no deixa cobrar.
/// </summary>
public class SeedServiceTests
{
    private static (SeedService seed, ConfiguracioService config) Muntar(BaseDadesProva bd)
    {
        var factory = new FabricaDeProva(bd.Opcions);
        var config = new ConfiguracioService(factory);
        return (new SeedService(factory, config), config);
    }

    [Fact] // P-01
    public async Task Una_base_de_dades_nova_queda_amb_metodes_de_pagament_per_cobrar()
    {
        await using var bd = new BaseDadesProva();
        var (seed, _) = Muntar(bd);

        await seed.Sembrar();

        await using var db = bd.Context();
        db.MetodesPagament.Select(m => m.Nom).Should().Contain("Efectiu");
        db.MetodesPagament.Should().OnlyContain(m => m.Actiu);
    }

    [Fact] // P-02
    public async Task Les_claus_de_configuracio_es_creen_amb_els_valors_documentats()
    {
        await using var bd = new BaseDadesProva();
        var (seed, config) = Muntar(bd);

        await seed.Sembrar();

        (await config.Obtenir(ClausConfig.IvaModeActual)).Should().Be(nameof(IvaMode.Inclos));
        (await config.ObtenirInt(ClausConfig.IvaBpDefecte, 0)).Should().Be(2100);
        (await config.ObtenirInt(ClausConfig.DuradaDefecteCitaMin, 0)).Should().Be(30);
        (await config.ObtenirInt(ClausConfig.BackupsAConservar, 0)).Should().Be(15);
        (await config.Obtenir(ClausConfig.HoraBackup)).Should().Be("20:00");
    }

    [Fact] // P-03
    public async Task Els_booleans_sembrats_com_a_zero_i_u_es_llegeixen_be()
    {
        await using var bd = new BaseDadesProva();
        var (seed, config) = Muntar(bd);

        await seed.Sembrar();

        // The stored shape is "1"/"0", which bool.TryParse rejects: reading these with
        // it alone silently returned the fallback for every single flag.
        (await config.ObtenirBool(ClausConfig.MostrarAvisConvidat, false)).Should().BeTrue();
        (await config.ObtenirBool(ClausConfig.SoConfirmacio, false)).Should().BeTrue();
        (await config.ObtenirBool(ClausConfig.AplicarIvaCaixa, true)).Should().BeFalse();
    }

    [Fact] // P-04
    public async Task Tornar_a_sembrar_no_desfa_el_que_l_usuaria_ha_canviat()
    {
        await using var bd = new BaseDadesProva();
        var (seed, config) = Muntar(bd);
        await seed.Sembrar();

        await config.Guardar(ClausConfig.BarberiaNom, "Barberia Eva");
        await config.GuardarBool(ClausConfig.SoConfirmacio, false);

        await using (var db = bd.Context())
        {
            db.MetodesPagament.RemoveRange(db.MetodesPagament.Where(m => m.Nom == "Bizum"));
            await db.SaveChangesAsync();
        }

        await seed.Sembrar();

        (await config.Obtenir(ClausConfig.BarberiaNom)).Should().Be("Barberia Eva");
        (await config.ObtenirBool(ClausConfig.SoConfirmacio, true)).Should().BeFalse();

        await using var comprovacio = bd.Context();
        comprovacio.MetodesPagament.Select(m => m.Nom).Should().NotContain("Bizum",
            "un mètode esborrat a posta no ha de tornar a cada arrencada");
    }

    [Fact] // P-05
    public async Task Una_clau_nova_arriba_a_una_base_de_dades_que_ja_existia()
    {
        await using var bd = new BaseDadesProva();
        var (seed, config) = Muntar(bd);

        // Simulates an older database that predates the settings key
        await config.Guardar(ClausConfig.BarberiaNom, "Barberia Eva");
        (await config.Obtenir(ClausConfig.HoraBackup)).Should().BeNull();

        await seed.Sembrar();

        (await config.Obtenir(ClausConfig.HoraBackup)).Should().Be("20:00");
    }
}
