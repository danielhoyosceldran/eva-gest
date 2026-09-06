using AwesomeAssertions;
using EvaGest.Models;
using EvaGest.Services;
using EvaGest.Tests.Infra;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace EvaGest.Tests.Serveis;

/// <summary>Bloc G (vendes) + F-05 (cita cancel·lada no pot associar venda).</summary>
public class VendaServiceTests
{
    private static readonly DateOnly Avui = new(2026, 9, 7);

    private static VendaService CreaServei(BaseDadesProva bd)
        => new(new FabricaDeProva(bd.Opcions), new ConfiguracioDeProva());

    private static async Task<int> AfegeixMetode(BaseDadesProva bd)
    {
        await using var db = bd.Context();
        var m = Fes.Metode();
        db.MetodesPagament.Add(m);
        await db.SaveChangesAsync();
        return m.Id;
    }

    private static Venda NovaVenda(int metodeId, params VendaLinia[] linies) => new()
    {
        Data = Avui, Hora = new TimeOnly(10, 0), NomConvidat = "Client de prova",
        MetodePagamentId = metodeId, Linies = [.. linies]
    };

    private static VendaLinia Linia(int importCents, int ivaBp = 2100, int quantitat = 1,
        int? serveiId = null, int? producteId = null) => new()
    {
        ServeiId = serveiId, ProducteId = producteId, Descripcio = "Prova",
        Quantitat = quantitat, PreuUnitariCents = importCents / quantitat,
        IvaBp = ivaBp, ImportCents = importCents
    };

    [Fact] // G-01
    public async Task Venda_independent_amb_un_servei_es_guarda_amb_totals_correctes()
    {
        await using var bd = new BaseDadesProva();
        int metodeId = await AfegeixMetode(bd);
        var vendes = CreaServei(bd);

        int id = await vendes.Crear(NovaVenda(metodeId), [Linia(1500, 2100)]);

        var venda = await vendes.ObtenirPerId(id);
        venda!.BaseCents.Should().Be(1240);
        venda.IvaCents.Should().Be(260);
        venda.TotalCents.Should().Be(1500);
    }

    [Fact] // G-02
    public async Task Venda_des_duna_cita_precarrega_client_i_servei()
    {
        await using var bd = new BaseDadesProva();
        int metodeId = await AfegeixMetode(bd);
        int citaId;
        await using (var db = bd.Context())
        {
            var servei = Fes.Servei();
            db.Serveis.Add(servei);
            await db.SaveChangesAsync();

            var cita = new Cita
            {
                Data = Avui, Hora = new TimeOnly(10, 0), DuradaMin = 30,
                NomConvidat = "Anna", ServeiId = servei.Id
            };
            db.Cites.Add(cita);
            await db.SaveChangesAsync();
            citaId = cita.Id;
        }

        var vendes = CreaServei(bd);
        var preparada = await vendes.PreparaDesDeCita(citaId);

        preparada.NomConvidat.Should().Be("Anna");
        preparada.CitaId.Should().Be(citaId);
    }

    [Fact] // G-03
    public async Task Linia_personalitzada_es_tipus_altres()
    {
        var linia = Linia(1000, 2100);
        linia.Tipus.Should().Be(TipusLinia.Altres);
    }

    [Fact] // G-04 / G-05
    public async Task Modificar_preu_dun_servei_desprs_no_afecta_vendes_anteriors()
    {
        await using var bd = new BaseDadesProva();
        int metodeId = await AfegeixMetode(bd);
        int serveiId;
        await using (var db = bd.Context())
        {
            var servei = Fes.Servei("Tall", 1500, 2100);
            db.Serveis.Add(servei);
            await db.SaveChangesAsync();
            serveiId = servei.Id;
        }

        var vendes = CreaServei(bd);
        int vendaId = await vendes.Crear(NovaVenda(metodeId), [Linia(1500, 2100, serveiId: serveiId)]);

        // El preu del catàleg canvia després
        await using (var db = bd.Context())
        {
            var servei = await db.Serveis.FindAsync(serveiId);
            servei!.PreuCents = 2000;
            await db.SaveChangesAsync();
        }

        var venda = await vendes.ObtenirPerId(vendaId);
        venda!.TotalCents.Should().Be(1500); // manté el preu congelat
    }

    [Fact] // G-07
    public async Task IvaMode_es_congela_amb_la_venda()
    {
        await using var bd = new BaseDadesProva();
        int metodeId = await AfegeixMetode(bd);
        var vendes = CreaServei(bd);

        int id = await vendes.Crear(NovaVenda(metodeId), [Linia(1500, 2100)]);

        (await vendes.ObtenirPerId(id))!.IvaMode.Should().Be(IvaMode.Inclos);
    }

    [Fact] // G-10 / G-11
    public async Task Anullar_una_venda_no_lesborra_i_segueix_visible()
    {
        await using var bd = new BaseDadesProva();
        int metodeId = await AfegeixMetode(bd);
        var vendes = CreaServei(bd);

        int id = await vendes.Crear(NovaVenda(metodeId), [Linia(1500, 2100)]);
        await vendes.Anullar(id);

        var venda = await vendes.ObtenirPerId(id);
        venda!.Estat.Should().Be(EstatVenda.Anullada);
        (await vendes.Cercar(new FiltreVendes())).Should().ContainSingle(v => v.Id == id);
    }

    [Fact] // G-12 / G-13
    public async Task Editar_una_venda_recalcula_totals_i_desglossaments()
    {
        await using var bd = new BaseDadesProva();
        int metodeId = await AfegeixMetode(bd);
        var vendes = CreaServei(bd);

        int id = await vendes.Crear(NovaVenda(metodeId), [Linia(1000, 2100)]);

        var actualitzada = new Venda { Id = id, Data = Avui, Hora = new TimeOnly(11, 0), NomConvidat = "C", MetodePagamentId = metodeId };
        await vendes.Actualitzar(actualitzada, [Linia(2000, 2100), Linia(1000, 1000)]);

        var venda = await vendes.ObtenirPerId(id);
        venda!.Linies.Should().HaveCount(2);

        await using var db = bd.Context();
        var desglossaments = db.VendaDesglossaments.Where(d => d.VendaId == id).ToList();
        desglossaments.Should().HaveCount(2);
    }

    [Fact] // G-14
    public async Task Venda_amb_client_convidat_es_guarda_i_compta_als_totals()
    {
        await using var bd = new BaseDadesProva();
        int metodeId = await AfegeixMetode(bd);
        var vendes = CreaServei(bd);

        await vendes.Crear(NovaVenda(metodeId), [Linia(1500, 2100)]);

        long total = 0;
        await using (var db = bd.Context())
            total = await db.Vendes.SumAsync(v => (long)v.TotalCents);

        total.Should().Be(1500);
    }

    [Fact] // F-05
    public async Task Cita_cancellada_no_pot_associar_venda()
    {
        await using var bd = new BaseDadesProva();
        int citaId;
        await using (var db = bd.Context())
        {
            var cita = new Cita
            {
                Data = Avui, Hora = new TimeOnly(10, 0), DuradaMin = 30,
                NomConvidat = "C", Estat = EstatCita.Cancellada
            };
            db.Cites.Add(cita);
            await db.SaveChangesAsync();
            citaId = cita.Id;
        }

        var vendes = CreaServei(bd);
        var accio = async () => await vendes.PreparaDesDeCita(citaId);

        await accio.Should().ThrowAsync<InvalidOperationException>();
    }
}
