using AwesomeAssertions;
using EvaGest.Models;
using EvaGest.Services;
using EvaGest.Tests.Infra;
using EvaGest.ViewModels.Pagines;
using Xunit;

namespace EvaGest.Tests.Serveis;

/// <summary>
/// Bloc Q: la pàgina de Configuració. Cada clau de RF-23 ha de sobreviure a tancar i
/// tornar a obrir la pantalla; si no, l'usuària canvia una opció i no passa res.
/// </summary>
public class ConfiguracioPaginaTests
{
    private static ConfiguracioViewModel Muntar(BaseDadesProva bd, DialogServiceDeProva? dialegs = null)
    {
        var factory = new FabricaDeProva(bd.Opcions);
        var rutes = new RutesApp(
            Path.Combine(Path.GetTempPath(), "eva-prova.db"), Path.GetTempPath());
        var config = new ConfiguracioService(factory);

        return new ConfiguracioViewModel(
            new BackupService(rutes, config), new ExportService(factory), config,
            new DisponibilitatService(factory), dialegs ?? new DialogServiceDeProva());
    }

    [Fact] // Q-01
    public async Task Les_dades_de_la_barberia_es_desen_i_es_tornen_a_llegir()
    {
        await using var bd = new BaseDadesProva();
        var vm = Muntar(bd);
        await vm.Carregar();

        vm.BarberiaNom = "Barberia Eva";
        vm.BarberiaAdreca = "Carrer Major, 1";
        vm.BarberiaTelefon = "600111222";
        await vm.GuardarDadesBarberiaCommand.ExecuteAsync(null);

        var altre = Muntar(bd);
        await altre.Carregar();
        altre.BarberiaNom.Should().Be("Barberia Eva");
        altre.BarberiaAdreca.Should().Be("Carrer Major, 1");
        altre.BarberiaTelefon.Should().Be("600111222");
    }

    [Fact] // Q-02
    public async Task Les_caselles_es_desen_tot_just_canviar_les()
    {
        await using var bd = new BaseDadesProva();
        var vm = Muntar(bd);
        await vm.Carregar();

        vm.SoConfirmacio = false;
        vm.MostrarAvisConvidat = false;
        vm.AplicarIvaCaixa = true;

        var altre = Muntar(bd);
        await altre.Carregar();
        altre.SoConfirmacio.Should().BeFalse();
        altre.MostrarAvisConvidat.Should().BeFalse();
        altre.AplicarIvaCaixa.Should().BeTrue();
    }

    [Fact] // Q-03
    public async Task Un_iva_per_defecte_mal_escrit_no_es_desa()
    {
        await using var bd = new BaseDadesProva();
        var vm = Muntar(bd);
        await vm.Carregar();

        vm.IvaDefecteText = "cent-vint";
        await vm.GuardarIvaDefecteCommand.ExecuteAsync(null);

        vm.ErrorIva.Should().NotBeNull();
        var altre = Muntar(bd);
        await altre.Carregar();
        altre.IvaDefecteText.Should().Be("21");
    }

    [Fact] // Q-04
    public async Task Canviar_el_mode_d_iva_demana_confirmacio_i_la_desa()
    {
        await using var bd = new BaseDadesProva();
        var dialegs = new DialogServiceDeProva { ResultatConfirmar = true };
        var vm = Muntar(bd, dialegs);
        await vm.Carregar();

        vm.ModeIva = IvaMode.NoInclos;

        dialegs.ConfirmacionsDemanades.Should().ContainSingle();
        var altre = Muntar(bd);
        await altre.Carregar();
        altre.ModeIva.Should().Be(IvaMode.NoInclos);
    }

    [Fact] // Q-05
    public async Task Rebutjar_el_canvi_de_mode_d_iva_deixa_el_selector_com_estava()
    {
        await using var bd = new BaseDadesProva();
        var dialegs = new DialogServiceDeProva { ResultatConfirmar = false };
        var vm = Muntar(bd, dialegs);
        await vm.Carregar();

        vm.ModeIva = IvaMode.NoInclos;

        vm.ModeIva.Should().Be(IvaMode.Inclos, "el selector no pot mostrar un valor que no s'ha desat");
        var altre = Muntar(bd);
        await altre.Carregar();
        altre.ModeIva.Should().Be(IvaMode.Inclos);
    }

    [Fact] // Q-06
    public async Task El_mode_d_iva_desat_es_el_que_congela_una_venda_nova()
    {
        await using var bd = new BaseDadesProva();
        var factory = new FabricaDeProva(bd.Opcions);
        var config = new ConfiguracioService(factory);
        await config.Guardar(ClausConfig.IvaModeActual, nameof(IvaMode.NoInclos));

        int metodeId;
        await using (var db = bd.Context())
        {
            var m = Fes.Metode();
            db.MetodesPagament.Add(m);
            await db.SaveChangesAsync();
            metodeId = m.Id;
        }

        var vendes = new VendaService(factory, config);
        int id = await vendes.Crear(
            new Venda
            {
                Data = new DateOnly(2026, 9, 7), Hora = new TimeOnly(10, 0),
                NomConvidat = "Client de prova", MetodePagamentId = metodeId
            },
            [Fes.Linia(1000)]);

        var venda = await vendes.ObtenirPerId(id);
        venda!.IvaMode.Should().Be(IvaMode.NoInclos);
        venda.BaseCents.Should().Be(1000, "sense IVA inclòs, el preu escrit és la base");
        venda.TotalCents.Should().Be(1210);
    }

    [Fact] // Q-07
    public async Task Editar_una_venda_antiga_no_la_reinterpreta_amb_el_mode_d_avui()
    {
        await using var bd = new BaseDadesProva();
        var factory = new FabricaDeProva(bd.Opcions);
        var config = new ConfiguracioService(factory);
        await config.Guardar(ClausConfig.IvaModeActual, nameof(IvaMode.Inclos));

        int metodeId;
        await using (var db = bd.Context())
        {
            var m = Fes.Metode();
            db.MetodesPagament.Add(m);
            await db.SaveChangesAsync();
            metodeId = m.Id;
        }

        var vendes = new VendaService(factory, config);
        var venda = new Venda
        {
            Data = new DateOnly(2026, 9, 7), Hora = new TimeOnly(10, 0),
            NomConvidat = "Client de prova", MetodePagamentId = metodeId
        };
        venda.Id = await vendes.Crear(venda, [Fes.Linia(1000)]);

        // The shop switches to VAT-exclusive prices, then an old ticket gets corrected
        await config.Guardar(ClausConfig.IvaModeActual, nameof(IvaMode.NoInclos));
        await vendes.Actualitzar(venda, [Fes.Linia(1000)]);

        var desada = await vendes.ObtenirPerId(venda.Id);
        desada!.IvaMode.Should().Be(IvaMode.Inclos);
        desada.TotalCents.Should().Be(1000, "el tiquet es va cobrar amb l'IVA dins del preu");
    }

    [Fact] // Q-08
    public async Task Un_dia_tancat_afegit_arriba_a_la_comprovacio_de_disponibilitat()
    {
        await using var bd = new BaseDadesProva();
        var vm = Muntar(bd);
        await vm.Carregar();

        vm.NovaDataTancada = new DateOnly(2026, 12, 25);
        vm.NouMotiuTancat = "Nadal";
        await vm.AfegirDiaTancatCommand.ExecuteAsync(null);

        vm.DiesTancats.Should().ContainSingle();
        vm.NoHiHaDiesTancats.Should().BeFalse();
        vm.NouMotiuTancat.Should().BeEmpty("el camp es buida per poder afegir el següent");

        var resultat = await new DisponibilitatService(new FabricaDeProva(bd.Opcions))
            .Comprovar(new DateOnly(2026, 12, 25), new TimeOnly(10, 0), 30, null);
        resultat.DiaTancat.Should().BeTrue();
        resultat.MotiuDiaTancat.Should().Be("Nadal");
    }

    [Fact] // Q-09
    public async Task Marcar_dues_vegades_el_mateix_dia_n_actualitza_el_motiu()
    {
        await using var bd = new BaseDadesProva();
        var vm = Muntar(bd);
        await vm.Carregar();

        vm.NovaDataTancada = new DateOnly(2026, 12, 25);
        vm.NouMotiuTancat = "Nadal";
        await vm.AfegirDiaTancatCommand.ExecuteAsync(null);

        vm.NouMotiuTancat = "Festiu";
        await vm.AfegirDiaTancatCommand.ExecuteAsync(null);

        vm.DiesTancats.Should().ContainSingle("l'índex únic per data no ha de fer petar res");
        vm.DiesTancats[0].Motiu.Should().Be("Festiu");
    }

    [Fact] // Q-10
    public async Task Treure_un_dia_tancat_el_torna_a_obrir()
    {
        await using var bd = new BaseDadesProva();
        var vm = Muntar(bd);
        await vm.Carregar();
        vm.NovaDataTancada = new DateOnly(2026, 12, 25);
        await vm.AfegirDiaTancatCommand.ExecuteAsync(null);

        await vm.TreureDiaTancatCommand.ExecuteAsync(vm.DiesTancats[0]);

        vm.DiesTancats.Should().BeEmpty();
        vm.NoHiHaDiesTancats.Should().BeTrue();
    }

    [Fact] // Q-11
    public async Task La_durada_per_defecte_d_una_cita_es_desa_si_es_valida()
    {
        await using var bd = new BaseDadesProva();
        var vm = Muntar(bd);
        await vm.Carregar();

        vm.DuradaDefecteCitaText = "0";
        await vm.GuardarDuradaDefecteCommand.ExecuteAsync(null);
        vm.ErrorAgenda.Should().NotBeNull();

        vm.DuradaDefecteCitaText = "45";
        await vm.GuardarDuradaDefecteCommand.ExecuteAsync(null);
        vm.ErrorAgenda.Should().BeNull();

        var altre = Muntar(bd);
        await altre.Carregar();
        altre.DuradaDefecteCitaText.Should().Be("45");
    }

    [Fact] // Q-12
    public async Task Les_opcions_de_copia_es_validen_abans_de_desar_se()
    {
        await using var bd = new BaseDadesProva();
        var vm = Muntar(bd);
        await vm.Carregar();

        vm.HoraBackupText = "vint";
        await vm.GuardarOpcionsBackupCommand.ExecuteAsync(null);
        vm.ErrorBackup.Should().NotBeNull();

        vm.HoraBackupText = "21:30";
        vm.BackupsAConservarText = "0";
        await vm.GuardarOpcionsBackupCommand.ExecuteAsync(null);
        vm.ErrorBackup.Should().Contain("una còpia");

        vm.BackupsAConservarText = "7";
        await vm.GuardarOpcionsBackupCommand.ExecuteAsync(null);
        vm.ErrorBackup.Should().BeNull();

        var altre = Muntar(bd);
        await altre.Carregar();
        altre.HoraBackupText.Should().Be("21:30");
        altre.BackupsAConservarText.Should().Be("7");
    }
}
