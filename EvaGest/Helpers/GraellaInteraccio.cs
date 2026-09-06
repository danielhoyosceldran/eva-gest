using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using EvaGest.ViewModels.Elements;

namespace EvaGest.Helpers;

/// <summary>
/// Turns clicks and hovers on the empty space of a day column into (date, slot).
/// An attached property rather than code-behind, so the same three handlers serve all
/// seven columns and unhook themselves when a column is recycled.
///
/// Note the Canvas must have Background="Transparent": with a null background WPF does
/// not hit-test it at all and clicks on empty space are simply lost.
/// </summary>
public static class GraellaInteraccio
{
    public static readonly DependencyProperty DiaProperty = DependencyProperty.RegisterAttached(
        "Dia", typeof(DiaGraellaViewModel), typeof(GraellaInteraccio),
        new PropertyMetadata(null, AlCanviarDia));

    public static DiaGraellaViewModel? GetDia(DependencyObject d) => (DiaGraellaViewModel?)d.GetValue(DiaProperty);
    public static void SetDia(DependencyObject d, DiaGraellaViewModel? valor) => d.SetValue(DiaProperty, valor);

    private static void AlCanviarDia(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is not Canvas llenc) return;

        llenc.MouseLeftButtonUp -= AlClicar;
        llenc.MouseMove -= AlMoure;
        llenc.MouseLeave -= AlSortir;

        if (e.NewValue is null) return;

        llenc.MouseLeftButtonUp += AlClicar;
        llenc.MouseMove += AlMoure;
        llenc.MouseLeave += AlSortir;
    }

    private static void AlClicar(object remitent, MouseButtonEventArgs e)
    {
        // An appointment block is a Button and marks the event handled, so clicking one
        // never also books a new appointment underneath it.
        if (e.Handled) return;
        var llenc = (Canvas)remitent;
        GetDia(llenc)?.ClicarAPosicio(e.GetPosition(llenc).Y);
    }

    private static void AlMoure(object remitent, MouseEventArgs e)
    {
        var llenc = (Canvas)remitent;
        var dia = GetDia(llenc);
        if (dia is null) return;

        if (e.OriginalSource is DependencyObject origen && SobreUnaCita(origen, llenc)) dia.DeixarDeSobrevolar();
        else dia.SobrevolarAPosicio(e.GetPosition(llenc).Y);
    }

    private static void AlSortir(object remitent, MouseEventArgs e)
        => GetDia((Canvas)remitent)?.DeixarDeSobrevolar();

    /// <summary>MouseMove does not bubble as "handled", so the hover preview has to ask
    /// whether the pointer is actually over a block rather than over empty space.</summary>
    private static bool SobreUnaCita(DependencyObject origen, Canvas llenc)
    {
        for (var actual = origen; actual is not null && actual != llenc;)
        {
            if (actual is Button) return true;
            actual = actual is System.Windows.Media.Visual or System.Windows.Media.Media3D.Visual3D
                ? System.Windows.Media.VisualTreeHelper.GetParent(actual)
                : LogicalTreeHelper.GetParent(actual);
        }
        return false;
    }
}
