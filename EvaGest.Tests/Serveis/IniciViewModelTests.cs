using AwesomeAssertions;
using EvaGest.Models;
using EvaGest.Services;
using EvaGest.Tests.Infra;
using EvaGest.ViewModels.Pagines;
using Xunit;

namespace EvaGest.Tests.Serveis;

/// <summary>Bloc M: pantalla d'Inici — the counters the user checks every day.</summary>
public class IniciViewModelTests
{
    private static readonly DateOnly Avui = DateOnly.FromDateTime(DateTime.Today);

    private static IniciViewModel CreaViewModel(BaseDadesProva bd)
    {
        var factory = new FabricaDeProva(bd.Opcions);
        return new IniciViewModel(
            new CitaService(factory), new VendaService(factory), new CaixaService(factory),
            new ClientService(factory), new DisponibilitatService(factory), new CatalegService(factory),
            new TreballadoraService(factory), new ConfiguracioService(factory),
            new SoundServiceDeProva(), new DialogServiceDeProva());
    }

    private static async Task<int> AfegeixMetode(BaseDadesProva bd)
    {
        await using var db = bd.Context();
        var m = Fes.Metode();
        db.MetodesPagament.Add(m);
        await db.SaveChangesAsync();
        return m.Id;
    }

    [Fact] // M-01
    public async Task Comptador_de_cites_avui_compta_totes_siguin_quin_sigui_lestat()
    {
        await using var bd = new BaseDadesProva();
        await using (var db = bd.Context())
        {
            db.Cites.Add(new Cita { Data = Avui, Hora = new TimeOnly(9, 0), DuradaMin = 30, NomConvidat = "A", Estat = EstatCita.Pendent });
            db.Cites.Add(new Cita { Data = Avui, Hora = new TimeOnly(10, 0), DuradaMin = 30, NomConvidat = "B", Estat = EstatCita.Cancellada });
            await db.SaveChangesAsync();
        }

        var vm = CreaViewModel(bd);
        await vm.Carregar();

        vm.CitesAvui.Should().Be(2);
    }

    [Fact] // M-02
    public async Task Comptador_de_vendes_avui_nomes_les_actives()
    {
        await using var bd = new BaseDadesProva();
        int metodeId = await AfegeixMetode(bd);
        await using (var db = bd.Context())
        {
            db.Vendes.Add(Fes.Venda(Avui, metodeId, EstatVenda.Activa, Fes.Linia(1000, 2100)));
            db.Vendes.Add(Fes.Venda(Avui, metodeId, EstatVenda.Anullada, Fes.Linia(9999, 2100)));
            await db.SaveChangesAsync();
        }

        var vm = CreaViewModel(bd);
        await vm.Carregar();

        vm.VendesAvui.Should().Be(1);
    }

    [Fact] // M-03 / M-04
    public async Task Cobrat_avui_i_balanc_coincideixen_amb_caixaservice()
    {
        await using var bd = new BaseDadesProva();
        int metodeId = await AfegeixMetode(bd);
        await using (var db = bd.Context())
        {
            db.Vendes.Add(Fes.Venda(Avui, metodeId, EstatVenda.Activa, Fes.Linia(1500, 2100)));
            db.MovimentsCaixa.Add(new MovimentCaixa
            { Data = Avui, Tipus = TipusMoviment.Entrada, ImportCents = 500, MetodePagamentId = metodeId, Concepte = "E" });
            await db.SaveChangesAsync();
        }

        var caixa = new CaixaService(new FabricaDeProva(bd.Opcions));
        var resum = await caixa.Resum(Avui, Avui);

        var vm = CreaViewModel(bd);
        await vm.Carregar();

        vm.CobratAvuiText.Should().Be(Diners.Format((int)resum.VendesCents));
        vm.BalancText.Should().Be(Diners.Format((int)resum.BalancCents));
    }

    [Fact] // M-05
    public async Task Dia_sense_activitat_tot_a_zero_sense_excepcio()
    {
        await using var bd = new BaseDadesProva();
        var vm = CreaViewModel(bd);

        await vm.Carregar();

        vm.CitesAvui.Should().Be(0);
        vm.VendesAvui.Should().Be(0);
        vm.CobratAvuiText.Should().Be(Diners.Format(0));
    }

    [Fact] // M-06
    public async Task Avis_daniversari_amb_un_client_que_fa_anys_avui()
    {
        await using var bd = new BaseDadesProva();
        await using (var db = bd.Context())
        {
            db.Clients.Add(new Client
            {
                Nom = "Anna", Mobil = "600000000", ClientKey = "anna600000000",
                DataNaixement = new DateOnly(1990, Avui.Month, Avui.Day)
            });
            await db.SaveChangesAsync();
        }

        var vm = CreaViewModel(bd);
        await vm.Carregar();

        vm.AvisAniversariText.Should().Contain("Anna");
    }

    [Fact] // M-07
    public async Task Sense_cap_aniversari_no_es_mostra_avis()
    {
        await using var bd = new BaseDadesProva();
        await using (var db = bd.Context())
        {
            var dataNoAvui = Avui.AddDays(10);
            db.Clients.Add(new Client
            {
                Nom = "Anna", Mobil = "600000000", ClientKey = "anna600000000",
                DataNaixement = new DateOnly(1990, dataNoAvui.Month, dataNoAvui.Day)
            });
            await db.SaveChangesAsync();
        }

        var vm = CreaViewModel(bd);
        await vm.Carregar();

        vm.AvisAniversariText.Should().BeNull();
    }

    [Fact] // M-08
    public async Task Client_adormit_que_fa_anys_avui_no_apareix_a_lavis()
    {
        await using var bd = new BaseDadesProva();
        await using (var db = bd.Context())
        {
            db.Clients.Add(new Client
            {
                Nom = "Anna", Mobil = "600000000", ClientKey = "anna600000000",
                DataNaixement = new DateOnly(1990, Avui.Month, Avui.Day), Adormit = true
            });
            await db.SaveChangesAsync();
        }

        var vm = CreaViewModel(bd);
        await vm.Carregar();

        vm.AvisAniversariText.Should().BeNull();
    }
}
