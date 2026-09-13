using EvaGest.Data;
using EvaGest.Models;
using Microsoft.EntityFrameworkCore;

namespace EvaGest.Services;

public class CitaService(IDbContextFactory<BarberiaDbContext> factory) : ICitaService
{
    public async Task<List<Cita>> ObtenirPerDia(DateOnly data)
    {
        await using var db = await factory.CreateDbContextAsync();
        return await Consulta(db).Where(c => c.Data == data).OrderBy(c => c.Hora).ToListAsync();
    }

    public async Task<List<Cita>> ObtenirPerRang(DateOnly des, DateOnly fins)
    {
        await using var db = await factory.CreateDbContextAsync();
        return await Consulta(db)
            .Where(c => c.Data >= des && c.Data <= fins)
            .OrderBy(c => c.Data).ThenBy(c => c.Hora)
            .ToListAsync();
    }

    public async Task<List<Cita>> ObtenirPerClient(int clientId)
    {
        await using var db = await factory.CreateDbContextAsync();
        return await Consulta(db)
            .Where(c => c.ClientId == clientId)
            .OrderByDescending(c => c.Data).ThenByDescending(c => c.Hora)
            .ToListAsync();
    }

    public async Task<Cita?> ObtenirPerId(int id)
    {
        await using var db = await factory.CreateDbContextAsync();
        return await Consulta(db).FirstOrDefaultAsync(c => c.Id == id);
    }

    public async Task<List<Cita>> ObtenirRealitzadesSenseVenda()
    {
        await using var db = await factory.CreateDbContextAsync();
        return await Consulta(db)
            .Where(c => c.Estat == EstatCita.Realitzada && c.Venda == null)
            .OrderBy(c => c.Data).ThenBy(c => c.Hora)
            .ToListAsync();
    }

    public async Task<int> ComptarPerEstat(DateOnly des, DateOnly fins, EstatCita estat)
    {
        await using var db = await factory.CreateDbContextAsync();
        return await db.Cites.CountAsync(c => c.Data >= des && c.Data <= fins && c.Estat == estat);
    }

    public async Task<int> Crear(Cita cita)
    {
        await using var db = await factory.CreateDbContextAsync();
        db.Cites.Add(cita);
        await db.SaveChangesAsync();
        return cita.Id;
    }

    public async Task Actualitzar(Cita cita)
    {
        await using var db = await factory.CreateDbContextAsync();
        db.Cites.Update(cita);
        await db.SaveChangesAsync();
    }

    public async Task<bool> CanviarEstat(int citaId, EstatCita nouEstat)
    {
        await using var db = await factory.CreateDbContextAsync();
        var cita = await db.Cites.Include(c => c.Venda).FirstAsync(c => c.Id == citaId);

        if (nouEstat == EstatCita.Pendent && cita.Venda is { Estat: EstatVenda.Activa })
            return false;

        cita.Estat = nouEstat;
        await db.SaveChangesAsync();
        return true;
    }

    public async Task<ResultatEsborrat> Eliminar(int citaId)
    {
        await using var db = await factory.CreateDbContextAsync();

        var cita = await db.Cites.FirstOrDefaultAsync(c => c.Id == citaId);
        if (cita is null) return ResultatEsborrat.Eliminat;

        // The sale's cita_id is SET NULL, so this would succeed and quietly cut the sale
        // loose from the appointment it was charged for. Refuse instead and say why.
        if (await db.Vendes.AnyAsync(v => v.CitaId == citaId))
            return ResultatEsborrat.Bloquejat;

        db.Cites.Remove(cita);
        await db.SaveChangesAsync();
        return ResultatEsborrat.Eliminat;
    }

    private static IQueryable<Cita> Consulta(BarberiaDbContext db)
        => db.Cites.AsNoTracking()
            .Include(c => c.Client)
            .Include(c => c.Servei)
            .Include(c => c.Treballadora);
}
