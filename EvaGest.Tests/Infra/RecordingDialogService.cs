using EvaGest.Services;

namespace EvaGest.Tests.Infra;

/// <summary>
/// Like <see cref="TestDialogService"/>, but keeps the message of every question as well
/// as its title, so a test can check what the user was actually told. Can also be told
/// to fail every call, standing for a dialog that could not be shown.
/// </summary>
public sealed class RecordingDialogService : IDialogService
{
    public record Question(string Title, string Message);

    /// <summary>What a plain confirmation answers. Defaults to "cancelled".</summary>
    public bool ConfirmAnswer { get; set; }

    /// <summary>What a confirmation with the owner's PIN answers. Defaults to "cancelled".</summary>
    public bool PinConfirmAnswer { get; set; }

    /// <summary>When set, every call fails with this exception (as a faulted task).</summary>
    public Exception? FailWith { get; set; }

    public List<Question> Confirmations { get; } = [];
    public List<Question> PinConfirmations { get; } = [];
    public List<Question> Informed { get; } = [];
    public List<object> DialogsShown { get; } = [];

    /// <summary>Every message the user was asked about, plain or with the PIN.</summary>
    public IEnumerable<string> AllQuestionMessages
        => Confirmations.Concat(PinConfirmations).Select(q => q.Message);

    public Task<bool> ShowDialog<TViewModel>(TViewModel viewModel) where TViewModel : class
    {
        if (FailWith is { } failure) return Task.FromException<bool>(failure);
        DialogsShown.Add(viewModel);
        return Task.FromResult(false);
    }

    public Task<bool> Confirm(string title, string message, string textConfirm, string? textCancel = null)
    {
        if (FailWith is { } failure) return Task.FromException<bool>(failure);
        Confirmations.Add(new Question(title, message));
        return Task.FromResult(ConfirmAnswer);
    }

    public Task<bool> ConfirmWithOwnerPin(string title, string message, string textConfirm)
    {
        if (FailWith is { } failure) return Task.FromException<bool>(failure);
        PinConfirmations.Add(new Question(title, message));
        return Task.FromResult(PinConfirmAnswer);
    }

    public Task Inform(string title, string message)
    {
        if (FailWith is { } failure) return Task.FromException(failure);
        Informed.Add(new Question(title, message));
        return Task.CompletedTask;
    }

    public Task<string?> AskFolder(string title)
        => FailWith is { } failure ? Task.FromException<string?>(failure) : Task.FromResult<string?>(null);
}
