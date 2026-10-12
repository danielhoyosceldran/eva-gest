using System.Windows;
using System.Windows.Controls;

namespace EvaGest.Views.Elements;

/// <summary>
/// The frame every dialog view sits in: the form itself (<see cref="ContentControl.Content"/>)
/// scrolls vertically, and <see cref="Actions"/> — the validation message and the
/// buttons — stays pinned underneath it, always visible.
///
/// DialogWindow caps itself to the screen, so on a small screen a tall dialog (a sale
/// with many lines, the appointment form) is cut down to the work area. Without this
/// split the Guardar / Cobrar buttons were simply below the bottom of the screen; with
/// it, the form scrolls and the buttons never move.
///
/// The look lives in the implicit style in Resources/Controls.xaml.
/// </summary>
public class DialogShell : ContentControl
{
    public static readonly DependencyProperty ActionsProperty = DependencyProperty.Register(
        nameof(Actions), typeof(object), typeof(DialogShell), new PropertyMetadata(null));

    public static readonly DependencyProperty ScrollsContentProperty = DependencyProperty.Register(
        nameof(ScrollsContent), typeof(bool), typeof(DialogShell), new PropertyMetadata(true));

    /// <summary>
    /// False for a dialog whose content scrolls by itself — a table. The shell then gives
    /// the content exactly the height left above the buttons instead of unlimited room:
    /// inside an outer scroll a DataGrid measures every row at once (no virtualisation)
    /// and its header scrolls away with the rows.
    /// </summary>
    public bool ScrollsContent
    {
        get => (bool)GetValue(ScrollsContentProperty);
        set => SetValue(ScrollsContentProperty, value);
    }

    /// <summary>What stays pinned under the scrolling form: usually the error banner
    /// and the row of buttons.</summary>
    public object? Actions
    {
        get => GetValue(ActionsProperty);
        set => SetValue(ActionsProperty, value);
    }
}
