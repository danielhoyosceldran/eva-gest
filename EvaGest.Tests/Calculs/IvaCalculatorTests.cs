using AwesomeAssertions;
using EvaGest.Models;
using EvaGest.Services;
using Xunit;

namespace EvaGest.Tests.Calculs;

public class IvaCalculatorTests
{
    private static VendaLinia Linia(int importCents, int ivaBp = 2100, int quantitat = 1)
        => new()
        {
            Descripcio = "Prova",
            Quantitat = quantitat,
            PreuUnitariCents = importCents / quantitat,
            IvaBp = ivaBp,
            ImportCents = importCents
        };

    [Fact] // A-01
    public void Linia_15_al_21_percent_inclos()
    {
        var r = IvaCalculator.Calcular([Linia(1500, 2100)], IvaMode.Inclos);
        r.BaseCents.Should().Be(1240);
        r.IvaCents.Should().Be(260);
        r.TotalCents.Should().Be(1500);
    }

    [Fact] // A-02
    public void Linia_15_al_21_percent_no_inclos()
    {
        var r = IvaCalculator.Calcular([Linia(1500, 2100)], IvaMode.NoInclos);
        r.BaseCents.Should().Be(1500);
        r.IvaCents.Should().Be(315);
        r.TotalCents.Should().Be(1815);
    }

    [Fact] // A-03
    public void Tres_linies_de_10_al_21_inclos_no_arrodoneix_per_linia()
    {
        var linies = new[] { Linia(1000), Linia(1000), Linia(1000) };
        var r = IvaCalculator.Calcular(linies, IvaMode.Inclos);
        r.BaseCents.Should().Be(2479);
        r.IvaCents.Should().Be(521);
    }

    [Fact] // A-04
    public void Tipus_mixtos_es_calculen_per_separat_i_se_sumen()
    {
        var linies = new[] { Linia(1500, 2100), Linia(1000, 1000) };
        var perTipus = IvaCalculator.CalcularPerTipus(linies, IvaMode.NoInclos);
        perTipus.Should().HaveCount(2);

        var r = IvaCalculator.Calcular(linies, IvaMode.NoInclos);
        r.BaseCents.Should().Be(perTipus.Sum(p => p.BaseCents));
        r.IvaCents.Should().Be(perTipus.Sum(p => p.IvaCents));
    }

    [Fact] // A-05
    public void Venda_import_zero_no_peta()
    {
        var r = IvaCalculator.Calcular([Linia(0, 2100)], IvaMode.Inclos);
        r.BaseCents.Should().Be(0);
        r.IvaCents.Should().Be(0);
        r.TotalCents.Should().Be(0);
    }

    [Fact] // A-06
    public void Import_dun_centim_al_21_percent()
    {
        var r = IvaCalculator.Calcular([Linia(1, 2100)], IvaMode.Inclos);
        r.BaseCents.Should().Be(1);
        r.IvaCents.Should().Be(0);
    }

    [Fact] // A-07
    public void Tipus_iva_zero_percent()
    {
        var r = IvaCalculator.Calcular([Linia(1500, 0)], IvaMode.Inclos);
        r.BaseCents.Should().Be(1500);
        r.IvaCents.Should().Be(0);
        r.TotalCents.Should().Be(1500);
    }

    [Fact] // A-08
    public void Recarrec_equivalencia_5_2_percent_sense_perdua_precisio()
    {
        var r = IvaCalculator.Calcular([Linia(1500, 520)], IvaMode.NoInclos);
        (r.BaseCents + r.IvaCents).Should().Be(r.TotalCents);
    }

    [Fact] // A-09
    public void Import_gran_un_milio_euros()
    {
        var r = IvaCalculator.Calcular([Linia(100_000_000, 2100)], IvaMode.Inclos);
        r.BaseCents.Should().Be(82644628);
        r.IvaCents.Should().Be(17355372);
    }

    [Fact] // A-10
    public void Quantitat_superior_a_u()
    {
        var linia = Linia(2700, 2100, quantitat: 3);
        linia.ImportCents.Should().Be(2700);
        linia.PreuUnitariCents.Should().Be(900);
    }

    [Fact] // A-11
    public void Invariant_base_mes_iva_igual_total_en_combinacions_aleatories()
    {
        var random = new Random(12345);
        int[] tipus = [2100, 1000, 400, 520, 0];

        for (int i = 0; i < 1000; i++)
        {
            var mode = i % 2 == 0 ? IvaMode.Inclos : IvaMode.NoInclos;
            var linies = Enumerable.Range(0, random.Next(1, 6))
                .Select(_ => Linia(random.Next(0, 500_000), tipus[random.Next(tipus.Length)]))
                .ToList();

            var r = IvaCalculator.Calcular(linies, mode);
            (r.BaseCents + r.IvaCents).Should().Be(r.TotalCents);
        }
    }

    [Fact] // A-12
    public void Arrodoniment_mig_centim_sempre_cap_amunt()
    {
        // 21% de 50 = 10,5 -> ha d'arrodonir a 11, no bancari (10)
        var r = IvaCalculator.Calcular([Linia(50, 2100)], IvaMode.NoInclos);
        r.IvaCents.Should().Be(11);
    }

    [Fact] // A-13
    public void Ordre_de_les_linies_no_afecta_el_resultat()
    {
        var a = new[] { Linia(1500, 2100), Linia(900, 1000) };
        var b = new[] { Linia(900, 1000), Linia(1500, 2100) };

        IvaCalculator.Calcular(a, IvaMode.Inclos).Should()
            .Be(IvaCalculator.Calcular(b, IvaMode.Inclos));
    }

    [Fact] // A-14
    public void ARegistres_genera_una_fila_per_tipus_present()
    {
        var linies = new[] { Linia(1500, 2100), Linia(900, 1000) };
        var registres = IvaCalculator.ARegistres(linies, IvaMode.Inclos);
        registres.Should().HaveCount(2);
    }

    [Fact] // A-15
    public void Suma_dels_registres_iguala_els_totals_de_la_venda()
    {
        var linies = new[] { Linia(1500, 2100), Linia(900, 1000), Linia(300, 2100) };
        var totals = IvaCalculator.Calcular(linies, IvaMode.Inclos);
        var registres = IvaCalculator.ARegistres(linies, IvaMode.Inclos);

        registres.Sum(r => r.BaseCents).Should().Be(totals.BaseCents);
        registres.Sum(r => r.IvaCents).Should().Be(totals.IvaCents);
        registres.Sum(r => r.TotalCents).Should().Be(totals.TotalCents);
    }
}
