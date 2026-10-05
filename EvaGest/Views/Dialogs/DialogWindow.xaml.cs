using System.ComponentModel;
using System.Windows;
using System.Windows.Input;
using EvaGest.ViewModels.Dialogs;

namespace EvaGest.Views.Dialogs;

/// <summary>Generic host: content comes from a DataTemplate registered for the
/// dialog ViewModel's type (App.xaml), so this window never needs its own per-dialog XAML.</summary>
public partial class DialogWindow : Window
{
    private readonly DialogViewModelBase _viewModel;

    /// <summary>Set once closing has been decided — the ViewModel asked for it (saved, or
    /// cancelled after its own question), or the user chose to discard — so the Closing
    /// handler lets it through instead of asking again.</summary>
    private bool _closeAllowed;

    public DialogWindow(DialogViewModelBase viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel;
        DataContext = viewModel;
        viewModel.Close += confirmed =>
        {
            _closeAllowed = true;
            // Setting DialogResult on a modal window closes it.
            DialogResult = confirmed;
        };

        Closing += OnClosing;
        KeyDown += OnKeyDown;
    }

    /// <summary>
    /// Esc closes a dialog that asks before discarding (its Cancel button has no
    /// IsCancel), through <see cref="OnClosing"/> like the X button, so a dialog with
    /// unsaved input asks first. Bubbling, not Preview: an open ComboBox takes its own
    /// Esc first and marks it handled.
    /// </summary>
    private void OnKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key != Key.Escape || e.Handled || !_viewModel.AsksBeforeDiscarding) return;
        e.Handled = true;
        Close();
    }

    /// <summary>
    /// The X button and Esc used to close straight away, so a half-entered sale was lost
    /// with no question (F-02). When the dialog has changes, the close is held back and
    /// the ViewModel asks; on "discard" the window closes on the next dispatcher turn,
    /// since a window cannot be closed again from inside its own Closing event.
    /// </summary>
    private async void OnClosing(object? sender, CancelEventArgs e)
    {
        if (_closeAllowed || !_viewModel.HasUnsavedChanges) return;

        e.Cancel = true;
        if (await _viewModel.CanDiscard())
        {
            _closeAllowed = true;
            await Dispatcher.InvokeAsync(() => { if (IsVisible) Close(); });
        }
    }
}
