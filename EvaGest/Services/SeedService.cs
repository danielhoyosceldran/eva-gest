using EvaGest.Data;
using EvaGest.Models;
using Microsoft.EntityFrameworkCore;

namespace EvaGest.Services;

/// <summary>
/// Without this, a fresh database has no payment method, and a sale cannot be saved
/// without one — the application would install and then refuse to take any money.
///
/// Deliberately idempotent rather than a migration seed: settings keys get added as
/// features land, and an existing database has to pick up the new ones too. Payment
/// methods are only seeded when the table is completely empty, so a user who deleted
/// "Bizum" does not find it back after every restart.
/// </summary>
public class SeedService(
    IDbContextFactory<BarberiaDbContext> factory,
    IConfiguracioService configuracio) : ISeedService
{
    /// <summary>Defaults from esquema-bbdd 2.14. Booleans are stored as 0/1.</summary>
    private static readonly (string clau, string valor)[] ConfiguracioPerDefecte =
    [
        (ClausConfig.BarberiaNom, ""),
        (ClausConfig.BarberiaAdreca, ""),
        (ClausConfig.BarberiaTelefon, ""),
        (ClausConfig.IvaBpDefecte, "2100"),
        (ClausConfig.IvaModeActual, nameof(IvaMode.Inclos)),
        (ClausConfig.AplicarIvaCaixa, "0"),
        (ClausConfig.DuradaDefecteCitaMin, "30"),
        (ClausConfig.HoraBackup, "20:00"),
        (ClausConfig.BackupsAConservar, "15"),
        (ClausConfig.UltimaCopiaAutomatica, ""),
        (ClausConfig.MostrarAvisConvidat, "1"),
        (ClausConfig.SoConfirmacio, "1"),
    ];

    private static readonly string[] MetodesPerDefecte = ["Efectiu", "Targeta", "Bizum"];

    public async Task Sembrar()
    {
        await using var db = await factory.CreateDbContextAsync();

        var jaHiSon = await db.Configuracio.AsNoTracking()
            .Select(c => c.Clau)
            .ToListAsync();

        var quePosar = ConfiguracioPerDefecte
            .Where(p => !jaHiSon.Contains(p.clau))
            .Select(p => new ConfiguracioItem { Clau = p.clau, Valor = p.valor })
            .ToList();

        if (quePosar.Count > 0)
        {
            db.Configuracio.AddRange(quePosar);
            await db.SaveChangesAsync();
            configuracio.InvalidarCache();
        }

        if (!await db.MetodesPagament.AnyAsync())
        {
            db.MetodesPagament.AddRange(
                MetodesPerDefecte.Select(nom => new MetodePagament { Nom = nom, Actiu = true }));
            await db.SaveChangesAsync();
        }
    }
}
