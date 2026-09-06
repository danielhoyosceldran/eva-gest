using AwesomeAssertions;
using EvaGest.Models;
using EvaGest.Services;
using EvaGest.Tests.Infra;
using EvaGest.ViewModels.Pagines;
using Xunit;

namespace EvaGest.Tests.Serveis;

/// <summary>Bloc N: l'horari d'obertura, des del formulari fins a la graella.</summary>
public class HorariBarberiaTests
{
    private static ConfiguracioViewModel Muntar(BaseDadesProva bd)
    {
        var factory = new FabricaDeProva(bd.Opcions);
        var rutes = new RutesApp(Path.Combine(Path.GetTempPath(), "eva-prova.db"), Path.GetTempPath());
        return new ConfiguracioViewModel(
            new BackupService(rutes), new ExportService(factory),
            new ConfiguracioService(factory), new DisponibilitatService(factory),
            new DialogServiceDeProva());
    }

    [Fact] // N-01
    public async Task Desar_l_horari_el_deixa_llegible_per_la_graella()
    {
        await using var bd = new BaseDadesProva();
        var vm = Muntar(bd);
        await vm.Carregar();

        var dilluns = vm.DiesHorari[0];
        dilluns.Obert = true;
        dilluns.MatiInici = "09:00";
        dilluns.MatiFi = "13:00";
        dilluns.TardaInici = "16:00";
        dilluns.TardaFi = "20:00";

        await vm.GuardarHorariCommand.ExecuteAsync(null);

        vm.ErrorHorari.Should().BeNull();
        var franges = await new DisponibilitatService(new FabricaDeProva(bd.Opcions)).FranjesSetmanals();
        franges[DiaSetmana.Dl].Should().Equal(
            (new TimeOnly(9, 0), new TimeOnly(13, 0)),
            (new TimeOnly(16, 0), new TimeOnly(20, 0)));
        franges.Should().NotContainKey(DiaSetmana.Dt);
    }

    [Fact] // N-02
    public async Task Tornar_a_carregar_reomple_el_formulari_amb_el_que_es_va_desar()
    {
        await using var bd = new BaseDadesProva();
        var vm = Muntar(bd);
        await vm.Carregar();
        vm.DiesHorari[2].Obert = true;
        vm.DiesHorari[2].MatiInici = "10";
        vm.DiesHorari[2].MatiFi = "18";
        await vm.GuardarHorariCommand.ExecuteAsync(null);

        var altre = Muntar(bd);
        await altre.Carregar();

        var dimecres = altre.DiesHorari[2];
        dimecres.Obert.Should().BeTrue();
        dimecres.MatiInici.Should().Be("10:00");
        dimecres.MatiFi.Should().Be("18:00");
        dimecres.TardaInici.Should().BeEmpty();
        altre.DiesHorari[0].Obert.Should().BeFalse();
    }

    [Fact] // N-03
    public async Task Un_dia_mal_omplert_bloqueja_el_desat_i_es_marca
        () {
        await using var bd = new BaseDadesProva();
        var vm = Muntar(bd);
        await vm.Carregar();
        vm.DiesHorari[0].Obert = true;
        vm.DiesHorari[0].MatiInici = "20:00";
        vm.DiesHorari[0].MatiFi = "09:00";

        await vm.GuardarHorariCommand.ExecuteAsync(null);

        vm.ErrorHorari.Should().NotBeNull();
        vm.DiesHorari[0].Error.Should().NotBeNull();
        var franges = await new DisponibilitatService(new FabricaDeProva(bd.Opcions)).FranjesSetmanals();
        franges.Should().BeEmpty("no s'ha de desar res si hi ha errors");
    }

    [Fact] // N-04
    public async Task Desar_substitueix_l_horari_anterior_en_lloc_d_acumular_lo()
    {
        await using var bd = new BaseDadesProva();
        var vm = Muntar(bd);
        await vm.Carregar();
        vm.DiesHorari[0].Obert = true;
        vm.DiesHorari[0].MatiInici = "09:00";
        vm.DiesHorari[0].MatiFi = "20:00";
        await vm.GuardarHorariCommand.ExecuteAsync(null);

        vm.DiesHorari[0].MatiInici = "10:00";
        await vm.GuardarHorariCommand.ExecuteAsync(null);

        await using var db = bd.Context();
        db.HorariBarberia.Count().Should().Be(1);
        db.HorariBarberia.Single().HoraObertura.Should().Be(new TimeOnly(10, 0));
    }

    [Fact] // N-05
    public async Task Tancar_tots_els_dies_buida_l_horari_i_avisa()
    {
        await using var bd = new BaseDadesProva();
        var vm = Muntar(bd);
        await vm.Carregar();
        vm.DiesHorari[0].Obert = true;
        vm.DiesHorari[0].MatiInici = "09:00";
        vm.DiesHorari[0].MatiFi = "20:00";
        await vm.GuardarHorariCommand.ExecuteAsync(null);

        vm.DiesHorari[0].Obert = false;
        await vm.GuardarHorariCommand.ExecuteAsync(null);

        vm.ErrorHorari.Should().BeNull();
        vm.ConfirmacioHorari.Should().Contain("tancats");
        await using var db = bd.Context();
        db.HorariBarberia.Should().BeEmpty();
    }

    [Fact] // N-06
    public async Task Copiar_dilluns_omple_de_dimarts_a_divendres_i_no_el_cap_de_setmana()
    {
        await using var bd = new BaseDadesProva();
        var vm = Muntar(bd);
        await vm.Carregar();
        vm.DiesHorari[0].Obert = true;
        vm.DiesHorari[0].MatiInici = "09:00";
        vm.DiesHorari[0].MatiFi = "20:00";

        vm.AplicarDillunsALaRestaCommand.Execute(null);

        vm.DiesHorari.Take(5).Should().OnlyContain(d => d.Obert && d.MatiInici == "09:00");
        vm.DiesHorari[5].Obert.Should().BeFalse("dissabte no s'ha de tocar");
        vm.DiesHorari[6].Obert.Should().BeFalse("diumenge no s'ha de tocar");
    }

    [Fact] // N-07
    public async Task L_horari_desat_arriba_a_la_graella_com_a_bandes_fora_d_horari()
    {
        await using var bd = new BaseDadesProva();
        var vm = Muntar(bd);
        await vm.Carregar();
        foreach (var dia in vm.DiesHorari.Take(5))
        {
            dia.Obert = true;
            dia.MatiInici = "10:00";
            dia.MatiFi = "18:00";
        }
        await vm.GuardarHorariCommand.ExecuteAsync(null);

        var factory = new FabricaDeProva(bd.Opcions);
        var graella = new EvaGest.ViewModels.Elements.GraellaSetmanaViewModel(
            new CitaService(factory), new DisponibilitatService(factory), new ConfiguracioService(factory),
            EvaGest.ViewModels.Elements.ModeGraella.Agenda, (_, _) => { });
        await graella.CarregarSetmana(new DateOnly(2026, 9, 7));

        (graella.MinutIniciGraella, graella.MinutFiGraella).Should().Be((10 * 60, 18 * 60));
        graella.Dies[0].Bandes.Should().BeEmpty("dilluns obre tot el rang visible");
        graella.Dies[6].Bandes.Should().ContainSingle("diumenge està tancat tot el dia");
    }
}
