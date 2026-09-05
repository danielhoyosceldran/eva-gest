using EvaGest.Models;

namespace EvaGest.Services;

// Aggregates use long: individual amounts fit in int, but a multi-year sum
// should never depend on turnover staying below int.MaxValue.
public record ResumCaixa(
    long VendesCents,
    long EntradesCents,
    long SortidesCents,
    long BalancCents,
    long BaseCents,
    long IvaCents,
    List<DesglossamentPerTipus> DesglossamentIva);

public interface ICaixaService
{
    Task<List<MovimentCaixa>> ObtenirPerPeriode(DateOnly des, DateOnly fins);

    /// <summary>
    /// Period summary. Cancelled sales are always excluded.
    ///
    /// CANONICAL RULE: totals are summed from the frozen values on Vendes and
    /// VendaDesglossaments. They are NEVER recomputed from VendaLinies: the two
    /// methods disagree, and the gap grows without bound. See decision 6.4 / Bloc B.
    /// </summary>
    Task<ResumCaixa> Resum(DateOnly des, DateOnly fins);

    Task<int> Crear(MovimentCaixa moviment);
    Task Actualitzar(MovimentCaixa moviment);
    Task Eliminar(int movimentId);
}
