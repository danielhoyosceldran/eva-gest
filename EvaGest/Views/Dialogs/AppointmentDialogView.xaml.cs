using System.Windows;
using System.Windows.Controls;
using EvaGest.Helpers;

namespace EvaGest.Views.Dialogs;

public partial class AppointmentDialogView : UserControl
{
    public AppointmentDialogView() => InitializeComponent();

    /// <summary>
    /// Sizes the week picker from the width the dialog is offered, before the content
    /// is measured.
    ///
    /// The picker's width must never come from the grid itself: the window measures
    /// once while the grid is still loading, and a content-sized picker opened too narrow
    /// (L-05). It used to be a fixed 700, which made the dialog about 1200 DIP wide —
    /// wider than a 1280x800 or 1366x768 laptop at 125 %. Now it takes whatever the
    /// form column leaves, between AppointmentPickerMinWidth and AppointmentPickerWidth.
    /// Measured at infinity (a window with room to spare) it gets the full width, as
    /// before.
    ///
    /// The scroll bar's width is kept free too: on a short screen the shell scrolls the
    /// two columns, and its bar must not push the picker past the window edge.
    /// </summary>
    protected override Size MeasureOverride(Size constraint)
    {
        double form = (double)FindResource("AppointmentFormWidth");
        double available = constraint.Width - form - Picker.Margin.Left - Picker.Margin.Right
                           - SystemParameters.VerticalScrollBarWidth;

        double width = LayoutFit.Flexible(available,
            (double)FindResource("AppointmentPickerMinWidth"),
            (double)FindResource("AppointmentPickerWidth"));

        // Only when it changes: setting Width invalidates the picker's measure, which
        // the pass below then satisfies.
        if (!Picker.Width.Equals(width)) Picker.Width = width;

        return base.MeasureOverride(constraint);
    }
}
