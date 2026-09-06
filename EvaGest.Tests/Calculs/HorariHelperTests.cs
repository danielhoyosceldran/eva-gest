using AwesomeAssertions;
using EvaGest.Helpers;
using Xunit;

namespace EvaGest.Tests.Calculs;

public class HorariHelperTests
{
    [Theory] // H-01
    [InlineData("09:00", 9, 0)]
    [InlineData("9:00", 9, 0)]
    [InlineData("9", 9, 0)]
    [InlineData("0930", 9, 30)]
    [InlineData("9.30", 9, 30)]
    [InlineData("  09:30  ", 9, 30)]
    [InlineData("20:15", 20, 15)]
    public void Analitzar_accepta_les_formes_que_la_gent_escriu(string text, int hora, int minut)
        => HorariHelper.Analitzar(text).Should().Be(new TimeOnly(hora, minut));

    [Theory] // H-02
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    [InlineData("25:00")]
    [InlineData("09:70")]
    [InlineData("matí")]
    public void Analitzar_rebutja_el_que_no_es_una_hora(string? text)
        => HorariHelper.Analitzar(text).Should().BeNull();

    [Fact] // H-03
    public void Un_dia_sense_cap_torn_no_dona_cap_franja_ni_cap_error()
    {
        var r = HorariHelper.Comprovar(false, "qualsevol", "cosa", false, "", "");
        r.EsValid.Should().BeTrue();
        r.Franges.Should().BeEmpty();
    }

    [Fact] // H-04
    public void Nomes_mati_dona_una_sola_franja()
    {
        var r = HorariHelper.Comprovar(true, "09:00", "20:00", false, "", "");
        r.EsValid.Should().BeTrue();
        r.Franges.Should().ContainSingle()
            .Which.Should().Be((new TimeOnly(9, 0), new TimeOnly(20, 0)));
    }

    [Fact] // H-05
    public void Mati_i_tarda_donen_dues_franges()
    {
        var r = HorariHelper.Comprovar(true, "09:00", "13:00", true, "16:00", "20:00");
        r.EsValid.Should().BeTrue();
        r.Franges.Should().Equal(
            (new TimeOnly(9, 0), new TimeOnly(13, 0)),
            (new TimeOnly(16, 0), new TimeOnly(20, 0)));
    }

    [Fact] // H-06
    public void Un_torn_marcat_sense_hores_es_un_error()
    {
        HorariHelper.Comprovar(true, "", "", false, "", "").Error.Should().NotBeNull();
        HorariHelper.Comprovar(false, "", "", true, "", "").Error.Should().NotBeNull();
    }

    [Fact] // H-07
    public void La_franja_no_pot_acabar_abans_de_comencar()
    {
        HorariHelper.Comprovar(true, "20:00", "09:00", false, "", "").Error.Should().NotBeNull();
        HorariHelper.Comprovar(true, "09:00", "09:00", false, "", "").Error.Should().NotBeNull();
        HorariHelper.Comprovar(false, "", "", true, "20:00", "16:00").Error.Should().NotBeNull();
    }

    [Fact] // H-08
    public void Una_tarda_marcada_a_mitges_es_un_error_i_no_s_ignora_en_silenci()
    {
        HorariHelper.Comprovar(true, "09:00", "13:00", true, "16:00", "").Error.Should().NotBeNull();
        HorariHelper.Comprovar(true, "09:00", "13:00", true, "", "20:00").Error.Should().NotBeNull();
    }

    [Fact] // H-09
    public void La_tarda_no_pot_encavalcar_se_amb_el_mati()
        => HorariHelper.Comprovar(true, "09:00", "14:00", true, "13:00", "20:00").Error.Should().NotBeNull();

    [Fact] // H-10
    public void Les_franges_poden_tocar_se_sense_ser_un_error()
        => HorariHelper.Comprovar(true, "09:00", "14:00", true, "14:00", "20:00").EsValid.Should().BeTrue();

    [Fact] // H-11
    public void Format_torna_sempre_hores_de_dues_xifres()
        => HorariHelper.Format(new TimeOnly(9, 5)).Should().Be("09:05");

    [Fact] // H-12
    public void Nomes_tarda_dona_la_franja_de_tarda_sola()
    {
        var r = HorariHelper.Comprovar(false, "", "", true, "16:00", "20:00");
        r.EsValid.Should().BeTrue();
        r.Franges.Should().ContainSingle()
            .Which.Should().Be((new TimeOnly(16, 0), new TimeOnly(20, 0)));
    }

    [Fact] // H-13
    public void Les_hores_d_un_torn_desmarcat_no_es_validen()
    {
        // Unticking a shift has to be enough; nobody should have to clear its boxes too.
        var r = HorariHelper.Comprovar(true, "09:00", "14:00", false, "20:00", "16:00");
        r.EsValid.Should().BeTrue();
        r.Franges.Should().ContainSingle();
    }

    [Fact] // H-14
    public void El_selector_ofereix_els_quarts_d_hora_del_dia_de_feina()
    {
        HorariHelper.Slots.Should().StartWith(["06:00", "06:15", "06:30", "06:45", "07:00"]);
        HorariHelper.Slots.Should().EndWith(["23:00"]);
        HorariHelper.Slots.Should().Contain("09:00").And.Contain("20:30");
    }
}
