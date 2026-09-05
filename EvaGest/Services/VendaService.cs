using EvaGest.Data;
using EvaGest.Models;
using Microsoft.EntityFrameworkCore;

namespace EvaGest.Services;

/// <summary>
/// The core of the business (Fase 6). Freezes price, VAT rate and the VAT mode in
/// force onto every line and breakdown row at save time (decision 6.4): past sales
/// must never move when the catalogue or the global VAT mode changes later.
/// </summary>
public class VendaService(IDbContextFactory<BarberiaDbContext> factory) : IVendaService
{
    // TODO: read from ConfiguracioService once it exists (Fase 9+). Hardcoded default
    // matches the documented default (esquema-bbdd 2.13) so behaviour is correct meanwhile.
    private const IvaMode ModeIvaPerDefecte = IvaMode.Inclos;

    public async Task<List<Venda>> Cercar(FiltreVendes filtre)
    {
        await using var db = await factory.CreateDbContextAsync();
        var query = Consulta(db);

        if (filtre.Des is DateOnly des) query = query.Where(v => v.Data >= des);
        if (filtre.Fins is DateOnly fins) query = query.Where(v => v.Data <= fins);
        if (filtre.ClientId is int clientId) query = query.Where(v => v.ClientId == clientId);
        if (filtre.MetodePagamentId is int metodeId) query = query.Where(v => v.MetodePagamentId == metodeId);
        if (filtre.TreballadoraId is int treballadoraId) query = query.Where(v => v.TreballadoraId == treballadoraId);
        if (filtre.Estat is EstatVenda estat) query = query.Where(v => v.Estat == estat);
        if (filtre.ServeiId is int serveiId) query = query.Where(v => v.Linies.Any(l => l.ServeiId == serveiId));
        if (filtre.ProducteId is int producteId) query = query.Where(v => v.Linies.Any(l => l.ProducteId == producteId));

        return await query.OrderByDescending(v => v.Data).ThenByDescending(v => v.Hora).ToListAsync();
    }

    public async Task<Venda?> ObtenirPerId(int id)
    {
        await using var db = await factory.CreateDbContextAsync();
        return await Consulta(db).FirstOrDefaultAsync(v => v.Id == id);
    }

    public async Task<List<Venda>> ObtenirPerClient(int clientId)
    {
        await using var db = await factory.CreateDbContextAsync();
        return await Consulta(db)
            .Where(v => v.ClientId == clientId)
            .OrderByDescending(v => v.Data).ThenByDescending(v => v.Hora)
            .ToListAsync();
    }

    public async Task<int> Crear(Venda venda, List<VendaLinia> linies)
    {
        CongelarTotals(venda, linies);

        await using var db = await factory.CreateDbContextAsync();
        venda.Linies = linies;
        db.Vendes.Add(venda);
        await db.SaveChangesAsync();

        if (venda.CitaId is int citaId)
        {
            var cita = await db.Cites.FirstAsync(c => c.Id == citaId);
            cita.Estat = EstatCita.Realitzada;
            await db.SaveChangesAsync();
        }

        return venda.Id;
    }

    public async Task Actualitzar(Venda venda, List<VendaLinia> linies)
    {
        CongelarTotals(venda, linies);

        await using var db = await factory.CreateDbContextAsync();

        var existent = await db.Vendes
            .Include(v => v.Linies)
            .Include(v => v.Desglossaments)
            .FirstAsync(v => v.Id == venda.Id);

        db.VendaLinies.RemoveRange(existent.Linies);
        db.VendaDesglossaments.RemoveRange(existent.Desglossaments);

        existent.Data = venda.Data;
        existent.Hora = venda.Hora;
        existent.ClientId = venda.ClientId;
        existent.NomConvidat = venda.NomConvidat;
        existent.TelefonConvidat = venda.TelefonConvidat;
        existent.TreballadoraId = venda.TreballadoraId;
        existent.MetodePagamentId = venda.MetodePagamentId;
        existent.Observacions = venda.Observacions;
        existent.BaseCents = venda.BaseCents;
        existent.IvaCents = venda.IvaCents;
        existent.TotalCents = venda.TotalCents;
        existent.Linies = linies;
        existent.Desglossaments = venda.Desglossaments;

        await db.SaveChangesAsync();
    }

    public async Task Anullar(int vendaId)
    {
        await using var db = await factory.CreateDbContextAsync();
        var venda = await db.Vendes.FirstAsync(v => v.Id == vendaId);
        venda.Estat = EstatVenda.Anullada;
        await db.SaveChangesAsync();
    }

    public async Task<Venda> PreparaDesDeCita(int citaId)
    {
        await using var db = await factory.CreateDbContextAsync();
        var cita = await db.Cites.AsNoTracking()
            .Include(c => c.Client)
            .Include(c => c.Servei)
            .Include(c => c.Treballadora)
            .FirstOrDefaultAsync(c => c.Id == citaId)
            ?? throw new InvalidOperationException("La cita no existeix.");

        // A cancelled or no-show appointment can never carry a sale (F-05).
        if (cita.Estat is EstatCita.Cancellada or EstatCita.NoAssistida)
            throw new InvalidOperationException(
                "No es pot associar una venda a una cita cancel·lada o no assistida.");

        return new Venda
        {
            Data = cita.Data,
            Hora = cita.Hora,
            ClientId = cita.ClientId,
            NomConvidat = cita.NomConvidat,
            TelefonConvidat = cita.TelefonConvidat,
            TreballadoraId = cita.TreballadoraId,
            CitaId = cita.Id
        };
    }

    /// <summary>Computes the VAT breakdown and freezes it onto the sale and its lines.
    /// Never recomputed later: this is the one place BaseCents/IvaCents/TotalCents are set.</summary>
    private static void CongelarTotals(Venda venda, List<VendaLinia> linies)
    {
        var desglossat = IvaCalculator.Calcular(linies, ModeIvaPerDefecte);
        venda.BaseCents = desglossat.BaseCents;
        venda.IvaCents = desglossat.IvaCents;
        venda.TotalCents = desglossat.TotalCents;
        venda.IvaMode = ModeIvaPerDefecte;
        venda.Desglossaments = IvaCalculator.ARegistres(linies, ModeIvaPerDefecte);
    }

    private static IQueryable<Venda> Consulta(BarberiaDbContext db)
        => db.Vendes.AsNoTracking()
            .Include(v => v.Client)
            .Include(v => v.Treballadora)
            .Include(v => v.MetodePagament)
            .Include(v => v.Linies);
}
