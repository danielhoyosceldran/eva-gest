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
    public void Un_dia_tancat_no_dona_cap_franja_ni_cap_error()
    {
        var r = HorariHelper.Comprovar(obert: false, "qualsevol", "cosa", "", "");
        r.EsValid.Should().BeTrue();
        r.Franges.Should().BeEmpty();
    }

    [Fact] // H-04
    public void Horari_seguit_dona_una_sola_franja()
    {
        var r = HorariHelper.Comprovar(true, "09:00", "20:00", "", "");
        r.EsValid.Should().BeTrue();
        r.Franges.Should().ContainSingle()
            .Which.Should().Be((new TimeOnly(9, 0), new TimeOnly(20, 0)));
    }

    [Fact] // H-05
    public void Torn_partit_dona_dues_franges()
    {
        var r = HorariHelper.Comprovar(true, "09:00", "13:00", "16:00", "20:00");
        r.EsValid.Should().BeTrue();
        r.Franges.Should().Equal(
            (new TimeOnly(9, 0), new TimeOnly(13, 0)),
            (new TimeOnly(16, 0), new TimeOnly(20, 0)));
    }

    [Fact] // H-06
    public void Un_dia_obert_sense_hores_es_un_error()
        => HorariHelper.Comprovar(true, "", "", "", "").Error.Should().NotBeNull();

    [Fact] // H-07
    public void La_franja_no_pot_acabar_abans_de_comencar()
    {
        HorariHelper.Comprovar(true, "20:00", "09:00", "", "").Error.Should().NotBeNull();
        HorariHelper.Comprovar(true, "09:00", "09:00", "", "").Error.Should().NotBeNull();
    }

    [Fact] // H-08
    public void Una_segona_franja_a_mitges_es_un_error_i_no_s_ignora_en_silenci()
    {
        HorariHelper.Comprovar(true, "09:00", "13:00", "16:00", "").Error.Should().NotBeNull();
        HorariHelper.Comprovar(true, "09:00", "13:00", "", "20:00").Error.Should().NotBeNull();
    }

    [Fact] // H-09
    public void La_segona_franja_no_pot_encavalcar_se_amb_la_primera()
        => HorariHelper.Comprovar(true, "09:00", "14:00", "13:00", "20:00").Error.Should().NotBeNull();

    [Fact] // H-10
    public void Les_franges_poden_tocar_se_sense_ser_un_error()
        => HorariHelper.Comprovar(true, "09:00", "14:00", "14:00", "20:00").EsValid.Should().BeTrue();

    [Fact] // H-11
    public void Format_torna_sempre_hores_de_dues_xifres()
        => HorariHelper.Format(new TimeOnly(9, 5)).Should().Be("09:05");
}
