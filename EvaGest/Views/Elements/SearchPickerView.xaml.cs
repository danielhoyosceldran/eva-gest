using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using EvaGest.Resources;
using EvaGest.ViewModels.Elements;

namespace EvaGest.Views.Elements;

/// <summary>
/// The view of a <see cref="SearchPickerViewModel"/>. Placed directly with its
/// DataContext bound to the owner's picker (<c>&lt;el:SearchPickerView DataContext="{Binding WorkerPicker}" /&gt;</c>),
/// so a filter cell can ask for the <see cref="Compact"/> button.
/// </summary>
public partial class SearchPickerView : UserControl
{
    public static readonly DependencyProperty CompactProperty = DependencyProperty.Register(
        nameof(Compact), typeof(bool), typeof(SearchPickerView),
        new PropertyMetadata(false, (d, _) => ((SearchPickerView)d).ApplyCompact()));

    /// <summary>True where space is short (a filter cell, a toolbar): the list button
    /// shrinks to "…" and says what it does in its tooltip.</summary>
    public bool Compact
    {
        get => (bool)GetValue(CompactProperty);
        set => SetValue(CompactProperty, value);
    }

    private SearchPickerViewModel? _vm;

    public SearchPickerView()
    {
        InitializeComponent();

        DataContextChanged += (_, _) => _vm = DataContext as SearchPickerViewModel;

        // Leaving the box settles what was typed (picks the one option it names, or
        // empties it). Not when focus only moved to the list button: that opens the list
        // filtered by the very text a commit would have thrown away.
        Box.LostKeyboardFocus += (_, e) =>
        {
            if (e.NewFocus is DependencyObject target && IsAncestorOf(target)) return;
            _vm?.Commit();
        };
    }

    private void ApplyCompact()
    {
        BrowseButton.Content = Compact ? "…" : Texts.BrowseList;
        BrowseButton.ToolTip = Compact ? Texts.BrowseList : null;
        BrowseButton.MinWidth = Compact ? 40 : 0;
        BrowseButton.Padding = Compact ? new Thickness(0) : BrowseButton.Padding;
        BrowseButton.Margin = new Thickness(Compact ? 4 : 8, 0, 0, 0);
    }

    /// <summary>A pick writes its label into the box from code, which leaves the caret at
    /// the start; put it at the end, where the user would carry on typing. TargetUpdated
    /// only fires for text coming from the view model, never for a keystroke, so typing
    /// in the middle of the box is left alone.</summary>
    private void OnTextWrittenByCode(object? sender, DataTransferEventArgs e)
        => Box.CaretIndex = Box.Text.Length;
}
