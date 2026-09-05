using EvaGest.Data;
using EvaGest.Models;
using Microsoft.EntityFrameworkCore;

namespace EvaGest.Services;

/// <summary>
/// The simplest CRUD in the project, and deliberately the first one built (Fase 3):
/// it fixes the View -> ViewModel -> Service pattern that every later page repeats.
/// Services and products are never deleted from the UI, only deactivated, so that
/// existing sale lines (which already freeze their own price and VAT copy) stay intact.
/// </summary>
public class CatalegService(IDbContextFactory<BarberiaDbContext> factory) : ICatalegService
{
    public async Task<List<Servei>> ObtenirServeis(bool nomesActius = false)
    {
        await using var db = await factory.CreateDbContextAsync();
        var query = db.Serveis.AsNoTracking().AsQueryable();
        if (nomesActius) query = query.Where(s => s.Actiu);
        return await query.OrderBy(s => s.Nom).ToListAsync();
    }

    public async Task<Servei?> ObtenirServei(int id)
    {
        await using var db = await factory.CreateDbContextAsync();
        return await db.Serveis.AsNoTracking().FirstOrDefaultAsync(s => s.Id == id);
    }

    public async Task<int> CrearServei(Servei servei)
    {
        await using var db = await factory.CreateDbContextAsync();
        db.Serveis.Add(servei);
        await db.SaveChangesAsync();
        return servei.Id;
    }

    public async Task ActualitzarServei(Servei servei)
    {
        await using var db = await factory.CreateDbContextAsync();
        db.Serveis.Update(servei);
        await db.SaveChangesAsync();
    }

    public async Task CanviarEstatServei(int id, bool actiu)
    {
        await using var db = await factory.CreateDbContextAsync();
        var servei = await db.Serveis.FirstAsync(s => s.Id == id);
        servei.Actiu = actiu;
        await db.SaveChangesAsync();
    }

    public async Task<List<Producte>> ObtenirProductes(bool nomesActius = false)
    {
        await using var db = await factory.CreateDbContextAsync();
        var query = db.Productes.AsNoTracking().AsQueryable();
        if (nomesActius) query = query.Where(p => p.Actiu);
        return await query.OrderBy(p => p.Nom).ToListAsync();
    }

    public async Task<Producte?> ObtenirProducte(int id)
    {
        await using var db = await factory.CreateDbContextAsync();
        return await db.Productes.AsNoTracking().FirstOrDefaultAsync(p => p.Id == id);
    }

    public async Task<int> CrearProducte(Producte producte)
    {
        await using var db = await factory.CreateDbContextAsync();
        db.Productes.Add(producte);
        await db.SaveChangesAsync();
        return producte.Id;
    }

    public async Task ActualitzarProducte(Producte producte)
    {
        await using var db = await factory.CreateDbContextAsync();
        db.Productes.Update(producte);
        await db.SaveChangesAsync();
    }

    public async Task CanviarEstatProducte(int id, bool actiu)
    {
        await using var db = await factory.CreateDbContextAsync();
        var producte = await db.Productes.FirstAsync(p => p.Id == id);
        producte.Actiu = actiu;
        await db.SaveChangesAsync();
    }

    public async Task<List<MetodePagament>> ObtenirMetodes(bool nomesActius = false)
    {
        await using var db = await factory.CreateDbContextAsync();
        var query = db.MetodesPagament.AsNoTracking().AsQueryable();
        if (nomesActius) query = query.Where(m => m.Actiu);
        return await query.OrderBy(m => m.Nom).ToListAsync();
    }

    public async Task<int> CrearMetode(string nom)
    {
        await using var db = await factory.CreateDbContextAsync();
        var metode = new MetodePagament { Nom = nom, Actiu = true };
        db.MetodesPagament.Add(metode);
        await db.SaveChangesAsync();
        return metode.Id;
    }

    public async Task ActualitzarMetode(MetodePagament metode)
    {
        await using var db = await factory.CreateDbContextAsync();
        db.MetodesPagament.Update(metode);
        await db.SaveChangesAsync();
    }

    public async Task<bool> PotDesactivarMetode(int id)
    {
        await using var db = await factory.CreateDbContextAsync();
        int actiusRestants = await db.MetodesPagament.CountAsync(m => m.Actiu && m.Id != id);
        return actiusRestants > 0;
    }

    public async Task CanviarEstatMetode(int id, bool actiu)
    {
        await using var db = await factory.CreateDbContextAsync();
        var metode = await db.MetodesPagament.FirstAsync(m => m.Id == id);
        metode.Actiu = actiu;
        await db.SaveChangesAsync();
    }
}
