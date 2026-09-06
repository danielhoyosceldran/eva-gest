using EvaGest.Data;
using EvaGest.Models;
using Microsoft.EntityFrameworkCore;

namespace EvaGest.Services;

/// <summary>
/// The least obvious calculation in the project (capa-mvvm 4.4, casos-us CU-01b),
/// isolated in its own service precisely because it is the one worth testing hardest.
/// Never blocks saving: it only reports what the UI should warn about.
/// </summary>
public class DisponibilitatService(IDbContextFactory<BarberiaDbContext> factory) : IDisponibilitatService
{
    public async Task<ResultatDisponibilitat> Comprovar(
        DateOnly data, TimeOnly hora, int duradaMin,
        int? treballadoraId, int? citaIdExclosa = null)
    {
        await using var db = await factory.CreateDbContextAsync();

        var diaTancat = await db.DiesTancats.AsNoTracking().FirstOrDefaultAsync(d => d.Data == data);
        bool foraHorari = !await EsDinsHorari(db, data, hora, duradaMin);

        var solapades = await db.Cites.AsNoTracking()
            .Where(c => c.Data == data
                     && c.Estat != EstatCita.Cancellada && c.Estat != EstatCita.NoAssistida
                     && (citaIdExclosa == null || c.Id != citaIdExclosa))
            .ToListAsync();

        bool SeSolapen(Cita c) => hora < c.Hora.AddMinutes(c.DuradaMin) && hora.AddMinutes(duradaMin) > c.Hora;

        if (treballadoraId is int idTreballadora)
        {
            int citesDeLaTreballadora = solapades.Count(c => c.TreballadoraId == idTreballadora && SeSolapen(c));
            return new ResultatDisponibilitat(
                HiHaSolapament: citesDeLaTreballadora > 0,
                ForaHorari: foraHorari,
                DiaTancat: diaTancat is not null,
                MotiuDiaTancat: diaTancat?.Motiu,
                TreballadoresDisponibles: 1,
                CitesExistents: citesDeLaTreballadora);
        }

        var disponibles = await TreballadoresDisponibles(data, hora, duradaMin);
        int citesExistents = solapades.Count(SeSolapen);

        return new ResultatDisponibilitat(
            HiHaSolapament: citesExistents >= disponibles.Count,
            ForaHorari: foraHorari,
            DiaTancat: diaTancat is not null,
            MotiuDiaTancat: diaTancat?.Motiu,
            TreballadoresDisponibles: disponibles.Count,
            CitesExistents: citesExistents);
    }

    public async Task<List<Treballadora>> TreballadoresDisponibles(DateOnly data, TimeOnly hora, int duradaMin)
    {
        await using var db = await factory.CreateDbContextAsync();
        var dia = ADiaSetmana(data);
        var horaFi = hora.AddMinutes(duradaMin);

        return await db.Treballadores.AsNoTracking()
            .Where(t => t.Actiu && t.Horaris.Any(h =>
                h.DiaSetmana == dia && h.HoraInici <= hora && h.HoraFi >= horaFi))
            .ToListAsync();
    }

    public async Task<bool> EsDiaObert(DateOnly data)
    {
        await using var db = await factory.CreateDbContextAsync();
        var dia = ADiaSetmana(data);
        return await db.HorariBarberia.AsNoTracking().AnyAsync(h => h.DiaSetmana == dia);
    }

    public async Task<List<(TimeOnly inici, TimeOnly fi)>> FranjesObertura(DateOnly data)
    {
        await using var db = await factory.CreateDbContextAsync();
        var dia = ADiaSetmana(data);
        var franges = await db.HorariBarberia.AsNoTracking()
            .Where(h => h.DiaSetmana == dia)
            .OrderBy(h => h.HoraObertura)
            .Select(h => new { h.HoraObertura, h.HoraTancament })
            .ToListAsync();

        return franges.Select(f => (f.HoraObertura, f.HoraTancament)).ToList();
    }

    public async Task<Dictionary<DiaSetmana, List<(TimeOnly inici, TimeOnly fi)>>> FranjesSetmanals()
    {
        await using var db = await factory.CreateDbContextAsync();
        var files = await db.HorariBarberia.AsNoTracking()
            .OrderBy(h => h.DiaSetmana).ThenBy(h => h.HoraObertura)
            .Select(h => new { h.DiaSetmana, h.HoraObertura, h.HoraTancament })
            .ToListAsync();

        return files
            .GroupBy(h => h.DiaSetmana)
            .ToDictionary(g => g.Key, g => g.Select(h => (h.HoraObertura, h.HoraTancament)).ToList());
    }

    public async Task GuardarHorariSetmanal(
        IReadOnlyDictionary<DiaSetmana, List<(TimeOnly inici, TimeOnly fi)>> horari)
    {
        await using var db = await factory.CreateDbContextAsync();
        await using var transaccio = await db.Database.BeginTransactionAsync();

        // Replace rather than reconcile: the rows carry no identity of their own beyond
        // the day and the times, so there is nothing worth preserving.
        db.HorariBarberia.RemoveRange(await db.HorariBarberia.ToListAsync());
        await db.SaveChangesAsync();

        foreach (var (dia, franges) in horari)
            foreach (var (inici, fi) in franges)
                db.HorariBarberia.Add(new HorariBarberia
                {
                    DiaSetmana = dia,
                    HoraObertura = inici,
                    HoraTancament = fi
                });

        await db.SaveChangesAsync();
        await transaccio.CommitAsync();
    }

    public async Task<Dictionary<DateOnly, string?>> DiesTancatsA(DateOnly des, DateOnly fins)
    {
        await using var db = await factory.CreateDbContextAsync();
        var tancats = await db.DiesTancats.AsNoTracking()
            .Where(d => d.Data >= des && d.Data <= fins)
            .ToListAsync();

        return tancats.ToDictionary(d => d.Data, d => d.Motiu);
    }

    public async Task<List<DiaTancat>> DiesTancats()
    {
        await using var db = await factory.CreateDbContextAsync();
        return await db.DiesTancats.AsNoTracking().OrderBy(d => d.Data).ToListAsync();
    }

    public async Task AfegirDiaTancat(DateOnly data, string? motiu)
    {
        await using var db = await factory.CreateDbContextAsync();

        string? netejat = string.IsNullOrWhiteSpace(motiu) ? null : motiu.Trim();
        var existent = await db.DiesTancats.FirstOrDefaultAsync(d => d.Data == data);

        if (existent is null) db.DiesTancats.Add(new DiaTancat { Data = data, Motiu = netejat });
        else existent.Motiu = netejat;

        await db.SaveChangesAsync();
    }

    public async Task EliminarDiaTancat(int id)
    {
        await using var db = await factory.CreateDbContextAsync();

        var dia = await db.DiesTancats.FirstOrDefaultAsync(d => d.Id == id);
        if (dia is null) return;

        db.DiesTancats.Remove(dia);
        await db.SaveChangesAsync();
    }

    private async Task<bool> EsDinsHorari(BarberiaDbContext db, DateOnly data, TimeOnly hora, int duradaMin)
    {
        var dia = ADiaSetmana(data);
        var horaFi = hora.AddMinutes(duradaMin);

        return await db.HorariBarberia.AsNoTracking()
            .AnyAsync(h => h.DiaSetmana == dia && h.HoraObertura <= hora && h.HoraTancament >= horaFi);
    }

    /// <summary>Monday = Dl, ..., Sunday = Dg, matching the enum declared in models-domini.</summary>
    private static DiaSetmana ADiaSetmana(DateOnly data) => (DiaSetmana)(((int)data.DayOfWeek + 6) % 7);
}
