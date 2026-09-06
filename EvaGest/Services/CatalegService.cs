using EvaGest.Data;
using EvaGest.Models;
using Microsoft.EntityFrameworkCore;

namespace EvaGest.Services;

/// <summary>
/// The simplest CRUD in the project, and deliberately the first one built (Fase 3):
/// it fixes the View -> ViewModel -> Service pattern that every later page repeats.
/// Deleting is conditional: an entry nothing points at is removed for real, while one
/// already used by an appointment or a sale is only deactivated, so those rows keep
/// their meaning. Either way it disappears from the pickers, which is what the user
/// wanted out of "delete".
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

    public async Task<ResultatEsborrat> EliminarServei(int id)
    {
        await using var db = await factory.CreateDbContextAsync();

        var servei = await db.Serveis.FirstOrDefaultAsync(s => s.Id == id);
        if (servei is null) return ResultatEsborrat.Eliminat;

        // An appointment nulls its service on delete and a sale line would lose the link
        // its per-service report totals are built from: both count as history.
        bool usat = await db.Cites.AnyAsync(c => c.ServeiId == id)
                    || await db.VendaLinies.AnyAsync(l => l.ServeiId == id);

        if (usat)
        {
            servei.Actiu = false;
            await db.SaveChangesAsync();
            return ResultatEsborrat.Desactivat;
        }

        db.Serveis.Remove(servei);
        await db.SaveChangesAsync();
        return ResultatEsborrat.Eliminat;
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

    public async Task<ResultatEsborrat> EliminarProducte(int id)
    {
        await using var db = await factory.CreateDbContextAsync();

        var producte = await db.Productes.FirstOrDefaultAsync(p => p.Id == id);
        if (producte is null) return ResultatEsborrat.Eliminat;

        bool usat = await db.VendaLinies.AnyAsync(l => l.ProducteId == id);

        if (usat)
        {
            producte.Actiu = false;
            await db.SaveChangesAsync();
            return ResultatEsborrat.Desactivat;
        }

        db.Productes.Remove(producte);
        await db.SaveChangesAsync();
        return ResultatEsborrat.Eliminat;
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

    public async Task<ResultatEsborrat> EliminarMetode(int id)
    {
        await using var db = await factory.CreateDbContextAsync();

        var metode = await db.MetodesPagament.FirstOrDefaultAsync(m => m.Id == id);
        if (metode is null) return ResultatEsborrat.Eliminat;

        // Removing the last active one leaves no way to charge, exactly as deactivating
        // it would (pantalles 2.5), so the same guard covers both.
        bool esUltimActiu = metode.Actiu
                            && !await db.MetodesPagament.AnyAsync(m => m.Actiu && m.Id != id);
        if (esUltimActiu) return ResultatEsborrat.Bloquejat;

        // The foreign keys from sales and cash movements cascade: deleting a used method
        // would take the sales themselves with it, which is the opposite of the intent.
        bool usat = await db.Vendes.AnyAsync(v => v.MetodePagamentId == id)
                    || await db.MovimentsCaixa.AnyAsync(m => m.MetodePagamentId == id);

        if (usat)
        {
            metode.Actiu = false;
            await db.SaveChangesAsync();
            return ResultatEsborrat.Desactivat;
        }

        db.MetodesPagament.Remove(metode);
        await db.SaveChangesAsync();
        return ResultatEsborrat.Eliminat;
    }
}
