using AwesomeAssertions;
using EvaGest.Services;
using Xunit;

namespace EvaGest.Tests.Calculs;

public class DinersTests
{
    [Fact] // C-01
    public void TryParse_enter() => Assert(15, "15", 1500);

    [Fact] // C-02
    public void TryParse_coma_decimal() => Assert(15, "15,50", 1550);

    [Fact] // C-03
    public void TryParse_punt_decimal() => Assert(15, "15.50", 1550);

    [Fact] // C-04
    public void TryParse_separador_milers()
    {
        Diners.TryParse("1.234,56", out int cents).Should().BeTrue();
        cents.Should().Be(123456);
    }

    [Fact] // C-05
    public void TryParse_espais_i_simbol_euro()
    {
        Diners.TryParse("  15,00 €  ", out int cents).Should().BeTrue();
        cents.Should().Be(1500);
    }

    [Fact] // C-06
    public void TryParse_decimals_petits()
    {
        Diners.TryParse("0,05", out int cents).Should().BeTrue();
        cents.Should().Be(5);
    }

    [Fact] // C-07
    public void TryParse_text_invalid_o_buit()
    {
        Diners.TryParse("abc", out _).Should().BeFalse();
        Diners.TryParse("", out _).Should().BeFalse();
    }

    [Fact] // C-08
    public void Format_amb_coma_decimal()
        => Diners.Format(1500).Should().Contain("15,00");

    [Fact] // C-09
    public void FormatExport_sense_simbol()
        => Diners.FormatExport(1500).Should().Be("15,00");

    [Fact] // C-10
    public void Anada_i_tornada_es_estable()
    {
        var random = new Random(42);
        for (int i = 0; i < 500; i++)
        {
            int original = random.Next(0, 10_000_000);
            Diners.TryParse(Diners.FormatExport(original), out int recuperat).Should().BeTrue();
            recuperat.Should().Be(original);
        }
    }

    [Fact] // C-11
    public void Percentatges_format()
    {
        Percentatges.Format(2100).Should().Be("21 %");
        Percentatges.Format(520).Should().Be("5,2 %");
    }

    private static void Assert(int _, string text, int esperat)
    {
        Diners.TryParse(text, out int cents).Should().BeTrue();
        cents.Should().Be(esperat);
    }
}
