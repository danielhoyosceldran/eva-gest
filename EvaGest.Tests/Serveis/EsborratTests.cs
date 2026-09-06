using AwesomeAssertions;
using EvaGest.Models;
using EvaGest.Services;
using EvaGest.Tests.Infra;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace EvaGest.Tests.Serveis;

/// <summary>
/// The one rule every delete in the app follows: what nothing points at is removed, what
/// the history already names is deactivated instead, and the caller is told which of the
/// two happened. These tests exist because the difference is invisible from the UI —
/// both look like the row disappearing from the pickers.
/// </summary>
public class EsborratTests
{
    private static readonly DateOnly Avui = new(2026, 9, 7);

    private static CatalegService Cataleg(BaseDadesProva bd) => new(new FabricaDeProva(bd.Opcions));
    private static CitaService Cites(BaseDadesProva bd) => new(new FabricaDeProva(bd.Opcions));
    private static TreballadoraService Treballadores(BaseDadesProva bd) => new(new FabricaDeProva(bd.Opcions));

    private static VendaService Vendes(BaseDadesProva bd)
        => new(new FabricaDeProva(bd.Opcions), new ConfiguracioDeProva());

    // ── Serveis ─────────────────────────────────────────────────────────────

    [Fact]
    public async Task Servei_sense_us_selimina_de_debo()
    {
        await using var bd = new BaseDadesProva();
        var cataleg = Cataleg(bd);
        int id = await cataleg.CrearServei(Fes.Servei("Tall"));

        var resultat = await cataleg.EliminarServei(id);

        resultat.Should().Be(ResultatEsborrat.Eliminat);
        (await cataleg.ObtenirServei(id)).Should().BeNull();
    }

    [Fact]
    public async Task Servei_usat_en_una_cita_es_desactiva_i_la_cita_el_conserva()
    {
        await using var bd = new BaseDadesProva();
        var cataleg = Cataleg(bd);
        int id = await cataleg.CrearServei(Fes.Servei("Tall"));

        await using (var db = bd.Context())
        {
            db.Cites.Add(new Cita
            {
                Data = Avui, Hora = new TimeOnly(10, 0), DuradaMin = 30,
                NomConvidat = "Convidat", ServeiId = id
            });
            await db.SaveChangesAsync();
        }

        var resultat = await cataleg.EliminarServei(id);

        resultat.Should().Be(ResultatEsborrat.Desactivat);
        (await cataleg.ObtenirServei(id))!.Actiu.Should().BeFalse();

        // The appointment still says what it was booked for...
        await using var comprovacio = bd.Context();
        (await comprovacio.Cites.SingleAsync()).ServeiId.Should().Be(id);

        // ...but nobody can pick it again for a new one.
        (await cataleg.ObtenirServeis(nomesActius: true)).Should().BeEmpty();
    }

    [Fact]
    public async Task Servei_venut_es_desactiva_i_la_linia_de_venda_es_manté()
    {
        await using var bd = new BaseDadesProva();
        var cataleg = Cataleg(bd);
        int metode = await cataleg.CrearMetode("Efectiu");
        int id = await cataleg.CrearServei(Fes.Servei("Tall"));

        var linia = Fes.Linia(1500);
        linia.ServeiId = id;
        await Vendes(bd).Crear(Fes.Venda(Avui, metode, EstatVenda.Activa), [linia]);

        var resultat = await cataleg.EliminarServei(id);

        resultat.Should().Be(ResultatEsborrat.Desactivat);
        await using var db = bd.Context();
        (await db.VendaLinies.SingleAsync()).ServeiId.Should().Be(id);
    }

    // ── Productes ───────────────────────────────────────────────────────────

    [Fact]
    public async Task Producte_sense_vendes_selimina_i_venut_es_desactiva()
    {
        await using var bd = new BaseDadesProva();
        var cataleg = Cataleg(bd);
        int metode = await cataleg.CrearMetode("Efectiu");
        int mai = await cataleg.CrearProducte(Fes.Producte("Cera"));
        int venut = await cataleg.CrearProducte(Fes.Producte("Xampú"));

        var linia = Fes.Linia(900);
        linia.ProducteId = venut;
        await Vendes(bd).Crear(Fes.Venda(Avui, metode, EstatVenda.Activa), [linia]);

        (await cataleg.EliminarProducte(mai)).Should().Be(ResultatEsborrat.Eliminat);
        (await cataleg.EliminarProducte(venut)).Should().Be(ResultatEsborrat.Desactivat);

        (await cataleg.ObtenirProducte(mai)).Should().BeNull();
        (await cataleg.ObtenirProducte(venut))!.Actiu.Should().BeFalse();
    }

    // ── Mètodes de pagament ─────────────────────────────────────────────────

    [Fact]
    public async Task Ultim_metode_actiu_no_selimina()
    {
        await using var bd = new BaseDadesProva();
        var cataleg = Cataleg(bd);
        int efectiu = await cataleg.CrearMetode("Efectiu");

        (await cataleg.EliminarMetode(efectiu)).Should().Be(ResultatEsborrat.Bloquejat);
        (await cataleg.ObtenirMetodes()).Should().ContainSingle();
    }

    /// <summary>
    /// The sharp edge this whole feature exists for: the sale's foreign key cascades, so
    /// removing a used method would delete the sales along with it.
    /// </summary>
    [Fact]
    public async Task Metode_usat_en_una_venda_es_desactiva_i_no_sarrossega_la_venda()
    {
        await using var bd = new BaseDadesProva();
        var cataleg = Cataleg(bd);
        int efectiu = await cataleg.CrearMetode("Efectiu");
        await cataleg.CrearMetode("Targeta");

        await Vendes(bd).Crear(Fes.Venda(Avui, efectiu, EstatVenda.Activa), [Fes.Linia(1500)]);

        var resultat = await cataleg.EliminarMetode(efectiu);

        resultat.Should().Be(ResultatEsborrat.Desactivat);
        await using var db = bd.Context();
        (await db.Vendes.CountAsync()).Should().Be(1);
        (await db.MetodesPagament.SingleAsync(m => m.Id == efectiu)).Actiu.Should().BeFalse();
    }

    [Fact]
    public async Task Metode_no_usat_selimina_si_en_queda_un_altre_actiu()
    {
        await using var bd = new BaseDadesProva();
        var cataleg = Cataleg(bd);
        await cataleg.CrearMetode("Efectiu");
        int targeta = await cataleg.CrearMetode("Targeta");

        (await cataleg.EliminarMetode(targeta)).Should().Be(ResultatEsborrat.Eliminat);
        (await cataleg.ObtenirMetodes()).Should().ContainSingle(m => m.Nom == "Efectiu");
    }

    [Fact]
    public async Task Metode_usat_en_un_moviment_de_caixa_es_desactiva()
    {
        await using var bd = new BaseDadesProva();
        var cataleg = Cataleg(bd);
        int efectiu = await cataleg.CrearMetode("Efectiu");
        await cataleg.CrearMetode("Targeta");

        var caixa = new CaixaService(new FabricaDeProva(bd.Opcions));
        await caixa.Crear(new MovimentCaixa
        {
            Data = Avui, Tipus = TipusMoviment.Sortida, ImportCents = 2000,
            MetodePagamentId = efectiu, Concepte = "Compra material"
        });

        (await cataleg.EliminarMetode(efectiu)).Should().Be(ResultatEsborrat.Desactivat);

        await using var db = bd.Context();
        (await db.MovimentsCaixa.CountAsync()).Should().Be(1);
    }

    // ── Treballadores ───────────────────────────────────────────────────────

    [Fact]
    public async Task Treballadora_sense_historial_selimina_amb_el_seu_horari()
    {
        await using var bd = new BaseDadesProva();
        var treballadores = Treballadores(bd);

        var creada = await treballadores.Crear(Fes.Treballadora("Marta"),
            new Dictionary<DiaSetmana, List<(TimeOnly, TimeOnly)>>
            {
                [DiaSetmana.Dl] = [(new TimeOnly(9, 0), new TimeOnly(14, 0))]
            });

        (await treballadores.Eliminar(creada.Id)).Should().Be(ResultatEsborrat.Eliminat);

        await using var db = bd.Context();
        (await db.Treballadores.CountAsync()).Should().Be(0);
        (await db.HorarisTreballadora.CountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task Treballadora_amb_cites_es_desactiva()
    {
        await using var bd = new BaseDadesProva();
        var treballadores = Treballadores(bd);

        var creada = await treballadores.Crear(Fes.Treballadora("Marta"),
            new Dictionary<DiaSetmana, List<(TimeOnly, TimeOnly)>>());

        await Cites(bd).Crear(new Cita
        {
            Data = Avui, Hora = new TimeOnly(10, 0), DuradaMin = 30,
            NomConvidat = "Convidat", TreballadoraId = creada.Id
        });

        (await treballadores.Eliminar(creada.Id)).Should().Be(ResultatEsborrat.Desactivat);

        (await treballadores.ObtenirTotes()).Should().ContainSingle(t => !t.Actiu);
        (await treballadores.ObtenirTotes(nomesActives: true)).Should().BeEmpty();
    }

    // ── Cites ───────────────────────────────────────────────────────────────

    [Fact]
    public async Task Cita_sense_venda_selimina()
    {
        await using var bd = new BaseDadesProva();
        var cites = Cites(bd);
        int id = await cites.Crear(new Cita
        {
            Data = Avui, Hora = new TimeOnly(10, 0), DuradaMin = 30, NomConvidat = "Convidat"
        });

        (await cites.Eliminar(id)).Should().Be(ResultatEsborrat.Eliminat);
        (await cites.ObtenirPerId(id)).Should().BeNull();
    }

    [Fact]
    public async Task Cita_amb_venda_associada_no_selimina()
    {
        await using var bd = new BaseDadesProva();
        var cites = Cites(bd);
        var cataleg = Cataleg(bd);
        int metode = await cataleg.CrearMetode("Efectiu");

        int citaId = await cites.Crear(new Cita
        {
            Data = Avui, Hora = new TimeOnly(10, 0), DuradaMin = 30,
            NomConvidat = "Convidat", Estat = EstatCita.Realitzada
        });

        var venda = Fes.Venda(Avui, metode, EstatVenda.Activa);
        venda.CitaId = citaId;
        await Vendes(bd).Crear(venda, [Fes.Linia(1500)]);

        (await cites.Eliminar(citaId)).Should().Be(ResultatEsborrat.Bloquejat);
        (await cites.ObtenirPerId(citaId)).Should().NotBeNull();
    }

    // ── Vendes ──────────────────────────────────────────────────────────────

    [Fact]
    public async Task Eliminar_una_venda_activa_lanulla_i_eliminar_la_anullada_lesborra()
    {
        await using var bd = new BaseDadesProva();
        var cataleg = Cataleg(bd);
        var vendes = Vendes(bd);
        int metode = await cataleg.CrearMetode("Efectiu");

        int id = await vendes.Crear(Fes.Venda(Avui, metode, EstatVenda.Activa), [Fes.Linia(1500)]);

        (await vendes.Eliminar(id)).Should().Be(ResultatEsborrat.Desactivat);
        (await vendes.ObtenirPerId(id))!.Estat.Should().Be(EstatVenda.Anullada);

        (await vendes.Eliminar(id)).Should().Be(ResultatEsborrat.Eliminat);
        (await vendes.ObtenirPerId(id)).Should().BeNull();

        await using var db = bd.Context();
        (await db.VendaLinies.CountAsync()).Should().Be(0);
        (await db.VendaDesglossaments.CountAsync()).Should().Be(0);
    }
}
