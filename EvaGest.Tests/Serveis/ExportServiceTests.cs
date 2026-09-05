using AwesomeAssertions;
using EvaGest.Models;
using EvaGest.Services;
using EvaGest.Tests.Infra;
using Xunit;

namespace EvaGest.Tests.Serveis;

/// <summary>Bloc L (exportació): CSV files for the assessoria.</summary>
public class ExportServiceTests : IDisposable
{
    private readonly string _carpeta = Path.Combine(Path.GetTempPath(), "EvaGestExportTests_" + Guid.NewGuid());
    private static readonly DateOnly Avui = new(2026, 9, 7);

    public ExportServiceTests() => Directory.CreateDirectory(_carpeta);
    public void Dispose() => Directory.Delete(_carpeta, recursive: true);

    private static async Task<int> AfegeixMetode(BaseDadesProva bd)
    {
        await using var db = bd.Context();
        var m = Fes.Metode();
        db.MetodesPagament.Add(m);
        await db.SaveChangesAsync();
        return m.Id;
    }

    [Fact] // L-07
    public async Task Una_fila_per_venda_no_per_linia()
    {
        await using var bd = new BaseDadesProva();
        int metodeId = await AfegeixMetode(bd);
        await using (var db = bd.Context())
        {
            db.Vendes.Add(Fes.Venda(Avui, metodeId, EstatVenda.Activa, Fes.Linia(1000, 2100), Fes.Linia(900, 1000)));
            db.Vendes.Add(Fes.Venda(Avui, metodeId, EstatVenda.Activa, Fes.Linia(500, 2100)));
            await db.SaveChangesAsync();
        }

        var export = new ExportService(new FabricaDeProva(bd.Opcions));
        await export.ExportarVendes(Avui, Avui, _carpeta);

        string contingut = await File.ReadAllTextAsync(Path.Combine(_carpeta, $"vendes_{Avui:yyyyMMdd}-{Avui:yyyyMMdd}.csv"));
        var linies = contingut.Split('\n', StringSplitOptions.RemoveEmptyEntries);
        linies.Should().HaveCount(3); // header + 2 sales, not 3 lines
    }

    [Fact] // L-08
    public async Task Suma_de_la_columna_base_iguala_la_base_del_periode()
    {
        await using var bd = new BaseDadesProva();
        int metodeId = await AfegeixMetode(bd);
        await using (var db = bd.Context())
        {
            db.Vendes.Add(Fes.Venda(Avui, metodeId, EstatVenda.Activa, Fes.Linia(1500, 2100)));
            db.Vendes.Add(Fes.Venda(Avui, metodeId, EstatVenda.Activa, Fes.Linia(900, 1000)));
            await db.SaveChangesAsync();
        }

        var caixa = new CaixaService(new FabricaDeProva(bd.Opcions));
        var resum = await caixa.Resum(Avui, Avui);

        var export = new ExportService(new FabricaDeProva(bd.Opcions));
        await export.ExportarVendes(Avui, Avui, _carpeta);

        var linies = (await File.ReadAllLinesAsync(
            Path.Combine(_carpeta, $"vendes_{Avui:yyyyMMdd}-{Avui:yyyyMMdd}.csv")))
            .Skip(1).Where(l => l.Length > 0);

        decimal sumaBase = linies.Sum(l => decimal.Parse(l.Split(';')[6], System.Globalization.CultureInfo.GetCultureInfo("ca-ES")));
        sumaBase.Should().Be(resum.BaseCents / 100m);
    }

    [Fact] // L-09
    public async Task Fitxer_diva_una_fila_per_tipus()
    {
        await using var bd = new BaseDadesProva();
        int metodeId = await AfegeixMetode(bd);
        await using (var db = bd.Context())
        {
            db.Vendes.Add(Fes.Venda(Avui, metodeId, EstatVenda.Activa, Fes.Linia(1500, 2100)));
            db.Vendes.Add(Fes.Venda(Avui, metodeId, EstatVenda.Activa, Fes.Linia(900, 1000)));
            await db.SaveChangesAsync();
        }

        var export = new ExportService(new FabricaDeProva(bd.Opcions));
        await export.ExportarVendes(Avui, Avui, _carpeta);

        var linies = await File.ReadAllLinesAsync(Path.Combine(_carpeta, $"iva_{Avui:yyyyMMdd}-{Avui:yyyyMMdd}.csv"));
        linies.Where(l => l.Length > 0).Should().HaveCount(3); // header + 2 rates
    }

    [Fact] // L-10
    public async Task Format_decimal_amb_coma()
    {
        await using var bd = new BaseDadesProva();
        int metodeId = await AfegeixMetode(bd);
        await using (var db = bd.Context())
        {
            db.Vendes.Add(Fes.Venda(Avui, metodeId, EstatVenda.Activa, Fes.Linia(1500, 2100)));
            await db.SaveChangesAsync();
        }

        var export = new ExportService(new FabricaDeProva(bd.Opcions));
        await export.ExportarVendes(Avui, Avui, _carpeta);

        string contingut = await File.ReadAllTextAsync(Path.Combine(_carpeta, $"vendes_{Avui:yyyyMMdd}-{Avui:yyyyMMdd}.csv"));
        contingut.Should().Contain("15,00");
    }

    [Fact] // L-11
    public async Task Exportacio_dun_periode_buit_no_peta()
    {
        await using var bd = new BaseDadesProva();
        var export = new ExportService(new FabricaDeProva(bd.Opcions));

        await export.ExportarVendes(Avui, Avui, _carpeta);

        var linies = await File.ReadAllLinesAsync(Path.Combine(_carpeta, $"vendes_{Avui:yyyyMMdd}-{Avui:yyyyMMdd}.csv"));
        linies.Where(l => l.Length > 0).Should().ContainSingle(); // header only
    }
}
