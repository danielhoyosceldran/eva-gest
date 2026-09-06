using EvaGest.Data;
using EvaGest.Models;
using Microsoft.EntityFrameworkCore;

namespace EvaGest.Services;

/// <summary>
/// Registered as a singleton: the whole table is small and read far more often than
/// written, so it is loaded once and kept in memory until a write invalidates it.
/// </summary>
public class ConfiguracioService(IDbContextFactory<BarberiaDbContext> factory) : IConfiguracioService
{
    private readonly SemaphoreSlim _pany = new(1, 1);
    private Dictionary<string, string>? _cache;

    public async Task<string?> Obtenir(string clau)
    {
        var cache = await Carregar();
        return cache.GetValueOrDefault(clau);
    }

    public async Task<int> ObtenirInt(string clau, int perDefecte)
        => int.TryParse(await Obtenir(clau), out int valor) ? valor : perDefecte;

    /// <summary>Booleans are seeded as "0"/"1" (esquema-bbdd 2.14), which bool.TryParse
    /// rejects outright — reading them with it alone silently returned the default for
    /// every stored setting.</summary>
    public async Task<bool> ObtenirBool(string clau, bool perDefecte)
    {
        string? valor = (await Obtenir(clau))?.Trim();
        if (string.IsNullOrEmpty(valor)) return perDefecte;

        if (valor is "1") return true;
        if (valor is "0") return false;

        return bool.TryParse(valor, out bool resultat) ? resultat : perDefecte;
    }

    /// <summary>Writes in the same 0/1 shape the seed uses, so the table stays uniform.</summary>
    public Task GuardarBool(string clau, bool valor) => Guardar(clau, valor ? "1" : "0");

    public async Task Guardar(string clau, string valor)
    {
        await using var db = await factory.CreateDbContextAsync();

        var item = await db.Configuracio.FirstOrDefaultAsync(c => c.Clau == clau);
        if (item is null) db.Configuracio.Add(new ConfiguracioItem { Clau = clau, Valor = valor });
        else item.Valor = valor;

        await db.SaveChangesAsync();

        await _pany.WaitAsync();
        try
        {
            // Patch the cache instead of dropping it: a settings screen writes several
            // keys in a row and would otherwise reload the table once per field.
            if (_cache is not null) _cache[clau] = valor;
        }
        finally { _pany.Release(); }
    }

    public void InvalidarCache()
    {
        _pany.Wait();
        try { _cache = null; }
        finally { _pany.Release(); }
    }

    private async Task<Dictionary<string, string>> Carregar()
    {
        if (_cache is { } actual) return actual;

        await _pany.WaitAsync();
        try
        {
            if (_cache is { } jaCarregat) return jaCarregat;

            await using var db = await factory.CreateDbContextAsync();
            var items = await db.Configuracio.AsNoTracking().ToListAsync();
            _cache = items.ToDictionary(i => i.Clau, i => i.Valor);
            return _cache;
        }
        finally { _pany.Release(); }
    }
}
