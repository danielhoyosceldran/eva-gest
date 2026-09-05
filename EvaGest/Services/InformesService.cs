using EvaGest.Data;
using EvaGest.Models;
using Microsoft.EntityFrameworkCore;

namespace EvaGest.Services;

public record IndicadorsClient(
    int Visites, int Cancellades, int NoAssistides,
    long TotalGastatCents,
    decimal? MitjanaPerVisitaEuros,     // null when no completed visits
    double? FrequenciaDies,             // null with fewer than 2 visits
    DateOnly? PrimeraVisita, DateOnly? UltimaVisita);

/// <summary>
/// Fase 8. Reports only make sense once there is real data to aggregate over, so
/// this is deliberately one of the last modules built. Everything here reads
/// Vendes / VendaLinies / VendaDesglossaments; it never writes.
/// </summary>
public class InformesService(IDbContextFactory<BarberiaDbContext> factory) : IInformesService
{
    public async Task<IndicadorsClient> IndicadorsDeClient(int clientId)
    {
        await using var db = await factory.CreateDbContextAsync();

        int cancellades = await db.Cites.CountAsync(c => c.ClientId == clientId && c.Estat == EstatCita.Cancellada);
        int noAssistides = await db.Cites.CountAsync(c => c.ClientId == clientId && c.Estat == EstatCita.NoAssistida);

        var dataVendesActives = await db.Vendes.AsNoTracking()
            .Where(v => v.ClientId == clientId && v.Estat == EstatVenda.Activa)
            .Select(v => new { v.Data, v.TotalCents })
            .ToListAsync();

        int visites = dataVendesActives.Count;
        long totalCents = dataVendesActives.Sum(v => (long)v.TotalCents);
        decimal? mitjana = visites == 0 ? null : totalCents / 100m / visites;
        double? frequencia = Indicadors.FrequenciaDies(dataVendesActives.Select(v => v.Data));

        DateOnly? primera = dataVendesActives.Count == 0 ? null : dataVendesActives.Min(v => v.Data);
        DateOnly? ultima = dataVendesActives.Count == 0 ? null : dataVendesActives.Max(v => v.Data);

        return new IndicadorsClient(visites, cancellades, noAssistides, totalCents,
            mitjana, frequencia, primera, ultima);
    }

    public async Task<List<(Client client, int visites, long totalCents)>> TopPerVisites(int limit = 10)
    {
        await using var db = await factory.CreateDbContextAsync();

        // Guest sales have no ClientId and never appear in a per-client ranking (I-13),
        // even though they still count towards global totals elsewhere (I-14).
        var grups = await db.Vendes.AsNoTracking()
            .Where(v => v.Estat == EstatVenda.Activa && v.ClientId != null)
            .GroupBy(v => v.ClientId!.Value)
            .Select(g => new { ClientId = g.Key, Visites = g.Count(), Total = g.Sum(v => (long)v.TotalCents) })
            .OrderByDescending(g => g.Visites)
            .Take(limit)
            .ToListAsync();

        var clients = await ClientsPerIds(db, grups.Select(g => g.ClientId));
        return grups.Select(g => (clients[g.ClientId], g.Visites, g.Total)).ToList();
    }

    public async Task<List<(Client client, long totalCents)>> TopPerDespesa(int limit = 10)
    {
        await using var db = await factory.CreateDbContextAsync();

        var grups = await db.Vendes.AsNoTracking()
            .Where(v => v.Estat == EstatVenda.Activa && v.ClientId != null)
            .GroupBy(v => v.ClientId!.Value)
            .Select(g => new { ClientId = g.Key, Total = g.Sum(v => (long)v.TotalCents) })
            .OrderByDescending(g => g.Total)
            .Take(limit)
            .ToListAsync();

        var clients = await ClientsPerIds(db, grups.Select(g => g.ClientId));
        return grups.Select(g => (clients[g.ClientId], g.Total)).ToList();
    }

    public async Task<List<(Client client, decimal mitjanaEuros)>> TopPerMitjana(int limit = 10)
    {
        await using var db = await factory.CreateDbContextAsync();

        var grups = await db.Vendes.AsNoTracking()
            .Where(v => v.Estat == EstatVenda.Activa && v.ClientId != null)
            .GroupBy(v => v.ClientId!.Value)
            .Select(g => new { ClientId = g.Key, Total = g.Sum(v => (long)v.TotalCents), Visites = g.Count() })
            .ToListAsync();

        var ordenats = grups
            .Select(g => (g.ClientId, Mitjana: g.Total / 100m / g.Visites))
            .OrderByDescending(g => g.Mitjana)
            .Take(limit)
            .ToList();

        var clients = await ClientsPerIds(db, ordenats.Select(g => g.ClientId));
        return ordenats.Select(g => (clients[g.ClientId], g.Mitjana)).ToList();
    }

    public async Task<List<(Client client, DateOnly ultima, int diesSense)>> FaTempsQueNoVenen(int limit = 10)
    {
        await using var db = await factory.CreateDbContextAsync();
        var avui = DateOnly.FromDateTime(DateTime.Today);

        var grups = await db.Vendes.AsNoTracking()
            .Where(v => v.Estat == EstatVenda.Activa && v.ClientId != null)
            .GroupBy(v => v.ClientId!.Value)
            .Select(g => new { ClientId = g.Key, Ultima = g.Max(v => v.Data) })
            .OrderBy(g => g.Ultima)
            .Take(limit)
            .ToListAsync();

        var clients = await ClientsPerIds(db, grups.Select(g => g.ClientId));
        return grups.Select(g => (clients[g.ClientId], g.Ultima, avui.DayNumber - g.Ultima.DayNumber)).ToList();
    }

    public async Task<DetallTreballadora> DetallDeTreballadora(int treballadoraId, DateOnly des, DateOnly fins)
    {
        await using var db = await factory.CreateDbContextAsync();

        var treballadora = await db.Treballadores.AsNoTracking().FirstAsync(t => t.Id == treballadoraId);

        var vendes = await db.Vendes.AsNoTracking()
            .Where(v => v.TreballadoraId == treballadoraId && v.Estat == EstatVenda.Activa
                     && v.Data >= des && v.Data <= fins)
            .Include(v => v.Linies)
            .ToListAsync();

        long totalPeriodeCents = await db.Vendes.AsNoTracking()
            .Where(v => v.Estat == EstatVenda.Activa && v.Data >= des && v.Data <= fins)
            .SumAsync(v => (long)v.TotalCents);

        long ingressosCents = vendes.Sum(v => (long)v.TotalCents);
        var totesLinies = vendes.SelectMany(v => v.Linies).ToList();

        long productesCents = totesLinies.Where(l => l.Tipus == TipusLinia.Producte).Sum(l => (long)l.ImportCents);
        long altresCents = totesLinies.Where(l => l.Tipus == TipusLinia.Altres).Sum(l => (long)l.ImportCents);

        var serveis = totesLinies.Where(l => l.Tipus == TipusLinia.Servei)
            .GroupBy(l => l.Descripcio)
            .Select(g => (g.Key, g.Count()))
            .ToList();

        var productes = totesLinies.Where(l => l.Tipus == TipusLinia.Producte)
            .GroupBy(l => l.Descripcio)
            .Select(g => (g.Key, g.Sum(l => l.Quantitat)))
            .ToList();

        var activitat = vendes
            .GroupBy(v => ADiaSetmana(v.Data))
            .Select(g => (g.Key, g.Count(), g.Sum(v => (long)v.TotalCents)))
            .ToList();

        return new DetallTreballadora(
            treballadoraId, treballadora.Nom,
            vendes.Count, ingressosCents,
            Indicadors.PercentatgeTreball((int)Math.Min(ingressosCents, int.MaxValue), (int)Math.Min(totalPeriodeCents, int.MaxValue)),
            Indicadors.PercentatgeProductes((int)Math.Min(productesCents, int.MaxValue), (int)Math.Min(ingressosCents, int.MaxValue)),
            serveis, productes, altresCents, activitat);
    }

    public async Task<List<DetallTreballadora>> RanquingTreballadores(DateOnly des, DateOnly fins)
    {
        await using var db = await factory.CreateDbContextAsync();
        var ids = await db.Treballadores.AsNoTracking().Select(t => t.Id).ToListAsync();

        var resultats = new List<DetallTreballadora>();
        foreach (var id in ids) resultats.Add(await DetallDeTreballadora(id, des, fins));

        return resultats.OrderByDescending(r => r.IngressosCents).ToList();
    }

    public async Task<List<(int any, int mes, long totalCents)>> EvolucioMensual(int mesos = 12)
    {
        await using var db = await factory.CreateDbContextAsync();
        var avui = DateOnly.FromDateTime(DateTime.Today);
        var des = new DateOnly(avui.Year, avui.Month, 1).AddMonths(-(mesos - 1));

        var vendes = await db.Vendes.AsNoTracking()
            .Where(v => v.Estat == EstatVenda.Activa && v.Data >= des)
            .Select(v => new { v.Data, v.TotalCents })
            .ToListAsync();

        var resultat = new List<(int any, int mes, long totalCents)>();
        for (int i = 0; i < mesos; i++)
        {
            var mesDate = des.AddMonths(i);
            long total = vendes
                .Where(v => v.Data.Year == mesDate.Year && v.Data.Month == mesDate.Month)
                .Sum(v => (long)v.TotalCents);
            resultat.Add((mesDate.Year, mesDate.Month, total));
        }
        return resultat;
    }

    public async Task<(Client client, int visites, long totalCents)?> ClientDelMes()
    {
        await using var db = await factory.CreateDbContextAsync();
        var avui = DateOnly.FromDateTime(DateTime.Today);
        var primerDelMes = new DateOnly(avui.Year, avui.Month, 1);

        var grup = await db.Vendes.AsNoTracking()
            .Where(v => v.Estat == EstatVenda.Activa && v.ClientId != null && v.Data >= primerDelMes && v.Data <= avui)
            .GroupBy(v => v.ClientId!.Value)
            .Select(g => new { ClientId = g.Key, Visites = g.Count(), Total = g.Sum(v => (long)v.TotalCents) })
            .OrderByDescending(g => g.Total)
            .FirstOrDefaultAsync();

        if (grup is null) return null;

        var client = await db.Clients.AsNoTracking().FirstAsync(c => c.Id == grup.ClientId);
        return (client, grup.Visites, grup.Total);
    }

    private static async Task<Dictionary<int, Client>> ClientsPerIds(BarberiaDbContext db, IEnumerable<int> ids)
    {
        var llista = ids.Distinct().ToList();
        var clients = await db.Clients.AsNoTracking().Where(c => llista.Contains(c.Id)).ToListAsync();
        return clients.ToDictionary(c => c.Id);
    }

    /// <summary>Monday = Dl, ..., Sunday = Dg, matching DisponibilitatService.</summary>
    private static DiaSetmana ADiaSetmana(DateOnly data) => (DiaSetmana)(((int)data.DayOfWeek + 6) % 7);
}
