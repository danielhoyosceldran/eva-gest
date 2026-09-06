using AwesomeAssertions;
using EvaGest.Helpers;
using EvaGest.Models;
using EvaGest.Services;
using EvaGest.Tests.Infra;
using EvaGest.ViewModels.Elements;
using Xunit;

namespace EvaGest.Tests.Serveis;

public class GraellaSetmanaViewModelTests
{
    private static readonly DateOnly Dilluns = new(2026, 9, 7);

    private sealed class Registre
    {
        public List<(DateOnly, TimeOnly)> Slots { get; } = [];
        public List<Cita> Cites { get; } = [];
    }

    private static (GraellaSetmanaViewModel graella, Registre registre) Muntar(
        BaseDadesProva bd, ModeGraella mode = ModeGraella.Agenda)
    {
        var factory = new FabricaDeProva(bd.Opcions);
        var registre = new Registre();
        var graella = new GraellaSetmanaViewModel(
            new CitaService(factory), new DisponibilitatService(factory), new ConfiguracioService(factory),
            mode,
            (d, h) => registre.Slots.Add((d, h)),
            c => registre.Cites.Add(c));
        return (graella, registre);
    }

    private static async Task AfegirHorari(BaseDadesProva bd, DiaSetmana dia, int obre, int tanca)
    {
        await using var db = bd.Context();
        db.HorariBarberia.Add(new HorariBarberia
        {
            DiaSetmana = dia,
            HoraObertura = new TimeOnly(obre, 0),
            HoraTancament = new TimeOnly(tanca, 0)
        });
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    private static async Task<int> AfegirCita(BaseDadesProva bd, DateOnly data, int hora, int minut,
        int durada = 30, EstatCita estat = EstatCita.Pendent, string nom = "Convidat")
    {
        await using var db = bd.Context();
        var cita = new Cita
        {
            Data = data, Hora = new TimeOnly(hora, minut), DuradaMin = durada,
            NomConvidat = nom, Estat = estat
        };
        db.Cites.Add(cita);
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        return cita.Id;
    }

    [Fact] // GV-01
    public async Task La_setmana_te_sempre_set_dies_a_partir_del_dilluns()
    {
        await using var bd = new BaseDadesProva();
        var (graella, _) = Muntar(bd);

        await graella.CarregarSetmana(Dilluns);

        graella.Dies.Should().HaveCount(7);
        graella.Dies[0].Data.Should().Be(Dilluns);
        graella.Dies[6].Data.Should().Be(Dilluns.AddDays(6));
    }

    [Fact] // GV-02
    public async Task Cada_cita_cau_a_la_columna_del_seu_dia()
    {
        await using var bd = new BaseDadesProva();
        await AfegirHorari(bd, DiaSetmana.Dl, 9, 20);
        await AfegirCita(bd, Dilluns, 10, 0, nom: "Anna");
        await AfegirCita(bd, Dilluns.AddDays(2), 11, 0, nom: "Berta");
        var (graella, _) = Muntar(bd);

        await graella.CarregarSetmana(Dilluns);

        graella.Dies[0].Cites.Should().ContainSingle().Which.Titol.Should().Be("Anna");
        graella.Dies[1].Cites.Should().BeEmpty();
        graella.Dies[2].Cites.Should().ContainSingle().Which.Titol.Should().Be("Berta");
    }

    [Fact] // GV-03
    public async Task Un_dia_tancat_es_marca_i_es_tapa_de_dalt_a_baix()
    {
        await using var bd = new BaseDadesProva();
        await AfegirHorari(bd, DiaSetmana.Dl, 9, 20);
        await using (var db = bd.Context())
        {
            db.DiesTancats.Add(new DiaTancat { Data = Dilluns, Motiu = "Festiu local" });
            await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        }
        var (graella, _) = Muntar(bd);

        await graella.CarregarSetmana(Dilluns);

        var dia = graella.Dies[0];
        dia.Tancat.Should().BeTrue();
        dia.MotiuTancat.Should().Be("Festiu local");
        dia.Bandes.Should().ContainSingle();
        dia.Bandes[0].Tipus.Should().Be(TipusBanda.DiaTancat);
        dia.Bandes[0].Alcada.Should().BeApproximately(graella.AlcadaTotalPx, 1e-9);
    }

    [Fact] // GV-04
    public async Task Un_torn_partit_deixa_la_banda_del_migdia()
    {
        await using var bd = new BaseDadesProva();
        await AfegirHorari(bd, DiaSetmana.Dl, 9, 13);
        await AfegirHorari(bd, DiaSetmana.Dl, 16, 20);
        var (graella, _) = Muntar(bd);

        await graella.CarregarSetmana(Dilluns);

        graella.MinutIniciGraella.Should().Be(9 * 60);
        graella.MinutFiGraella.Should().Be(20 * 60);
        graella.Dies[0].Bandes.Should().ContainSingle()
            .Which.Top.Should().BeApproximately(4 * 60 * graella.PixelsPerMinut, 1e-9);
    }

    [Fact] // GV-05
    public async Task Clicar_una_franja_buida_avisa_amb_el_dia_i_l_hora_ajustada()
    {
        await using var bd = new BaseDadesProva();
        await AfegirHorari(bd, DiaSetmana.Dc, 9, 20);
        var (graella, registre) = Muntar(bd);
        await graella.CarregarSetmana(Dilluns);

        // 90 minuts després de l'inici de la graella, a la columna de dimecres
        graella.Dies[2].ClicarAPosicio(90 * graella.PixelsPerMinut);

        registre.Slots.Should().ContainSingle()
            .Which.Should().Be((Dilluns.AddDays(2), new TimeOnly(10, 30)));
    }

    [Fact] // GV-06
    public async Task Les_cites_cancellades_no_ocupen_carril()
    {
        await using var bd = new BaseDadesProva();
        await AfegirHorari(bd, DiaSetmana.Dl, 9, 20);
        await AfegirCita(bd, Dilluns, 10, 0, estat: EstatCita.Cancellada, nom: "Anul·lada");
        await AfegirCita(bd, Dilluns, 10, 0, nom: "Vigent");
        var (graella, _) = Muntar(bd);

        await graella.CarregarSetmana(Dilluns);

        var vigent = graella.Dies[0].Cites.Single(c => c.Titol == "Vigent");
        vigent.NombreCarrils.Should().Be(1);
        vigent.CarrilIndex.Should().Be(0);
        graella.Dies[0].Cites.Single(c => c.Titol == "Anul·lada").EsCancellada.Should().BeTrue();
    }

    [Fact] // GV-07
    public async Task Dues_cites_solapades_es_reparteixen_la_columna()
    {
        await using var bd = new BaseDadesProva();
        await AfegirHorari(bd, DiaSetmana.Dl, 9, 20);
        await AfegirCita(bd, Dilluns, 10, 0, durada: 60, nom: "Anna");
        await AfegirCita(bd, Dilluns, 10, 30, durada: 60, nom: "Berta");
        var (graella, _) = Muntar(bd);

        await graella.CarregarSetmana(Dilluns);

        graella.Dies[0].Cites.Should().OnlyContain(c => c.NombreCarrils == 2);
        graella.Dies[0].Cites.Select(c => c.CarrilIndex).Should().BeEquivalentTo([0, 1]);
    }

    [Fact] // GV-08
    public async Task Una_cita_fora_d_horari_segueix_sent_visible()
    {
        await using var bd = new BaseDadesProva();
        await AfegirHorari(bd, DiaSetmana.Dl, 9, 20);
        await AfegirCita(bd, Dilluns, 8, 30, nom: "Matiner");
        var (graella, _) = Muntar(bd);

        await graella.CarregarSetmana(Dilluns);

        graella.MinutIniciGraella.Should().Be(8 * 60);
        graella.Dies[0].Cites.Single().Top.Should().BeApproximately(30 * graella.PixelsPerMinut, 1e-9);
    }

    [Fact] // GV-09
    public async Task La_granularitat_invalida_de_la_configuracio_cau_a_trenta()
    {
        await using var bd = new BaseDadesProva();
        var factory = new FabricaDeProva(bd.Opcions);
        await new ConfiguracioService(factory).Guardar(ClausConfig.MinutsSlotAgenda, "45");
        var (graella, _) = Muntar(bd);

        await graella.CarregarSetmana(Dilluns);

        graella.MinutsSlot.Should().Be(30);
    }

    [Fact] // GV-10
    public async Task La_granularitat_valida_de_la_configuracio_s_aplica()
    {
        await using var bd = new BaseDadesProva();
        var factory = new FabricaDeProva(bd.Opcions);
        await new ConfiguracioService(factory).Guardar(ClausConfig.MinutsSlotAgenda, "15");
        var (graella, _) = Muntar(bd);

        await graella.CarregarSetmana(Dilluns);

        graella.MinutsSlot.Should().Be(15);
        graella.PixelsPerMinut.Should().BeApproximately(graella.AlcadaSlotPx / 15, 1e-9);
    }

    [Fact] // GV-11
    public async Task Clicar_una_cita_avisa_amb_el_model_sencer()
    {
        await using var bd = new BaseDadesProva();
        await AfegirHorari(bd, DiaSetmana.Dl, 9, 20);
        int id = await AfegirCita(bd, Dilluns, 10, 0, nom: "Anna");
        var (graella, registre) = Muntar(bd);
        await graella.CarregarSetmana(Dilluns);

        graella.ClicCitaCommand.Execute(graella.Dies[0].Cites.Single());

        registre.Cites.Should().ContainSingle().Which.Id.Should().Be(id);
    }

    [Fact] // GV-12
    public async Task El_fantasma_es_mou_amb_la_data_i_l_hora()
    {
        await using var bd = new BaseDadesProva();
        await AfegirHorari(bd, DiaSetmana.Dl, 9, 20);
        await AfegirHorari(bd, DiaSetmana.Dc, 9, 20);
        var (graella, _) = Muntar(bd, ModeGraella.Selector);
        await graella.CarregarSetmana(Dilluns);

        graella.MostrarFantasma(Dilluns, new TimeOnly(10, 0), 30);
        graella.Dies[0].Cites.Should().ContainSingle(c => c.EsFantasma);

        graella.MostrarFantasma(Dilluns.AddDays(2), new TimeOnly(12, 0), 60);
        graella.Dies[0].Cites.Should().NotContain(c => c.EsFantasma);
        var fantasma = graella.Dies[2].Cites.Should().ContainSingle(c => c.EsFantasma).Subject;
        fantasma.Top.Should().BeApproximately(3 * 60 * graella.PixelsPerMinut, 1e-9);
        fantasma.Alcada.Should().BeApproximately(60 * graella.PixelsPerMinut - 1, 1e-9);
    }

    [Fact] // GV-13
    public async Task El_mode_selector_es_mes_compacte_que_l_agenda()
    {
        await using var bd = new BaseDadesProva();
        var (agenda, _) = Muntar(bd);
        var (selector, _) = Muntar(bd, ModeGraella.Selector);

        selector.AlcadaSlotPx.Should().BeLessThan(agenda.AlcadaSlotPx);
        selector.AmpladaReglaPx.Should().BeLessThan(agenda.AmpladaReglaPx);
    }

    [Fact] // GV-14
    public async Task Sense_horaris_ni_cites_la_graella_mostra_el_rang_per_defecte()
    {
        await using var bd = new BaseDadesProva();
        var (graella, _) = Muntar(bd);

        await graella.CarregarSetmana(Dilluns);

        (graella.MinutIniciGraella, graella.MinutFiGraella).Should().Be(GraellaHelper.RangPerDefecte);
        graella.Dies.Should().OnlyContain(d => d.Bandes.Count == 0,
            "ombrejar tota la setmana com a fora d'horari quan encara no hi ha horaris no diu res");
    }

    [Fact] // GV-16
    public async Task Amb_horaris_configurats_els_dies_sense_horari_si_que_s_ombregen()
    {
        await using var bd = new BaseDadesProva();
        await AfegirHorari(bd, DiaSetmana.Dl, 9, 20);   // només dilluns obre
        var (graella, _) = Muntar(bd);

        await graella.CarregarSetmana(Dilluns);

        graella.Dies[0].Bandes.Should().BeEmpty("dilluns obre tot el rang visible");
        graella.Dies[1].Bandes.Should().ContainSingle("dimarts no té horari");
    }

    [Fact] // GV-15
    public async Task Canviar_de_setmana_reaprofita_les_mateixes_columnes()
    {
        await using var bd = new BaseDadesProva();
        var (graella, _) = Muntar(bd);
        await graella.CarregarSetmana(Dilluns);
        var abans = graella.Dies.ToList();

        await graella.CarregarSetmana(Dilluns.AddDays(7));

        graella.Dies.Should().BeSameAs(graella.Dies);
        graella.Dies.Zip(abans).Should().OnlyContain(p => ReferenceEquals(p.First, p.Second));
        graella.Dies[0].Data.Should().Be(Dilluns.AddDays(7));
    }
}
