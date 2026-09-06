using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using AwesomeAssertions;
using EvaGest.Models;
using EvaGest.Services;
using EvaGest.Tests.Infra;
using EvaGest.ViewModels.Elements;
using EvaGest.Views.Elements;
using Xunit;

namespace EvaGest.Tests.Vistes;

/// <summary>
/// Real layout arithmetic: these arrange the grid and compare where the hour ruler
/// actually puts "10:00" against where an appointment at 10:00 actually lands. Pure
/// ViewModel tests cannot catch a misalignment that WPF introduces during arrange.
/// </summary>
[Collection(ColleccioWpf.Nom)]
public class GraellaLayoutTests(AplicacioWpf app)
{
    private static readonly DateOnly Dilluns = new(2026, 9, 7);

    private static async Task<GraellaSetmanaViewModel> Muntar(BaseDadesProva bd, int obre, int tanca)
    {
        await using (var db = bd.Context())
        {
            for (int d = 0; d < 7; d++)
                db.HorariBarberia.Add(new HorariBarberia
                {
                    DiaSetmana = (DiaSetmana)d,
                    HoraObertura = new TimeOnly(obre, 0),
                    HoraTancament = new TimeOnly(tanca, 0)
                });
            db.Cites.Add(new Cita
            {
                Data = Dilluns, Hora = new TimeOnly(10, 0), DuradaMin = 30, NomConvidat = "Anna"
            });
            await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        var factory = new FabricaDeProva(bd.Opcions);
        var vm = new GraellaSetmanaViewModel(
            new CitaService(factory), new DisponibilitatService(factory), new ConfiguracioService(factory),
            ModeGraella.Agenda, (_, _) => { });
        await vm.CarregarSetmana(Dilluns);
        return vm;
    }

    private static IEnumerable<T> Descendents<T>(DependencyObject arrel) where T : DependencyObject
    {
        int n = VisualTreeHelper.GetChildrenCount(arrel);
        for (int i = 0; i < n; i++)
        {
            var fill = VisualTreeHelper.GetChild(arrel, i);
            if (fill is T trobat) yield return trobat;
            foreach (var net in Descendents<T>(fill)) yield return net;
        }
    }

    /// <summary>Absolute Y of an element relative to the whole control.</summary>
    private static double YAbsolut(Visual element, Visual arrel)
        => element.TransformToAncestor(arrel).Transform(new Point(0, 0)).Y;

    private static (double yRegla, double yCita) Posicions(GraellaSetmanaViewModel vm, double alcadaFinestra)
    {
        var vista = new GraellaSetmanaView { DataContext = vm };
        vista.Measure(new Size(1200, alcadaFinestra));
        vista.Arrange(new Rect(0, 0, 1200, alcadaFinestra));
        vista.UpdateLayout();

        var etiqueta = Descendents<TextBlock>(vista).First(t => t.Text == "10:00");

        // The appointment block is the only Button carrying a CitaGraellaViewModel
        var bloc = Descendents<Button>(vista).First(b => b.DataContext is CitaGraellaViewModel);

        return (YAbsolut(etiqueta, vista), YAbsolut(bloc, vista));
    }

    [Fact] // L-01
    public async Task La_regla_i_les_cites_s_alineen_quan_la_graella_no_cap_a_la_finestra()
    {
        await using var bd = new BaseDadesProva();
        var vm = await Muntar(bd, 9, 20);          // 11 h x 88 px/h = 968 px

        double yRegla = 0, yCita = 0;
        app.Executa(() => (yRegla, yCita) = Posicions(vm, 600));

        // L'etiqueta va centrada sobre la línia (margin -8), la cita comença a la línia
        (yCita - yRegla).Should().BeApproximately(8, 1.5);
    }

    [Fact] // L-02
    public async Task La_regla_i_les_cites_s_alineen_tambe_quan_la_graella_hi_cap_de_sobres()
    {
        // Setmana curta: 9-12 = 3 h x 88 = 264 px dins d'una finestra de 900. Aquest és el
        // cas que abans desalineava l'hora respecte de la cita.
        await using var bd = new BaseDadesProva();
        var vm = await Muntar(bd, 9, 12);

        double yRegla = 0, yCita = 0;
        app.Executa(() => (yRegla, yCita) = Posicions(vm, 900));

        (yCita - yRegla).Should().BeApproximately(8, 1.5);
    }

    [Fact] // L-03
    public async Task Les_columnes_no_s_estiren_mes_enlla_de_l_horari_visible()
    {
        await using var bd = new BaseDadesProva();
        var vm = await Muntar(bd, 9, 12);

        app.Executa(() =>
        {
            var vista = new GraellaSetmanaView { DataContext = vm };
            vista.Measure(new Size(1200, 900));
            vista.Arrange(new Rect(0, 0, 1200, 900));
            vista.UpdateLayout();

            var panel = (ItemsControl)vista.FindName("PanelDies")!;
            panel.ActualHeight.Should().BeApproximately(vm.AlcadaTotalPx, 1);
        });
    }

    [Fact] // L-05
    public async Task Les_set_columnes_caben_al_selector_del_dialeg_abans_i_tot_de_carregar()
    {
        // El diàleg és SizeToContent: es mesura una sola vegada, mentre la graella encara
        // s'està carregant. Si l'espai del selector no queda reservat des del primer
        // moment, la finestra s'obre massa estreta per veure la setmana sencera.
        await using var bd = new BaseDadesProva();
        var factory = new FabricaDeProva(bd.Opcions);
        var vm = new EvaGest.ViewModels.Dialegs.CitaDialogViewModel(
            new CitaService(factory), new DisponibilitatService(factory), new ClientService(factory),
            new CatalegService(factory), new TreballadoraService(factory), new ConfiguracioService(factory),
            Dilluns);

        app.Executa(() =>
        {
            var vista = new EvaGest.Views.Dialegs.CitaDialogView { DataContext = vm };
            vista.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));

            // 420 del formulari + 20 de marge + 700 del selector
            vista.DesiredSize.Width.Should().BeGreaterThanOrEqualTo(1140);
        });

        await vm.Inicialitzacio;
        vm.Graella!.Dies.Should().HaveCount(7);
    }

    [Fact] // L-06
    public async Task El_selector_del_dialeg_pot_canviar_de_setmana()
    {
        await using var bd = new BaseDadesProva();
        var factory = new FabricaDeProva(bd.Opcions);
        var vm = new EvaGest.ViewModels.Dialegs.CitaDialogViewModel(
            new CitaService(factory), new DisponibilitatService(factory), new ClientService(factory),
            new CatalegService(factory), new TreballadoraService(factory), new ConfiguracioService(factory),
            Dilluns);
        await vm.Inicialitzacio;

        var graella = vm.Graella!;
        graella.RangText.Should().NotBeEmpty();

        await graella.SetmanaSeguentCommand.ExecuteAsync(null);
        graella.InicSetmana.Should().Be(Dilluns.AddDays(7));

        await graella.SetmanaAnteriorCommand.ExecuteAsync(null);
        graella.InicSetmana.Should().Be(Dilluns);
    }

    [Fact] // L-04
    public async Task L_etiqueta_de_l_hora_no_envaeix_la_primera_columna()
    {
        await using var bd = new BaseDadesProva();
        var vm = await Muntar(bd, 9, 20);

        app.Executa(() =>
        {
            var vista = new GraellaSetmanaView { DataContext = vm };
            vista.Measure(new Size(1200, 600));
            vista.Arrange(new Rect(0, 0, 1200, 600));
            vista.UpdateLayout();

            var etiqueta = Descendents<TextBlock>(vista).First(t => t.Text == "10:00");
            double dreta = etiqueta.TransformToAncestor(vista)
                .Transform(new Point(etiqueta.ActualWidth, 0)).X;

            dreta.Should().BeLessThanOrEqualTo(vm.AmpladaReglaPx);
        });
    }
}
