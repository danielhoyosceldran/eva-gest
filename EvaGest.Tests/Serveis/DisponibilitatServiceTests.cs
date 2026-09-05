using AwesomeAssertions;
using EvaGest.Models;
using EvaGest.Services;
using EvaGest.Tests.Infra;
using Xunit;

namespace EvaGest.Tests.Serveis;

/// <summary>Bloc E: disponibilitat i solapament (casos-us CU-01b). The least obvious
/// calculation in the project, so it gets the widest test coverage.</summary>
public class DisponibilitatServiceTests
{
    private static readonly DateOnly Dilluns = new(2026, 9, 7); // a Monday

    private static DisponibilitatService CreaServei(BaseDadesProva bd) => new(new FabricaDeProva(bd.Opcions));

    private static async Task<int> AfegeixTreballadora(BaseDadesProva bd, bool activa = true,
        TimeOnly? inici = null, TimeOnly? fi = null)
    {
        await using var db = bd.Context();
        var t = new Treballadora { Nom = "Marta", Actiu = activa, Color = "#0F766E" };
        t.Horaris.Add(new HorariTreballadora
        {
            DiaSetmana = DiaSetmana.Dl,
            HoraInici = inici ?? new TimeOnly(9, 0),
            HoraFi = fi ?? new TimeOnly(18, 0)
        });
        db.Treballadores.Add(t);
        await db.SaveChangesAsync();
        return t.Id;
    }

    private static async Task AfegeixCita(BaseDadesProva bd, TimeOnly hora, int duradaMin,
        int? treballadoraId = null, EstatCita estat = EstatCita.Pendent)
    {
        await using var db = bd.Context();
        db.Cites.Add(new Cita
        {
            Data = Dilluns, Hora = hora, DuradaMin = duradaMin,
            NomConvidat = "Convidat", TreballadoraId = treballadoraId, Estat = estat
        });
        await db.SaveChangesAsync();
    }

    [Fact] // E-01
    public async Task Una_treballadora_cap_cita_sense_avis()
    {
        await using var bd = new BaseDadesProva();
        int t = await AfegeixTreballadora(bd);
        var disp = CreaServei(bd);

        var r = await disp.Comprovar(Dilluns, new TimeOnly(10, 0), 30, t);
        r.HiHaSolapament.Should().BeFalse();
    }

    [Fact] // E-02
    public async Task Una_treballadora_ja_ocupada_avisa()
    {
        await using var bd = new BaseDadesProva();
        int t = await AfegeixTreballadora(bd);
        await AfegeixCita(bd, new TimeOnly(10, 0), 30, t);
        var disp = CreaServei(bd);

        var r = await disp.Comprovar(Dilluns, new TimeOnly(10, 0), 30, t);
        r.HiHaSolapament.Should().BeTrue();
    }

    [Fact] // E-03
    public async Task Dues_treballadores_una_cita_sense_avis()
    {
        await using var bd = new BaseDadesProva();
        await AfegeixTreballadora(bd);
        await AfegeixTreballadora(bd);
        await AfegeixCita(bd, new TimeOnly(10, 0), 30);
        var disp = CreaServei(bd);

        var r = await disp.Comprovar(Dilluns, new TimeOnly(10, 0), 30, treballadoraId: null);
        r.HiHaSolapament.Should().BeFalse();
    }

    [Fact] // E-04
    public async Task Dues_treballadores_dues_cites_avisa()
    {
        await using var bd = new BaseDadesProva();
        await AfegeixTreballadora(bd);
        await AfegeixTreballadora(bd);
        await AfegeixCita(bd, new TimeOnly(10, 0), 30);
        await AfegeixCita(bd, new TimeOnly(10, 0), 30);
        var disp = CreaServei(bd);

        var r = await disp.Comprovar(Dilluns, new TimeOnly(10, 0), 30, treballadoraId: null);
        r.HiHaSolapament.Should().BeTrue();
    }

    [Fact] // E-05
    public async Task Treballadora_inactiva_no_compta_com_disponible()
    {
        await using var bd = new BaseDadesProva();
        await AfegeixTreballadora(bd, activa: false);
        await AfegeixCita(bd, new TimeOnly(10, 0), 30);
        var disp = CreaServei(bd);

        var r = await disp.Comprovar(Dilluns, new TimeOnly(10, 0), 30, treballadoraId: null);
        r.HiHaSolapament.Should().BeTrue();
    }

    [Fact] // E-06
    public async Task Cita_cancellada_no_compta()
    {
        await using var bd = new BaseDadesProva();
        int t = await AfegeixTreballadora(bd);
        await AfegeixCita(bd, new TimeOnly(10, 0), 30, t, EstatCita.Cancellada);
        var disp = CreaServei(bd);

        var r = await disp.Comprovar(Dilluns, new TimeOnly(10, 0), 30, t);
        r.HiHaSolapament.Should().BeFalse();
    }

    [Fact] // E-07
    public async Task Cita_no_assistida_no_compta()
    {
        await using var bd = new BaseDadesProva();
        int t = await AfegeixTreballadora(bd);
        await AfegeixCita(bd, new TimeOnly(10, 0), 30, t, EstatCita.NoAssistida);
        var disp = CreaServei(bd);

        var r = await disp.Comprovar(Dilluns, new TimeOnly(10, 0), 30, t);
        r.HiHaSolapament.Should().BeFalse();
    }

    [Fact] // E-08
    public async Task Cites_consecutives_sense_encavalcar_sense_avis()
    {
        await using var bd = new BaseDadesProva();
        int t = await AfegeixTreballadora(bd);
        await AfegeixCita(bd, new TimeOnly(10, 0), 30, t);
        var disp = CreaServei(bd);

        var r = await disp.Comprovar(Dilluns, new TimeOnly(10, 30), 30, t);
        r.HiHaSolapament.Should().BeFalse();
    }

    [Fact] // E-09
    public async Task Encavalcament_parcial_avisa()
    {
        await using var bd = new BaseDadesProva();
        int t = await AfegeixTreballadora(bd);
        await AfegeixCita(bd, new TimeOnly(10, 0), 45, t);
        var disp = CreaServei(bd);

        var r = await disp.Comprovar(Dilluns, new TimeOnly(10, 30), 30, t);
        r.HiHaSolapament.Should().BeTrue();
    }

    [Fact] // E-10
    public async Task Treballadora_assignada_i_laltra_ocupada_sense_avis()
    {
        await using var bd = new BaseDadesProva();
        int t1 = await AfegeixTreballadora(bd);
        int t2 = await AfegeixTreballadora(bd);
        await AfegeixCita(bd, new TimeOnly(10, 0), 30, t2);
        var disp = CreaServei(bd);

        var r = await disp.Comprovar(Dilluns, new TimeOnly(10, 0), 30, t1);
        r.HiHaSolapament.Should().BeFalse();
    }

    [Fact] // E-11
    public async Task Editar_una_cita_no_xoca_amb_ella_mateixa()
    {
        await using var bd = new BaseDadesProva();
        int t = await AfegeixTreballadora(bd);
        int citaId;
        await using (var db = bd.Context())
        {
            var c = new Cita
            {
                Data = Dilluns, Hora = new TimeOnly(10, 0), DuradaMin = 30,
                NomConvidat = "Convidat", TreballadoraId = t
            };
            db.Cites.Add(c);
            await db.SaveChangesAsync();
            citaId = c.Id;
        }
        var disp = CreaServei(bd);

        var r = await disp.Comprovar(Dilluns, new TimeOnly(10, 0), 30, t, citaIdExclosa: citaId);
        r.HiHaSolapament.Should().BeFalse();
    }

    [Fact] // E-12
    public async Task Hora_fora_de_lhorari_de_la_barberia_avisa()
    {
        await using var bd = new BaseDadesProva();
        await using (var db = bd.Context())
        {
            db.HorariBarberia.Add(new HorariBarberia
            {
                DiaSetmana = DiaSetmana.Dl, HoraObertura = new TimeOnly(9, 0), HoraTancament = new TimeOnly(14, 0)
            });
            await db.SaveChangesAsync();
        }
        var disp = CreaServei(bd);

        var r = await disp.Comprovar(Dilluns, new TimeOnly(20, 0), 30, treballadoraId: null);
        r.ForaHorari.Should().BeTrue();
    }

    [Fact] // E-13
    public async Task Data_a_dies_tancats_avisa_amb_motiu()
    {
        await using var bd = new BaseDadesProva();
        await using (var db = bd.Context())
        {
            db.DiesTancats.Add(new DiaTancat { Data = Dilluns, Motiu = "Festiu local" });
            await db.SaveChangesAsync();
        }
        var disp = CreaServei(bd);

        var r = await disp.Comprovar(Dilluns, new TimeOnly(10, 0), 30, treballadoraId: null);
        r.DiaTancat.Should().BeTrue();
        r.MotiuDiaTancat.Should().Be("Festiu local");
    }

    [Fact] // E-14
    public async Task Cap_avis_bloqueja_el_desat()
    {
        // Comprovar mai llança excepció ni impedeix res: només informa (RF-06)
        await using var bd = new BaseDadesProva();
        int t = await AfegeixTreballadora(bd);
        await AfegeixCita(bd, new TimeOnly(10, 0), 30, t);
        var disp = CreaServei(bd);

        var r = await disp.Comprovar(Dilluns, new TimeOnly(10, 0), 30, t);
        r.HiHaSolapament.Should().BeTrue(); // still just information, nothing thrown
    }

    [Fact] // E-20
    public async Task ObtenirPerRang_inclou_els_dos_extrems()
    {
        await using var bd = new BaseDadesProva();
        var diumenge = Dilluns.AddDays(6);
        await using (var db = bd.Context())
        {
            db.Cites.Add(new Cita { Data = Dilluns, Hora = new TimeOnly(9, 0), DuradaMin = 30, NomConvidat = "A" });
            db.Cites.Add(new Cita { Data = diumenge, Hora = new TimeOnly(9, 0), DuradaMin = 30, NomConvidat = "B" });
            await db.SaveChangesAsync();
        }

        var cites = new CitaService(new FabricaDeProva(bd.Opcions));
        var resultat = await cites.ObtenirPerRang(Dilluns, diumenge);

        resultat.Should().HaveCount(2);
    }
}
