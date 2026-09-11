using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using AwesomeAssertions;
using EvaGest.Services;
using EvaGest.Tests.Infra;
using EvaGest.Models;
using EvaGest.ViewModels.Elements;
using EvaGest.ViewModels.Pagines;
using Xunit;

namespace EvaGest.Tests.Vistes;

/// <summary>
/// A XAML typo only shows up when the view is first navigated to, which in this app can
/// be several clicks deep. These tests parse the real dictionaries and the real views on
/// an STA thread so a broken StaticResource or binding path fails the build instead of
/// the user's afternoon.
/// </summary>
[Collection(ColleccioWpf.Nom)]
public class XamlCarregaTests(AplicacioWpf app)
{
    [Fact] // X-01
    public void Els_diccionaris_de_recursos_es_carreguen()
        => app.Executa(() =>
        {
            Application.Current.Resources["BotoPrimari"].Should().NotBeNull();
            Application.Current.Resources["CitaGraella"].Should().NotBeNull();
            Application.Current.Resources["DateOnlyConverter"].Should().NotBeNull();
        });

    [Fact] // X-02
    public async Task La_graella_setmanal_es_construeix_i_s_enllaça_amb_dades_reals()
    {
        await using var bd = new BaseDadesProva();
        var factory = new FabricaDeProva(bd.Opcions);
        var vm = new GraellaSetmanaViewModel(
            new CitaService(factory), new DisponibilitatService(factory), new ConfiguracioService(factory),
            ModeGraella.Agenda, (_, _) => { });
        await vm.CarregarSetmana(new DateOnly(2026, 9, 7));

        app.Executa(() =>
        {
            var vista = new EvaGest.Views.Elements.GraellaSetmanaView { DataContext = vm };

            // Measure/Arrange forces the templates to expand, which is what actually
            // resolves every StaticResource and MultiBinding inside them.
            vista.Measure(new Size(1200, 800));
            vista.Arrange(new Rect(0, 0, 1200, 800));

            vista.ActualWidth.Should().BeGreaterThan(0);
        });
    }

    [Fact] // X-03
    public void La_vista_de_l_agenda_es_construeix()
        => app.Executa(() =>
        {
            var vista = new EvaGest.Views.Pagines.AgendaView();
            vista.Measure(new Size(1200, 800));
            vista.Arrange(new Rect(0, 0, 1200, 800));
        });

    [Fact] // X-04
    public void El_dialeg_de_cita_es_construeix()
        => app.Executa(() =>
        {
            var vista = new EvaGest.Views.Dialegs.CitaDialogView();
            vista.Measure(new Size(1200, 800));
            vista.Arrange(new Rect(0, 0, 1200, 800));
        });

    [Fact] // X-05
    public void La_vista_de_configuracio_es_construeix()
        => app.Executa(() =>
        {
            var vista = new EvaGest.Views.Pagines.ConfiguracioView();
            vista.Measure(new Size(1200, 800));
            vista.Arrange(new Rect(0, 0, 1200, 800));
        });

    [Fact] // X-06
    public async Task La_vista_de_treballadores_es_construeix_amb_dades_reals()
    {
        await using var bd = new BaseDadesProva();
        var servei = new TreballadoraService(new FabricaDeProva(bd.Opcions));
        await servei.Crear(Fes.Treballadora(), new Dictionary<DiaSetmana, List<(TimeOnly, TimeOnly)>>
        {
            [DiaSetmana.Dl] = [(new TimeOnly(9, 0), new TimeOnly(14, 0))]
        });

        var vm = new TreballadoresViewModel(servei, new DialogServiceDeProva());
        await vm.Carregar();

        app.Executa(() =>
        {
            var vista = new EvaGest.Views.Pagines.TreballadoresView { DataContext = vm };
            vista.Measure(new Size(1200, 800));
            vista.Arrange(new Rect(0, 0, 1200, 800));
            vista.ActualWidth.Should().BeGreaterThan(0);
        });
    }

    [Fact] // X-08
    public async Task La_vista_de_vendes_es_construeix_amb_filtres_i_peu()
    {
        await using var bd = new BaseDadesProva();
        var factory = new FabricaDeProva(bd.Opcions);
        var config = new ConfiguracioService(factory);
        await new SeedService(factory, config).Sembrar();

        var vm = new VendesViewModel(
            new VendaService(factory, config), new ClientService(factory), new CatalegService(factory),
            new TreballadoraService(factory), new SoundServiceDeProva(), config,
            new ExportService(factory), new DialogServiceDeProva());
        await vm.Carregar();

        app.Executa(() =>
        {
            var vista = new EvaGest.Views.Pagines.VendesView { DataContext = vm };
            vista.Measure(new Size(1400, 800));
            vista.Arrange(new Rect(0, 0, 1400, 800));
            vista.ActualWidth.Should().BeGreaterThan(0);
        });
    }

    [Fact] // X-09
    public async Task La_vista_de_l_agenda_dibuixa_el_detall_del_dia()
    {
        await using var bd = new BaseDadesProva();
        var factory = new FabricaDeProva(bd.Opcions);
        var config = new ConfiguracioService(factory);
        await new SeedService(factory, config).Sembrar();

        var dia = new DateOnly(2026, 9, 7);
        await using (var db = bd.Context())
        {
            db.Cites.Add(new Cita
            {
                Data = dia, Hora = new TimeOnly(10, 0), DuradaMin = 30,
                NomConvidat = "Pere", Estat = EstatCita.Pendent
            });
            await db.SaveChangesAsync();
        }

        var vm = new AgendaViewModel(
            new CitaService(factory), new VendaService(factory, config),
            new DisponibilitatService(factory), new ClientService(factory),
            new CatalegService(factory), new TreballadoraService(factory), config,
            new SoundServiceDeProva(), new DialogServiceDeProva());
        await vm.Graella.CarregarSetmana(dia);
        await vm.SeleccionarDiaCommand.ExecuteAsync(dia);

        app.Executa(() =>
        {
            var vista = new EvaGest.Views.Pagines.AgendaView { DataContext = vm };
            vista.Measure(new Size(1400, 800));
            vista.Arrange(new Rect(0, 0, 1400, 800));
            vista.ActualWidth.Should().BeGreaterThan(0);
        });
    }

    [Fact] // X-10
    public void El_calendari_del_datepicker_usa_les_plantilles_de_l_app()
        => app.Executa(() =>
        {
            var vista = new EvaGest.Views.Dialegs.CitaDialogView();
            vista.Measure(new Size(1200, 800));
            vista.Arrange(new Rect(0, 0, 1200, 800));

            // DatePicker builds its popup Calendar in code and binds Calendar.Style to this
            // property, so an implicit Style TargetType="Calendar" never reaches it.
            var picker = Descendents<DatePicker>(vista).First();
            picker.CalendarStyle.Should().NotBeNull();

            // Expanding the calendar's own templates is what resolves every StaticResource
            // and the day/month cell templates inside them.
            var calendari = new Calendar { Style = picker.CalendarStyle };
            calendari.Measure(new Size(600, 600));
            calendari.Arrange(new Rect(0, 0, 600, 600));

            var dies = Descendents<CalendarDayButton>(calendari).ToList();
            dies.Should().HaveCount(42);
            dies.Should().AllSatisfy(d => d.ActualWidth.Should().Be(40));
        });

    private static IEnumerable<T> Descendents<T>(DependencyObject arrel) where T : DependencyObject
    {
        for (var i = 0; i < VisualTreeHelper.GetChildrenCount(arrel); i++)
        {
            var fill = VisualTreeHelper.GetChild(arrel, i);
            if (fill is T trobat) yield return trobat;
            foreach (var net in Descendents<T>(fill)) yield return net;
        }
    }

    [Fact] // X-07
    public void El_dialeg_de_treballadora_es_construeix()
        => app.Executa(() =>
        {
            var vista = new EvaGest.Views.Dialegs.TreballadoraDialogView
            {
                DataContext = new EvaGest.ViewModels.Dialegs.TreballadoraDialogViewModel([])
            };
            vista.Measure(new Size(1200, 800));
            vista.Arrange(new Rect(0, 0, 1200, 800));
        });
}
