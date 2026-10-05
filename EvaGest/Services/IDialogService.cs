namespace EvaGest.Services;

/// <summary>Lets ViewModels request dialogs without knowing about WPF windows.</summary>
public interface IDialogService
{
    /// <summary>Opens a modal dialog bound to the given ViewModel.
    /// Returns true when the user confirmed.</summary>
    Task<bool> ShowDialog<TViewModel>(TViewModel viewModel) where TViewModel : class;

    /// <summary>Yes/no confirmation. Used for every destructive action (RF-21).</summary>
    Task<bool> Confirm(string title, string message,
                         string textConfirm, string? textCancel = null);

    /// <summary>
    /// The confirmation for every delete and void (F-05): the same question as
    /// <see cref="Confirm"/>, answered by typing the owner's PIN, even when owner mode is
    /// already open. True only once the PIN was accepted. Until a PIN exists it falls
    /// back to the plain question.
    /// </summary>
    Task<bool> ConfirmWithOwnerPin(string title, string message, string textConfirm);

    /// <summary>Informational message with a single OK button.</summary>
    Task Inform(string title, string message);

    /// <summary>Native folder picker. Returns null when cancelled.</summary>
    Task<string?> AskFolder(string title);
}
