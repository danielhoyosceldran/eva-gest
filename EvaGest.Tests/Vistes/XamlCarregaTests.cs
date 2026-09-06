using System.Windows;
using AwesomeAssertions;
using EvaGest.Services;
using EvaGest.Tests.Infra;
using EvaGest.ViewModels.Elements;
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
}
