using AwesomeAssertions;
using EvaGest.Models;
using EvaGest.Services;
using EvaGest.Tests.Infra;
using EvaGest.ViewModels.Pagines;
using Xunit;

namespace EvaGest.Tests.Serveis;

/// <summary>
/// Bloc T: la pàgina de Vendes. Els filtres de RF-15 i, sobretot, que el peu de taula
/// sumi només les actives: una venda anul·lada segueix a la llista precisament perquè
/// es vegi que no compta.
/// </summary>
public class VendesPaginaTests
{
    private static readonly DateOnly Avui = new(2026, 9, 7);

    private static async Task<VendesViewModel> Muntar(BaseDadesProva bd, DialogServiceDeProva? dialegs = null)
    {
        var factory = new FabricaDeProva(bd.Opcions);
        var config = new ConfiguracioService(factory);
        await new SeedService(factory, config).Sembrar();

        return new VendesViewModel(
            new VendaService(factory, config), new ClientService(factory), new CatalegService(factory),
            new TreballadoraService(factory), new SoundServiceDeProva(), config,
            new ExportService(factory), dialegs ?? new DialogServiceDeProva());
    }

    private sealed record Dades(int MetodeId, int AltreMetodeId, int ServeiId, int ClientId);

    private static async Task<Dades> Sembrar(BaseDadesProva bd)
    {
        await using var db = bd.Context();

        var servei = Fes.Servei("Tall", preuCents: 1500);
        var client = Fes.Client("Joana", "600111222");
        var efectiu = Fes.Metode("Efectiu");
        var targeta = Fes.Metode("Targeta");

        db.Serveis.Add(servei);
        db.Clients.Add(client);
        db.MetodesPagament.AddRange(efectiu, targeta);
        await db.SaveChangesAsync();

        return new Dades(efectiu.Id, targeta.Id, servei.Id, client.Id);
    }

    private static async Task<int> AfegeixVenda(
        BaseDadesProva bd, Dades d, int importCents, EstatVenda estat = EstatVenda.Activa,
        DateOnly? data = null, int? metodeId = null, int? clientId = null, int? serveiId = null)
    {
        await using var db = bd.Context();

        var linia = Fes.Linia(importCents);
        linia.ServeiId = serveiId;

        var venda = Fes.Venda(data ?? Avui, metodeId ?? d.MetodeId, estat, linia);
        if (clientId is int id)
        {
            venda.ClientId = id;
            venda.NomConvidat = null;
        }

        db.Vendes.Add(venda);
        await db.SaveChangesAsync();
        return venda.Id;
    }

    [Fact] // T-01
    public async Task El_peu_suma_nomes_les_vendes_actives()
    {
        await using var bd = new BaseDadesProva();
        var d = await Sembrar(bd);
        await AfegeixVenda(bd, d, 1000);
        await AfegeixVenda(bd, d, 2000);
        await AfegeixVenda(bd, d, 5000, EstatVenda.Anullada);

        var vm = await Muntar(bd);
        await vm.Carregar();

        vm.Vendes.Should().HaveCount(3, "l'anul·lada continua visible a l'historial");
        vm.ComptadorActives.Should().Be(2);
        vm.TotalTotalText.Should().Be(Diners.Format(3000));
    }

    [Fact] // T-02
    public async Task Els_imports_es_mostren_en_euros_i_no_en_centims()
    {
        await using var bd = new BaseDadesProva();
        var d = await Sembrar(bd);
        await AfegeixVenda(bd, d, 3650);

        var vm = await Muntar(bd);
        await vm.Carregar();

        // The XAML used to bind TotalCents to a {0:0.00} format string, which turned
        // 36,50 € into "3650,00".
        vm.Vendes[0].TotalText.Should().Be(Diners.Format(3650));
        vm.Vendes[0].TotalText.Should().NotContain("3650,00");
        vm.Vendes[0].DataText.Should().Be("07/09/2026");
    }

    [Fact] // T-03
    public async Task Filtrar_per_estat_deixa_nomes_les_anullades()
    {
        await using var bd = new BaseDadesProva();
        var d = await Sembrar(bd);
        await AfegeixVenda(bd, d, 1000);
        await AfegeixVenda(bd, d, 5000, EstatVenda.Anullada);

        var vm = await Muntar(bd);
        await vm.Carregar();
        vm.Estat = EstatVenda.Anullada;
        await vm.Carregar();

        vm.Vendes.Should().ContainSingle();
        vm.ComptadorActives.Should().Be(0);
        vm.TotalTotalText.Should().Be(Diners.Format(0));
    }

    [Fact] // T-04
    public async Task Filtrar_per_dates_deixa_fora_el_que_no_hi_cau()
    {
        await using var bd = new BaseDadesProva();
        var d = await Sembrar(bd);
        await AfegeixVenda(bd, d, 1000, data: Avui);
        await AfegeixVenda(bd, d, 2000, data: Avui.AddDays(-10));

        var vm = await Muntar(bd);
        await vm.Carregar();
        vm.Des = Avui.AddDays(-2);
        await vm.Carregar();

        vm.Vendes.Should().ContainSingle();
        vm.TotalTotalText.Should().Be(Diners.Format(1000));
    }

    [Fact] // T-05
    public async Task Filtrar_per_client_i_per_metode_de_pagament()
    {
        await using var bd = new BaseDadesProva();
        var d = await Sembrar(bd);
        await AfegeixVenda(bd, d, 1000, clientId: d.ClientId);
        await AfegeixVenda(bd, d, 2000, metodeId: d.AltreMetodeId);

        var vm = await Muntar(bd);
        await vm.Carregar();

        vm.Client = vm.ClientsFiltre.Single(c => c.Id == d.ClientId);
        await vm.Carregar();
        vm.Vendes.Should().ContainSingle();

        await vm.NetejarFiltresCommand.ExecuteAsync(null);
        vm.MetodePagament = vm.MetodesFiltre.Single(m => m.Id == d.AltreMetodeId);
        await vm.Carregar();
        vm.Vendes.Should().ContainSingle();
        vm.Vendes[0].TotalText.Should().Be(Diners.Format(2000));
    }

    [Fact] // T-06
    public async Task Netejar_els_filtres_torna_a_ensenyar_ho_tot()
    {
        await using var bd = new BaseDadesProva();
        var d = await Sembrar(bd);
        await AfegeixVenda(bd, d, 1000);
        await AfegeixVenda(bd, d, 2000, EstatVenda.Anullada);

        var vm = await Muntar(bd);
        await vm.Carregar();
        vm.Estat = EstatVenda.Activa;
        await vm.Carregar();
        vm.Vendes.Should().ContainSingle();

        await vm.NetejarFiltresCommand.ExecuteAsync(null);

        vm.Vendes.Should().HaveCount(2);
        vm.NoHiHaResultats.Should().BeFalse();
    }

    [Fact] // T-07
    public async Task Uns_filtres_sense_cap_resultat_ho_diuen()
    {
        await using var bd = new BaseDadesProva();
        var d = await Sembrar(bd);
        await AfegeixVenda(bd, d, 1000);

        var vm = await Muntar(bd);
        await vm.Carregar();
        vm.Des = Avui.AddDays(30);
        await vm.Carregar();

        vm.NoHiHaResultats.Should().BeTrue();
        vm.TotalTotalText.Should().Be(Diners.Format(0));
    }

    [Fact] // T-08
    public async Task Nomes_una_venda_activa_ofereix_anullar_se()
    {
        await using var bd = new BaseDadesProva();
        var d = await Sembrar(bd);
        await AfegeixVenda(bd, d, 1000);
        await AfegeixVenda(bd, d, 2000, EstatVenda.Anullada);

        var vm = await Muntar(bd);
        await vm.Carregar();

        vm.Vendes.Single(f => f.Venda.Estat == EstatVenda.Activa).PotAnullar.Should().BeTrue();
        vm.Vendes.Single(f => f.Venda.Estat == EstatVenda.Anullada).PotAnullar.Should().BeFalse();
    }

    [Fact] // T-09
    public async Task Anullar_demana_confirmacio_i_treu_la_venda_dels_totals()
    {
        await using var bd = new BaseDadesProva();
        var d = await Sembrar(bd);
        await AfegeixVenda(bd, d, 1000);

        var dialegs = new DialogServiceDeProva { ResultatConfirmar = false };
        var vm = await Muntar(bd, dialegs);
        await vm.Carregar();

        await vm.AnullarVendaCommand.ExecuteAsync(vm.Vendes[0].Venda);
        vm.ComptadorActives.Should().Be(1, "sense confirmar no ha de passar res");

        dialegs.ResultatConfirmar = true;
        await vm.AnullarVendaCommand.ExecuteAsync(vm.Vendes[0].Venda);

        vm.ComptadorActives.Should().Be(0);
        vm.Vendes.Should().ContainSingle("continua a l'historial");
    }
}
