using System.Globalization;
using System.Text;
using EvaGest.Data;
using EvaGest.Models;
using Microsoft.EntityFrameworkCore;

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

    public async Task<Client?> FindPossibleDuplicate(string name, string mobile)
    {
        string key = ComputeClientKey(name, mobile);
        await using var db = await factory.CreateDbContextAsync();
        return await db.Clients.AsNoTracking().FirstOrDefaultAsync(c => c.ClientKey == key);
    }

    public async Task<int> Create(Client client)
    {
        client.ClientKey = ComputeClientKey(client.Name, client.Mobile);
        await using var db = await factory.CreateDbContextAsync();
        db.Clients.Add(client);
        await db.SaveChangesAsync();
        return client.Id;
    }

    public async Task Update(Client client)
    {
        client.ClientKey = ComputeClientKey(client.Name, client.Mobile);
        await using var db = await factory.CreateDbContextAsync();
        db.Clients.Update(client);
        await db.SaveChangesAsync();
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
    /// Builds the duplicate-detection key: normalised first name + last 9 phone digits.
    /// "Joan" + "+34 612 345 678" and "joán" + "612345678" produce the same key.
    /// </summary>
    public static string ComputeClientKey(string name, string mobile)
    {
        string firstName = name.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries)
                              .FirstOrDefault() ?? string.Empty;

        string normalized = new string(firstName.Normalize(NormalizationForm.FormD)
            .Where(c => CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark)
            .ToArray()).ToLowerInvariant();

        string digits = new string(mobile.Where(char.IsDigit).ToArray());
        string last9 = digits.Length > 9 ? digits[^9..] : digits;

        return normalized + last9;
    }
}
