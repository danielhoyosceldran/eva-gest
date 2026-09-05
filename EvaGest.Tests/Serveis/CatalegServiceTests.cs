using AwesomeAssertions;
using EvaGest.Services;
using EvaGest.Tests.Infra;
using Xunit;

namespace EvaGest.Tests.Serveis;

/// <summary>
/// Fase 3 smoke tests: the catalogue is the first CRUD, and these confirm the
/// View -> ViewModel -> Service pattern actually reaches a real SQLite engine.
/// </summary>
public class CatalegServiceTests
{
    private static CatalegService CreaServei(BaseDadesProva bd)
        => new(new FabricaDeProva(bd.Opcions));

    [Fact]
    public async Task Crear_i_obtenir_servei()
    {
        await using var bd = new BaseDadesProva();
        var cataleg = CreaServei(bd);

        int id = await cataleg.CrearServei(Fes.Servei("Tall", 1500, 2100));

        var servei = await cataleg.ObtenirServei(id);
        servei.Should().NotBeNull();
        servei!.Nom.Should().Be("Tall");
        servei.PreuCents.Should().Be(1500);
    }

    [Fact]
    public async Task Desactivar_servei_no_lelimina()
    {
        await using var bd = new BaseDadesProva();
        var cataleg = CreaServei(bd);

        int id = await cataleg.CrearServei(Fes.Servei());
        await cataleg.CanviarEstatServei(id, false);

        var tots = await cataleg.ObtenirServeis();
        var nomesActius = await cataleg.ObtenirServeis(nomesActius: true);

        tots.Should().ContainSingle(s => s.Id == id);
        nomesActius.Should().BeEmpty();
    }

    [Fact]
    public async Task Crear_i_desactivar_producte()
    {
        await using var bd = new BaseDadesProva();
        var cataleg = CreaServei(bd);

        int id = await cataleg.CrearProducte(Fes.Producte("Cera", 900, 2100));
        await cataleg.CanviarEstatProducte(id, false);

        var producte = await cataleg.ObtenirProducte(id);
        producte!.Actiu.Should().BeFalse();
    }

    [Fact]
    public async Task Ultim_metode_actiu_no_es_pot_desactivar()
    {
        await using var bd = new BaseDadesProva();
        var cataleg = CreaServei(bd);

        int id = await cataleg.CrearMetode("Efectiu");

        (await cataleg.PotDesactivarMetode(id)).Should().BeFalse();
    }

    [Fact]
    public async Task Es_pot_desactivar_un_metode_si_un_altre_segueix_actiu()
    {
        await using var bd = new BaseDadesProva();
        var cataleg = CreaServei(bd);

        int efectiu = await cataleg.CrearMetode("Efectiu");
        await cataleg.CrearMetode("Targeta");

        (await cataleg.PotDesactivarMetode(efectiu)).Should().BeTrue();
    }
}
