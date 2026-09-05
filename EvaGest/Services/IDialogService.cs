namespace EvaGest.Services;

/// <summary>Lets ViewModels request dialogs without knowing about WPF windows.</summary>
public interface IDialogService
{
    /// <summary>Opens a modal dialog bound to the given ViewModel.
    /// Returns true when the user confirmed.</summary>
    Task<bool> MostrarDialeg<TViewModel>(TViewModel viewModel) where TViewModel : class;

    /// <summary>Yes/no confirmation. Used for every destructive action (RF-21).</summary>
    Task<bool> Confirmar(string titol, string missatge,
                         string textConfirmar, string textCancellar = "Cancel·lar");

    /// <summary>Informational message with a single OK button.</summary>
    Task Informar(string titol, string missatge);

    /// <summary>Native folder picker. Returns null when cancelled.</summary>
    Task<string?> DemanarCarpeta(string titol);
}
