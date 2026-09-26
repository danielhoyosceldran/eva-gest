using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using EvaGest.Helpers;
using EvaGest.Models;
using EvaGest.Services;
using EvaGest.ViewModels.Dialogs;

namespace EvaGest.ViewModels.Elements;

/// <summary>
/// "Who is this for", shared by the appointment and sale dialogs (CU-01). A toggle says
/// which of the two it is:
///
/// - <b>Existing client</b>: a search box. Typing offers the matching registered
///   clients in a dropdown; a button opens the whole list in its own dialog to browse
///   by scrolling. Only a client actually picked counts — text left in the box is a
///   search, not a name.
/// - <b>New client</b>: a plain box for the name of someone not registered, saved as a
///   guest (RF-05bis). The dialog can still offer to register them.
///
/// Each mode keeps its own text, so flipping the toggle back and forth loses nothing.
/// </summary>
public partial class ClientPickerViewModel : ObservableObject
{
    private readonly List<Client> _clients = [];
    private readonly IDialogService? _dialogs;

    /// <summary>Set while the search text is written by code rather than typed, so it
    /// neither drops the pick being made nor pops the dropdown open on its own.</summary>
    private bool _writingText;

    /// <summary>The client picked before switching to "new", restored on switching back.</summary>
    private Client? _pickedBeforeNew;

    /// <param name="dialogs">Opens the browse-the-list dialog. Null where there is no
    /// window to open one from (the tests of the search itself).</param>
    public ClientPickerViewModel(IDialogService? dialogs = null) => _dialogs = dialogs;

    /// <summary>True for "new client" (a guest's free name), false for "existing client".</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsExistingClient))]
    [NotifyPropertyChangedFor(nameof(GuestName))]
    private bool _isNewClient;

    /// <summary>The search box of the "existing client" mode.</summary>
    [ObservableProperty] private string _text = string.Empty;

    /// <summary>The name box of the "new client" mode.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(GuestName))]
    private string _newName = string.Empty;

    /// <summary>The registered client picked, or null (nothing picked yet, or a guest).</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsRegistered))]
    private Client? _selectedClient;

    /// <summary>The match the arrow keys are on; Enter picks it.</summary>
    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(PickHighlightedCommand))]
    private Client? _highlighted;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(PickHighlightedCommand))]
    [NotifyCanExecuteChangedFor(nameof(CloseDropdownCommand))]
    [NotifyCanExecuteChangedFor(nameof(MoveUpCommand))]
    private bool _isDropdownOpen;

    public ObservableCollection<Client> Matches { get; } = [];

    /// <summary>The other side of the toggle, for the second radio button to bind to.</summary>
    public bool IsExistingClient
    {
        get => !IsNewClient;
        set => IsNewClient = !value;
    }

    public bool IsRegistered => SelectedClient is not null;

    /// <summary>The free name to save for a guest: what the "new client" box says, and
    /// nothing at all in the "existing client" mode.</summary>
    public string GuestName => IsNewClient ? NewName : string.Empty;

    /// <summary>Replaces the searchable clients, e.g. once the dialog has loaded them.</summary>
    public void SetClients(IEnumerable<Client> clients)
    {
        _clients.Clear();
        _clients.AddRange(clients);
    }

    /// <summary>Makes one more client searchable: one registered from the dialog, or an
    /// asleep one an edited record still points at.</summary>
    public void Add(Client client)
    {
        if (_clients.All(c => c.Id != client.Id)) _clients.Add(client);
    }

    /// <summary>
    /// Picks a registered client, switching to "existing client" and showing their name
    /// in the search box. Null just drops the pick, keeping whatever text is there.
    /// </summary>
    public void Select(Client? client)
    {
        IsDropdownOpen = false;
        if (client is null)
        {
            SelectedClient = null;
            return;
        }

        // Forget any pick kept from before "new client", or leaving that mode below
        // would restore it over this one.
        _pickedBeforeNew = null;
        IsNewClient = false;
        // Text first: the view moves the caret to the end when the client changes,
        // and by then the name must already be in the box.
        WriteText(client.Name);
        SelectedClient = client;
    }

    /// <summary>Writes a guest's name from code (loading a saved record, a test),
    /// switching to "new client".</summary>
    public void SetGuestName(string name)
    {
        IsNewClient = true;
        NewName = name;
    }

    partial void OnIsNewClientChanged(bool value)
    {
        IsDropdownOpen = false;
        if (value)
        {
            // A guest is nobody registered; remember the pick in case this was a misclick
            _pickedBeforeNew = SelectedClient;
            SelectedClient = null;
        }
        else if (_pickedBeforeNew is { } picked)
        {
            _pickedBeforeNew = null;
            Select(picked);
        }
    }

    private void WriteText(string text)
    {
        _writingText = true;
        try { Text = text; }
        finally { _writingText = false; }
    }

    partial void OnTextChanged(string value)
    {
        if (_writingText) return;

        // The user edited the box: whatever was picked no longer matches what it says
        SelectedClient = null;
        _pickedBeforeNew = null;
        RefreshMatches(Text);
        IsDropdownOpen = !string.IsNullOrWhiteSpace(value) && Matches.Count > 0;
    }

    private void RefreshMatches(string query)
    {
        Matches.Clear();
        foreach (var c in ClientSearch.Find(_clients, query)) Matches.Add(c);
        Highlighted = SelectedClient is { } picked
            ? Matches.FirstOrDefault(c => c.Id == picked.Id) ?? Matches.FirstOrDefault()
            : Matches.FirstOrDefault();
    }

    /// <summary>Down opens the list (even on an empty box, to browse) or moves down it.</summary>
    [RelayCommand]
    private void MoveDown()
    {
        if (!IsDropdownOpen)
        {
            // With a client already picked the box holds their full name, which would
            // only match themselves; browsing from there should show everyone.
            RefreshMatches(SelectedClient is null ? Text : string.Empty);
            IsDropdownOpen = Matches.Count > 0;
            return;
        }
        Step(+1);
    }

    [RelayCommand(CanExecute = nameof(IsDropdownOpen))]
    private void MoveUp() => Step(-1);

    private void Step(int delta)
    {
        if (Matches.Count == 0) return;
        int index = Highlighted is null ? -1 : Matches.IndexOf(Highlighted);
        Highlighted = Matches[Math.Clamp(index + delta, 0, Matches.Count - 1)];
    }

    /// <summary>A click on a match in the dropdown.</summary>
    [RelayCommand]
    private void Pick(Client client) => Select(client);

    /// <summary>
    /// Enter while the list is open. Only enabled then, so that with the list closed
    /// the key falls through to the dialog's default button as it always did.
    /// </summary>
    [RelayCommand(CanExecute = nameof(CanPickHighlighted))]
    private void PickHighlighted() => Select(Highlighted);

    private bool CanPickHighlighted() => IsDropdownOpen && Highlighted is not null;

    /// <summary>Escape while the list is open closes the list, not the dialog.</summary>
    [RelayCommand(CanExecute = nameof(IsDropdownOpen))]
    private void CloseDropdown() => IsDropdownOpen = false;

    /// <summary>
    /// Opens every client in a dialog of its own, to find someone by scrolling when
    /// the name will not come to mind. Starts filtered by whatever is being searched.
    /// </summary>
    [RelayCommand]
    private async Task Browse()
    {
        if (_dialogs is null) return;
        IsDropdownOpen = false;

        var browser = new ClientBrowserDialogViewModel(_clients, SelectedClient is null ? Text : string.Empty);
        if (await _dialogs.ShowDialog(browser) && browser.SelectedClient is { } chosen)
            Select(chosen);
    }
}
