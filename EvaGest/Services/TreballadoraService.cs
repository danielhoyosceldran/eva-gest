using EvaGest.Data;
using EvaGest.Models;
using Microsoft.EntityFrameworkCore;

namespace EvaGest.Services;

public class TreballadoraService(IDbContextFactory<BarberiaDbContext> factory) : ITreballadoraService
{
    public async Task<List<Treballadora>> ObtenirTotes(bool nomesActives = false)
    {
        await using var db = await factory.CreateDbContextAsync();
        var query = db.Treballadores.AsNoTracking().AsQueryable();
        if (nomesActives) query = query.Where(t => t.Actiu);
        return await query.OrderBy(t => t.Nom).ToListAsync();
    }
}
