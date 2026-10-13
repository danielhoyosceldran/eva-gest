using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using EvaGest.ViewModels.Elements;

namespace EvaGest.Helpers;

/// <summary>
/// Set <c>PickerKeys.Enabled="True"</c> on a search box whose DataContext is an
/// <see cref="IPickerKeys"/>: the arrows move along its list, Enter picks and Esc closes
/// it. A key whose command is disabled (Enter or Esc with the list closed) is left alone,
/// so it reaches the dialog's default and cancel buttons.
///
/// This replaces KeyBindings, which did not let those keys through (B-32): a KeyBinding
/// marks its key handled whenever it matches, even when its command cannot run, so with
/// the focus in the box Esc never closed the dialog and Enter never saved it.
/// </summary>
public static class PickerKeys
{
    public static readonly DependencyProperty EnabledProperty = DependencyProperty.RegisterAttached(
        "Enabled", typeof(bool), typeof(PickerKeys), new PropertyMetadata(false, OnEnabledChanged));

    public static bool GetEnabled(TextBox box) => (bool)box.GetValue(EnabledProperty);
    public static void SetEnabled(TextBox box, bool value) => box.SetValue(EnabledProperty, value);

    private static void OnEnabledChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is not TextBox box) return;
        box.PreviewKeyDown -= OnPreviewKeyDown;
        if ((bool)e.NewValue) box.PreviewKeyDown += OnPreviewKeyDown;
    }

    /// <summary>Preview, so the arrows reach the list before the box moves its caret.</summary>
    private static void OnPreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (sender is not TextBox { DataContext: IPickerKeys keys }) return;

        var command = e.Key switch
        {
            Key.Down => keys.MoveDownCommand,
            Key.Up => keys.MoveUpCommand,
            Key.Enter => keys.PickHighlightedCommand,
            Key.Escape => keys.CloseDropdownCommand,
            _ => null
        };
        if (command is null || !command.CanExecute(null)) return;

        command.Execute(null);
        e.Handled = true;
    }
}
