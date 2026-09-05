using AwesomeAssertions;
using EvaGest.Models;
using EvaGest.Services;
using EvaGest.Tests.Infra;
using EvaGest.ViewModels.Dialegs;
using Xunit;

namespace EvaGest.Tests.Serveis;

public class CitaDialogViewModelTests
{
    private static (ICitaService, IDisponibilitatService, IClientService, ICatalegService, ITreballadoraService)
        Serveis(BaseDadesProva bd)
    {
        var factory = new FabricaDeProva(bd.Opcions);
        return (new CitaService(factory), new DisponibilitatService(factory),
                new ClientService(factory), new CatalegService(factory), new TreballadoraService(factory));
    }

    [Fact] // F-07
    public async Task Durada_surt_del_servei_si_en_te()
    {
        await using var bd = new BaseDadesProva();
        var (cites, disp, clients, cataleg, treb) = Serveis(bd);
        int serveiId = await cataleg.CrearServei(Fes.Servei("Tall", durada: 45));
        var servei = await cataleg.ObtenirServei(serveiId);

        var vm = new CitaDialogViewModel(cites, disp, clients, cataleg, treb, DateOnly.FromDateTime(DateTime.Today));
        vm.Servei = servei;

        vm.DuradaMin.Should().Be(45);
    }

    [Fact] // F-08
    public async Task Durada_es_mante_al_valor_per_defecte_si_el_servei_no_en_te()
    {
        await using var bd = new BaseDadesProva();
        var (cites, disp, clients, cataleg, treb) = Serveis(bd);
        int serveiId = await cataleg.CrearServei(Fes.Servei("Massatge", durada: null));
        var servei = await cataleg.ObtenirServei(serveiId);

        var vm = new CitaDialogViewModel(cites, disp, clients, cataleg, treb, DateOnly.FromDateTime(DateTime.Today));
        int duradaAbans = vm.DuradaMin;
        vm.Servei = servei;

        vm.DuradaMin.Should().Be(duradaAbans); // no el toca; queda el per-defecte (30)
    }
}
