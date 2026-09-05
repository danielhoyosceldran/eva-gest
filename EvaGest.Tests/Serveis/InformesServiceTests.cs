using AwesomeAssertions;
using EvaGest.Models;
using EvaGest.Services;
using EvaGest.Tests.Infra;
using Xunit;

namespace EvaGest.Tests.Serveis;

/// <summary>Bloc I: indicadors i informes.</summary>
public class InformesServiceTests
{
    private static InformesService CreaServei(BaseDadesProva bd) => new(new FabricaDeProva(bd.Opcions));

    private static async Task<int> AfegeixMetode(BaseDadesProva bd)
    {
        await using var db = bd.Context();
        var m = Fes.Metode();
        db.MetodesPagament.Add(m);
        await db.SaveChangesAsync();
        return m.Id;
    }

    private static async Task<int> AfegeixClient(BaseDadesProva bd, string nom = "Joan", string mobil = "612345678")
    {
        await using var db = bd.Context();
        var c = new Client { Nom = nom, Mobil = mobil, ClientKey = ClientService.CalcularClientKey(nom, mobil) };
        db.Clients.Add(c);
        await db.SaveChangesAsync();
        return c.Id;
    }

    [Fact] // I-01
    public async Task IndicadorsDeClient_sense_vendes_torna_nulls()
    {
        await using var bd = new BaseDadesProva();
        int id = await AfegeixClient(bd);
        var informes = CreaServei(bd);

        var indicadors = await informes.IndicadorsDeClient(id);

        indicadors.Visites.Should().Be(0);
        indicadors.MitjanaPerVisitaEuros.Should().BeNull();
    }

    [Fact] // I-02
    public async Task IndicadorsDeClient_una_visita_frequencia_es_null()
    {
        await using var bd = new BaseDadesProva();
        int metodeId = await AfegeixMetode(bd);
        int clientId = await AfegeixClient(bd);
        await using (var db = bd.Context())
        {
            var v = Fes.Venda(new DateOnly(2026, 1, 1), metodeId, EstatVenda.Activa, Fes.Linia(1000, 2100));
            v.ClientId = clientId; v.NomConvidat = null;
            db.Vendes.Add(v);
            await db.SaveChangesAsync();
        }

        var indicadors = await CreaServei(bd).IndicadorsDeClient(clientId);
        indicadors.FrequenciaDies.Should().BeNull();
    }

    [Fact] // I-03
    public async Task IndicadorsDeClient_nomes_compta_vendes_actives()
    {
        await using var bd = new BaseDadesProva();
        int metodeId = await AfegeixMetode(bd);
        int clientId = await AfegeixClient(bd);
        await using (var db = bd.Context())
        {
            var activa = Fes.Venda(new DateOnly(2026, 1, 1), metodeId, EstatVenda.Activa, Fes.Linia(1000, 2100));
            activa.ClientId = clientId; activa.NomConvidat = null;
            db.Vendes.Add(activa);

            var anullada = Fes.Venda(new DateOnly(2026, 1, 2), metodeId, EstatVenda.Anullada, Fes.Linia(9999, 2100));
            anullada.ClientId = clientId; anullada.NomConvidat = null;
            db.Vendes.Add(anullada);
            await db.SaveChangesAsync();
        }

        var indicadors = await CreaServei(bd).IndicadorsDeClient(clientId);
        indicadors.Visites.Should().Be(1);
    }

    [Fact] // I-05
    public async Task DetallTreballadora_periode_sense_vendes_percentatge_treball_null()
    {
        await using var bd = new BaseDadesProva();
        int treballadoraId;
        await using (var db = bd.Context())
        {
            var t = Fes.Treballadora();
            db.Treballadores.Add(t);
            await db.SaveChangesAsync();
            treballadoraId = t.Id;
        }

        var detall = await CreaServei(bd).DetallDeTreballadora(treballadoraId, new DateOnly(2026, 1, 1), new DateOnly(2026, 1, 31));
        detall.PercentatgeTreball.Should().BeNull();
    }

    [Fact] // I-06
    public async Task DetallTreballadora_sense_vendes_percentatge_productes_null()
    {
        await using var bd = new BaseDadesProva();
        int treballadoraId;
        await using (var db = bd.Context())
        {
            var t = Fes.Treballadora();
            db.Treballadores.Add(t);
            await db.SaveChangesAsync();
            treballadoraId = t.Id;
        }

        var detall = await CreaServei(bd).DetallDeTreballadora(treballadoraId, new DateOnly(2026, 1, 1), new DateOnly(2026, 1, 31));
        detall.PercentatgeProductes.Should().BeNull();
    }

    [Fact] // I-07
    public async Task Percentatge_de_treball_de_dues_treballadores_suma_100()
    {
        await using var bd = new BaseDadesProva();
        int metodeId = await AfegeixMetode(bd);
        int t1, t2;
        await using (var db = bd.Context())
        {
            var a = Fes.Treballadora("A");
            var b = Fes.Treballadora("B");
            db.Treballadores.Add(a);
            db.Treballadores.Add(b);
            await db.SaveChangesAsync();
            t1 = a.Id; t2 = b.Id;
        }
        var data = new DateOnly(2026, 1, 1);
        await using (var db = bd.Context())
        {
            var v1 = Fes.Venda(data, metodeId, EstatVenda.Activa, Fes.Linia(6000, 2100));
            v1.TreballadoraId = t1;
            var v2 = Fes.Venda(data, metodeId, EstatVenda.Activa, Fes.Linia(4000, 2100));
            v2.TreballadoraId = t2;
            db.Vendes.Add(v1);
            db.Vendes.Add(v2);
            await db.SaveChangesAsync();
        }

        var informes = CreaServei(bd);
        var d1 = await informes.DetallDeTreballadora(t1, data, data);
        var d2 = await informes.DetallDeTreballadora(t2, data, data);

        (d1.PercentatgeTreball!.Value + d2.PercentatgeTreball!.Value).Should().Be(100m);
    }

    [Fact] // I-09 / I-10
    public async Task Indicadors_compten_cancellades_i_no_assistides_per_separat()
    {
        await using var bd = new BaseDadesProva();
        int clientId = await AfegeixClient(bd);
        await using (var db = bd.Context())
        {
            db.Cites.Add(new Cita { Data = new DateOnly(2026, 1, 1), Hora = new TimeOnly(10, 0), DuradaMin = 30, ClientId = clientId, Estat = EstatCita.Cancellada });
            db.Cites.Add(new Cita { Data = new DateOnly(2026, 1, 2), Hora = new TimeOnly(10, 0), DuradaMin = 30, ClientId = clientId, Estat = EstatCita.NoAssistida });
            db.Cites.Add(new Cita { Data = new DateOnly(2026, 1, 3), Hora = new TimeOnly(10, 0), DuradaMin = 30, ClientId = clientId, Estat = EstatCita.NoAssistida });
            await db.SaveChangesAsync();
        }

        var indicadors = await CreaServei(bd).IndicadorsDeClient(clientId);
        indicadors.Cancellades.Should().Be(1);
        indicadors.NoAssistides.Should().Be(2);
    }

    [Fact] // I-13
    public async Task Clients_convidats_no_apareixen_al_ranquing_per_visites()
    {
        await using var bd = new BaseDadesProva();
        int metodeId = await AfegeixMetode(bd);
        int clientId = await AfegeixClient(bd);
        await using (var db = bd.Context())
        {
            var registrada = Fes.Venda(new DateOnly(2026, 1, 1), metodeId, EstatVenda.Activa, Fes.Linia(1000, 2100));
            registrada.ClientId = clientId; registrada.NomConvidat = null;
            db.Vendes.Add(registrada);
            db.Vendes.Add(Fes.Venda(new DateOnly(2026, 1, 1), metodeId, EstatVenda.Activa, Fes.Linia(9999, 2100))); // guest
            await db.SaveChangesAsync();
        }

        var top = await CreaServei(bd).TopPerVisites();
        top.Should().ContainSingle(t => t.client.Id == clientId);
    }

    [Fact] // I-14
    public async Task Clients_convidats_compten_als_totals_globals_de_caixa()
    {
        await using var bd = new BaseDadesProva();
        int metodeId = await AfegeixMetode(bd);
        await using (var db = bd.Context())
        {
            db.Vendes.Add(Fes.Venda(new DateOnly(2026, 1, 1), metodeId, EstatVenda.Activa, Fes.Linia(1000, 2100)));
            await db.SaveChangesAsync();
        }

        var caixa = new CaixaService(new FabricaDeProva(bd.Opcions));
        var resum = await caixa.Resum(new DateOnly(2026, 1, 1), new DateOnly(2026, 1, 1));
        resum.VendesCents.Should().Be(1000);
    }

    [Fact] // I-18
    public async Task ClientDelMes_es_el_de_mes_despesa()
    {
        await using var bd = new BaseDadesProva();
        int metodeId = await AfegeixMetode(bd);
        int clientPetit = await AfegeixClient(bd, "Petit", "600000001");
        int clientGran = await AfegeixClient(bd, "Gran", "600000002");
        var avui = DateOnly.FromDateTime(DateTime.Today);
        var primerDelMes = new DateOnly(avui.Year, avui.Month, 1);

        await using (var db = bd.Context())
        {
            var venda1 = Fes.Venda(primerDelMes, metodeId, EstatVenda.Activa, Fes.Linia(1000, 2100));
            venda1.ClientId = clientPetit; venda1.NomConvidat = null;
            var venda2 = Fes.Venda(primerDelMes, metodeId, EstatVenda.Activa, Fes.Linia(9000, 2100));
            venda2.ClientId = clientGran; venda2.NomConvidat = null;
            db.Vendes.Add(venda1);
            db.Vendes.Add(venda2);
            await db.SaveChangesAsync();
        }

        var resultat = await CreaServei(bd).ClientDelMes();
        resultat!.Value.client.Id.Should().Be(clientGran);
    }
}
