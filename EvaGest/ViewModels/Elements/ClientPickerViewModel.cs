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
/// which of these it is:
///
/// - <b>Existing client</b>: a search box. Typing offers the matching registered
///   clients in a dropdown; a button opens the whole list in its own dialog to browse
///   by scrolling. Only a client actually picked counts — text left in the box is a
///   search, not a name.
/// - <b>New client</b>: a plain box for the name of someone not registered, saved as a
///   guest (RF-05bis). The dialog can still offer to register them.
/// - <b>No client</b>: only where <see cref="AllowsNoClient"/> is set (sales). Nobody at
///   all, chosen on purpose: the owner of the dialog saves it as
///   <see cref="Sale.NoClientName"/>. A deliberate third option rather than an empty
///   box, so a client forgotten by accident is still refused.
///
/// Each mode keeps its own text, so flipping the toggle back and forth loses nothing.
/// </summary>
public partial class ClientPickerViewModel : ObservableObject
{
    /// <summary>The three sides of the toggle.</summary>
    public enum ClientMode { Existing, New, None }

    private readonly List<Client> _clients = [];
    private readonly IDialogService? _dialogs;

    /// <summary>Set while the search text is written by code rather than typed, so it
    /// neither drops the pick being made nor pops the dropdown open on its own.</summary>
    private bool _writingText;

    /// <summary>The client picked before leaving "existing client", restored on switching back.</summary>
    private Client? _pickedBeforeLeaving;

    /// <param name="dialogs">Opens the browse-the-list dialog. Null where there is no
    /// window to open one from (the tests of the search itself).</param>
    /// <param name="allowsNoClient">Offers the third "no client" option (sales only).</param>
    public ClientPickerViewModel(IDialogService? dialogs = null, bool allowsNoClient = false)
    {
        _dialogs = dialogs;
        AllowsNoClient = allowsNoClient;
    }

    /// <summary>Whether the "no client" option is offered at all. Appointments leave it
    /// off: an appointment is always booked for somebody.</summary>
    public bool AllowsNoClient { get; }

    /// <summary>Which side of the toggle is on.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsExistingClient))]
    [NotifyPropertyChangedFor(nameof(IsNewClient))]
    [NotifyPropertyChangedFor(nameof(IsNoClient))]
    [NotifyPropertyChangedFor(nameof(GuestName))]
    private ClientMode _mode;

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

    // One bool per side of the toggle, for the radio buttons to bind to. Only "true" is
    // acted on: a radio button writes "false" as it unchecks itself, and that must not
    // move the mode a second time.

    /// <summary>"Existing client": a registered client searched or browsed.</summary>
    public bool IsExistingClient
    {
        get => Mode == ClientMode.Existing;
        set { if (value) Mode = ClientMode.Existing; }
    }

    /// <summary>"New client": a guest's free name.</summary>
    public bool IsNewClient
    {
        get => Mode == ClientMode.New;
        set { if (value) Mode = ClientMode.New; }
    }

    /// <summary>"No client": nobody, on purpose. Ignored where it is not offered.</summary>
    public bool IsNoClient
    {
        get => Mode == ClientMode.None;
        set { if (value && AllowsNoClient) Mode = ClientMode.None; }
    }

    public bool IsRegistered => SelectedClient is not null;

    /// <summary>The free name to save for a guest: what the "new client" box says, and
    /// nothing at all in the other two modes.</summary>
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
        _pickedBeforeLeaving = null;
        Mode = ClientMode.Existing;
        // Text first: the view moves the caret to the end when the client changes,
        // and by then the name must already be in the box.
        WriteText(client.Name);
        SelectedClient = client;
    }

    /// <summary>Writes a guest's name from code (loading a saved record, a test),
    /// switching to "new client".</summary>
    public void SetGuestName(string name)
    {
        Mode = ClientMode.New;
        NewName = name;
    }

    partial void OnModeChanged(ClientMode oldValue, ClientMode newValue)
    {
        IsDropdownOpen = false;
        if (oldValue == ClientMode.Existing)
        {
            // A guest or no client is nobody registered; remember the pick in case
            // this was a misclick
            _pickedBeforeLeaving = SelectedClient;
            SelectedClient = null;
        }
        else if (newValue == ClientMode.Existing && _pickedBeforeLeaving is { } picked)
        {
            _pickedBeforeLeaving = null;
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
        _pickedBeforeLeaving = null;
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
