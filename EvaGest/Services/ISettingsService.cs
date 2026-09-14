namespace EvaGest.Services;

/// <summary>
/// Key-value settings store (RF-23). Reads are cached in memory because the agenda
/// grid asks for the slot granularity on every week load.
/// </summary>
public interface ISettingsService
{
    Task<string?> Get(string key);
    Task<int> GetInt(string key, int perDefault);
    Task<bool> GetBool(string key, bool perDefault);
    Task Save(string key, string value);

    /// <summary>Stores a flag as "0"/"1", the shape the seed and the schema doc use.</summary>
    Task SaveBool(string key, bool value);

    /// <summary>Drops the in-memory cache. Needed after restoring a backup.</summary>
    void InvalidateCache();
}
