using System.Globalization;
using System.Text;
using EvaGest.Data;
using EvaGest.Models;
using Microsoft.EntityFrameworkCore;

using Serilog;

namespace EvaGest.Services;

public class ClientService(IDbContextFactory<ShopDbContext> factory) : IClientService
{
    public async Task<List<Client>> GetActive()
    {
        await using var db = await factory.CreateDbContextAsync();
        return await db.Clients.AsNoTracking()
            .Where(c => !c.Asleep)
            .OrderBy(c => c.Name)
            .ToListAsync();
    }

    public async Task<List<Client>> GetAsleep()
    {
        await using var db = await factory.CreateDbContextAsync();
        return await db.Clients.AsNoTracking()
            .Where(c => c.Asleep)
            .OrderBy(c => c.Name)
            .ToListAsync();
    }

    public async Task<List<Client>> Search(string text)
    {
        await using var db = await factory.CreateDbContextAsync();
        string pattern = $"%{text.Trim()}%";
        return await db.Clients.AsNoTracking()
            .Where(c => !c.Asleep && (EF.Functions.Like(c.Name, pattern) || EF.Functions.Like(c.Mobile, pattern)))
            .OrderBy(c => c.Name)
            .ToListAsync();
    }

    public async Task<Client?> GetById(int id)
    {
        await using var db = await factory.CreateDbContextAsync();
        return await db.Clients.AsNoTracking().FirstOrDefaultAsync(c => c.Id == id);
    }

    public async Task<Client?> FindByName(string name)
    {
        string key = ComputeClientKey(name);
        await using var db = await factory.CreateDbContextAsync();
        return await db.Clients.AsNoTracking().FirstOrDefaultAsync(c => c.ClientKey == key);
    }

    public async Task<int> Create(Client client)
    {
        client.ClientKey = ComputeClientKey(client.Name);
        await using var db = await factory.CreateDbContextAsync();
        db.Clients.Add(client);
        await db.SaveChangesAsync();
        Log.Information("Client {ClientId} created", client.Id);
        return client.Id;
    }

    public async Task Update(Client client)
    {
        client.ClientKey = ComputeClientKey(client.Name);
        await using var db = await factory.CreateDbContextAsync();
        db.Clients.Update(client);
        await db.SaveChangesAsync();
        Log.Information("Client {ClientId} updated", client.Id);
    }

    public async Task Sleep(int clientId)
    {
        await using var db = await factory.CreateDbContextAsync();
        var client = await db.Clients.FirstAsync(c => c.Id == clientId);
        client.Asleep = true;
        await db.SaveChangesAsync();
    }

    public async Task Wake(int clientId)
    {
        await using var db = await factory.CreateDbContextAsync();
        var client = await db.Clients.FirstAsync(c => c.Id == clientId);
        client.Asleep = false;
        await db.SaveChangesAsync();
    }

    public async Task Delete(int clientId)
    {
        await using var db = await factory.CreateDbContextAsync();
        var client = await db.Clients.FirstAsync(c => c.Id == clientId);
        db.Clients.Remove(client); // cascades to Appointments and Sales (DbContext OnModelCreating)
        await db.SaveChangesAsync();
    }

    public async Task<(int appointments, int sales)> CountHistory(int clientId)
    {
        await using var db = await factory.CreateDbContextAsync();
        int appointments = await db.Appointments.CountAsync(c => c.ClientId == clientId);
        int sales = await db.Sales.CountAsync(v => v.ClientId == clientId);
        return (appointments, sales);
    }

    public async Task<List<Client>> BirthdaysToday()
    {
        var today = DateOnly.FromDateTime(DateTime.Today);
        await using var db = await factory.CreateDbContextAsync();
        return await db.Clients.AsNoTracking()
            .Where(c => !c.Asleep && c.BirthDate != null
                     && c.BirthDate.Value.Month == today.Month
                     && c.BirthDate.Value.Day == today.Day)
            .ToListAsync();
    }

    public async Task<List<ClientHistoryRow>> GetClientHistory(int clientId)
    {
        await using var db = await factory.CreateDbContextAsync();

        var appointments = await db.Appointments.AsNoTracking()
            .Where(c => c.ClientId == clientId)
            .Include(c => c.Service)
            .Select(c => new ClientHistoryRow(c.Date, HistoryType.Appointment,
                c.Service != null ? c.Service.Name : "—",
                c.Status.ToString(), null))
            .ToListAsync();

        var sales = await db.Sales.AsNoTracking()
            .Where(v => v.ClientId == clientId)
            .Select(v => new ClientHistoryRow(v.Date, HistoryType.Sale, null,
                v.Status.ToString(), v.TotalCents))
            .ToListAsync();

        return appointments.Concat(sales).OrderByDescending(f => f.Date).ToList();
    }

    /// <summary>
    /// Builds the identity key: the whole name, normalised. Two clients may not share
    /// a name (decision 6.1, revised) — "Joan García" twice is refused even on two
    /// different phone numbers, and the user tells them apart by adding a surname or a
    /// second given name.
    ///
    /// The phone deliberately takes no part. It used to: the key was the FIRST name
    /// plus the last nine digits, which made this a duplicate-detection heuristic that
    /// wanted false positives, sitting under a unique index that could not tolerate
    /// one. Those two jobs cannot share a key, and the index was the half that won —
    /// by crashing.
    ///
    /// Accents, case and spacing are normalised away, so "Joan García", "joan garcia"
    /// and "JoanGarcía" are all one client.
    /// </summary>
    public static string ComputeClientKey(string name)
    {
        string withoutAccents = new(name.Normalize(NormalizationForm.FormD)
            .Where(c => CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark)
            .ToArray());

        return new string(withoutAccents.Where(c => !char.IsWhiteSpace(c)).ToArray())
            .ToLowerInvariant();
    }
}
