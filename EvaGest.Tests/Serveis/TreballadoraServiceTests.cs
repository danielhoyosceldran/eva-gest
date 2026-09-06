using AwesomeAssertions;
using EvaGest.Helpers;
using EvaGest.Models;
using EvaGest.Services;
using EvaGest.Tests.Infra;
using EvaGest.ViewModels.Dialegs;
using EvaGest.ViewModels.Pagines;
using Xunit;

namespace EvaGest.Tests.Serveis;

/// <summary>
/// Bloc O: treballadores i els seus horaris. Sense aquestes files, el càlcul de
/// disponibilitat (bloc E) compta zero treballadores i avisa de solapament sempre,
/// així que aquestes proves cobreixen el que fa que l'agenda tingui sentit.
/// </summary>
public class TreballadoraServiceTests
{
    private static Dictionary<DiaSetmana, List<(TimeOnly inici, TimeOnly fi)>> Horari(
        params (DiaSetmana dia, int desHora, int finsHora)[] franges)
        => franges
            .GroupBy(f => f.dia)
            .ToDictionary(
                g => g.Key,
                g => g.Select(f => (new TimeOnly(f.desHora, 0), new TimeOnly(f.finsHora, 0))).ToList());

    [Fact] // O-01
    public async Task Crear_desa_la_treballadora_amb_totes_les_seves_franges()
    {
        await using var bd = new BaseDadesProva();
        var servei = new TreballadoraService(new FabricaDeProva(bd.Opcions));

        await servei.Crear(Fes.Treballadora("Marta"),
            Horari((DiaSetmana.Dl, 9, 14), (DiaSetmana.Dl, 16, 20), (DiaSetmana.Dt, 9, 14)));

        var horari = await servei.HorariDe((await servei.ObtenirTotes()).Single().Id);
        horari[DiaSetmana.Dl].Should().Equal(
            (new TimeOnly(9, 0), new TimeOnly(14, 0)),
            (new TimeOnly(16, 0), new TimeOnly(20, 0)));
        horari[DiaSetmana.Dt].Should().ContainSingle();
        horari.Should().NotContainKey(DiaSetmana.Dc);
    }

    [Fact] // O-02
    public async Task Actualitzar_substitueix_l_horari_en_lloc_d_acumular_lo()
    {
        await using var bd = new BaseDadesProva();
        var servei = new TreballadoraService(new FabricaDeProva(bd.Opcions));
        var creada = await servei.Crear(Fes.Treballadora(), Horari((DiaSetmana.Dl, 9, 14)));

        creada.Nom = "Marta Puig";
        await servei.Actualitzar(creada, Horari((DiaSetmana.Dv, 10, 18)));

        await using var db = bd.Context();
        db.HorarisTreballadora.Should().ContainSingle();
        db.HorarisTreballadora.Single().DiaSetmana.Should().Be(DiaSetmana.Dv);
        db.Treballadores.Single().Nom.Should().Be("Marta Puig");
    }

    [Fact] // O-03
    public async Task Marcar_inactiva_conserva_l_horari_pero_la_treu_de_la_disponibilitat()
    {
        await using var bd = new BaseDadesProva();
        var factory = new FabricaDeProva(bd.Opcions);
        var servei = new TreballadoraService(factory);
        var creada = await servei.Crear(Fes.Treballadora(), Horari((DiaSetmana.Dl, 9, 20)));

        await servei.CanviarEstat(creada.Id, actiu: false);

        var horari = await servei.HorariDe(creada.Id);
        horari[DiaSetmana.Dl].Should().ContainSingle("l'horari es conserva per quan torni");

        var disponibles = await new DisponibilitatService(factory)
            .TreballadoresDisponibles(new DateOnly(2026, 9, 7), new TimeOnly(10, 0), 30);
        disponibles.Should().BeEmpty("una treballadora inactiva no compta com a disponible");
    }

    [Fact] // O-04
    public async Task Una_treballadora_amb_horari_evita_l_avis_de_solapament_de_la_primera_cita()
    {
        await using var bd = new BaseDadesProva();
        var factory = new FabricaDeProva(bd.Opcions);
        await new TreballadoraService(factory).Crear(Fes.Treballadora(), Horari((DiaSetmana.Dl, 9, 20)));

        var resultat = await new DisponibilitatService(factory)
            .Comprovar(new DateOnly(2026, 9, 7), new TimeOnly(10, 0), 30, treballadoraId: null);

        resultat.TreballadoresDisponibles.Should().Be(1);
        resultat.HiHaSolapament.Should().BeFalse();
    }

    [Fact] // O-05
    public async Task La_pagina_llista_les_treballadores_amb_l_horari_resumit()
    {
        await using var bd = new BaseDadesProva();
        var servei = new TreballadoraService(new FabricaDeProva(bd.Opcions));
        await servei.Crear(Fes.Treballadora("Marta"), Horari((DiaSetmana.Dl, 9, 14), (DiaSetmana.Dt, 9, 14)));

        var vm = new TreballadoresViewModel(servei, new DialogServiceDeProva());
        await vm.Carregar();

        vm.NoHiHaCap.Should().BeFalse();
        var fila = vm.Files.Single();
        fila.HorariResum.Should().Be("Dl, Dt · 09:00–14:00");
        fila.EstatText.Should().Be("Activa");
        fila.Dies.Should().HaveCount(7);
        fila.Dies[0].Hex.Should().Be(fila.Treballadora.Color);
        fila.Dies[2].Hex.Should().Be("#00000000", "dimecres no treballa");
    }

    [Fact] // O-06
    public async Task Sense_cap_treballadora_la_pagina_ensenya_l_estat_buit()
    {
        await using var bd = new BaseDadesProva();
        var vm = new TreballadoresViewModel(
            new TreballadoraService(new FabricaDeProva(bd.Opcions)), new DialogServiceDeProva());

        await vm.Carregar();

        vm.NoHiHaCap.Should().BeTrue();
        vm.Files.Should().BeEmpty();
    }

    [Fact] // O-07
    public async Task Crear_des_de_la_pagina_desa_el_que_s_ha_omplert_al_dialeg()
    {
        await using var bd = new BaseDadesProva();
        var servei = new TreballadoraService(new FabricaDeProva(bd.Opcions));
        var dialegs = new DialogServiceDeProva
        {
            ResultatDialeg = true,
            OmplirDialeg = d =>
            {
                var dialeg = (TreballadoraDialogViewModel)d;
                dialeg.Nom = "Berta";
                dialeg.DiesHorari[0].Obert = true;
                dialeg.DiesHorari[0].MatiInici = "09:00";
                dialeg.DiesHorari[0].MatiFi = "14:00";
                dialeg.GuardarCommand.Execute(null);
                return Task.CompletedTask;
            }
        };

        var vm = new TreballadoresViewModel(servei, dialegs);
        await vm.Carregar();
        await vm.NovaTreballadoraCommand.ExecuteAsync(null);

        vm.Files.Should().ContainSingle();
        vm.Files[0].Treballadora.Nom.Should().Be("Berta");
        (await servei.HorariDe(vm.Files[0].Treballadora.Id))[DiaSetmana.Dl].Should().ContainSingle();
    }

    [Fact] // O-08
    public void El_dialeg_no_deixa_guardar_una_treballadora_sense_cap_franja()
    {
        var dialeg = new TreballadoraDialogViewModel([]) { Nom = "Berta" };

        dialeg.GuardarCommand.Execute(null);

        dialeg.ErrorValidacio.Should().Be("Indica com a mínim un dia i un horari.");
    }

    [Fact] // O-09
    public void El_dialeg_no_deixa_guardar_sense_nom()
    {
        var dialeg = new TreballadoraDialogViewModel([]);
        dialeg.DiesHorari[0].Obert = true;
        dialeg.DiesHorari[0].MatiInici = "09:00";
        dialeg.DiesHorari[0].MatiFi = "14:00";

        dialeg.GuardarCommand.Execute(null);

        dialeg.ErrorValidacio.Should().Contain("nom");
    }

    [Fact] // O-10
    public void El_dialeg_marca_el_dia_que_te_les_hores_al_reves()
    {
        var dialeg = new TreballadoraDialogViewModel([]) { Nom = "Berta" };
        dialeg.DiesHorari[0].Obert = true;
        dialeg.DiesHorari[0].MatiInici = "20:00";
        dialeg.DiesHorari[0].MatiFi = "09:00";

        dialeg.GuardarCommand.Execute(null);

        dialeg.ErrorValidacio.Should().Contain("vermell");
        dialeg.DiesHorari[0].Error.Should().NotBeNull();
    }

    [Fact] // O-11
    public void Copiar_dilluns_omple_de_dimarts_a_divendres_i_no_el_cap_de_setmana()
    {
        var dialeg = new TreballadoraDialogViewModel([]) { Nom = "Berta" };
        dialeg.DiesHorari[0].Obert = true;
        dialeg.DiesHorari[0].MatiInici = "09:00";
        dialeg.DiesHorari[0].MatiFi = "14:00";

        dialeg.CopiarADiesLaborablesCommand.Execute(null);

        dialeg.DiesHorari.Take(5).Should().OnlyContain(d => d.Obert && d.MatiInici == "09:00");
        dialeg.DiesHorari[5].Obert.Should().BeFalse();
        dialeg.DiesHorari[6].Obert.Should().BeFalse();
    }

    [Fact] // O-12
    public void El_color_suggerit_es_el_primer_que_ningu_no_fa_servir()
    {
        var primer = PaletaTreballadores.Colors[0].Hex;
        var segon = PaletaTreballadores.Colors[1].Hex;

        new TreballadoraDialogViewModel([]).Color.Hex.Should().Be(primer);
        new TreballadoraDialogViewModel([primer]).Color.Hex.Should().Be(segon);
    }
}
