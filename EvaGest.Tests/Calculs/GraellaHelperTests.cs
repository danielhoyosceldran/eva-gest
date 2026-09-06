using AwesomeAssertions;
using EvaGest.Helpers;
using Xunit;

namespace EvaGest.Tests.Calculs;

public class GraellaHelperTests
{
    private const double AlcadaSlot = 44;
    private static readonly int Nou = 9 * 60;

    // ---------- Geometria vertical ----------

    [Theory] // G-01
    [InlineData(15, 44 / 15.0)]
    [InlineData(30, 44 / 30.0)]
    [InlineData(60, 44 / 60.0)]
    public void PixelsPerMinut_depen_de_la_granularitat(int minutsSlot, double esperat)
        => GraellaHelper.PixelsPerMinut(AlcadaSlot, minutsSlot).Should().BeApproximately(esperat, 1e-9);

    [Fact] // G-02
    public void Top_situa_les_deu_a_una_franja_de_distancia_de_les_nou()
    {
        double ppm = GraellaHelper.PixelsPerMinut(AlcadaSlot, 30);
        GraellaHelper.Top(new TimeOnly(10, 0), Nou, ppm).Should().BeApproximately(88, 1e-9);
        GraellaHelper.Top(new TimeOnly(9, 0), Nou, ppm).Should().Be(0);
    }

    [Fact] // G-03
    public void Alcada_es_proporcional_a_la_durada_menys_la_separacio()
    {
        double ppm = GraellaHelper.PixelsPerMinut(AlcadaSlot, 30);
        GraellaHelper.Alcada(45, ppm, 18).Should().BeApproximately(66 - 1, 1e-9);
    }

    [Fact] // G-04
    public void Alcada_mai_baixa_del_minim_clicable()
    {
        double ppm = GraellaHelper.PixelsPerMinut(AlcadaSlot, 60);
        GraellaHelper.Alcada(5, ppm, 18).Should().BeApproximately(18 - 1, 1e-9);
    }

    // ---------- Franja des d'una posició ----------

    [Fact] // G-05
    public void SlotDesDeY_a_la_frontera_exacta_cau_a_la_franja_que_comenca
        () {
        double ppm = GraellaHelper.PixelsPerMinut(AlcadaSlot, 30);
        GraellaHelper.SlotDesDeY(0, Nou, ppm, 30).Should().Be(new TimeOnly(9, 0));
        GraellaHelper.SlotDesDeY(44, Nou, ppm, 30).Should().Be(new TimeOnly(9, 30));
        GraellaHelper.SlotDesDeY(88, Nou, ppm, 30).Should().Be(new TimeOnly(10, 0));
    }

    [Fact] // G-06
    public void SlotDesDeY_a_mig_slot_ajusta_cap_avall()
    {
        double ppm = GraellaHelper.PixelsPerMinut(AlcadaSlot, 30);
        GraellaHelper.SlotDesDeY(43.9, Nou, ppm, 30).Should().Be(new TimeOnly(9, 0));
        GraellaHelper.SlotDesDeY(60, Nou, ppm, 30).Should().Be(new TimeOnly(9, 30));
    }

    [Theory] // G-07
    [InlineData(15, 9, 15)]
    [InlineData(30, 9, 0)]
    [InlineData(60, 9, 0)]
    public void SlotDesDeY_ajusta_a_la_granularitat_configurada(int minutsSlot, int horaEsperada, int minutEsperat)
    {
        double ppm = GraellaHelper.PixelsPerMinut(AlcadaSlot, minutsSlot);
        // 20 minuts després de l'inici de la graella
        GraellaHelper.SlotDesDeY(20 * ppm, Nou, ppm, minutsSlot)
            .Should().Be(new TimeOnly(horaEsperada, minutEsperat));
    }

    [Fact] // G-08
    public void SlotDesDeY_mes_enlla_del_final_del_dia_es_limita()
    {
        double ppm = GraellaHelper.PixelsPerMinut(AlcadaSlot, 30);
        GraellaHelper.SlotDesDeY(100_000, Nou, ppm, 30).Should().Be(new TimeOnly(23, 30));
    }

    // ---------- Rang visible ----------

    [Fact] // G-09
    public void RangVisible_amb_torn_partit_cobreix_de_la_primera_obertura_al_darrer_tancament()
    {
        var franges = new[] { (new TimeOnly(9, 0), new TimeOnly(13, 0)), (new TimeOnly(16, 0), new TimeOnly(20, 0)) };
        GraellaHelper.RangVisible(franges, []).Should().Be((9 * 60, 20 * 60));
    }

    [Fact] // G-10
    public void RangVisible_s_eixampla_per_una_cita_fora_d_horari_per_dalt()
    {
        var franges = new[] { (new TimeOnly(9, 0), new TimeOnly(20, 0)) };
        var cites = new[] { (new TimeOnly(8, 30), 30) };
        GraellaHelper.RangVisible(franges, cites).Should().Be((8 * 60, 20 * 60));
    }

    [Fact] // G-11
    public void RangVisible_s_eixampla_per_una_cita_fora_d_horari_per_baix()
    {
        var franges = new[] { (new TimeOnly(9, 0), new TimeOnly(20, 0)) };
        var cites = new[] { (new TimeOnly(20, 15), 45) };
        GraellaHelper.RangVisible(franges, cites).Should().Be((9 * 60, 21 * 60));
    }

    [Fact] // G-12
    public void RangVisible_tracta_mitjanit_com_a_final_del_dia()
    {
        var franges = new[] { (new TimeOnly(18, 0), TimeOnly.MinValue) };
        GraellaHelper.RangVisible(franges, []).Should().Be((18 * 60, 24 * 60));
    }

    [Fact] // G-13
    public void RangVisible_sense_horaris_ni_cites_cau_al_recanvi()
    {
        GraellaHelper.RangVisible([], []).Should().Be(GraellaHelper.RangPerDefecte);
    }

    [Fact] // G-14
    public void RangVisible_mai_torna_una_graella_de_menys_d_una_hora()
    {
        var cites = new[] { (new TimeOnly(10, 10), 5) };
        var (inici, fi) = GraellaHelper.RangVisible([], cites);
        (fi - inici).Should().BeGreaterThanOrEqualTo(60);
    }

    // ---------- Bandes fora d'horari ----------

    [Fact] // G-15
    public void BandesForaHorari_sense_franges_tapa_tot_el_dia()
    {
        GraellaHelper.BandesForaHorari([], 9 * 60, 20 * 60)
            .Should().ContainSingle().Which.Should().Be((9 * 60, 20 * 60));
    }

    [Fact] // G-16
    public void BandesForaHorari_amb_una_franja_dona_dues_bandes()
    {
        var franges = new[] { (new TimeOnly(10, 0), new TimeOnly(18, 0)) };
        GraellaHelper.BandesForaHorari(franges, 9 * 60, 20 * 60)
            .Should().Equal((9 * 60, 10 * 60), (18 * 60, 20 * 60));
    }

    [Fact] // G-17
    public void BandesForaHorari_amb_torn_partit_dona_la_banda_del_migdia()
    {
        var franges = new[] { (new TimeOnly(9, 0), new TimeOnly(13, 0)), (new TimeOnly(16, 0), new TimeOnly(20, 0)) };
        GraellaHelper.BandesForaHorari(franges, 9 * 60, 20 * 60)
            .Should().ContainSingle().Which.Should().Be((13 * 60, 16 * 60));
    }

    [Fact] // G-18
    public void BandesForaHorari_amb_una_franja_mes_ampla_que_el_rang_no_dona_cap_banda()
    {
        var franges = new[] { (new TimeOnly(6, 0), new TimeOnly(23, 0)) };
        GraellaHelper.BandesForaHorari(franges, 9 * 60, 20 * 60).Should().BeEmpty();
    }

    // ---------- Repartiment de carrils ----------

    private static BlocTemporal Bloc(int horaInici, int minutInici, int durada)
    {
        int inici = horaInici * 60 + minutInici;
        return new BlocTemporal(inici, inici + durada);
    }

    [Fact] // G-19
    public void RepartirCarrils_sense_blocs_no_peta()
        => GraellaHelper.RepartirCarrils([]).Should().BeEmpty();

    [Fact] // G-20
    public void RepartirCarrils_sense_solapament_deixa_tothom_al_carril_zero()
    {
        var blocs = new[] { Bloc(10, 0, 30), Bloc(12, 0, 30), Bloc(15, 0, 30) };
        GraellaHelper.RepartirCarrils(blocs).Should().AllBeEquivalentTo(new AssignacioCarril(0, 1));
    }

    [Fact] // G-21
    public void RepartirCarrils_dos_blocs_solapats_es_reparteixen_la_columna()
    {
        var blocs = new[] { Bloc(10, 0, 60), Bloc(10, 30, 60) };
        GraellaHelper.RepartirCarrils(blocs)
            .Should().Equal(new AssignacioCarril(0, 2), new AssignacioCarril(1, 2));
    }

    [Fact] // G-22
    public void RepartirCarrils_una_cadena_forma_un_sol_clus_ter()
    {
        // A(10-11) B(10:30-11:30) C(11-12): C solapa amb B, així que tots comparteixen
        // amplada. Només calen 2 carrils —C reaprofita el d'A— i tots tres han de tenir
        // el mateix Total, altrament A i C es dibuixarien més amples que B.
        var blocs = new[] { Bloc(10, 0, 60), Bloc(10, 30, 60), Bloc(11, 0, 60) };
        var r = GraellaHelper.RepartirCarrils(blocs);
        r.Should().OnlyContain(a => a.Total == 2);
        r.Select(a => a.Index).Should().Equal(0, 1, 0);
    }

    [Fact] // G-23
    public void RepartirCarrils_blocs_que_nomes_es_toquen_no_solapen()
    {
        // A acaba exactament quan comença B: dos clústers independents, columna sencera cadascun
        var blocs = new[] { Bloc(10, 0, 60), Bloc(11, 0, 60) };
        GraellaHelper.RepartirCarrils(blocs).Should().AllBeEquivalentTo(new AssignacioCarril(0, 1));
    }

    [Fact] // G-24
    public void RepartirCarrils_un_bloc_niat_ocupa_un_carril_propi()
    {
        var blocs = new[] { Bloc(10, 0, 120), Bloc(10, 30, 30) };
        GraellaHelper.RepartirCarrils(blocs)
            .Should().Equal(new AssignacioCarril(0, 2), new AssignacioCarril(1, 2));
    }

    [Fact] // G-25
    public void RepartirCarrils_tres_blocs_identics_ocupen_tres_carrils()
    {
        var blocs = new[] { Bloc(10, 0, 30), Bloc(10, 0, 30), Bloc(10, 0, 30) };
        var r = GraellaHelper.RepartirCarrils(blocs);
        r.Should().OnlyContain(a => a.Total == 3);
        r.Select(a => a.Index).Should().BeEquivalentTo([0, 1, 2]);
    }

    [Fact] // G-26
    public void RepartirCarrils_un_clus_ter_no_aprima_els_altres()
    {
        // Solapament triple a les 10:00 + una cita solitària a les 18:00
        var blocs = new[] { Bloc(10, 0, 60), Bloc(10, 0, 60), Bloc(10, 0, 60), Bloc(18, 0, 30) };
        var r = GraellaHelper.RepartirCarrils(blocs);
        r[0].Total.Should().Be(3);
        r[3].Should().Be(new AssignacioCarril(0, 1));
    }

    [Fact] // G-27
    public void RepartirCarrils_amb_durada_zero_no_es_menja_un_carril_per_sempre()
    {
        var blocs = new[] { new BlocTemporal(600, 600), Bloc(11, 0, 30) };
        GraellaHelper.RepartirCarrils(blocs).Should().AllBeEquivalentTo(new AssignacioCarril(0, 1));
    }

    // ---------- Granularitat ----------

    [Theory] // G-28
    [InlineData(15, 15)]
    [InlineData(30, 30)]
    [InlineData(60, 60)]
    [InlineData(45, 30)]
    [InlineData(0, 30)]
    [InlineData(-5, 30)]
    [InlineData(1440, 30)]
    public void MinutsSlotValid_nomes_accepta_quinze_trenta_o_seixanta(int entrada, int esperat)
        => GraellaHelper.MinutsSlotValid(entrada).Should().Be(esperat);
}
