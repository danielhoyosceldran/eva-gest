using AwesomeAssertions;
using EvaGest.Models;
using EvaGest.Services;
using EvaGest.Tests.Infra;
using Xunit;

namespace EvaGest.Tests.Escenaris;

/// <summary>
/// Bloc K: jornades senceres simulades, per comprovar que al final del dia tot quadra.
/// La resta de blocs proven una peça cada un; aquest les fa treballar juntes, que és
/// on apareixen els descuadres d'un cèntim que ningú no veu fins al trimestre.
/// </summary>
public class DiaCompletTests
{
    private static readonly DateOnly Dia = new(2026, 9, 9);

    private sealed record Escenari(
        BaseDadesProva Bd, CitaService Cites, VendaService Vendes, CaixaService Caixa,
        InformesService Informes, CatalegService Cataleg, ConfiguracioService Configuracio);

    private static async Task<Escenari> Muntar(BaseDadesProva bd)
    {
        var factory = new FabricaDeProva(bd.Opcions);
        var config = new ConfiguracioService(factory);
        await new SeedService(factory, config).Sembrar();

        return new Escenari(bd, new CitaService(factory), new VendaService(factory, config),
            new CaixaService(factory), new InformesService(factory), new CatalegService(factory), config);
    }

    private static async Task<int> PrimerMetode(BaseDadesProva bd)
    {
        await using var db = bd.Context();
        return db.MetodesPagament.OrderBy(m => m.Id).First().Id;
    }

    private static async Task<int> Afegeix<T>(BaseDadesProva bd, T entitat) where T : class
    {
        await using var db = bd.Context();
        db.Add(entitat);
        await db.SaveChangesAsync();
        return (int)typeof(T).GetProperty("Id")!.GetValue(entitat)!;
    }

    private static VendaLinia Linia(int importCents, int ivaBp = 2100,
        int? serveiId = null, int? producteId = null, string descripcio = "Tall")
        => new()
        {
            Descripcio = descripcio,
            Quantitat = 1,
            PreuUnitariCents = importCents,
            IvaBp = ivaBp,
            ImportCents = importCents,
            ServeiId = serveiId,
            ProducteId = producteId
        };

    private static Venda NovaVenda(DateOnly data, int metodeId, int? clientId = null,
        int? treballadoraId = null, string? convidat = null)
        => new()
        {
            Data = data,
            Hora = new TimeOnly(10, 0),
            ClientId = clientId,
            NomConvidat = clientId is null ? convidat ?? "Client de prova" : null,
            TreballadoraId = treballadoraId,
            MetodePagamentId = metodeId
        };

    [Fact] // K-01
    public async Task Dia_normal_quadra_el_balanc_i_l_IVA()
    {
        await using var bd = new BaseDadesProva();
        var e = await Muntar(bd);
        int metodeId = await PrimerMetode(bd);

        // Six appointments: five completed and charged, one no-show
        for (int i = 0; i < 6; i++)
            await e.Cites.Crear(new Cita
            {
                Data = Dia, Hora = new TimeOnly(9 + i, 0), DuradaMin = 30,
                NomConvidat = $"Client {i}", Estat = EstatCita.Pendent
            });

        var cites = await e.Cites.ObtenirPerDia(Dia);
        foreach (var cita in cites.Take(5))
        {
            await e.Cites.CanviarEstat(cita.Id, EstatCita.Realitzada);
            await e.Vendes.Crear(NovaVenda(Dia, metodeId), [Linia(1500)]);
        }
        await e.Cites.CanviarEstat(cites[5].Id, EstatCita.NoAssistida);

        await e.Caixa.Crear(new MovimentCaixa
        {
            Data = Dia, Tipus = TipusMoviment.Entrada, ImportCents = 5000,
            MetodePagamentId = metodeId, Concepte = "Aportació"
        });
        await e.Caixa.Crear(new MovimentCaixa
        {
            Data = Dia, Tipus = TipusMoviment.Sortida, ImportCents = 3000,
            MetodePagamentId = metodeId, Concepte = "Material"
        });

        var resum = await e.Caixa.Resum(Dia, Dia);

        resum.VendesCents.Should().Be(7500);
        resum.EntradesCents.Should().Be(5000);
        resum.SortidesCents.Should().Be(3000);
        resum.BalancCents.Should().Be(9500);

        // The invariant that makes the quarterly return add up
        (resum.BaseCents + resum.IvaCents).Should().Be(resum.VendesCents);

        (await e.Cites.ComptarPerEstat(Dia, Dia, EstatCita.NoAssistida)).Should().Be(1);
        (await e.Cites.ComptarPerEstat(Dia, Dia, EstatCita.Realitzada)).Should().Be(5);
    }

    [Fact] // K-02
    public async Task Dia_amb_correccions_reflecteix_l_edicio_i_exclou_l_anullada()
    {
        await using var bd = new BaseDadesProva();
        var e = await Muntar(bd);
        int metodeId = await PrimerMetode(bd);

        var ids = new List<int>();
        foreach (int import in new[] { 1000, 2000, 3000, 4000 })
            ids.Add(await e.Vendes.Crear(NovaVenda(Dia, metodeId), [Linia(import)]));

        var segona = await e.Vendes.ObtenirPerId(ids[1]);
        await e.Vendes.Actualitzar(segona!, [Linia(2500)]);
        await e.Vendes.Anullar(ids[3]);

        var resum = await e.Caixa.Resum(Dia, Dia);

        resum.VendesCents.Should().Be(1000 + 2500 + 3000);
        (resum.BaseCents + resum.IvaCents).Should().Be(resum.VendesCents);
    }

    [Fact] // K-03
    public async Task Un_convidat_compta_al_total_pero_no_al_ranquing_de_clients()
    {
        await using var bd = new BaseDadesProva();
        var e = await Muntar(bd);
        int metodeId = await PrimerMetode(bd);
        int clientId = await Afegeix(bd, Fes.Client("Joana", "600111222"));

        await e.Vendes.Crear(NovaVenda(Dia, metodeId, clientId), [Linia(1000)]);
        await e.Vendes.Crear(NovaVenda(Dia, metodeId, clientId), [Linia(2000)]);
        await e.Vendes.Crear(NovaVenda(Dia, metodeId, convidat: "Passavolant"), [Linia(3000)]);

        var resum = await e.Caixa.Resum(Dia, Dia);
        resum.VendesCents.Should().Be(6000, "el convidat també ha pagat");

        var ranquing = await e.Informes.TopPerDespesa();
        ranquing.Should().ContainSingle();
        ranquing[0].client.Id.Should().Be(clientId);
        ranquing[0].totalCents.Should().Be(3000);

        await using var db = bd.Context();
        db.Clients.Should().ContainSingle("un convidat no genera fitxa");
    }

    [Fact] // K-04
    public async Task Amb_dues_treballadores_els_percentatges_de_treball_sumen_cent()
    {
        await using var bd = new BaseDadesProva();
        var e = await Muntar(bd);
        int metodeId = await PrimerMetode(bd);
        int martaId = await Afegeix(bd, Fes.Treballadora("Marta"));
        int bertaId = await Afegeix(bd, Fes.Treballadora("Berta"));

        for (int i = 0; i < 5; i++)
            await e.Vendes.Crear(NovaVenda(Dia, metodeId, treballadoraId: martaId), [Linia(1000)]);
        for (int i = 0; i < 3; i++)
            await e.Vendes.Crear(NovaVenda(Dia, metodeId, treballadoraId: bertaId), [Linia(1000)]);

        var ranquing = await e.Informes.RanquingTreballadores(Dia, Dia);

        ranquing.Should().HaveCount(2);
        ranquing.Sum(d => d.PercentatgeTreball ?? 0).Should().Be(100m);
        ranquing.Single(d => d.TreballadoraId == martaId).VendesAteses.Should().Be(5);
    }

    [Fact] // K-05
    public async Task Les_linies_personalitzades_es_respecten_i_compten_com_a_altres()
    {
        await using var bd = new BaseDadesProva();
        var e = await Muntar(bd);
        int metodeId = await PrimerMetode(bd);
        int treballadoraId = await Afegeix(bd, Fes.Treballadora("Marta"));
        int serveiId = await Afegeix(bd, Fes.Servei("Tall", preuCents: 1500));

        await e.Vendes.Crear(NovaVenda(Dia, metodeId, treballadoraId: treballadoraId),
            [Linia(1500, serveiId: serveiId)]);
        await e.Vendes.Crear(NovaVenda(Dia, metodeId, treballadoraId: treballadoraId),
            [Linia(500, descripcio: "Retoc de favor")]);

        var detall = await e.Informes.DetallDeTreballadora(treballadoraId, Dia, Dia);

        detall.AltresConceptesCents.Should().Be(500, "el preu reduït es guarda tal com s'ha escrit");
        detall.Serveis.Should().ContainSingle(s => s.nom == "Tall");
        (await e.Caixa.Resum(Dia, Dia)).VendesCents.Should().Be(2000);
    }

    [Fact] // K-06
    public async Task El_total_setmanal_es_la_suma_dels_cinc_balancos_diaris()
    {
        await using var bd = new BaseDadesProva();
        var e = await Muntar(bd);
        int metodeId = await PrimerMetode(bd);

        var dilluns = new DateOnly(2026, 9, 7);
        for (int d = 0; d < 5; d++)
        {
            var dia = dilluns.AddDays(d);
            for (int i = 0; i < 5; i++)
                await e.Vendes.Crear(NovaVenda(dia, metodeId), [Linia(1500)]);

            await e.Caixa.Crear(new MovimentCaixa
            {
                Data = dia, Tipus = TipusMoviment.Entrada, ImportCents = 5000,
                MetodePagamentId = metodeId, Concepte = "Aportació"
            });
            await e.Caixa.Crear(new MovimentCaixa
            {
                Data = dia, Tipus = TipusMoviment.Sortida, ImportCents = 3000,
                MetodePagamentId = metodeId, Concepte = "Material"
            });
        }

        long sumaDiaria = 0;
        long baseDiaria = 0;
        long ivaDiari = 0;
        for (int d = 0; d < 5; d++)
        {
            var resumDia = await e.Caixa.Resum(dilluns.AddDays(d), dilluns.AddDays(d));
            sumaDiaria += resumDia.BalancCents;
            baseDiaria += resumDia.BaseCents;
            ivaDiari += resumDia.IvaCents;
        }

        var setmana = await e.Caixa.Resum(dilluns, dilluns.AddDays(4));

        setmana.BalancCents.Should().Be(sumaDiaria);
        setmana.BaseCents.Should().Be(baseDiaria);
        setmana.IvaCents.Should().Be(ivaDiari);
    }

    [Fact] // K-07
    public async Task El_trimestre_quadra_amb_la_suma_de_les_bases_guardades()
    {
        await using var bd = new BaseDadesProva();
        var e = await Muntar(bd);
        int metodeId = await PrimerMetode(bd);

        var inici = new DateOnly(2026, 7, 1);
        for (int d = 0; d < 60; d++)
            await e.Vendes.Crear(NovaVenda(inici.AddDays(d), metodeId), [Linia(1333)]);

        var resum = await e.Caixa.Resum(inici, inici.AddDays(59));

        await using var db = bd.Context();
        long baseGuardada = db.VendaDesglossaments.Sum(d => (long)d.BaseCents);
        long ivaGuardat = db.VendaDesglossaments.Sum(d => (long)d.IvaCents);

        // Summed from the frozen rows, never recomputed from the lines: with 60 tickets
        // the two methods drift apart by cents (decision 6.4).
        resum.BaseCents.Should().Be(baseGuardada);
        resum.IvaCents.Should().Be(ivaGuardat);
        (resum.BaseCents + resum.IvaCents).Should().Be(resum.VendesCents);
    }

    [Fact] // K-08
    public async Task Tancar_i_reobrir_el_context_dona_les_mateixes_xifres()
    {
        await using var bd = new BaseDadesProva();
        var e = await Muntar(bd);
        int metodeId = await PrimerMetode(bd);

        for (int i = 0; i < 4; i++)
            await e.Vendes.Crear(NovaVenda(Dia, metodeId), [Linia(1750)]);

        var abans = await e.Caixa.Resum(Dia, Dia);

        // A fresh service graph over the same file: nothing may live only in memory
        var altre = await Muntar(bd);
        var despres = await altre.Caixa.Resum(Dia, Dia);

        despres.Should().BeEquivalentTo(abans);
    }

    [Fact] // K-09
    public async Task Un_dia_sense_activitat_dona_zeros_i_no_peta()
    {
        await using var bd = new BaseDadesProva();
        var e = await Muntar(bd);

        var resum = await e.Caixa.Resum(Dia, Dia);

        resum.VendesCents.Should().Be(0);
        resum.BalancCents.Should().Be(0);
        resum.BaseCents.Should().Be(0);
        resum.IvaCents.Should().Be(0);
        resum.DesglossamentIva.Should().BeEmpty();

        // No sales in the period means the share of work has no denominator: it must
        // read as "no data", never as 0 % (RF-17).
        int treballadoraId = await Afegeix(bd, Fes.Treballadora("Marta"));
        var detall = await e.Informes.DetallDeTreballadora(treballadoraId, Dia, Dia);
        detall.PercentatgeTreball.Should().BeNull();
        detall.PercentatgeProductes.Should().BeNull();
    }

    [Fact] // K-10
    public async Task Un_dia_amb_dos_tipus_d_IVA_els_separa_al_desglossament()
    {
        await using var bd = new BaseDadesProva();
        var e = await Muntar(bd);
        int metodeId = await PrimerMetode(bd);
        int serveiId = await Afegeix(bd, Fes.Servei("Tall", preuCents: 1500));
        int producteId = await Afegeix(bd, Fes.Producte("Xampú", preuCents: 900, ivaBp: 1000));

        await e.Vendes.Crear(NovaVenda(Dia, metodeId),
        [
            Linia(1500, ivaBp: 2100, serveiId: serveiId),
            Linia(900, ivaBp: 1000, producteId: producteId, descripcio: "Xampú")
        ]);

        var resum = await e.Caixa.Resum(Dia, Dia);

        resum.DesglossamentIva.Should().HaveCount(2);
        resum.VendesCents.Should().Be(2400);
        (resum.BaseCents + resum.IvaCents).Should().Be(resum.VendesCents);

        // Each rate is computed on its own group, then summed (decision 6.4 / CU-03b)
        resum.DesglossamentIva.Sum(d => d.TotalCents).Should().Be(2400);
    }
}
