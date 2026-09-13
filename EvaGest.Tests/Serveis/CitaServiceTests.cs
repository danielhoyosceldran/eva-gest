using AwesomeAssertions;
using EvaGest.Models;
using EvaGest.Services;
using EvaGest.Tests.Infra;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace EvaGest.Tests.Serveis;

/// <summary>Bloc F: cites i estats.</summary>
public class CitaServiceTests
{
    private static readonly DateOnly Avui = new(2026, 9, 7);

    private static CitaService CreaServei(BaseDadesProva bd) => new(new FabricaDeProva(bd.Opcions));

    [Fact] // F-01
    public async Task Cita_nova_neix_pendent()
    {
        await using var bd = new BaseDadesProva();
        var cites = CreaServei(bd);

        int id = await cites.Crear(new Cita
        {
            Data = Avui, Hora = new TimeOnly(10, 0), DuradaMin = 30, NomConvidat = "Convidat"
        });

        (await cites.ObtenirPerId(id))!.Estat.Should().Be(EstatCita.Pendent);
    }

    [Fact] // F-02
    public async Task Marcar_realitzada()
    {
        await using var bd = new BaseDadesProva();
        var cites = CreaServei(bd);
        int id = await cites.Crear(new Cita { Data = Avui, Hora = new TimeOnly(10, 0), DuradaMin = 30, NomConvidat = "C" });

        await cites.CanviarEstat(id, EstatCita.Realitzada);

        (await cites.ObtenirPerId(id))!.Estat.Should().Be(EstatCita.Realitzada);
    }

    [Fact] // F-03
    public async Task Marcar_cancellada()
    {
        await using var bd = new BaseDadesProva();
        var cites = CreaServei(bd);
        int id = await cites.Crear(new Cita { Data = Avui, Hora = new TimeOnly(10, 0), DuradaMin = 30, NomConvidat = "C" });

        await cites.CanviarEstat(id, EstatCita.Cancellada);

        (await cites.ObtenirPerId(id))!.Estat.Should().Be(EstatCita.Cancellada);
    }

    [Fact] // F-04
    public async Task Marcar_no_assistida()
    {
        await using var bd = new BaseDadesProva();
        var cites = CreaServei(bd);
        int id = await cites.Crear(new Cita { Data = Avui, Hora = new TimeOnly(10, 0), DuradaMin = 30, NomConvidat = "C" });

        await cites.CanviarEstat(id, EstatCita.NoAssistida);

        (await cites.ObtenirPerId(id))!.Estat.Should().Be(EstatCita.NoAssistida);
    }

    [Fact] // F-05
    public async Task Cancellada_es_pot_tornar_a_pendent()
    {
        await using var bd = new BaseDadesProva();
        var cites = CreaServei(bd);
        int id = await cites.Crear(new Cita { Data = Avui, Hora = new TimeOnly(10, 0), DuradaMin = 30, NomConvidat = "C" });
        await cites.CanviarEstat(id, EstatCita.Cancellada);

        bool canviat = await cites.CanviarEstat(id, EstatCita.Pendent);

        canviat.Should().BeTrue();
        (await cites.ObtenirPerId(id))!.Estat.Should().Be(EstatCita.Pendent);
    }

    [Fact] // F-05
    public async Task Realitzada_amb_venda_activa_no_es_pot_tornar_a_pendent()
    {
        await using var bd = new BaseDadesProva();
        var cites = CreaServei(bd);

        int metodeId, citaId;
        await using (var db = bd.Context())
        {
            var metode = Fes.Metode();
            db.MetodesPagament.Add(metode);
            var cita = new Cita { Data = Avui, Hora = new TimeOnly(10, 0), DuradaMin = 30, NomConvidat = "C" };
            db.Cites.Add(cita);
            await db.SaveChangesAsync();
            metodeId = metode.Id;
            citaId = cita.Id;
        }

        await using (var db = bd.Context())
        {
            db.Vendes.Add(new Venda
            {
                Data = Avui, Hora = new TimeOnly(10, 0), NomConvidat = "C", CitaId = citaId,
                MetodePagamentId = metodeId, BaseCents = 100, IvaCents = 21, TotalCents = 121, IvaMode = IvaMode.Inclos
            });
            await db.SaveChangesAsync();
        }
        await cites.CanviarEstat(citaId, EstatCita.Realitzada);

        bool canviat = await cites.CanviarEstat(citaId, EstatCita.Pendent);

        canviat.Should().BeFalse();
        (await cites.ObtenirPerId(citaId))!.Estat.Should().Be(EstatCita.Realitzada);
    }

    [Fact] // F-05
    public async Task Realitzada_amb_venda_anullada_es_pot_tornar_a_pendent()
    {
        await using var bd = new BaseDadesProva();
        var cites = CreaServei(bd);
        var vendes = new VendaService(new FabricaDeProva(bd.Opcions), new ConfiguracioService(new FabricaDeProva(bd.Opcions)));

        int metodeId, citaId, vendaId;
        await using (var db = bd.Context())
        {
            var metode = Fes.Metode();
            db.MetodesPagament.Add(metode);
            var cita = new Cita { Data = Avui, Hora = new TimeOnly(10, 0), DuradaMin = 30, NomConvidat = "C" };
            db.Cites.Add(cita);
            await db.SaveChangesAsync();
            metodeId = metode.Id;
            citaId = cita.Id;
        }

        await using (var db = bd.Context())
        {
            var venda = new Venda
            {
                Data = Avui, Hora = new TimeOnly(10, 0), NomConvidat = "C", CitaId = citaId,
                MetodePagamentId = metodeId, BaseCents = 100, IvaCents = 21, TotalCents = 121, IvaMode = IvaMode.Inclos
            };
            db.Vendes.Add(venda);
            await db.SaveChangesAsync();
            vendaId = venda.Id;
        }
        await cites.CanviarEstat(citaId, EstatCita.Realitzada);
        await vendes.Anullar(vendaId);

        bool canviat = await cites.CanviarEstat(citaId, EstatCita.Pendent);

        canviat.Should().BeTrue();
        (await cites.ObtenirPerId(citaId))!.Estat.Should().Be(EstatCita.Pendent);
    }

    [Fact] // F-06
    public async Task Cita_realitzada_sense_venda_es_valida()
    {
        await using var bd = new BaseDadesProva();
        var cites = CreaServei(bd);
        int id = await cites.Crear(new Cita { Data = Avui, Hora = new TimeOnly(10, 0), DuradaMin = 30, NomConvidat = "C" });
        await cites.CanviarEstat(id, EstatCita.Realitzada);

        (await cites.ObtenirRealitzadesSenseVenda()).Should().ContainSingle(c => c.Id == id);
    }

    [Fact] // F-07
    public async Task Durada_surt_del_servei_si_en_te()
    {
        await using var bd = new BaseDadesProva();
        int serveiId;
        await using (var db = bd.Context())
        {
            var servei = Fes.Servei("Tall", durada: 30);
            db.Serveis.Add(servei);
            await db.SaveChangesAsync();
            serveiId = servei.Id;
        }

        var servei2 = (await bd.Context().Serveis.FindAsync(serveiId))!;
        servei2.DuradaMin.Should().Be(30);
    }

    [Fact] // F-09
    public async Task Cita_amb_client_convidat_es_guarda()
    {
        await using var bd = new BaseDadesProva();
        var cites = CreaServei(bd);

        int id = await cites.Crear(new Cita
        {
            Data = Avui, Hora = new TimeOnly(10, 0), DuradaMin = 30,
            NomConvidat = "Anna", TelefonConvidat = "600111222"
        });

        (await cites.ObtenirPerId(id))!.EsConvidat.Should().BeTrue();
    }

    [Fact] // F-10
    public async Task Cita_amb_client_registrat_i_nom_convidat_es_rebutjada()
    {
        await using var bd = new BaseDadesProva();
        await using var db = bd.Context();

        var client = new Client { Nom = "Joan", Mobil = "612345678" };
        db.Clients.Add(client);
        await db.SaveChangesAsync();

        db.Cites.Add(new Cita
        {
            Data = Avui, Hora = new TimeOnly(10, 0), DuradaMin = 30,
            ClientId = client.Id, NomConvidat = "Un altre nom"
        });

        var accio = async () => await db.SaveChangesAsync();
        await accio.Should().ThrowAsync<DbUpdateException>();
    }

    [Fact] // F-11
    public async Task Cita_sense_client_ni_convidat_es_rebutjada()
    {
        await using var bd = new BaseDadesProva();
        await using var db = bd.Context();

        db.Cites.Add(new Cita { Data = Avui, Hora = new TimeOnly(10, 0), DuradaMin = 30 });

        var accio = async () => await db.SaveChangesAsync();
        await accio.Should().ThrowAsync<DbUpdateException>();
    }

    [Fact] // F-12
    public async Task Una_cita_no_pot_tenir_dues_vendes()
    {
        await using var bd = new BaseDadesProva();

        int metodeId, citaId;
        await using (var db = bd.Context())
        {
            var metode = Fes.Metode();
            db.MetodesPagament.Add(metode);
            var cita = new Cita { Data = Avui, Hora = new TimeOnly(10, 0), DuradaMin = 30, NomConvidat = "C" };
            db.Cites.Add(cita);
            await db.SaveChangesAsync();
            metodeId = metode.Id;
            citaId = cita.Id;
        }

        // Two separate DbContext instances, like two independent operations in the
        // running app: this is what actually exercises the unique index at the SQLite
        // level. A single shared context instead triggers EF's own one-to-one fixup,
        // which silently nulls the first Venda's CitaId and never reaches the database.
        await using (var db = bd.Context())
        {
            db.Vendes.Add(new Venda
            {
                Data = Avui, Hora = new TimeOnly(10, 0), NomConvidat = "C", CitaId = citaId,
                MetodePagamentId = metodeId, BaseCents = 100, IvaCents = 21, TotalCents = 121, IvaMode = IvaMode.Inclos
            });
            await db.SaveChangesAsync();
        }

        await using var db2 = bd.Context();
        db2.Vendes.Add(new Venda
        {
            Data = Avui, Hora = new TimeOnly(11, 0), NomConvidat = "C", CitaId = citaId,
            MetodePagamentId = metodeId, BaseCents = 100, IvaCents = 21, TotalCents = 121, IvaMode = IvaMode.Inclos
        });

        var accio = async () => await db2.SaveChangesAsync();
        await accio.Should().ThrowAsync<DbUpdateException>();
    }
}
