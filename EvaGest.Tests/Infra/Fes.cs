using EvaGest.Models;

namespace EvaGest.Tests.Infra;

/// <summary>Fluent builders so each test states only what it actually cares about.</summary>
public static class Fes
{
    public static Client Client(string nom = "Joan García", string mobil = "612345678")
        => new() { Nom = nom, Mobil = mobil, ClientKey = $"{nom.ToLower()}{mobil}" };

    public static Servei Servei(string nom = "Tall", int preuCents = 1500,
                                int ivaBp = 2100, int? durada = 30)
        => new() { Nom = nom, PreuCents = preuCents, IvaBp = ivaBp,
                   DuradaMin = durada, Actiu = true };

    public static Producte Producte(string nom = "Cera", int preuCents = 900, int ivaBp = 2100)
        => new() { Nom = nom, PreuCents = preuCents, IvaBp = ivaBp, Actiu = true };

    public static Treballadora Treballadora(string nom = "Marta", bool activa = true)
        => new() { Nom = nom, Actiu = activa, Color = "#0F766E" };

    public static MetodePagament Metode(string nom = "Efectiu")
        => new() { Nom = nom, Actiu = true };

    public static VendaLinia Linia(int importCents, int ivaBp = 2100, int quantitat = 1)
        => new() { Descripcio = "Prova", Quantitat = quantitat,
                   PreuUnitariCents = importCents / quantitat,
                   IvaBp = ivaBp, ImportCents = importCents };

    /// <summary>Builds a sale together with its frozen VAT breakdown, mirroring what
    /// VendaService will do once it exists (decision 6.4: never recompute from lines).</summary>
    public static Venda Venda(DateOnly data, int metodePagamentId, EstatVenda estat,
                              params VendaLinia[] linies)
    {
        var d = EvaGest.Services.IvaCalculator.Calcular(linies, IvaMode.Inclos);
        return new Venda
        {
            Data = data,
            Hora = new TimeOnly(10, 0),
            NomConvidat = "Client de prova", // satisfies the client XOR guest check constraint
            MetodePagamentId = metodePagamentId,
            BaseCents = d.BaseCents,
            IvaCents = d.IvaCents,
            TotalCents = d.TotalCents,
            IvaMode = IvaMode.Inclos,
            Estat = estat,
            Linies = [.. linies],
            Desglossaments = EvaGest.Services.IvaCalculator.ARegistres(linies, IvaMode.Inclos)
        };
    }
}
