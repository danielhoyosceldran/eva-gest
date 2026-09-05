using System.Globalization;
using System.IO;

namespace EvaGest.Services;

/// <summary>
/// Fase 9. The backup folder itself is the source of truth: each file's name encodes
/// its timestamp and whether it was automatic or manual, so no extra table is needed.
/// </summary>
public class BackupService(RutesApp rutes) : IBackupService
{
    // Millisecond precision matters: a restore always takes a safety copy right before
    // overwriting the database, and a manual backup can follow another within the same
    // second — second-only precision made those two collide on the same filename.
    private const string Format = "yyyyMMdd_HHmmssfff";

    // TODO: read from ConfiguracioService once it exists (Fase 9+ follow-up).
    // Hardcoded defaults match esquema-bbdd 2.13.
    private const int BackupsAConservar = 15;

    public Task<List<CopiaSeguretat>> Llistar()
    {
        Directory.CreateDirectory(rutes.CarpetaBackups);

        var copies = Directory.GetFiles(rutes.CarpetaBackups, "*.db")
            .Select(ALlegir)
            .Where(c => c is not null)
            .Select(c => c!)
            .OrderByDescending(c => c.Data)
            .ToList();

        return Task.FromResult(copies);
    }

    public async Task<CopiaSeguretat> FerCopiaManual() => await Copiar(esAutomatica: false);

    public async Task<CopiaSeguretat?> FerCopiaAutomaticaSiCal()
    {
        var copies = await Llistar();
        bool jaFetaAvui = copies.Any(c => c.EsAutomatica && c.Data.Date == DateTime.Today);
        if (jaFetaAvui) return null;

        return await Copiar(esAutomatica: true);
    }

    public async Task NetejarAntigues()
    {
        var copies = await Llistar(); // newest first
        foreach (var vella in copies.Skip(BackupsAConservar))
            File.Delete(vella.Ruta);
    }

    public async Task Restaurar(string rutaCopia)
    {
        // Back up the CURRENT state first, so an accidental restore can still be
        // undone (CU-09b) — this must happen before the file is overwritten.
        await Copiar(esAutomatica: false);

        File.Copy(rutaCopia, rutes.BaseDades, overwrite: true);
    }

    private async Task<CopiaSeguretat> Copiar(bool esAutomatica)
    {
        Directory.CreateDirectory(rutes.CarpetaBackups);

        var ara = DateTime.Now;
        string sufix = esAutomatica ? "auto" : "manual";
        string nom = $"{ara.ToString(Format, CultureInfo.InvariantCulture)}_{sufix}.db";
        string destinacio = Path.Combine(rutes.CarpetaBackups, nom);

        File.Copy(rutes.BaseDades, destinacio, overwrite: false);
        await NetejarAntigues();

        return ALlegir(destinacio)!;
    }

    private static CopiaSeguretat? ALlegir(string ruta)
    {
        string nom = Path.GetFileNameWithoutExtension(ruta);
        var parts = nom.Split('_');
        if (parts.Length < 3) return null;

        string dataText = $"{parts[0]}_{parts[1]}";
        if (!DateTime.TryParseExact(dataText, Format, CultureInfo.InvariantCulture,
                DateTimeStyles.None, out var data))
            return null;

        bool esAutomatica = parts[2] == "auto";
        long mida = new FileInfo(ruta).Length;
        return new CopiaSeguretat(ruta, data, esAutomatica, mida);
    }
}
