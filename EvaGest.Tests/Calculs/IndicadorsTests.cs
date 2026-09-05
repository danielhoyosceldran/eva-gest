using AwesomeAssertions;
using EvaGest.Services;
using Xunit;

namespace EvaGest.Tests.Calculs;

public class IndicadorsTests
{
    [Fact] // I-01
    public void MitjanaPerVisita_zero_visites_es_null()
        => Indicadors.MitjanaPerVisita(0, 0).Should().BeNull();

    [Fact] // I-02
    public void FrequenciaDies_una_visita_es_null()
        => Indicadors.FrequenciaDies([new DateOnly(2026, 1, 1)]).Should().BeNull();

    [Fact] // I-03
    public void FrequenciaDies_dues_visites_separades_21_dies()
    {
        var dates = new[] { new DateOnly(2026, 1, 1), new DateOnly(2026, 1, 22) };
        Indicadors.FrequenciaDies(dates).Should().Be(21.0);
    }

    [Fact] // I-04
    public void FrequenciaDies_tres_visites_21_i_21_dies()
    {
        var dates = new[]
        {
            new DateOnly(2026, 1, 1),
            new DateOnly(2026, 1, 22),
            new DateOnly(2026, 2, 12)
        };
        Indicadors.FrequenciaDies(dates).Should().Be(21.0);
    }

    [Fact] // I-05
    public void PercentatgeTreball_periode_sense_vendes_es_null()
        => Indicadors.PercentatgeTreball(0, 0).Should().BeNull();

    [Fact] // I-06
    public void PercentatgeProductes_treballadora_sense_vendes_es_null()
        => Indicadors.PercentatgeProductes(500, 0).Should().BeNull();

    [Fact] // I-07
    public void PercentatgeTreball_de_dues_treballadores_suma_100()
    {
        int total = 10_000;
        var p1 = Indicadors.PercentatgeTreball(6_000, total)!.Value;
        var p2 = Indicadors.PercentatgeTreball(4_000, total)!.Value;
        (p1 + p2).Should().Be(100m);
    }
}
