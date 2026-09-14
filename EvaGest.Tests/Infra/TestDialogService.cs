using EvaGest.Services;

namespace EvaGest.Tests.Infra;

/// <summary>
/// Stand-in for the real dialogs: tests never show windows or message boxes. Both
/// answers default to "cancelled", so a test that does not opt in cannot accidentally
/// confirm a destructive action.
/// </summary>
public class TestDialogService : IDialogService
{
    /// <summary>What <see cref="ShowDialog"/> reports the user pressed.</summary>
    public bool ResultDialog { get; set; }

    /// <summary>What <see cref="Confirm"/> reports the user pressed.</summary>
    public bool ResultConfirm { get; set; }

    /// <summary>
    /// Runs against the dialog ViewModel before it "closes", which is where a test fills
    /// in the form the user would have typed into. Async because some of those dialogs
    /// save through a service, and an async void handler would race the assertions.
    /// </summary>
    public Func<object, Task>? FillDialog { get; set; }

    public List<object> DialogsDisplayed { get; } = [];
    public List<string> ConfirmacionsRequested { get; } = [];
    public List<string> InfoDisplayed { get; } = [];

    public string? FolderChosen { get; set; }

    public async Task<bool> ShowDialog<TViewModel>(TViewModel viewModel) where TViewModel : class
    {
        DialogsDisplayed.Add(viewModel);
        if (FillDialog is { } fill) await fill(viewModel);
        return ResultDialog;
    }

    public Task<bool> Confirm(string title, string message, string textConfirm, string? textCancel = null)
    {
        ConfirmacionsRequested.Add(title);
        return Task.FromResult(ResultConfirm);
    }

    public Task Inform(string title, string message)
    {
        InfoDisplayed.Add(title);
        return Task.CompletedTask;
    }

    public Task<string?> AskFolder(string title) => Task.FromResult(FolderChosen);
}
