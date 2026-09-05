using AwesomeAssertions;
using EvaGest.Models;
using EvaGest.Services;
using EvaGest.Tests.Infra;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace EvaGest.Tests.Serveis;

/// <summary>Bloc D (clau de client i duplicats) + Fase 4 smoke tests against a real
/// SQLite engine, so the unique index on ClientKey actually gets exercised.</summary>
public class ClientServiceTests
{
    private static ClientService CreaServei(BaseDadesProva bd) => new(new FabricaDeProva(bd.Opcions));

    [Fact] // D-01
    public void ClientKey_mateix_telefon_amb_prefix_diferent()
    {
        string a = ClientService.CalcularClientKey("Joan", "612345678");
        string b = ClientService.CalcularClientKey("Joan", "+34 612 345 678");
        a.Should().Be(b);
    }

    [Fact] // D-02
    public void ClientKey_accents_normalitzats()
    {
        string a = ClientService.CalcularClientKey("joán", "612345678");
        string b = ClientService.CalcularClientKey("Joan", "612345678");
        a.Should().Be(b);
    }

    [Fact] // D-03
    public void ClientKey_nomes_compta_el_nom_de_pila()
    {
        string a = ClientService.CalcularClientKey("Joan García", "612345678");
        string b = ClientService.CalcularClientKey("Joan Pérez", "612345678");
        a.Should().Be(b);
    }

    [Fact] // D-04
    public void ClientKey_prefix_0034()
    {
        string a = ClientService.CalcularClientKey("Joan", "0034612345678");
        string b = ClientService.CalcularClientKey("Joan", "612345678");
        a.Should().Be(b);
    }

    [Fact] // D-05
    public async Task Guardar_dos_clients_amb_la_mateixa_clau_es_rebutjat()
    {
        await using var bd = new BaseDadesProva();
        var clients = CreaServei(bd);

        await clients.Crear(new Client { Nom = "Joan", Mobil = "612345678" });

        var accio = async () => await clients.Crear(new Client { Nom = "Joan", Mobil = "612345678" });
        await accio.Should().ThrowAsync<Microsoft.EntityFrameworkCore.DbUpdateException>();
    }

    [Fact] // D-06
    public async Task BuscarPossibleDuplicat_torna_el_client_existent()
    {
        await using var bd = new BaseDadesProva();
        var clients = CreaServei(bd);

        int id = await clients.Crear(new Client { Nom = "Joan", Mobil = "612345678" });

        var duplicat = await clients.BuscarPossibleDuplicat("Joan", "612345678");
        duplicat.Should().NotBeNull();
        duplicat!.Id.Should().Be(id);
    }

    [Fact] // D-07
    public async Task Canviar_el_telefon_recalcula_la_clau()
    {
        await using var bd = new BaseDadesProva();
        var clients = CreaServei(bd);

        int id = await clients.Crear(new Client { Nom = "Joan", Mobil = "612345678" });
        var client = await clients.ObtenirPerId(id);
        client!.Mobil = "699999999";
        await clients.Actualitzar(client);

        var actualitzat = await clients.ObtenirPerId(id);
        actualitzat!.ClientKey.Should().Be(ClientService.CalcularClientKey("Joan", "699999999"));
    }

    [Fact]
    public async Task Adormir_treu_el_client_dels_actius_pero_no_lelimina()
    {
        await using var bd = new BaseDadesProva();
        var clients = CreaServei(bd);

        int id = await clients.Crear(new Client { Nom = "Joan", Mobil = "612345678" });
        await clients.Adormir(id);

        (await clients.ObtenirActius()).Should().BeEmpty();
        (await clients.ObtenirAdormits()).Should().ContainSingle(c => c.Id == id);
    }

    [Fact]
    public async Task Despertar_torna_el_client_als_actius()
    {
        await using var bd = new BaseDadesProva();
        var clients = CreaServei(bd);

        int id = await clients.Crear(new Client { Nom = "Joan", Mobil = "612345678" });
        await clients.Adormir(id);
        await clients.Despertar(id);

        (await clients.ObtenirActius()).Should().ContainSingle(c => c.Id == id);
    }

    [Fact]
    public async Task Cercar_per_nom_o_mobil()
    {
        await using var bd = new BaseDadesProva();
        var clients = CreaServei(bd);

        await clients.Crear(new Client { Nom = "Joan García", Mobil = "612345678" });
        await clients.Crear(new Client { Nom = "Maria Puig", Mobil = "699111222" });

        (await clients.Cercar("Joan")).Should().ContainSingle();
        (await clients.Cercar("699111")).Should().ContainSingle();
        (await clients.Cercar("inexistent")).Should().BeEmpty();
    }

    [Fact]
    public async Task Eliminar_client_esborra_tambe_les_seves_cites_i_vendes()
    {
        await using var bd = new BaseDadesProva();
        await using (var db = bd.Context())
        {
            var metode = Fes.Metode();
            db.MetodesPagament.Add(metode);
            var client = new Client { Nom = "Joan", Mobil = "612345678" };
            db.Clients.Add(client);
            await db.SaveChangesAsync();

            db.Cites.Add(new Cita
            {
                ClientId = client.Id, Data = new DateOnly(2026, 1, 1),
                Hora = new TimeOnly(10, 0), DuradaMin = 30
            });
            db.Vendes.Add(new Venda
            {
                ClientId = client.Id, Data = new DateOnly(2026, 1, 1), Hora = new TimeOnly(10, 0),
                MetodePagamentId = metode.Id, BaseCents = 100, IvaCents = 21, TotalCents = 121,
                IvaMode = IvaMode.Inclos
            });
            await db.SaveChangesAsync();
        }

        var clients = CreaServei(bd);
        var actiu = (await clients.ObtenirActius()).Single();
        await clients.Eliminar(actiu.Id);

        await using var verificacio = bd.Context();
        (await verificacio.Clients.CountAsync()).Should().Be(0);
        (await verificacio.Cites.CountAsync()).Should().Be(0);
        (await verificacio.Vendes.CountAsync()).Should().Be(0);
    }

}
