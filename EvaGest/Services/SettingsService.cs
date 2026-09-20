using EvaGest.Data;
using EvaGest.Models;
using Microsoft.EntityFrameworkCore;

namespace EvaGest.Services;

/// <summary>
/// Registered as a singleton: the whole table is small and read far more often than
/// written, so it is loaded once and kept in memory until a write invalidates it.
/// </summary>
public class SettingsService(IDbContextFactory<ShopDbContext> factory) : ISettingsService
{
    private readonly SemaphoreSlim _lock = new(1, 1);
    private Dictionary<string, string>? _cache;

    public async Task<string?> Get(string key)
    {
        var cache = await Load();
        return cache.GetValueOrDefault(key);
    }

    public async Task<int> GetInt(string key, int perDefault)
        => int.TryParse(await Get(key), out int value) ? value : perDefault;

    /// <summary>Booleans are seeded as "0"/"1" (esquema-bbdd 2.14), which bool.TryParse
    /// rejects outright — reading them with it alone silently returned the default for
    /// every stored setting.</summary>
    public async Task<bool> GetBool(string key, bool perDefault)
    {
        string? value = (await Get(key))?.Trim();
        if (string.IsNullOrEmpty(value)) return perDefault;

        if (value is "1") return true;
        if (value is "0") return false;

        return bool.TryParse(value, out bool result) ? result : perDefault;
    }

    /// <summary>Writes in the same 0/1 shape the seed uses, so the table stays uniform.</summary>
    public Task SaveBool(string key, bool value) => Save(key, value ? "1" : "0");

    public async Task Save(string key, string value)
    {
        await using var db = await factory.CreateDbContextAsync();

        var item = await db.Settings.FirstOrDefaultAsync(c => c.Key == key);
        if (item is null) db.Settings.Add(new SettingItem { Key = key, Value = value });
        else item.Value = value;

        await db.SaveChangesAsync();

        await _lock.WaitAsync();
        try
        {
            // Patch the cache instead of dropping it: a settings screen writes several
            // keys in a row and would otherwise reload the table once per field.
            if (_cache is not null) _cache[key] = value;
        }
        finally { _lock.Release(); }
    }

    /// <summary>
    /// Dropped without taking the lock on purpose. This runs on the UI thread (a restore
    /// replaces the file the settings live in), and <c>_lock.Wait()</c> here could park
    /// that thread while the semaphore is held by a <see cref="Load"/> awaiting the
    /// database: the continuation needs the dispatcher, which is exactly what is blocked.
    /// A reference assignment is atomic, and <see cref="Load"/>'s double-check already
    /// tolerates a racing reader — the worst case is one extra read of a small table.
    /// </summary>
    public void InvalidateCache() => Volatile.Write(ref _cache, null);

    private async Task<Dictionary<string, string>> Load()
    {
        if (_cache is { } current) return current;

        await _lock.WaitAsync();
        try
        {
            if (_cache is { } alreadyLoaded) return alreadyLoaded;

            await using var db = await factory.CreateDbContextAsync();
            var items = await db.Settings.AsNoTracking().ToListAsync();
            _cache = items.ToDictionary(i => i.Key, i => i.Value);
            return _cache;
        }
        finally { _lock.Release(); }
    }
}
