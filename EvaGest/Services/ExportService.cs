using System.Globalization;
using System.IO;
using System.Text;
using EvaGest.Data;
using EvaGest.Models;
using Microsoft.EntityFrameworkCore;

namespace EvaGest.Services;

/// <summary>
/// Fase 9. Writes the two CSV files the assessoria needs for Modelo 303, straight
/// from the frozen Vendes / VendaDesglossaments rows (casos-us CU-08).
/// </summary>
public class ExportService(IDbContextFactory<BarberiaDbContext> factory) : IExportService
{
    private static readonly CultureInfo Cultura = new("ca-ES");

    public async Task ExportarVendes(DateOnly des, DateOnly fins, string carpetaDesti)
    {
        Directory.CreateDirectory(carpetaDesti);
        string periode = $"{des:yyyyMMdd}-{fins:yyyyMMdd}";

        await using var db = await factory.CreateDbContextAsync();

        var vendes = await db.Vendes.AsNoTracking()
            .Where(v => v.Estat == EstatVenda.Activa && v.Data >= des && v.Data <= fins)
            .Include(v => v.Client)
            .Include(v => v.Treballadora)
            .Include(v => v.MetodePagament)
            .Include(v => v.Linies)
            .OrderBy(v => v.Data).ThenBy(v => v.Hora)
            .ToListAsync();

        await EscriureVendes(Path.Combine(carpetaDesti, $"vendes_{periode}.csv"), vendes);

        var desglossaments = await db.VendaDesglossaments.AsNoTracking()
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

        await EscriureIva(Path.Combine(carpetaDesti, $"iva_{periode}.csv"),
            desglossaments.Select(d => (d.IvaBp, d.Base, d.Iva, d.Total)).ToList());
    }

    private static async Task EscriureVendes(string ruta, List<Venda> vendes)
    {
        var sb = new StringBuilder();
        sb.AppendLine("data;hora;client;treballadora;conceptes;metode_pagament;base;iva;total");

        foreach (var v in vendes)
        {
            string concepte = string.Join(" + ", v.Linies.Select(l => l.Descripcio));
            sb.AppendLine(string.Join(';',
                v.Data.ToString("dd/MM/yyyy"),
                v.Hora.ToString("HH:mm"),
                Csv(v.NomMostrat),
                Csv(v.Treballadora?.Nom ?? ""),
                Csv(concepte),
                Csv(v.MetodePagament.Nom),
                Diners.FormatExport(v.BaseCents),
                Diners.FormatExport(v.IvaCents),
                Diners.FormatExport(v.TotalCents)));
        }

        await File.WriteAllTextAsync(ruta, sb.ToString(), Encoding.UTF8);
    }

    private static async Task EscriureIva(string ruta, List<(int ivaBp, long baseCents, long ivaCents, long totalCents)> files)
    {
        var sb = new StringBuilder();
        sb.AppendLine("tipus_iva;base;quota;total");

        foreach (var f in files)
        {
            sb.AppendLine(string.Join(';',
                Percentatges.Format(f.ivaBp),
                (f.baseCents / 100m).ToString("F2", Cultura),
                (f.ivaCents / 100m).ToString("F2", Cultura),
                (f.totalCents / 100m).ToString("F2", Cultura)));
        }

        await File.WriteAllTextAsync(ruta, sb.ToString(), Encoding.UTF8);
    }

    /// <summary>Quotes a field only when it actually contains the separator, a quote
    /// or a newline — keeps the common case readable in a plain text editor.</summary>
    private static string Csv(string valor)
        => valor.Contains(';') || valor.Contains('"') || valor.Contains('\n')
            ? $"\"{valor.Replace("\"", "\"\"")}\""
            : valor;
}
