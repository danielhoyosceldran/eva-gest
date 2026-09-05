using AwesomeAssertions;
using EvaGest.Models;
using EvaGest.Services;
using EvaGest.Tests.Infra;
using Xunit;

namespace EvaGest.Tests.Serveis;

/// <summary>Bloc H: caixa i balanç. Canonical rule: everything is summed from the
/// frozen Vendes / VendaDesglossaments rows, never recomputed from VendaLinies.</summary>
public class CaixaServiceTests
{
    private static readonly DateOnly Avui = new(2026, 9, 7);

    private static CaixaService CreaServei(BaseDadesProva bd) => new(new FabricaDeProva(bd.Opcions));

    private static async Task<int> AfegeixMetode(BaseDadesProva bd)
    {
        await using var db = bd.Context();
        var m = Fes.Metode();
        db.MetodesPagament.Add(m);
        await db.SaveChangesAsync();
        return m.Id;
    }

    [Fact] // H-01
    public async Task Vendes_mes_entrades_menys_sortides_iguala_balanc()
    {
        await using var bd = new BaseDadesProva();
        int metodeId = await AfegeixMetode(bd);
        await using (var db = bd.Context())
        {
            db.Vendes.Add(Fes.Venda(Avui, metodeId, EstatVenda.Activa, Fes.Linia(1500, 2100)));
            db.MovimentsCaixa.Add(new MovimentCaixa
            {
                Data = Avui, Tipus = TipusMoviment.Entrada, ImportCents = 500, MetodePagamentId = metodeId, Concepte = "Entrada"
            });
            db.MovimentsCaixa.Add(new MovimentCaixa
            {
                Data = Avui, Tipus = TipusMoviment.Sortida, ImportCents = 300, MetodePagamentId = metodeId, Concepte = "Sortida"
            });
            await db.SaveChangesAsync();
        }

        var caixa = CreaServei(bd);
        var resum = await caixa.Resum(Avui, Avui);

        resum.BalancCents.Should().Be(resum.VendesCents + resum.EntradesCents - resum.SortidesCents);
        resum.BalancCents.Should().Be(1500 + 500 - 300);
    }

    [Fact] // H-02
    public async Task Periode_avui_amb_tres_vendes_i_una_sortida()
    {
        await using var bd = new BaseDadesProva();
        int metodeId = await AfegeixMetode(bd);
        await using (var db = bd.Context())
        {
            db.Vendes.Add(Fes.Venda(Avui, metodeId, EstatVenda.Activa, Fes.Linia(1000, 2100)));
            db.Vendes.Add(Fes.Venda(Avui, metodeId, EstatVenda.Activa, Fes.Linia(1000, 2100)));
            db.Vendes.Add(Fes.Venda(Avui, metodeId, EstatVenda.Activa, Fes.Linia(1000, 2100)));
            db.MovimentsCaixa.Add(new MovimentCaixa
            {
                Data = Avui, Tipus = TipusMoviment.Sortida, ImportCents = 500, MetodePagamentId = metodeId, Concepte = "Sortida"
            });
            await db.SaveChangesAsync();
        }

        var caixa = CreaServei(bd);
        var resum = await caixa.Resum(Avui, Avui);

        resum.VendesCents.Should().Be(3000);
        resum.SortidesCents.Should().Be(500);
        resum.BalancCents.Should().Be(2500);
    }

    [Fact] // H-04
    public async Task Periode_personalitzat_inclou_els_dos_extrems()
    {
        await using var bd = new BaseDadesProva();
        int metodeId = await AfegeixMetode(bd);
        var fins = Avui.AddDays(5);
        await using (var db = bd.Context())
        {
            db.Vendes.Add(Fes.Venda(Avui, metodeId, EstatVenda.Activa, Fes.Linia(1000, 2100)));
            db.Vendes.Add(Fes.Venda(fins, metodeId, EstatVenda.Activa, Fes.Linia(1000, 2100)));
            db.Vendes.Add(Fes.Venda(fins.AddDays(1), metodeId, EstatVenda.Activa, Fes.Linia(9999, 2100)));
            await db.SaveChangesAsync();
        }

        var caixa = CreaServei(bd);
        var resum = await caixa.Resum(Avui, fins);

        resum.VendesCents.Should().Be(2000);
    }

    [Fact] // H-05
    public async Task Moviment_sense_iva_quan_lopcio_esta_desactivada()
    {
        await using var bd = new BaseDadesProva();
        int metodeId = await AfegeixMetode(bd);
        await using var db = bd.Context();
        db.MovimentsCaixa.Add(new MovimentCaixa
        {
            Data = Avui, Tipus = TipusMoviment.Entrada, ImportCents = 1000, MetodePagamentId = metodeId, Concepte = "Propina"
        });
        await db.SaveChangesAsync();

        var moviment = db.MovimentsCaixa.Single();
        moviment.BaseCents.Should().BeNull();
        moviment.IvaCents.Should().BeNull();
    }

    [Fact] // H-06
    public void Moviment_amb_iva_guarda_base_i_quota_calculades()
    {
        var vm = new EvaGest.ViewModels.Dialegs.MovimentDialogViewModel(TipusMoviment.Entrada)
        {
            PreuText = "15,00", IvaText = "21", DesglossarIva = true,
            Metode = new MetodePagament { Id = 1, Nom = "Efectiu" }
        };

        var model = vm.AModel();
        model.BaseCents.Should().Be(1240);
        model.IvaCents.Should().Be(260);
    }

    [Fact] // H-08
    public async Task Sortida_resta_al_balanc_entrada_suma()
    {
        await using var bd = new BaseDadesProva();
        int metodeId = await AfegeixMetode(bd);
        await using (var db = bd.Context())
        {
            db.MovimentsCaixa.Add(new MovimentCaixa
            {
                Data = Avui, Tipus = TipusMoviment.Entrada, ImportCents = 1000, MetodePagamentId = metodeId, Concepte = "E"
            });
            db.MovimentsCaixa.Add(new MovimentCaixa
            {
                Data = Avui, Tipus = TipusMoviment.Sortida, ImportCents = 400, MetodePagamentId = metodeId, Concepte = "S"
            });
            await db.SaveChangesAsync();
        }

        var caixa = CreaServei(bd);
        var resum = await caixa.Resum(Avui, Avui);

        resum.BalancCents.Should().Be(1000 - 400);
    }

    [Fact] // H-09
    public async Task Desglossament_per_tipus_una_fila_per_tipus_present()
    {
        await using var bd = new BaseDadesProva();
        int metodeId = await AfegeixMetode(bd);
        await using (var db = bd.Context())
        {
            db.Vendes.Add(Fes.Venda(Avui, metodeId, EstatVenda.Activa, Fes.Linia(1500, 2100)));
            db.Vendes.Add(Fes.Venda(Avui, metodeId, EstatVenda.Activa, Fes.Linia(900, 1000)));
            await db.SaveChangesAsync();
        }

        var caixa = CreaServei(bd);
        var resum = await caixa.Resum(Avui, Avui);

        resum.DesglossamentIva.Should().HaveCount(2);
    }

    [Fact] // B-04 reforçat: vendes anul·lades excloses del balanç de caixa
    public async Task Vendes_anullades_excloses_del_resum_caixa()
    {
        await using var bd = new BaseDadesProva();
        int metodeId = await AfegeixMetode(bd);
        await using (var db = bd.Context())
        {
            db.Vendes.Add(Fes.Venda(Avui, metodeId, EstatVenda.Activa, Fes.Linia(1500, 2100)));
            db.Vendes.Add(Fes.Venda(Avui, metodeId, EstatVenda.Anullada, Fes.Linia(9999, 2100)));
            await db.SaveChangesAsync();
        }

        var caixa = CreaServei(bd);
        var resum = await caixa.Resum(Avui, Avui);

        resum.VendesCents.Should().Be(1500);
    }

    [Fact] // H-03/H-06 complementari: període sense moviments ni vendes torna zero
    public async Task Periode_sense_res_torna_zero_sense_excepcio()
    {
        await using var bd = new BaseDadesProva();
        var caixa = CreaServei(bd);

        var resum = await caixa.Resum(Avui, Avui);

        resum.VendesCents.Should().Be(0);
        resum.BalancCents.Should().Be(0);
        resum.DesglossamentIva.Should().BeEmpty();
    }
}
