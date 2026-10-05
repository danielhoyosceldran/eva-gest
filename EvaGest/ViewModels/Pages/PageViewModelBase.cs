using CommunityToolkit.Mvvm.ComponentModel;
using EvaGest.Resources;
using Serilog;

namespace EvaGest.ViewModels.Pages;

/// <summary>Common shape for every page ViewModel: a title, a loading flag and a
/// transient note, so the shell and each page share the same binding names.</summary>
public abstract partial class PageViewModelBase : ObservableObject
{
    [ObservableProperty]
    private bool _loading;

    /// <summary>
    /// A quiet line under the toolbar, used to explain something the user did not ask
    /// about: mostly that a delete turned into a deactivation. It is not a dialog on
    /// purpose — the action did happen, so it must not need dismissing.
    /// </summary>
    [ObservableProperty]
    private string? _notice;

    public abstract string Title { get; }

    private CancellationTokenSource? _noticeCts;

    /// <summary>Shows the note and clears it a few seconds later, so it never becomes
    /// permanent furniture. A second note replaces the first rather than queueing.</summary>
    protected void ShowNotice(string message)
    {
        _noticeCts?.Cancel();
        _noticeCts = new CancellationTokenSource();
        var token = _noticeCts.Token;

        Notice = message;

        _ = Task.Delay(TimeSpan.FromSeconds(8), token)
            .ContinueWith(_ => Notice = null, token,
                TaskContinuationOptions.OnlyOnRanToCompletion,
                TaskScheduler.Default);
    }

    /// <summary>
    /// Starts an action that nothing awaits (a reload on a filter change, a click on the
    /// agenda grid) without losing its failure. A bare <c>_ = Load()</c> left an exception
    /// unobserved: the user saw nothing happen, and the log only got a line whenever the
    /// garbage collector happened to finalise the task, if ever. Here it is logged at once
    /// and the page says the action did not complete.
    /// </summary>
    protected void RunInBackground(Func<Task> action) => _ = Observe(action);

    private async Task Observe(Func<Task> action)
    {
        try
        {
            await action();
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Background action on {Page} failed", GetType().Name);
            ShowNotice(Texts.BackgroundActionFailed);
        }
    }

    /// <summary>Drops the note early, for the pages that reload on every keystroke.</summary>
    protected void ClearNotice()
    {
        _noticeCts?.Cancel();
        Notice = null;
    }
}
