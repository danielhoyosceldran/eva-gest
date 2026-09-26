using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using EvaGest.Helpers;
using EvaGest.Models;
using EvaGest.Resources;

namespace EvaGest.ViewModels.Dialogs;

/// <summary>
/// Every registered client in one scrollable list, with a search bar that narrows it
/// as you type. Opened from the client box of the appointment and sale dialogs, for
/// when the name will not come to mind and it is quicker to look than to type.
///
/// Works on the clients the calling dialog already loaded: no query of its own.
/// </summary>
public partial class ClientBrowserDialogViewModel : DialogViewModelBase
{
    private readonly IReadOnlyList<Client> _clients;

    [ObservableProperty] private string _searchText;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(ChooseCommand))]
    private Client? _selectedClient;

    public ObservableCollection<Client> Results { get; } = [];

    /// <summary>Nothing matches the search: the view shows a hint instead of an empty list.</summary>
    public bool IsEmpty => Results.Count == 0;

    public override string Title => Texts.ClientBrowserTitle;

    /// <param name="clients">The clients to list.</param>
    /// <param name="initialSearch">What was already typed in the client box, so the list
    /// opens where the user was looking.</param>
    public ClientBrowserDialogViewModel(IEnumerable<Client> clients, string initialSearch = "")
    {
        _clients = clients.ToList();
        _searchText = initialSearch;
        Refresh();
    }

    partial void OnSearchTextChanged(string value) => Refresh();

    private void Refresh()
    {
        Results.Clear();
        // No cap here, unlike the dropdown: this is the place to scroll the whole list
        foreach (var c in ClientSearch.Find(_clients, SearchText, int.MaxValue)) Results.Add(c);
        SelectedClient = Results.Count == 1 ? Results[0] : null;
        OnPropertyChanged(nameof(IsEmpty));
    }

    /// <summary>The button, a double-click on a row, or Enter.</summary>
    [RelayCommand(CanExecute = nameof(CanChoose))]
    private void Choose() => RequestClose(true);

    private bool CanChoose() => SelectedClient is not null;

    [RelayCommand]
    private void Cancel() => RequestClose(false);
}
