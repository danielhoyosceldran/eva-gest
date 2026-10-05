using CommunityToolkit.Mvvm.ComponentModel;
using EvaGest.Resources;
using EvaGest.Services;

namespace EvaGest.ViewModels.Dialogs;

/// <summary>Base for every dialog ViewModel. Raises <see cref="Close"/> instead of
/// closing a window directly, so dialog ViewModels stay unaware of WPF (capa-mvvm 3.2).</summary>
public abstract partial class DialogViewModelBase : ObservableObject
{
    [ObservableProperty]
    private string? _errorValidation;

    public abstract string Title { get; }

    public event Action<bool>? Close;

    protected void RequestClose(bool confirmed) => Close?.Invoke(confirmed);

    // ── Leaving without saving ───────────────────────────────────────────────

    /// <summary>
    /// What the user can edit in this dialog, as one comparable string. Null (the
    /// default) means the dialog never asks before being closed. The sale and the
    /// appointment dialogs override it, so a stray Esc at the counter no longer throws
    /// away a half-entered sale (F-02).
    /// </summary>
    protected virtual string? EditableState() => null;

    /// <summary>Where the confirmation is asked. Only the dialogs that override
    /// <see cref="EditableState"/> need one.</summary>
    protected virtual IDialogService? DiscardDialogs => null;

    private string? _openedState;

    /// <summary>Takes the state the dialog opened with, once it is fully filled in, as
    /// the "nothing changed yet" reference.</summary>
    protected void MarkOpened() => _openedState = EditableState();

    /// <summary>True when something was changed since the dialog opened. False until
    /// <see cref="MarkOpened"/> has run, so a dialog still loading never asks.</summary>
    public bool HasUnsavedChanges => _openedState is not null && EditableState() != _openedState;

    /// <summary>True for the dialogs that ask before discarding. The window handles Esc
    /// itself only for these; the others keep their IsCancel button.</summary>
    public bool AsksBeforeDiscarding => DiscardDialogs is not null;

    /// <summary>
    /// Asks before the dialog is closed without saving: true when it may close (nothing
    /// changed, or the user chose to discard), false to keep editing. Called by the
    /// Cancel command and by the window when it is closed with Esc or the X button.
    /// </summary>
    public async Task<bool> CanDiscard()
    {
        if (!HasUnsavedChanges || DiscardDialogs is not { } dialogs) return true;

        return await dialogs.Confirm(Texts.DiscardChangesTitle, Texts.DiscardChangesMessage,
            Texts.DiscardChanges, Texts.KeepEditing);
    }
}
