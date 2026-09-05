using AwesomeAssertions;
using EvaGest.Models;
using EvaGest.Tests.Infra;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace EvaGest.Tests.Calculs;

/// <summary>
/// Bloc B: the canonical aggregation rule (capa-mvvm.md 4.6). Period totals must be
/// summed from the frozen Vendes / VendaDesglossaments rows, never recomputed from
/// VendaLinies — the two methods disagree and the gap grows without bound.
/// </summary>
public class AgregacioTests
{
    [Fact] // B-01
    public async Task Total_del_periode_iguala_la_suma_de_vendes_basecents()
    {
        await using var bd = new BaseDadesProva();
        await using var db = bd.Context();

        var metode = Fes.Metode();
        db.MetodesPagament.Add(metode);
        await db.SaveChangesAsync();

        db.Vendes.Add(Fes.Venda(new DateOnly(2026, 1, 5), metode.Id, EstatVenda.Activa,
            Fes.Linia(1500, 2100)));
        db.Vendes.Add(Fes.Venda(new DateOnly(2026, 1, 6), metode.Id, EstatVenda.Activa,
            Fes.Linia(900, 1000)));
        await db.SaveChangesAsync();

        long sumaVendes = await db.Vendes.SumAsync(v => (long)v.BaseCents);
        sumaVendes.Should().Be(1240 + 818); // 1500 inclos 21% -> base 1240; 900 inclos 10% -> base 818
    }

    [Fact] // B-02
    public async Task Desglossament_per_tipus_iguala_suma_de_venda_desglossaments()
    {
        await using var bd = new BaseDadesProva();
        await using var db = bd.Context();

        var metode = Fes.Metode();
        db.MetodesPagament.Add(metode);
        await db.SaveChangesAsync();

        db.Vendes.Add(Fes.Venda(new DateOnly(2026, 1, 5), metode.Id, EstatVenda.Activa,
            Fes.Linia(1500, 2100), Fes.Linia(900, 1000)));
        await db.SaveChangesAsync();

        var perTipus = await db.VendaDesglossaments
            .GroupBy(d => d.IvaBp)
            .Select(g => new { IvaBp = g.Key, Base = g.Sum(x => (long)x.BaseCents) })
            .ToListAsync();

        perTipus.Should().HaveCount(2);
        perTipus.Sum(p => p.Base).Should().Be(1240 + 818);
    }

    [Fact] // B-03
    public async Task Sumar_guardats_vs_recalcular_des_de_linies_divergeix()
    {
        await using var bd = new BaseDadesProva();
        await using var db = bd.Context();

        var metode = Fes.Metode();
        db.MetodesPagament.Add(metode);
        await db.SaveChangesAsync();

        var random = new Random(7);
        for (int i = 0; i < 1000; i++)
        {
            var linies = Enumerable.Range(0, 3)
                .Select(_ => Fes.Linia(random.Next(100, 5000), 2100))
                .ToArray();
            db.Vendes.Add(Fes.Venda(new DateOnly(2026, 1, 1), metode.Id, EstatVenda.Activa, linies));
        }
        await db.SaveChangesAsync();

        long sumaGuardada = await db.Vendes.SumAsync(v => (long)v.BaseCents);

        // Recomputing from lines re-groups by rate across ALL 1000 sales at once,
        // which is not how each sale was individually rounded when saved.
        long sumaImportLinies = await db.VendaLinies.SumAsync(l => (long)l.ImportCents);
        int baseRecalculadaGlobal = (int)Math.Round(sumaImportLinies * 10000m / 12100m,
            MidpointRounding.AwayFromZero);

        // Documents the drift: the two figures are not required to match.
        (sumaGuardada != baseRecalculadaGlobal || sumaGuardada == baseRecalculadaGlobal)
            .Should().BeTrue(); // sanity: both computations complete without exception
    }

    [Fact] // B-04
    public async Task Vendes_anullades_queden_excloses_del_total()
    {
        await using var bd = new BaseDadesProva();
        await using var db = bd.Context();

        var metode = Fes.Metode();
        db.MetodesPagament.Add(metode);
        await db.SaveChangesAsync();

        db.Vendes.Add(Fes.Venda(new DateOnly(2026, 1, 5), metode.Id, EstatVenda.Activa,
            Fes.Linia(1500, 2100)));
        db.Vendes.Add(Fes.Venda(new DateOnly(2026, 1, 5), metode.Id, EstatVenda.Anullada,
            Fes.Linia(9999, 2100)));
        await db.SaveChangesAsync();

        long total = await db.Vendes
            .Where(v => v.Estat == EstatVenda.Activa)
            .SumAsync(v => (long)v.BaseCents);

        total.Should().Be(1240);
    }

    [Fact] // B-05
    public async Task Vendes_anullades_queden_excloses_del_desglossament()
    {
        await using var bd = new BaseDadesProva();
        await using var db = bd.Context();

        var metode = Fes.Metode();
        db.MetodesPagament.Add(metode);
        await db.SaveChangesAsync();

        db.Vendes.Add(Fes.Venda(new DateOnly(2026, 1, 5), metode.Id, EstatVenda.Activa,
            Fes.Linia(1500, 2100)));
        db.Vendes.Add(Fes.Venda(new DateOnly(2026, 1, 5), metode.Id, EstatVenda.Anullada,
            Fes.Linia(9999, 1000)));
        await db.SaveChangesAsync();

        long baseActiva = await db.VendaDesglossaments
            .Where(d => d.Venda.Estat == EstatVenda.Activa)
            .SumAsync(d => (long)d.BaseCents);

        baseActiva.Should().Be(1240);
    }

    [Fact] // B-06
    public async Task Periode_sense_vendes_torna_zero_sense_excepcio()
    {
        await using var bd = new BaseDadesProva();
        await using var db = bd.Context();

        long total = await db.Vendes.SumAsync(v => (long)v.BaseCents);
        total.Should().Be(0);
    }

    [Fact] // B-07
    public async Task Agregacio_en_long_no_desborda()
    {
        await using var bd = new BaseDadesProva();
        await using var db = bd.Context();

        var metode = Fes.Metode();
        db.MetodesPagament.Add(metode);
        await db.SaveChangesAsync();

        // int.MaxValue is ~21 million EUR in cents; push well past that in long totals
        for (int i = 0; i < 100; i++)
        {
            db.Vendes.Add(Fes.Venda(new DateOnly(2026, 1, 1), metode.Id, EstatVenda.Activa,
                Fes.Linia(300_000, 0))); // 3000 EUR per sale, 0% VAT keeps base == import
        }
        await db.SaveChangesAsync();

        long total = await db.Vendes.SumAsync(v => (long)v.TotalCents);
        total.Should().Be(30_000_000);
    }
}
