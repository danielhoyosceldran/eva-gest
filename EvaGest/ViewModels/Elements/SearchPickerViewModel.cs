using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using EvaGest.Helpers;
using EvaGest.Resources;
using EvaGest.Services;
using EvaGest.ViewModels.Dialogs;

namespace EvaGest.ViewModels.Elements;

/// <summary>
/// A search box that stands in for a combo box whose list can grow long (workers,
/// services, products, categories, clients in a filter). It works like the "existing
/// client" half of <see cref="ClientPickerViewModel"/>: typing offers the matching
/// options in a dropdown, and a "Veure la llista" button opens every option in a dialog
/// of its own, to find one by scrolling.
///
/// Only an option actually picked counts. Text left in the box when it loses focus is
/// settled by <see cref="Commit"/>: it picks the one option it names, or else is
/// cleared, so the box never shows a name that is not what will be saved.
///
/// This base holds everything the view binds to, untyped; the owner talks to
/// <see cref="SearchPickerViewModel{T}"/>, which knows the type of the options.
/// </summary>
public abstract partial class SearchPickerViewModel : ObservableObject
{
    private readonly IDialogService? _dialogs;
    private readonly string _browseTitle;

    /// <summary>Set while the text is written by code rather than typed, so it neither
    /// drops the pick being made nor pops the dropdown open on its own.</summary>
    private bool _writingText;

    /// <param name="dialogs">Opens the browse-the-list dialog. Null where there is no
    /// window to open one from (the tests of the search itself).</param>
    /// <param name="browseTitle">The title of that dialog ("Triar treballador").</param>
    /// <param name="placeholder">What the empty box says.</param>
    protected SearchPickerViewModel(IDialogService? dialogs, string browseTitle, string placeholder)
    {
        _dialogs = dialogs;
        _browseTitle = browseTitle;
        Placeholder = placeholder;
    }

    /// <summary>What the empty box says: what to type, or what an empty filter means.</summary>
    public string Placeholder { get; }

    /// <summary>The search box.</summary>
    [ObservableProperty] private string _text = string.Empty;

    /// <summary>The match the arrow keys are on; Enter picks it.</summary>
    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(PickHighlightedCommand))]
    private PickerOption? _highlighted;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(PickHighlightedCommand))]
    [NotifyCanExecuteChangedFor(nameof(CloseDropdownCommand))]
    [NotifyCanExecuteChangedFor(nameof(MoveUpCommand))]
    private bool _isDropdownOpen;

    public ObservableCollection<PickerOption> Matches { get; } = [];

    /// <summary>The value picked (an option's <see cref="PickerOption.Value"/>), or null.</summary>
    protected object? Current { get; private set; }

    /// <summary>Every option, read fresh each time from the owner's own list, so an option
    /// the owner adds or a list it refills is searchable straight away.</summary>
    protected abstract IEnumerable<PickerOption> Options();

    /// <summary>Tells the owner what is now picked (null when the pick was dropped).</summary>
    protected abstract void Report(object? value);

    /// <summary>Options that do not come from the list but from what is typed, shown
    /// after the real matches whatever their rank ("Afegir «…» com a concepte lliure").
    /// None by default.</summary>
    protected virtual IEnumerable<PickerOption> Extras(string query) => [];

    /// <summary>
    /// Shows <paramref name="value"/> as the pick without reporting it back: how the owner
    /// mirrors its own property into the box. Null clears a pick, but leaves alone text
    /// being typed when nothing was picked, because the owner hears about every keystroke
    /// that drops a pick and calls back here with null.
    /// </summary>
    protected void Show(object? value, string label)
    {
        if (value is null)
        {
            if (Current is null) return;
            Current = null;
            WriteText(string.Empty);
            IsDropdownOpen = false;
            return;
        }

        if (Equals(value, Current) && Text == label) return;
        Current = value;
        WriteText(label);
        IsDropdownOpen = false;
    }

    /// <summary>Empties the box whatever it holds, picked or half-typed: the filters'
    /// "Netejar filtres" button. Reports null if something was picked.</summary>
    public void Clear()
    {
        bool hadPick = Current is not null;
        Current = null;
        IsDropdownOpen = false;
        WriteText(string.Empty);
        if (hadPick) Report(null);
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
        bool hadPick = Current is not null;
        Current = null;
        RefreshMatches(value);
        IsDropdownOpen = !string.IsNullOrWhiteSpace(value) && Matches.Count > 0;
        if (hadPick) Report(null);
    }

    private void RefreshMatches(string query)
    {
        Matches.Clear();
        foreach (var option in OptionSearch.Find(Options(), query)) Matches.Add(option);
        foreach (var extra in Extras(query)) Matches.Add(extra);
        Highlighted = Current is { } picked
            ? Matches.FirstOrDefault(o => Equals(o.Value, picked)) ?? Matches.FirstOrDefault()
            : Matches.FirstOrDefault();
    }

    private void Pick(PickerOption? option)
    {
        IsDropdownOpen = false;
        if (option is null) return;

        // Text first: the view moves the caret to the end when the pick changes,
        // and by then the label must already be in the box.
        WriteText(option.Label);
        Current = option.Value;
        Report(option.Value);
    }

    /// <summary>
    /// The box lost focus. Text typed but not picked is settled here: the one option it
    /// still matches (or names exactly) is picked; otherwise the box is emptied, so it
    /// never shows something that is not what is chosen.
    /// </summary>
    public virtual void Commit()
    {
        IsDropdownOpen = false;
        if (Current is not null || string.IsNullOrWhiteSpace(Text))
        {
            if (Current is null && Text.Length > 0) WriteText(string.Empty);
            return;
        }

        var options = Options().ToList();
        var matches = OptionSearch.Find(options, Text, 2);
        var chosen = matches.Count == 1 ? matches[0] : OptionSearch.Exact(options, Text);
        if (chosen is not null) Pick(chosen);
        else WriteText(string.Empty);
    }

    /// <summary>Down opens the list (even on an empty box, to browse) or moves down it.</summary>
    [RelayCommand]
    private void MoveDown()
    {
        if (!IsDropdownOpen)
        {
            // With something picked the box holds its full label, which would only match
            // itself; browsing from there should show every option.
            RefreshMatches(Current is null ? Text : string.Empty);
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
    private void Choose(PickerOption option) => Pick(option);

    /// <summary>Enter while the list is open. Only enabled then, so that with the list
    /// closed the key falls through to the dialog's default button.</summary>
    [RelayCommand(CanExecute = nameof(CanPickHighlighted))]
    private void PickHighlighted() => Pick(Highlighted);

    private bool CanPickHighlighted() => IsDropdownOpen && Highlighted is not null;

    /// <summary>Escape while the list is open closes the list, not the dialog.</summary>
    [RelayCommand(CanExecute = nameof(IsDropdownOpen))]
    private void CloseDropdown() => IsDropdownOpen = false;

    /// <summary>Opens every option in a dialog of its own, starting filtered by whatever
    /// is being searched.</summary>
    [RelayCommand]
    private async Task Browse()
    {
        if (_dialogs is null) return;
        IsDropdownOpen = false;

        var browser = new OptionBrowserDialogViewModel(_browseTitle, Options(), Current is null ? Text : string.Empty);
        if (await _dialogs.ShowDialog(browser) && browser.SelectedOption is { } chosen)
            Pick(chosen);
    }
}

/// <summary>
/// The typed side of <see cref="SearchPickerViewModel"/>, the one the owner builds and
/// talks to. It reads the options from the owner's own collection and hands every pick
/// to a callback, so the owner keeps its typed property (<c>Worker</c>, <c>Service</c>…)
/// and the rest of its logic exactly as with a combo box. The owner mirrors that property
/// back with <see cref="Show"/> from its <c>OnXChanged</c>.
/// </summary>
public sealed class SearchPickerViewModel<T> : SearchPickerViewModel where T : class
{
    private readonly IEnumerable<T> _source;
    private readonly Func<T, string> _label;
    private readonly Func<T, string?>? _detail;
    private readonly Action<T?> _onPicked;

    /// <param name="source">The owner's list, read live on every search.</param>
    /// <param name="label">The text an option is shown and searched by.</param>
    /// <param name="onPicked">Sets the owner's property to the pick (null = none).</param>
    /// <param name="placeholder">What the empty box says; "Escriu per buscar" if null.</param>
    /// <param name="detail">Optional text on the right of each row, also searched as digits.</param>
    public SearchPickerViewModel(
        IEnumerable<T> source, Func<T, string> label, Action<T?> onPicked,
        IDialogService? dialogs, string browseTitle, string? placeholder = null,
        Func<T, string?>? detail = null)
        : base(dialogs, browseTitle, placeholder ?? Texts.OptionSearchPlaceholder)
    {
        _source = source;
        _label = label;
        _detail = detail;
        _onPicked = onPicked;
    }

    /// <summary>The option picked, or null.</summary>
    public T? Selected => Current as T;

    /// <summary>Shows the owner's current value in the box (see the base <c>Show</c>).</summary>
    public void Show(T? item) => Show(item, item is null ? string.Empty : _label(item));

    protected override IEnumerable<PickerOption> Options()
        => _source.Select(item => new PickerOption(item, _label(item), _detail?.Invoke(item)));

    protected override void Report(object? value) => _onPicked(value as T);
}
