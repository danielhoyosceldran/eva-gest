namespace EvaGest.Services;

/// <summary>
/// Key-value settings store (RF-23). Reads are cached in memory because the agenda
/// grid asks for the slot granularity on every week load.
/// </summary>
public interface IConfiguracioService
{
    Task<string?> Obtenir(string clau);
    Task<int> ObtenirInt(string clau, int perDefecte);
    Task<bool> ObtenirBool(string clau, bool perDefecte);
    Task Guardar(string clau, string valor);

    /// <summary>Drops the in-memory cache. Needed after restoring a backup.</summary>
    void InvalidarCache();
}
