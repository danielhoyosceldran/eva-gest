using EvaGest.Data;
using EvaGest.Models;
using Microsoft.EntityFrameworkCore;

namespace EvaGest.Services;

public class TreballadoraService(IDbContextFactory<BarberiaDbContext> factory) : ITreballadoraService
{
    public async Task<List<Treballadora>> ObtenirTotes(bool nomesActives = false)
    {
        await using var db = await factory.CreateDbContextAsync();
        var query = db.Treballadores.AsNoTracking().Include(t => t.Horaris).AsQueryable();
        if (nomesActives) query = query.Where(t => t.Actiu);
        return await query.OrderBy(t => t.Nom).ToListAsync();
    }

    public async Task<Dictionary<DiaSetmana, List<(TimeOnly inici, TimeOnly fi)>>> HorariDe(int treballadoraId)
    {
        await using var db = await factory.CreateDbContextAsync();
        var files = await db.HorarisTreballadora.AsNoTracking()
            .Where(h => h.TreballadoraId == treballadoraId)
            .OrderBy(h => h.DiaSetmana).ThenBy(h => h.HoraInici)
            .Select(h => new { h.DiaSetmana, h.HoraInici, h.HoraFi })
            .ToListAsync();

        return files
            .GroupBy(h => h.DiaSetmana)
            .ToDictionary(g => g.Key, g => g.Select(h => (h.HoraInici, h.HoraFi)).ToList());
    }

    public async Task<Treballadora> Crear(
        Treballadora treballadora,
        IReadOnlyDictionary<DiaSetmana, List<(TimeOnly inici, TimeOnly fi)>> horari)
    {
        await using var db = await factory.CreateDbContextAsync();

        var nova = new Treballadora
        {
            Nom = treballadora.Nom,
            Actiu = treballadora.Actiu,
            Color = treballadora.Color,
            Horaris = AFiles(horari)
        };

        db.Treballadores.Add(nova);
        await db.SaveChangesAsync();
        return nova;
    }

    public async Task Actualitzar(
        Treballadora treballadora,
        IReadOnlyDictionary<DiaSetmana, List<(TimeOnly inici, TimeOnly fi)>> horari)
    {
        await using var db = await factory.CreateDbContextAsync();
        await using var transaccio = await db.Database.BeginTransactionAsync();

        var existent = await db.Treballadores
            .Include(t => t.Horaris)
            .FirstOrDefaultAsync(t => t.Id == treballadora.Id)
            ?? throw new InvalidOperationException($"No existeix cap treballadora amb id {treballadora.Id}.");

        existent.Nom = treballadora.Nom;
        existent.Actiu = treballadora.Actiu;
        existent.Color = treballadora.Color;

        // Replace rather than reconcile: a schedule row carries no identity beyond its
        // day and times, so there is nothing worth matching up (same call as the
        // barbershop's own opening hours in DisponibilitatService).
        db.HorarisTreballadora.RemoveRange(existent.Horaris);
        await db.SaveChangesAsync();

        foreach (var fila in AFiles(horari))
        {
            fila.TreballadoraId = existent.Id;
            db.HorarisTreballadora.Add(fila);
        }

        await db.SaveChangesAsync();
        await transaccio.CommitAsync();
    }

    public async Task CanviarEstat(int treballadoraId, bool actiu)
    {
        await using var db = await factory.CreateDbContextAsync();

        var treballadora = await db.Treballadores.FirstOrDefaultAsync(t => t.Id == treballadoraId);
        if (treballadora is null) return;

        treballadora.Actiu = actiu;
        await db.SaveChangesAsync();
    }

    public async Task<ResultatEsborrat> Eliminar(int treballadoraId)
    {
        await using var db = await factory.CreateDbContextAsync();

        var treballadora = await db.Treballadores
            .Include(t => t.Horaris)
            .FirstOrDefaultAsync(t => t.Id == treballadoraId);
        if (treballadora is null) return ResultatEsborrat.Eliminat;

        bool usada = await db.Cites.AnyAsync(c => c.TreballadoraId == treballadoraId)
                     || await db.Vendes.AnyAsync(v => v.TreballadoraId == treballadoraId);

        if (usada)
        {
            treballadora.Actiu = false;
            await db.SaveChangesAsync();
            return ResultatEsborrat.Desactivat;
        }

        // Her schedule has no life of its own, so it goes with her rather than being
        // left behind pointing at nobody.
        db.HorarisTreballadora.RemoveRange(treballadora.Horaris);
        db.Treballadores.Remove(treballadora);
        await db.SaveChangesAsync();
        return ResultatEsborrat.Eliminat;
    }

    private static List<HorariTreballadora> AFiles(
        IReadOnlyDictionary<DiaSetmana, List<(TimeOnly inici, TimeOnly fi)>> horari)
        => [.. horari
            .SelectMany(parell => parell.Value.Select(franja => new HorariTreballadora
            {
                DiaSetmana = parell.Key,
                HoraInici = franja.inici,
                HoraFi = franja.fi
            }))];
}
