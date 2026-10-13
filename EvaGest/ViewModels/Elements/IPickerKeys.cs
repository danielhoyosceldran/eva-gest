using CommunityToolkit.Mvvm.Input;

namespace EvaGest.ViewModels.Elements;

/// <summary>
/// The keys a search box with a dropdown list answers to, shared by
/// <see cref="ClientPickerViewModel"/> and <see cref="SearchPickerViewModel"/> so one
/// view helper (<c>Helpers.PickerKeys</c>) can wire both. Pick and close are disabled
/// while the list is closed, which hands Enter and Esc on to the dialog.
/// </summary>
public interface IPickerKeys
{
    IRelayCommand MoveDownCommand { get; }
    IRelayCommand MoveUpCommand { get; }
    IRelayCommand PickHighlightedCommand { get; }
    IRelayCommand CloseDropdownCommand { get; }
}
