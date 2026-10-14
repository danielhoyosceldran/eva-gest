using CommunityToolkit.Mvvm.ComponentModel;
using EvaGest.Resources;
using EvaGest.Services;
using Serilog;

namespace EvaGest.ViewModels.Dialogs;

/// <summary>Base for every dialog ViewModel. Raises <see cref="Close"/> instead of
/// closing a window directly, so dialog ViewModels stay unaware of WPF (capa-mvvm 3.2).</summary>
public abstract partial class DialogViewModelBase : ObservableObject
{
    [ObservableProperty]
    private string? _errorValidation;

    public abstract string Title { get; }

    /// <summary>What the form held when <see cref="ErrorValidation"/> was last set.</summary>
    private string? _errorState;

    partial void OnErrorValidationChanged(string? value)
        => _errorState = value is null ? null : EditableState();

    /// <summary>
    /// The error line says what to fix, so once the user changes the form it is out of
    /// date: it goes away as soon as what they can edit differs from what it judged,
    /// instead of staying red until the next Guardar. Dialogs without an
    /// <see cref="EditableState"/> keep their message until the next attempt.
    /// </summary>
    protected override void OnPropertyChanged(System.ComponentModel.PropertyChangedEventArgs e)
    {
        base.OnPropertyChanged(e);
        if (e.PropertyName == nameof(ErrorValidation) || ErrorValidation is null) return;
        if (_errorState is not null && EditableState() != _errorState) ErrorValidation = null;
    }

    /// <summary>
    /// True for a dialog that is a workspace rather than a form — a table to browse —
    /// and opens as large as the screen allows instead of sizing to its content. Its
    /// view must not need scrolling as a whole: the table inside scrolls by itself.
    /// </summary>
    public virtual bool FillsScreen => false;

    /// <summary>
    /// True for a form that opens as tall as the screen allows, keeping its width sized
    /// to its content: the sale dialog, whose lines list takes the free height and
    /// scrolls by itself.
    /// </summary>
    public virtual bool FillsHeight => false;

    public event Action<bool>? Close;

    protected void RequestClose(bool confirmed) => Close?.Invoke(confirmed);

    /// <summary>
    /// The write this dialog's Guardar stands for, set by the page that opens it. Pages
    /// used to write only after the dialog had closed, so when that write failed the
    /// user got the generic error and everything typed in the dialog was gone (E-09).
    /// Run by <see cref="CloseAfterSaving"/> while the dialog is still open. Null for a
    /// dialog whose caller does not save anything (or saves on its own afterwards).
    /// </summary>
    public Func<Task>? Persist { get; set; }

    /// <summary>
    /// Where a dialog's Save ends once its form is valid: runs <see cref="Persist"/> and
    /// closes only if it went through. A failure is logged, said in the dialog's error
    /// line, and leaves the form as it was so the user can try again.
    /// </summary>
    protected async Task CloseAfterSaving()
    {
        if (Persist is { } persist)
        {
            try
            {
                await persist();
            }
            catch (Exception ex)
            {
                Log.Error(ex, "{Dialog} could not save", GetType().Name);
                ErrorValidation = Texts.SaveFailedKeepEditing;
                return;
            }
        }

        RequestClose(true);
    }

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
            Texts.DiscardChanges, Texts.KeepEditing, destructive: true);
    }
}
