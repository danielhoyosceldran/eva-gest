using AwesomeAssertions;
using EvaGest.Models;
using EvaGest.Services;
using EvaGest.Tests.Infra;
using EvaGest.ViewModels.Dialegs;
using Xunit;

namespace EvaGest.Tests.Serveis;

/// <summary>
/// Bloc S: l'avís de client no registrat (RF-05bis). Mai bloqueja: només ofereix
/// registrar-lo, i es pot apagar des de Configuració.
/// </summary>
public class AvisConvidatTests
{
    private static readonly DateOnly Avui = DateOnly.FromDateTime(DateTime.Today);

    private static async Task<CitaDialogViewModel> DialegCita(
        BaseDadesProva bd, DialogServiceDeProva dialegs, bool avisActivat = true)
    {
        var factory = new FabricaDeProva(bd.Opcions);
        var config = new ConfiguracioService(factory);
        await new SeedService(factory, config).Sembrar();
        await config.GuardarBool(ClausConfig.MostrarAvisConvidat, avisActivat);

        var vm = new CitaDialogViewModel(
            new CitaService(factory), new DisponibilitatService(factory), new ClientService(factory),
            new CatalegService(factory), new TreballadoraService(factory), config, dialegs, Avui);
        await vm.Inicialitzacio;
        return vm;
    }

    [Fact] // S-01
    public async Task Escriure_un_nom_lliure_encen_l_avis()
    {
        await using var bd = new BaseDadesProva();
        var vm = await DialegCita(bd, new DialogServiceDeProva());

        vm.AvisClientNoRegistrat.Should().BeFalse("un camp buit encara no és cap convidat");

        vm.TextClient = "Pere";

        vm.AvisClientNoRegistrat.Should().BeTrue();
    }

    [Fact] // S-02
    public async Task Amb_l_avis_desactivat_no_apareix_mai()
    {
        await using var bd = new BaseDadesProva();
        var vm = await DialegCita(bd, new DialogServiceDeProva(), avisActivat: false);

        vm.TextClient = "Pere";

        vm.AvisClientNoRegistrat.Should().BeFalse();
    }

    [Fact] // S-03
    public async Task Registrar_lo_ara_crea_el_client_i_el_deixa_seleccionat()
    {
        await using var bd = new BaseDadesProva();
        var dialegs = new DialogServiceDeProva
        {
            ResultatDialeg = true,
            OmplirDialeg = async d =>
            {
                if (d is not ClientDialogViewModel client) return;
                client.Mobil = "600111222";
                await client.GuardarCommand.ExecuteAsync(null);
            }
        };

        var vm = await DialegCita(bd, dialegs);
        vm.TextClient = "Pere";

        await vm.RegistrarClientAraCommand.ExecuteAsync(null);

        vm.ClientSeleccionat.Should().NotBeNull();
        vm.ClientSeleccionat!.Nom.Should().Be("Pere");
        vm.TextClient.Should().BeEmpty();
        vm.AvisClientNoRegistrat.Should().BeFalse("ja no és un convidat");

        await using var db = bd.Context();
        db.Clients.Should().ContainSingle(c => c.Nom == "Pere");
    }

    [Fact] // S-04
    public async Task Cancellar_el_registre_deixa_la_cita_tal_com_estava()
    {
        await using var bd = new BaseDadesProva();
        var vm = await DialegCita(bd, new DialogServiceDeProva()); // cancels by default
        vm.TextClient = "Pere";

        await vm.RegistrarClientAraCommand.ExecuteAsync(null);

        vm.ClientSeleccionat.Should().BeNull();
        vm.TextClient.Should().Be("Pere", "el que s'havia escrit no es perd");
        vm.AvisClientNoRegistrat.Should().BeTrue();
    }

    [Fact] // S-05
    public async Task Triar_un_client_registrat_apaga_l_avis()
    {
        await using var bd = new BaseDadesProva();
        await using (var db = bd.Context())
        {
            db.Clients.Add(Fes.Client("Joana"));
            await db.SaveChangesAsync();
        }

        var vm = await DialegCita(bd, new DialogServiceDeProva());
        vm.TextClient = "Pere";
        vm.AvisClientNoRegistrat.Should().BeTrue();

        vm.OpcioSeleccionada = vm.OpcionsClient.First(o => o.Client?.Nom == "Joana");

        vm.AvisClientNoRegistrat.Should().BeFalse();
    }

    [Fact] // S-06
    public async Task Una_cita_nova_agafa_la_durada_per_defecte_de_la_configuracio()
    {
        await using var bd = new BaseDadesProva();
        var factory = new FabricaDeProva(bd.Opcions);
        var config = new ConfiguracioService(factory);
        await config.Guardar(ClausConfig.DuradaDefecteCitaMin, "45");

        var vm = new CitaDialogViewModel(
            new CitaService(factory), new DisponibilitatService(factory), new ClientService(factory),
            new CatalegService(factory), new TreballadoraService(factory), config,
            new DialogServiceDeProva(), Avui);
        await vm.Inicialitzacio;

        vm.DuradaMin.Should().Be(45);
    }

    [Fact] // S-07
    public async Task Editar_una_cita_conserva_la_seva_durada_i_no_la_per_defecte()
    {
        await using var bd = new BaseDadesProva();
        var factory = new FabricaDeProva(bd.Opcions);
        var config = new ConfiguracioService(factory);
        await config.Guardar(ClausConfig.DuradaDefecteCitaMin, "45");

        var cita = new Cita
        {
            Data = Avui, Hora = new TimeOnly(10, 0), DuradaMin = 90,
            NomConvidat = "Pere", Estat = EstatCita.Pendent
        };
        await using (var db = bd.Context())
        {
            db.Cites.Add(cita);
            await db.SaveChangesAsync();
        }

        var vm = new CitaDialogViewModel(
            new CitaService(factory), new DisponibilitatService(factory), new ClientService(factory),
            new CatalegService(factory), new TreballadoraService(factory), config,
            new DialogServiceDeProva(), cita);
        await vm.Inicialitzacio;

        vm.DuradaMin.Should().Be(90);
    }
}
