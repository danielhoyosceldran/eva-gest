using AwesomeAssertions;
using EvaGest.Models;
using EvaGest.Services;
using EvaGest.Tests.Infra;
using EvaGest.ViewModels.Dialegs;
using EvaGest.ViewModels.Pagines;
using Xunit;

namespace EvaGest.Tests.Serveis;

/// <summary>
/// Bloc R: CU-02, el punt on l'agenda i la caixa es connecten. Marcar una cita com a
/// realitzada ha d'obrir el cobrament amb les dades ja posades; si no, l'usuària ha de
/// tornar a escriure el que l'aplicació ja sabia.
/// </summary>
public class FluxCitaVendaTests
{
    private static readonly DateOnly Avui = DateOnly.FromDateTime(DateTime.Today);

    private sealed record Muntatge(
        IniciViewModel Inici, DialogServiceDeProva Dialegs, CitaService Cites, VendaService Vendes);

    private static async Task<Muntatge> Muntar(BaseDadesProva bd, DialogServiceDeProva? dialegs = null)
    {
        var factory = new FabricaDeProva(bd.Opcions);
        var config = new ConfiguracioService(factory);
        await new SeedService(factory, config).Sembrar();

        dialegs ??= new DialogServiceDeProva();
        var cites = new CitaService(factory);
        var vendes = new VendaService(factory, config);

        var inici = new IniciViewModel(
            cites, vendes, new CaixaService(factory), new ClientService(factory),
            new DisponibilitatService(factory), new CatalegService(factory),
            new TreballadoraService(factory), config, new SoundServiceDeProva(), dialegs);

        return new Muntatge(inici, dialegs, cites, vendes);
    }

    private static async Task<Cita> AfegeixCita(BaseDadesProva bd, bool ambServei = true)
    {
        await using var db = bd.Context();

        var client = Fes.Client();
        db.Clients.Add(client);

        Servei? servei = null;
        if (ambServei)
        {
            servei = Fes.Servei("Tall", preuCents: 1500);
            db.Serveis.Add(servei);
        }

        await db.SaveChangesAsync();

        var cita = new Cita
        {
            Data = Avui, Hora = new TimeOnly(10, 0), DuradaMin = 30,
            ClientId = client.Id, ServeiId = servei?.Id, Estat = EstatCita.Pendent
        };
        db.Cites.Add(cita);
        await db.SaveChangesAsync();

        return cita;
    }

    [Fact] // R-01
    public async Task Marcar_realitzada_obre_el_dialeg_de_venda_amb_les_dades_de_la_cita()
    {
        await using var bd = new BaseDadesProva();
        var m = await Muntar(bd);
        var cita = await AfegeixCita(bd);
        await m.Inici.Carregar();

        await m.Inici.MarcarRealitzadaCommand.ExecuteAsync(m.Inici.CitesDelDia.Single());

        var dialeg = m.Dialegs.DialegsMostrats.OfType<VendaDialogViewModel>().Single();
        dialeg.ModeActual.Should().Be(VendaDialogViewModel.Mode.DesDeCita);
        dialeg.ClientSeleccionat!.Id.Should().Be(cita.ClientId);
        dialeg.CitaAssociadaText.Should().NotBeNull();
        dialeg.Linies.Should().ContainSingle("el servei de la cita entra com a primera línia");
        dialeg.TotalCents.Should().Be(1500);
    }

    [Fact] // R-02
    public async Task Tancar_el_cobrament_sense_cobrar_deixa_la_cita_realitzada_i_sense_venda()
    {
        await using var bd = new BaseDadesProva();
        var m = await Muntar(bd); // DialogServiceDeProva cancels by default
        await AfegeixCita(bd);
        await m.Inici.Carregar();

        await m.Inici.MarcarRealitzadaCommand.ExecuteAsync(m.Inici.CitesDelDia.Single());

        var cites = await m.Cites.ObtenirPerDia(Avui);
        cites.Single().Estat.Should().Be(EstatCita.Realitzada);
        (await m.Vendes.Cercar(new FiltreVendes())).Should().BeEmpty(
            "el client ha vingut però encara no s'ha cobrat, i això és un estat vàlid");
    }

    [Fact] // R-03
    public async Task Una_cita_sense_servei_obre_el_cobrament_amb_el_full_en_blanc()
    {
        await using var bd = new BaseDadesProva();
        var m = await Muntar(bd);
        await AfegeixCita(bd, ambServei: false);
        await m.Inici.Carregar();

        await m.Inici.MarcarRealitzadaCommand.ExecuteAsync(m.Inici.CitesDelDia.Single());

        m.Dialegs.DialegsMostrats.OfType<VendaDialogViewModel>().Single()
            .Linies.Should().BeEmpty();
    }

    [Fact] // R-04
    public async Task Cancellar_o_no_assistir_no_obre_cap_cobrament()
    {
        await using var bd = new BaseDadesProva();
        var m = await Muntar(bd);
        await AfegeixCita(bd);
        await m.Inici.Carregar();

        await m.Inici.MarcarCancelladaCommand.ExecuteAsync(m.Inici.CitesDelDia.Single());

        m.Dialegs.DialegsMostrats.Should().BeEmpty();
        (await m.Cites.ObtenirPerDia(Avui)).Single().Estat.Should().Be(EstatCita.Cancellada);
    }

    [Fact] // R-05
    public async Task Cobrar_des_de_la_cita_deixa_la_venda_lligada_a_la_cita()
    {
        await using var bd = new BaseDadesProva();
        var dialegs = new DialogServiceDeProva
        {
            ResultatDialeg = true,
            OmplirDialeg = async d =>
            {
                if (d is not VendaDialogViewModel venda) return;
                venda.MetodePagament = venda.MetodesActius[0];
                await venda.CobrarCommand.ExecuteAsync(null);
            }
        };

        var m = await Muntar(bd, dialegs);
        var cita = await AfegeixCita(bd);
        await m.Inici.Carregar();

        await m.Inici.MarcarRealitzadaCommand.ExecuteAsync(m.Inici.CitesDelDia.Single());

        var venda = (await m.Vendes.Cercar(new FiltreVendes())).Single();
        venda.CitaId.Should().Be(cita.Id);
        venda.TotalCents.Should().Be(1500);
    }
}
