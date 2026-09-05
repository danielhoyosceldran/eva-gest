using AwesomeAssertions;
using EvaGest.Services;
using Xunit;

namespace EvaGest.Tests.Serveis;

/// <summary>Bloc L (còpies de seguretat): pure file I/O, no database involved.</summary>
public class BackupServiceTests : IDisposable
{
    private readonly string _carpeta = Path.Combine(Path.GetTempPath(), "EvaGestTests_" + Guid.NewGuid());
    private readonly string _rutaBd;

    public BackupServiceTests()
    {
        Directory.CreateDirectory(_carpeta);
        _rutaBd = Path.Combine(_carpeta, "barberia.db");
        File.WriteAllText(_rutaBd, "contingut original");
    }

    public void Dispose() => Directory.Delete(_carpeta, recursive: true);

    private BackupService CreaServei() => new(new RutesApp(_rutaBd, _carpeta));

    [Fact] // L-01
    public async Task Copia_manual_crea_un_fitxer()
    {
        var backup = CreaServei();
        var copia = await backup.FerCopiaManual();

        File.Exists(copia.Ruta).Should().BeTrue();
        new FileInfo(copia.Ruta).Length.Should().BeGreaterThan(0);
    }

    [Fact] // L-02
    public async Task La_copia_es_restaurable()
    {
        var backup = CreaServei();
        var copia = await backup.FerCopiaManual();

        File.WriteAllText(_rutaBd, "dades malmeses");
        await backup.Restaurar(copia.Ruta);

        File.ReadAllText(_rutaBd).Should().Be("contingut original");
    }

    [Fact] // L-03
    public async Task Retencio_amb_20_copies_i_limit_15_queden_les_15_mes_noves()
    {
        var backup = CreaServei();
        for (int i = 0; i < 20; i++)
        {
            string nom = Path.Combine(_carpeta, "Backups", $"202601{i + 1:00}_120000000_manual.db");
            Directory.CreateDirectory(Path.Combine(_carpeta, "Backups"));
            File.WriteAllText(nom, "x");
        }

        await backup.NetejarAntigues();

        (await backup.Llistar()).Should().HaveCount(15);
    }

    [Fact] // L-04
    public async Task Copia_automatica_pendent_des_dahir_es_fa()
    {
        var backup = CreaServei();
        string carpetaBackups = Path.Combine(_carpeta, "Backups");
        Directory.CreateDirectory(carpetaBackups);
        string ahir = DateTime.Now.AddDays(-1).ToString("yyyyMMdd_HHmmssfff");
        File.WriteAllText(Path.Combine(carpetaBackups, $"{ahir}_auto.db"), "x");

        var resultat = await backup.FerCopiaAutomaticaSiCal();

        resultat.Should().NotBeNull();
    }

    [Fact] // L-05
    public async Task Copia_automatica_ja_feta_avui_no_es_repeteix()
    {
        var backup = CreaServei();
        string carpetaBackups = Path.Combine(_carpeta, "Backups");
        Directory.CreateDirectory(carpetaBackups);
        string avui = DateTime.Now.ToString("yyyyMMdd_HHmmssfff");
        File.WriteAllText(Path.Combine(carpetaBackups, $"{avui}_auto.db"), "x");

        var resultat = await backup.FerCopiaAutomaticaSiCal();

        resultat.Should().BeNull();
    }

    [Fact] // L-06
    public async Task Restaurar_fa_copia_previa_de_lestat_actual()
    {
        var backup = CreaServei();
        var copiaInicial = await backup.FerCopiaManual();

        int abans = (await backup.Llistar()).Count;
        await backup.Restaurar(copiaInicial.Ruta);
        int despres = (await backup.Llistar()).Count;

        despres.Should().Be(abans + 1); // the pre-restore safety copy
    }
}
