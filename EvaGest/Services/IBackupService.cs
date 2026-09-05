namespace EvaGest.Services;

public record CopiaSeguretat(string Ruta, DateTime Data, bool EsAutomatica, long BytesMida);

public interface IBackupService
{
    Task<List<CopiaSeguretat>> Llistar();

    Task<CopiaSeguretat> FerCopiaManual();

    /// <summary>
    /// Runs the daily backup if it is due. Called at startup, because the machine may
    /// have been off at the configured hour (CU-11).
    /// </summary>
    Task<CopiaSeguretat?> FerCopiaAutomaticaSiCal();

    /// <summary>Deletes the oldest backups beyond the configured retention.</summary>
    Task NetejarAntigues();

    /// <summary>
    /// Restores a backup. Always backs up the CURRENT state first, so an accidental
    /// restore can still be undone (CU-09b).
    /// </summary>
    Task Restaurar(string rutaCopia);
}
