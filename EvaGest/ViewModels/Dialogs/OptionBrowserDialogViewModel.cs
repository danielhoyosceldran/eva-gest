using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using EvaGest.Helpers;
using EvaGest.Resources;

namespace EvaGest.ViewModels.Dialogs;

/// <summary>
/// Every option of a search picker in one scrollable list, with a search bar that
/// narrows it as you type: the "Veure la llista" of the worker, service, product and
/// category boxes. The client box has its own, <see cref="ClientBrowserDialogViewModel"/>,
/// whose empty state talks about new clients.
///
/// Works on the options the calling box already has: no query of its own.
/// </summary>
public partial class OptionBrowserDialogViewModel : DialogViewModelBase
{
    private readonly IReadOnlyList<PickerOption> _options;
    private readonly string _title;

    [ObservableProperty] private string _searchText;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(ChooseCommand))]
    private PickerOption? _selectedOption;

    public ObservableCollection<PickerOption> Results { get; } = [];

    /// <summary>Nothing matches the search: the view shows a hint instead of an empty list.</summary>
    public bool IsEmpty => Results.Count == 0;

    public override string Title => _title;

    /// <param name="title">"Triar treballador", "Triar servei"…</param>
    /// <param name="options">The options to list, in the box's own order.</param>
    /// <param name="initialSearch">What was already typed in the box, so the list opens
    /// where the user was looking.</param>
    public OptionBrowserDialogViewModel(string title, IEnumerable<PickerOption> options, string initialSearch = "")
    {
        _title = title;
        _options = options.ToList();
        _searchText = initialSearch;
        Refresh();
    }

    partial void OnSearchTextChanged(string value) => Refresh();

    private void Refresh()
    {
        Results.Clear();
        // No cap here, unlike the dropdown: this is the place to scroll the whole list
        foreach (var o in OptionSearch.Find(_options, SearchText, int.MaxValue)) Results.Add(o);
        SelectedOption = Results.Count == 1 ? Results[0] : null;
        OnPropertyChanged(nameof(IsEmpty));
    }

    /// <summary>The button, a double-click on a row, or Enter.</summary>
    [RelayCommand(CanExecute = nameof(CanChoose))]
    private void Choose() => RequestClose(true);

    private bool CanChoose() => SelectedOption is not null;

    [RelayCommand]
    private void Cancel() => RequestClose(false);
}
