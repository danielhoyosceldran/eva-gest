using AwesomeAssertions;
using EvaGest.Models;
using EvaGest.Services;
using EvaGest.Tests.Infra;
using EvaGest.ViewModels.Dialegs;
using Xunit;

namespace EvaGest.Tests.Serveis;

public class CitaDialogViewModelTests
{
    private static readonly DateOnly Avui = DateOnly.FromDateTime(DateTime.Today);

    private sealed record Serveis(
        ICitaService Cites, IDisponibilitatService Disponibilitat, IClientService Clients,
        ICatalegService Cataleg, ITreballadoraService Treballadores, IConfiguracioService Configuracio);

    private static Serveis Muntar(BaseDadesProva bd)
    {
        var factory = new FabricaDeProva(bd.Opcions);
        return new Serveis(new CitaService(factory), new DisponibilitatService(factory),
            new ClientService(factory), new CatalegService(factory), new TreballadoraService(factory),
            new ConfiguracioService(factory));
    }

    private static CitaDialogViewModel Nova(Serveis s, DateOnly? data = null, IDialogService? dialegs = null)
        => new(s.Cites, s.Disponibilitat, s.Clients, s.Cataleg, s.Treballadores, s.Configuracio,
               dialegs ?? new DialogServiceDeProva(), data ?? Avui);

    private static CitaDialogViewModel Editar(Serveis s, Cita cita)
        => new(s.Cites, s.Disponibilitat, s.Clients, s.Cataleg, s.Treballadores, s.Configuracio,
               new DialogServiceDeProva(), cita);

    [Fact] // F-07
    public async Task Durada_surt_del_servei_si_en_te()
    {
        await using var bd = new BaseDadesProva();
        var s = Muntar(bd);
        int serveiId = await s.Cataleg.CrearServei(Fes.Servei("Tall", durada: 45));
        var servei = await s.Cataleg.ObtenirServei(serveiId);

        var vm = Nova(s);
        vm.Servei = servei;

        vm.DuradaMin.Should().Be(45);
    }

    [Fact] // F-08
    public async Task Durada_es_mante_al_valor_per_defecte_si_el_servei_no_en_te()
    {
        await using var bd = new BaseDadesProva();
        var s = Muntar(bd);
        int serveiId = await s.Cataleg.CrearServei(Fes.Servei("Massatge", durada: null));
        var servei = await s.Cataleg.ObtenirServei(serveiId);

        var vm = Nova(s);
        int duradaAbans = vm.DuradaMin;
        vm.Servei = servei;

        vm.DuradaMin.Should().Be(duradaAbans); // no el toca; queda el per-defecte (30)
    }

    [Fact] // F-09
    public async Task La_primera_opcio_sempre_es_Convidat_i_ve_triada_de_sortida()
    {
        await using var bd = new BaseDadesProva();
        var s = Muntar(bd);
        await s.Clients.Crear(Fes.Client("Joan García"));

        var vm = Nova(s);
        await vm.Inicialitzacio;

        vm.OpcionsClient[0].EsConvidat.Should().BeTrue();
        vm.OpcioSeleccionada.Should().BeSameAs(vm.OpcionsClient[0]);
        vm.PotEscriureConvidat.Should().BeTrue();
        vm.ClientSeleccionat.Should().BeNull();
    }

    [Fact] // F-10
    public async Task Editar_una_cita_d_un_client_registrat_el_mostra_al_selector()
    {
        // La regressió principal: la instància de Client de la cita ve d'una consulta
        // AsNoTracking diferent de la que omple la llista, així que comparar per
        // referència deixava el ComboBox buit.
        await using var bd = new BaseDadesProva();
        var s = Muntar(bd);
        int clientId = await s.Clients.Crear(Fes.Client("Joan García"));
        int citaId = await s.Cites.Crear(new Cita
        {
            Data = Avui, Hora = new TimeOnly(10, 0), DuradaMin = 30, ClientId = clientId
        });
        var cita = await s.Cites.ObtenirPerId(citaId);

        var vm = Editar(s, cita!);
        await vm.Inicialitzacio;

        vm.ClientSeleccionat.Should().NotBeNull();
        vm.ClientSeleccionat!.Id.Should().Be(clientId);
        vm.OpcioSeleccionada!.Nom.Should().Be("Joan García");
        vm.PotEscriureConvidat.Should().BeFalse();
    }

    [Fact] // F-10b
    public async Task Editar_manté_el_servei_la_treballadora_i_la_durada_guardades()
    {
        await using var bd = new BaseDadesProva();
        var s = Muntar(bd);
        int serveiId = await s.Cataleg.CrearServei(Fes.Servei("Tall", durada: 45));
        int treballadoraId;
        await using (var db = bd.Context())
        {
            var marta = Fes.Treballadora("Marta");
            db.Treballadores.Add(marta);
            await db.SaveChangesAsync(TestContext.Current.CancellationToken);
            treballadoraId = marta.Id;
        }
        int citaId = await s.Cites.Crear(new Cita
        {
            Data = Avui, Hora = new TimeOnly(10, 0), DuradaMin = 20, NomConvidat = "Algú",
            ServeiId = serveiId, TreballadoraId = treballadoraId
        });
        var cita = await s.Cites.ObtenirPerId(citaId);

        var vm = Editar(s, cita!);
        await vm.Inicialitzacio;

        vm.Servei!.Id.Should().Be(serveiId);
        vm.Treballadora!.Id.Should().Be(treballadoraId);
        vm.DuradaMin.Should().Be(20, "una cita editada conserva la durada amb què es va guardar");
    }

    [Fact] // F-11
    public async Task Tornar_a_Convidat_allibera_els_camps_i_neteja_el_telefon_del_client()
    {
        await using var bd = new BaseDadesProva();
        var s = Muntar(bd);
        await s.Clients.Crear(Fes.Client("Joan García", "612345678"));
        var vm = Nova(s);
        await vm.Inicialitzacio;

        vm.OpcioSeleccionada = vm.OpcionsClient.First(o => !o.EsConvidat);
        vm.PotEscriureConvidat.Should().BeFalse();
        vm.TelefonConvidat.Should().Be("612345678");

        vm.OpcioSeleccionada = vm.OpcionsClient[0];

        vm.PotEscriureConvidat.Should().BeTrue();
        vm.ClientSeleccionat.Should().BeNull();
        vm.TelefonConvidat.Should().BeNull("el mòbil del client anterior no és el del convidat");
    }

    [Fact] // F-12
    public async Task Triar_un_client_registrat_esborra_el_nom_de_convidat_escrit_abans()
    {
        await using var bd = new BaseDadesProva();
        var s = Muntar(bd);
        await s.Clients.Crear(Fes.Client("Joan García"));
        var vm = Nova(s);
        await vm.Inicialitzacio;
        vm.TextClient = "Algú de pas";

        vm.OpcioSeleccionada = vm.OpcionsClient.First(o => !o.EsConvidat);

        vm.TextClient.Should().BeEmpty();
    }

    [Fact] // F-13
    public async Task Sense_client_ni_nom_de_convidat_no_es_pot_guardar()
    {
        await using var bd = new BaseDadesProva();
        var s = Muntar(bd);
        var vm = Nova(s);
        await vm.Inicialitzacio;

        await vm.GuardarCommand.ExecuteAsync(null);

        vm.ErrorValidacio.Should().NotBeNull();
        (await s.Cites.ObtenirPerDia(Avui)).Should().BeEmpty();
    }

    [Fact] // F-14
    public async Task Guardar_un_convidat_no_deixa_cap_client_registrat_i_a_l_inreves()
    {
        await using var bd = new BaseDadesProva();
        var s = Muntar(bd);
        int clientId = await s.Clients.Crear(Fes.Client("Joan García"));

        var convidat = Nova(s);
        await convidat.Inicialitzacio;
        convidat.TextClient = "Algú de pas";
        await convidat.GuardarCommand.ExecuteAsync(null);

        var registrat = Nova(s);
        await registrat.Inicialitzacio;
        registrat.OpcioSeleccionada = registrat.OpcionsClient.First(o => !o.EsConvidat);
        registrat.Hora = new TimeOnly(12, 0);
        await registrat.GuardarCommand.ExecuteAsync(null);

        var cites = await s.Cites.ObtenirPerDia(Avui);
        cites.Should().HaveCount(2);
        var deConvidat = cites.Single(c => c.EsConvidat);
        deConvidat.NomConvidat.Should().Be("Algú de pas");
        deConvidat.ClientId.Should().BeNull();
        cites.Single(c => !c.EsConvidat).ClientId.Should().Be(clientId);
    }

    [Fact] // F-15
    public async Task L_hora_inicial_la_marca_qui_obre_el_dialeg
        () {
        await using var bd = new BaseDadesProva();
        var s = Muntar(bd);

        var vm = new CitaDialogViewModel(s.Cites, s.Disponibilitat, s.Clients, s.Cataleg,
            s.Treballadores, s.Configuracio, new DialogServiceDeProva(), Avui, new TimeOnly(16, 30));

        vm.Hora.Should().Be(new TimeOnly(16, 30));
    }

    [Fact] // F-16
    public async Task La_graella_del_dialeg_mostra_el_fantasma_a_l_hora_triada()
    {
        await using var bd = new BaseDadesProva();
        var s = Muntar(bd);
        var vm = new CitaDialogViewModel(s.Cites, s.Disponibilitat, s.Clients, s.Cataleg,
            s.Treballadores, s.Configuracio, new DialogServiceDeProva(), Avui, new TimeOnly(11, 0));
        await vm.Inicialitzacio;

        vm.Graella.Should().NotBeNull();
        var columna = vm.Graella!.Dies.Single(d => d.Data == Avui);
        columna.Cites.Should().ContainSingle(c => c.EsFantasma);
    }

    [Fact] // F-17
    public async Task Clicar_una_franja_de_la_graella_fixa_data_i_hora()
    {
        await using var bd = new BaseDadesProva();
        var s = Muntar(bd);
        var vm = Nova(s);
        await vm.Inicialitzacio;
        var graella = vm.Graella!;
        var altreDia = graella.Dies[graella.Dies.Count - 1];

        altreDia.ClicarAPosicio(60 * graella.PixelsPerMinut);

        vm.Data.Should().Be(altreDia.Data);
        vm.Hora.Should().Be(EvaGest.Helpers.GraellaHelper.AHora(graella.MinutIniciGraella + 60));
    }
}
