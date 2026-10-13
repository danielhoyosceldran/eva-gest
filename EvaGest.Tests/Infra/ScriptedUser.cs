using EvaGest.Services;
using EvaGest.ViewModels.Dialogs;

namespace EvaGest.Tests.Infra;

/// <summary>
/// The person at the counter, for the scenario tests. Where <see cref="TestDialogService"/>
/// answers every dialog the same way, this one plays a script: each dialog that opens is
/// handed to the next step written for its type, which types into it and presses its
/// buttons. A dialog counts as accepted only if the ViewModel itself asked to close with
/// true, exactly as the real window does, so a form the script filled in wrongly stays
/// "open" (returns false) instead of being waved through.
///
/// Questions (Confirm, the owner's PIN) are answered from their own queues; with nothing
/// queued the answer is "no", so a test can never confirm something destructive by
/// accident.
/// </summary>
public sealed class ScriptedUser : IDialogService
{
    private readonly Queue<(Type type, Func<object, Task> act)> _steps = new();
    private readonly Queue<bool> _confirmAnswers = new();
    private readonly Queue<bool> _pinAnswers = new();

    /// <summary>Every dialog opened, in order, whether the script handled it or not.</summary>
    public List<object> Opened { get; } = [];

    /// <summary>The titles of every plain question asked, in order.</summary>
    public List<string> Asked { get; } = [];

    /// <summary>The titles of every question asked with the owner's PIN.</summary>
    public List<string> AskedWithPin { get; } = [];

    /// <summary>The titles of every message shown.</summary>
    public List<string> Informed { get; } = [];

    /// <summary>The next dialog of type <typeparamref name="T"/> is handled by
    /// <paramref name="act"/>. A dialog of another type arriving first fails the test,
    /// because the flow did not go the way the script expected.</summary>
    public ScriptedUser Then<T>(Func<T, Task> act) where T : class
    {
        _steps.Enqueue((typeof(T), o => act((T)o)));
        return this;
    }

    /// <summary>Synchronous form of <see cref="Then{T}(Func{T, Task})"/>.</summary>
    public ScriptedUser Then<T>(Action<T> act) where T : class
        => Then<T>(vm => { act(vm); return Task.CompletedTask; });

    /// <summary>Queues the answers to the next plain questions.</summary>
    public ScriptedUser Answers(params bool[] answers)
    {
        foreach (var a in answers) _confirmAnswers.Enqueue(a);
        return this;
    }

    /// <summary>Queues the answers to the next questions asked with the owner's PIN
    /// (true stands for "the right PIN was typed").</summary>
    public ScriptedUser PinAnswers(params bool[] answers)
    {
        foreach (var a in answers) _pinAnswers.Enqueue(a);
        return this;
    }

    /// <summary>True when every step written has been played.</summary>
    public bool ScriptFinished => _steps.Count == 0;

    public async Task<bool> ShowDialog<TViewModel>(TViewModel viewModel) where TViewModel : class
    {
        Opened.Add(viewModel);

        if (_steps.Count == 0) return false;   // nothing scripted: the user closes it

        var (type, act) = _steps.Peek();
        if (!type.IsInstanceOfType(viewModel))
            throw new InvalidOperationException(
                $"The script expected a {type.Name} next, but a {viewModel.GetType().Name} opened.");
        _steps.Dequeue();

        // The answer is what the dialog itself decided, not what the script hoped.
        bool? closedWith = null;
        if (viewModel is DialogViewModelBase dialog) dialog.Close += ok => closedWith ??= ok;

        await act(viewModel);
        return closedWith ?? false;
    }

    public Task<bool> Confirm(string title, string message, string textConfirm, string? textCancel = null, bool destructive = false)
    {
        Asked.Add(title);
        return Task.FromResult(_confirmAnswers.Count > 0 && _confirmAnswers.Dequeue());
    }

    public Task<bool> ConfirmWithOwnerPin(string title, string message, string textConfirm)
    {
        AskedWithPin.Add(title);
        return Task.FromResult(_pinAnswers.Count > 0 && _pinAnswers.Dequeue());
    }

    public Task Inform(string title, string message)
    {
        Informed.Add(title);
        return Task.CompletedTask;
    }

    public Task<string?> AskFolder(string title) => Task.FromResult<string?>(null);
}
