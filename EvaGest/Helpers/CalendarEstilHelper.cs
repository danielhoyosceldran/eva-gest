using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace EvaGest.Helpers;

/// <summary>
/// Restyles the month/year header button and the previous/next arrows inside a
/// <see cref="Calendar"/> popup. Those parts belong to the internal CalendarItem
/// class, so their style keys (CalendarItem.HeaderButtonStyleKey, etc.) can't be
/// referenced from XAML — x:Static on them throws at load time. Walking the visual
/// tree for the named parts and setting their public Style property works around
/// that without touching the internal type.
/// </summary>
public static class CalendarEstilHelper
{
    public static readonly DependencyProperty AplicaProperty = DependencyProperty.RegisterAttached(
        "Aplica", typeof(bool), typeof(CalendarEstilHelper), new PropertyMetadata(false, OnAplicaCanviat));

    public static void SetAplica(DependencyObject element, bool value) => element.SetValue(AplicaProperty, value);
    public static bool GetAplica(DependencyObject element) => (bool)element.GetValue(AplicaProperty);

    private static void OnAplicaCanviat(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is not Calendar calendari || e.NewValue is not true) return;
        calendari.Loaded += (_, _) => Restilitza(calendari);
    }

    private static void Restilitza(Calendar calendari)
    {
        if (BuscaPerNom<Button>(calendari, "PART_HeaderButton") is { } capcalera)
            capcalera.Style = (Style)calendari.FindResource("EvaGest.CalendariBotoCapcalera");
        if (BuscaPerNom<Button>(calendari, "PART_PreviousButton") is { } anterior)
            anterior.Style = (Style)calendari.FindResource("EvaGest.CalendariBotoAnterior");
        if (BuscaPerNom<Button>(calendari, "PART_NextButton") is { } seguent)
            seguent.Style = (Style)calendari.FindResource("EvaGest.CalendariBotoSeguent");
    }

    private static T? BuscaPerNom<T>(DependencyObject arrel, string nom) where T : FrameworkElement
    {
        for (var i = 0; i < VisualTreeHelper.GetChildrenCount(arrel); i++)
        {
            var fill = VisualTreeHelper.GetChild(arrel, i);
            if (fill is T trobat && trobat.Name == nom) return trobat;
            if (BuscaPerNom<T>(fill, nom) is { } resultat) return resultat;
        }
        return null;
    }
}
