namespace EvaGest.Helpers;

/// <summary>
/// Decides whether the global error handler shows its message for an exception, or only
/// logs it (E-10). It used to show one message box per exception: an error that repeats
/// (raised by a timer every minute, or by a binding on every layout pass) buried the
/// user under identical boxes, and a message box pumps the dispatcher, so the next
/// occurrence could open a second box on top of the first while it was still up.
///
/// Pure and single-threaded on purpose: it is only used from the UI thread, by the
/// DispatcherUnhandledException handler, and the tests drive it with a fixed clock.
/// </summary>
/// <param name="quiet">How long the same failure stays quiet after its message was shown.</param>
public sealed class ErrorNoticeGate(TimeSpan quiet)
{
    /// <summary>The production setting: a repeating failure is told once a minute at most.</summary>
    public static readonly TimeSpan DefaultQuiet = TimeSpan.FromMinutes(1);

    private bool _showing;
    private string? _lastKey;
    private DateTime _lastShown;

    /// <summary>
    /// True when the message should be shown now; the caller must then call
    /// <see cref="Exit"/> once the box is closed. False while another box is up, and for
    /// the same failure (same exception type thrown from the same method) within
    /// <c>quiet</c> of the last time it was shown.
    /// </summary>
    public bool TryEnter(Exception ex, DateTime now)
    {
        if (_showing) return false;

        string key = Key(ex);
        if (key == _lastKey && now - _lastShown < quiet) return false;

        _showing = true;
        _lastKey = key;
        _lastShown = now;
        return true;
    }

    /// <summary>The message box opened after <see cref="TryEnter"/> has been closed.</summary>
    public void Exit() => _showing = false;

    /// <summary>What makes two exceptions "the same failure": their type and the method
    /// that threw. The message is left out, since it often carries an id or a value that
    /// changes on every repetition.</summary>
    private static string Key(Exception ex)
        => $"{ex.GetType().FullName}|{ex.TargetSite?.DeclaringType?.FullName}.{ex.TargetSite?.Name}";
}
