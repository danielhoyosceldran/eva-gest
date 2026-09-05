using EvaGest.Data;
using EvaGest.Models;
using Microsoft.EntityFrameworkCore;

namespace EvaGest.Services;

public class CaixaService(IDbContextFactory<BarberiaDbContext> factory) : ICaixaService
{
    public async Task<List<MovimentCaixa>> ObtenirPerPeriode(DateOnly des, DateOnly fins)
    {
        await using var db = await factory.CreateDbContextAsync();
        return await db.MovimentsCaixa.AsNoTracking()
            .Include(m => m.MetodePagament)
            .Where(m => m.Data >= des && m.Data <= fins)
            .OrderByDescending(m => m.Data)
            .ToListAsync();
    }

    public async Task<ResumCaixa> Resum(DateOnly des, DateOnly fins)
    {
        await using var db = await factory.CreateDbContextAsync();

        // Cast to long before summing: SQLite SUM is 64-bit, and long avoids any risk
        // of overflow on a very long period regardless of turnover.
        long vendesCents = await db.Vendes
            .Where(v => v.Estat == EstatVenda.Activa && v.Data >= des && v.Data <= fins)
            .SumAsync(v => (long)v.TotalCents);

        long entradesCents = await db.MovimentsCaixa
            .Where(m => m.Tipus == TipusMoviment.Entrada && m.Data >= des && m.Data <= fins)
            .SumAsync(m => (long)m.ImportCents);

        long sortidesCents = await db.MovimentsCaixa
            .Where(m => m.Tipus == TipusMoviment.Sortida && m.Data >= des && m.Data <= fins)
            .SumAsync(m => (long)m.ImportCents);

        // Per-rate breakdown straight from the frozen rows (never from VendaLinies, Bloc B).
        var perTipus = await db.VendaDesglossaments
            .Where(d => d.Venda.Estat == EstatVenda.Activa && d.Venda.Data >= des && d.Venda.Data <= fins)
            .GroupBy(d => d.IvaBp)
            .Select(g => new
            {
                IvaBp = g.Key,
                Base = g.Sum(x => (long)x.BaseCents),
                Iva = g.Sum(x => (long)x.IvaCents),
                Total = g.Sum(x => (long)x.TotalCents)
            })
            .OrderBy(g => g.IvaBp)
            .ToListAsync();

        long baseTotal = perTipus.Sum(g => g.Base);
        long ivaTotal = perTipus.Sum(g => g.Iva);

        var desglossament = perTipus
            .Select(g => new DesglossamentPerTipus(g.IvaBp, (int)g.Base, (int)g.Iva, (int)g.Total))
            .ToList();

        return new ResumCaixa(
            VendesCents: vendesCents,
            EntradesCents: entradesCents,
            SortidesCents: sortidesCents,
            BalancCents: vendesCents + entradesCents - sortidesCents,
            BaseCents: baseTotal,
            IvaCents: ivaTotal,
            DesglossamentIva: desglossament);
    }

    public async Task<int> Crear(MovimentCaixa moviment)
    {
        await using var db = await factory.CreateDbContextAsync();
        db.MovimentsCaixa.Add(moviment);
        await db.SaveChangesAsync();
        return moviment.Id;
    }

    public async Task Actualitzar(MovimentCaixa moviment)
    {
        await using var db = await factory.CreateDbContextAsync();
        db.MovimentsCaixa.Update(moviment);
        await db.SaveChangesAsync();
    }

    public async Task Eliminar(int movimentId)
    {
        await using var db = await factory.CreateDbContextAsync();
        var moviment = await db.MovimentsCaixa.FirstAsync(m => m.Id == movimentId);
        db.MovimentsCaixa.Remove(moviment);
        await db.SaveChangesAsync();
    }
}
