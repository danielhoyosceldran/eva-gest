using System.Globalization;
using System.Text;
using EvaGest.Data;
using EvaGest.Models;
using Microsoft.EntityFrameworkCore;

namespace EvaGest.Services;

public class ClientService(IDbContextFactory<BarberiaDbContext> factory) : IClientService
{
    public async Task<List<Client>> ObtenirActius()
    {
        await using var db = await factory.CreateDbContextAsync();
        return await db.Clients.AsNoTracking()
            .Where(c => !c.Adormit)
            .OrderBy(c => c.Nom)
            .ToListAsync();
    }

    public async Task<List<Client>> ObtenirAdormits()
    {
        await using var db = await factory.CreateDbContextAsync();
        return await db.Clients.AsNoTracking()
            .Where(c => c.Adormit)
            .OrderBy(c => c.Nom)
            .ToListAsync();
    }

    public async Task<List<Client>> Cercar(string text)
    {
        await using var db = await factory.CreateDbContextAsync();
        string patro = $"%{text.Trim()}%";
        return await db.Clients.AsNoTracking()
            .Where(c => !c.Adormit && (EF.Functions.Like(c.Nom, patro) || EF.Functions.Like(c.Mobil, patro)))
            .OrderBy(c => c.Nom)
            .ToListAsync();
    }

    public async Task<Client?> ObtenirPerId(int id)
    {
        await using var db = await factory.CreateDbContextAsync();
        return await db.Clients.AsNoTracking().FirstOrDefaultAsync(c => c.Id == id);
    }

    public async Task<Client?> BuscarPossibleDuplicat(string nom, string mobil)
    {
        string clau = CalcularClientKey(nom, mobil);
        await using var db = await factory.CreateDbContextAsync();
        return await db.Clients.AsNoTracking().FirstOrDefaultAsync(c => c.ClientKey == clau);
    }

    public async Task<int> Crear(Client client)
    {
        client.ClientKey = CalcularClientKey(client.Nom, client.Mobil);
        await using var db = await factory.CreateDbContextAsync();
        db.Clients.Add(client);
        await db.SaveChangesAsync();
        return client.Id;
    }

    public async Task Actualitzar(Client client)
    {
        client.ClientKey = CalcularClientKey(client.Nom, client.Mobil);
        await using var db = await factory.CreateDbContextAsync();
        db.Clients.Update(client);
        await db.SaveChangesAsync();
    }

    public async Task Adormir(int clientId)
    {
        await using var db = await factory.CreateDbContextAsync();
        var client = await db.Clients.FirstAsync(c => c.Id == clientId);
        client.Adormit = true;
        await db.SaveChangesAsync();
    }

    public async Task Despertar(int clientId)
    {
        await using var db = await factory.CreateDbContextAsync();
        var client = await db.Clients.FirstAsync(c => c.Id == clientId);
        client.Adormit = false;
        await db.SaveChangesAsync();
    }

    public async Task Eliminar(int clientId)
    {
        await using var db = await factory.CreateDbContextAsync();
        var client = await db.Clients.FirstAsync(c => c.Id == clientId);
        db.Clients.Remove(client); // cascades to Cites and Vendes (DbContext OnModelCreating)
        await db.SaveChangesAsync();
    }

    public async Task<(int cites, int vendes)> ComptarHistorial(int clientId)
    {
        await using var db = await factory.CreateDbContextAsync();
        int cites = await db.Cites.CountAsync(c => c.ClientId == clientId);
        int vendes = await db.Vendes.CountAsync(v => v.ClientId == clientId);
        return (cites, vendes);
    }

    public async Task<List<Client>> AniversarisAvui()
    {
        var avui = DateOnly.FromDateTime(DateTime.Today);
        await using var db = await factory.CreateDbContextAsync();
        return await db.Clients.AsNoTracking()
            .Where(c => !c.Adormit && c.DataNaixement != null
                     && c.DataNaixement.Value.Month == avui.Month
                     && c.DataNaixement.Value.Day == avui.Day)
            .ToListAsync();
    }

    public async Task<List<FilaHistorialClient>> HistorialDeClient(int clientId)
    {
        await using var db = await factory.CreateDbContextAsync();

        var cites = await db.Cites.AsNoTracking()
            .Where(c => c.ClientId == clientId)
            .Include(c => c.Servei)
            .Select(c => new FilaHistorialClient(c.Data, "Cita", c.Servei != null ? c.Servei.Nom : "—",
                c.Estat.ToString(), null))
            .ToListAsync();

        var vendes = await db.Vendes.AsNoTracking()
            .Where(v => v.ClientId == clientId)
            .Select(v => new FilaHistorialClient(v.Data, "Venda", "Venda", v.Estat.ToString(), v.TotalCents))
            .ToListAsync();

        return cites.Concat(vendes).OrderByDescending(f => f.Data).ToList();
    }

    /// <summary>
    /// Builds the duplicate-detection key: normalised first name + last 9 phone digits.
    /// "Joan" + "+34 612 345 678" and "joán" + "612345678" produce the same key.
    /// </summary>
    public static string CalcularClientKey(string nom, string mobil)
    {
        string primerNom = nom.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries)
                              .FirstOrDefault() ?? string.Empty;

        string normalitzat = new string(primerNom.Normalize(NormalizationForm.FormD)
            .Where(c => CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark)
            .ToArray()).ToLowerInvariant();

        string digits = new string(mobil.Where(char.IsDigit).ToArray());
        string ultims9 = digits.Length > 9 ? digits[^9..] : digits;

        return normalitzat + ultims9;
    }
}
