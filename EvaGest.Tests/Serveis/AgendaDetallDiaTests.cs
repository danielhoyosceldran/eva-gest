using AwesomeAssertions;
using EvaGest.Models;
using EvaGest.Services;
using EvaGest.Tests.Infra;
using EvaGest.ViewModels.Pagines;
using Xunit;

namespace EvaGest.Tests.Serveis;

/// <summary>
/// Bloc U: el detall del dia de l'agenda (pantalles 2.2). La graella és compacta a
/// posta i amaga els botons d'estat; aquest panell és on es veuen sempre.
/// </summary>
public class AgendaDetallDiaTests
{
    private static readonly DateOnly Dilluns = new(2026, 9, 7);

    private static async Task<AgendaViewModel> Muntar(BaseDadesProva bd, DialogServiceDeProva? dialegs = null)
    {
        var factory = new FabricaDeProva(bd.Opcions);
        var config = new ConfiguracioService(factory);
        await new SeedService(factory, config).Sembrar();

        return new AgendaViewModel(
            new CitaService(factory), new VendaService(factory, config),
            new DisponibilitatService(factory), new ClientService(factory),
            new CatalegService(factory), new TreballadoraService(factory), config,
            new SoundServiceDeProva(), dialegs ?? new DialogServiceDeProva());
    }

    private static async Task<Cita> AfegeixCita(
        BaseDadesProva bd, DateOnly data, TimeOnly hora, EstatCita estat = EstatCita.Pendent)
    {
        await using var db = bd.Context();
        var cita = new Cita
        {
            Data = data, Hora = hora, DuradaMin = 30,
            NomConvidat = "Pere", Estat = estat
        };
        db.Cites.Add(cita);
        await db.SaveChangesAsync();
        return cita;
    }

    [Fact] // U-01
    public async Task Cap_dia_seleccionat_al_principi()
    {
        await using var bd = new BaseDadesProva();
        var vm = await Muntar(bd);
        await vm.Carregar();

        vm.HiHaDiaSeleccionat.Should().BeFalse();
        vm.CitesDelDia.Should().BeEmpty();
    }

    [Fact] // U-02
    public async Task Seleccionar_un_dia_en_llista_les_cites_ordenades()
    {
        await using var bd = new BaseDadesProva();
        await AfegeixCita(bd, Dilluns, new TimeOnly(16, 0));
        await AfegeixCita(bd, Dilluns, new TimeOnly(10, 0));
        await AfegeixCita(bd, Dilluns.AddDays(1), new TimeOnly(11, 0));

        var vm = await Muntar(bd);
        await vm.Carregar();
        await vm.SeleccionarDiaCommand.ExecuteAsync(Dilluns);

        vm.HiHaDiaSeleccionat.Should().BeTrue();
        vm.DiaSeleccionatText.Should().StartWith("Dilluns");
        vm.CitesDelDia.Select(f => f.HoraText).Should().Equal("10:00", "16:00");
        vm.CitesDelDia[0].ClientText.Should().Be("Pere");
        vm.CitesDelDia[0].EsConvidat.Should().BeTrue();
        vm.CitesDelDia[0].ServeiText.Should().Be("Sense servei");
        vm.CitesDelDia[0].TreballadoraText.Should().Be("Sense assignar");
    }

    [Fact] // U-03
    public async Task Un_dia_sense_cites_ho_diu_en_lloc_de_quedar_en_blanc()
    {
        await using var bd = new BaseDadesProva();
        var vm = await Muntar(bd);
        await vm.Carregar();

        await vm.SeleccionarDiaCommand.ExecuteAsync(Dilluns.AddDays(3));

        vm.HiHaDiaSeleccionat.Should().BeTrue();
        vm.DiaSenseCites.Should().BeTrue();
    }

    [Fact] // U-04
    public async Task Nomes_una_cita_pendent_ofereix_canviar_d_estat()
    {
        await using var bd = new BaseDadesProva();
        await AfegeixCita(bd, Dilluns, new TimeOnly(10, 0));
        await AfegeixCita(bd, Dilluns, new TimeOnly(11, 0), EstatCita.Cancellada);

        var vm = await Muntar(bd);
        await vm.Carregar();
        await vm.SeleccionarDiaCommand.ExecuteAsync(Dilluns);

        vm.CitesDelDia[0].EsPendent.Should().BeTrue();
        vm.CitesDelDia[1].EsPendent.Should().BeFalse();
        vm.CitesDelDia[1].EstatText.Should().Be("Cancel·lada");
    }

    [Fact] // U-05
    public async Task Canviar_l_estat_des_del_detall_refresca_la_llista()
    {
        await using var bd = new BaseDadesProva();
        await AfegeixCita(bd, Dilluns, new TimeOnly(10, 0));

        var vm = await Muntar(bd);
        await vm.Graella.CarregarSetmana(Dilluns);
        await vm.SeleccionarDiaCommand.ExecuteAsync(Dilluns);

        await vm.MarcarNoAssistidaCommand.ExecuteAsync(vm.CitesDelDia[0].Cita);

        vm.CitesDelDia[0].EstatText.Should().Be("No assistida");
        vm.CitesDelDia[0].EsPendent.Should().BeFalse();
    }

    [Fact] // U-06
    public async Task Tancar_el_detall_l_amaga()
    {
        await using var bd = new BaseDadesProva();
        await AfegeixCita(bd, Dilluns, new TimeOnly(10, 0));

        var vm = await Muntar(bd);
        await vm.Carregar();
        await vm.SeleccionarDiaCommand.ExecuteAsync(Dilluns);

        vm.TancarDetallCommand.Execute(null);

        vm.HiHaDiaSeleccionat.Should().BeFalse();
        vm.CitesDelDia.Should().BeEmpty();
    }

    [Fact] // U-07
    public async Task Canviar_de_setmana_tanca_el_detall_d_un_dia_que_ja_no_es_veu()
    {
        await using var bd = new BaseDadesProva();
        await AfegeixCita(bd, Dilluns, new TimeOnly(10, 0));

        var vm = await Muntar(bd);
        await vm.Graella.CarregarSetmana(Dilluns);
        await vm.SeleccionarDiaCommand.ExecuteAsync(Dilluns);

        await vm.Graella.SetmanaSeguentCommand.ExecuteAsync(null);

        // The grid loads the new week on its own; the page notices on its next refresh
        vm.Graella.InicSetmana.Should().Be(Dilluns.AddDays(7));
        await vm.MarcarCancelladaCommand.ExecuteAsync(vm.CitesDelDia[0].Cita);

        vm.HiHaDiaSeleccionat.Should().BeFalse(
            "el panell no ha de seguir mostrant un dia que ja no és a la graella");
    }
}
